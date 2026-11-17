using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TKS.LeadPilot.Core.Enums;
using TKS.LeadPilot.Core.Models;
using TKS.LeadPilot.Data;
using TKS.LeadPilot.Data.Services;
using Xunit;

namespace TKS.LeadPilot.Data.Tests;

public class OutreachServiceTests
{
    private async Task<(AppDbContext Context, string DbPath)> CreateTestContextAsync()
    {
        var testDbPath = Path.Combine(Path.GetTempPath(), $"tks_outreach_{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={testDbPath}")
            .Options;

        var context = new AppDbContext(options);
        var initializer = new DatabaseInitializer(context);
        await initializer.InitializeAsync();
        return (context, testDbPath);
    }

    [Fact]
    public async Task GetTemplatesAsync_ReturnsSeededOutreachTemplates()
    {
        var (context, dbPath) = await CreateTestContextAsync();
        try
        {
            var service = new OutreachService(context);
            var templates = await service.GetTemplatesAsync();

            templates.Should().NotBeEmpty();
            templates.Should().Contain(t => t.Channel == OutreachChannel.Email);
            templates.Should().Contain(t => t.Channel == OutreachChannel.WhatsApp);
        }
        finally
        {
            await context.DisposeAsync();
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath)) File.Delete(dbPath);
        }
    }

    [Fact]
    public async Task CreateDraftAsync_GeneratesOutreachMessageWithSubstitutedTokens()
    {
        var (context, dbPath) = await CreateTestContextAsync();
        try
        {
            var service = new OutreachService(context);

            var biz = new Business
            {
                Name = "Apex Dental Clinic",
                Category = "Healthcare",
                City = "Lahore",
                WebsiteUrl = "https://apexdental.pk",
                EmailPrimary = "contact@apexdental.pk",
                PhonePrimary = "+92 42 35789012",
                PipelineStageId = "stage-discovered",
                LeadScore = 85
            };
            biz.Contacts.Add(new BusinessContact
            {
                FullName = "Dr. Tariq Khan",
                Email = "dr.tariq@apexdental.pk",
                Phone = "+92 300 9876543"
            });
            context.Businesses.Add(biz);
            await context.SaveChangesAsync();

            var templates = await service.GetTemplatesAsync(OutreachChannel.Email);
            var template = templates.First();

            var draft = await service.CreateDraftAsync(biz.Id, template.Id, biz.Contacts[0].Id);

            draft.Should().NotBeNull();
            draft.ApprovalStatus.Should().Be(OutreachApprovalStatus.PendingApproval);
            draft.RecipientAddress.Should().Be("dr.tariq@apexdental.pk");
            draft.RenderedBody.Should().Contain("Dr. Tariq Khan");
            draft.RenderedBody.Should().Contain("Apex Dental Clinic");
            draft.RenderedBody.Should().Contain("Lahore");
        }
        finally
        {
            await context.DisposeAsync();
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath)) File.Delete(dbPath);
        }
    }

    [Fact]
    public async Task CreateDraftAsync_WhenLeadIsSuppressed_ThrowsException()
    {
        var (context, dbPath) = await CreateTestContextAsync();
        try
        {
            var service = new OutreachService(context);

            var biz = new Business
            {
                Name = "Suppressed Enterprise",
                Category = "Retail",
                City = "Karachi",
                LeadStatus = LeadStatus.DoNotContact, // Suppressed
                PipelineStageId = "stage-do-not-contact"
            };
            context.Businesses.Add(biz);
            await context.SaveChangesAsync();

            var templates = await service.GetTemplatesAsync();
            var template = templates.First();

            Func<Task> act = async () => await service.CreateDraftAsync(biz.Id, template.Id);

            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*Do-Not-Contact suppression list*");
        }
        finally
        {
            await context.DisposeAsync();
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath)) File.Delete(dbPath);
        }
    }

    [Fact]
    public async Task ApproveMessageAsync_And_MarkAsSentAsync_UpdatesApprovalStatusAndMovesPipelineStage()
    {
        var (context, dbPath) = await CreateTestContextAsync();
        try
        {
            var service = new OutreachService(context);

            var biz = new Business
            {
                Name = "Sunrise Hotel",
                Category = "Hospitality",
                City = "Islamabad",
                EmailPrimary = "info@sunrisehotel.pk",
                PipelineStageId = "stage-discovered",
                LeadStatus = LeadStatus.Discovered
            };
            context.Businesses.Add(biz);
            await context.SaveChangesAsync();

            var templates = await service.GetTemplatesAsync(OutreachChannel.Email);
            var template = templates.First();

            var draft = await service.CreateDraftAsync(biz.Id, template.Id);

            // 1. Approve
            var okApprove = await service.ApproveMessageAsync(draft.Id);
            okApprove.Should().BeTrue();

            var queue = await service.GetApprovalQueueAsync();
            var approvedMsg = queue.First(m => m.Id == draft.Id);
            approvedMsg.ApprovalStatus.Should().Be(OutreachApprovalStatus.Approved);

            // 2. Mark Sent
            var okSent = await service.MarkAsSentAsync(draft.Id);
            okSent.Should().BeTrue();

            var updatedBiz = await context.Businesses.FirstAsync(b => b.Id == biz.Id);
            updatedBiz.PipelineStageId.Should().Be("stage-contacted");
            updatedBiz.LeadStatus.Should().Be(LeadStatus.Contacted);
        }
        finally
        {
            await context.DisposeAsync();
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath)) File.Delete(dbPath);
        }
    }

    [Fact]
    public void BuildMailtoUri_And_BuildWhatsAppUri_FormatsCorrectlyWithUrlEncoding()
    {
        var service = new OutreachService(null!);

        var emailMsg = new OutreachMessage
        {
            Channel = OutreachChannel.Email,
            RecipientAddress = "ceo@domain.pk",
            Subject = "Website Audit & Modernization",
            RenderedBody = "Hi Team,\nWe noticed opportunities on your website."
        };

        var mailto = service.BuildMailtoUri(emailMsg);
        mailto.Should().StartWith("mailto:ceo@domain.pk?subject=Website%20Audit");
        mailto.Should().Contain("body=Hi%20Team");

        var waMsg = new OutreachMessage
        {
            Channel = OutreachChannel.WhatsApp,
            RecipientAddress = "+92 300 1234567",
            RenderedBody = "Hello from TheKhanSoft!"
        };

        var waUri = service.BuildWhatsAppUri(waMsg);
        waUri.Should().StartWith("https://wa.me/923001234567?text=Hello%20from%20TheKhanSoft%21");
    }
}
