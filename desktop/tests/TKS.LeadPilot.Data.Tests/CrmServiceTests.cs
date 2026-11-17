using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TKS.LeadPilot.Core.Enums;
using TKS.LeadPilot.Core.Models;
using TKS.LeadPilot.Data;
using TKS.LeadPilot.Data.Repositories;
using TKS.LeadPilot.Data.Services;
using Xunit;

namespace TKS.LeadPilot.Data.Tests;

public class CrmServiceTests
{
    private async Task<(AppDbContext Context, string DbPath)> CreateTestContextAsync()
    {
        var testDbPath = Path.Combine(Path.GetTempPath(), $"tks_crm_{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={testDbPath}")
            .Options;

        var context = new AppDbContext(options);
        var initializer = new DatabaseInitializer(context);
        await initializer.InitializeAsync();
        return (context, testDbPath);
    }

    [Fact]
    public async Task GetPipelineStagesAsync_ReturnsAllSeededStagesInDisplayOrder()
    {
        var (context, dbPath) = await CreateTestContextAsync();
        try
        {
            var service = new CrmService(context);
            var stages = await service.GetPipelineStagesAsync();

            stages.Should().NotBeEmpty();
            stages.Count.Should().BeGreaterOrEqualTo(14);
            stages.First().Code.Should().Be("DISCOVERED");
            stages.Should().BeInAscendingOrder(s => s.DisplayOrder);
        }
        finally
        {
            await context.DisposeAsync();
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath)) File.Delete(dbPath);
        }
    }

    [Fact]
    public async Task MoveLeadStageAsync_UpdatesBusinessStageAndRecordsAuditNote()
    {
        var (context, dbPath) = await CreateTestContextAsync();
        try
        {
            var service = new CrmService(context);
            var biz = new Business
            {
                Name = "Apex Dental Clinic",
                Category = "Healthcare",
                City = "Karachi",
                PipelineStageId = "stage-discovered",
                LeadScore = 65
            };
            context.Businesses.Add(biz);
            await context.SaveChangesAsync();

            // Move to Proposal Sent
            var result = await service.MoveLeadStageAsync(biz.Id, "stage-proposal-sent");
            result.Should().BeTrue();

            var updatedBiz = await context.Businesses.FirstAsync(b => b.Id == biz.Id);
            updatedBiz.PipelineStageId.Should().Be("stage-proposal-sent");
            updatedBiz.LeadStatus.Should().Be(LeadStatus.ProposalSent);

            var notes = await service.GetNotesForBusinessAsync(biz.Id);
            notes.Should().Contain(n => n.Content.Contains("Proposal Sent"));
        }
        finally
        {
            await context.DisposeAsync();
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath)) File.Delete(dbPath);
        }
    }

    [Fact]
    public async Task CreateTaskAsync_And_CompleteTaskAsync_ManagesTaskLifecycle()
    {
        var (context, dbPath) = await CreateTestContextAsync();
        try
        {
            var service = new CrmService(context);
            var biz = new Business
            {
                Name = "Karachi Logistics Hub",
                Category = "Logistics",
                City = "Karachi",
                PipelineStageId = "stage-qualified"
            };
            context.Businesses.Add(biz);
            await context.SaveChangesAsync();

            var task = new SalesTask
            {
                BusinessId = biz.Id,
                Title = "Follow up regarding website redesign proposal",
                Priority = TaskPriority.High,
                DueDateUtc = DateTime.UtcNow.AddDays(1)
            };

            await service.CreateTaskAsync(task);
            task.Id.Should().NotBeNullOrEmpty();

            var tasks = await service.GetTasksForBusinessAsync(biz.Id);
            tasks.Should().HaveCount(1);
            tasks[0].IsCompleted.Should().BeFalse();

            // Mark complete
            var ok = await service.CompleteTaskAsync(task.Id);
            ok.Should().BeTrue();

            var updatedTasks = await service.GetTasksForBusinessAsync(biz.Id);
            updatedTasks[0].IsCompleted.Should().BeTrue();
            updatedTasks[0].CompletedAtUtc.Should().NotBeNull();
        }
        finally
        {
            await context.DisposeAsync();
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath)) File.Delete(dbPath);
        }
    }

    [Fact]
    public async Task AddNoteAsync_And_GetNotesForBusinessAsync_PersistsTimelineNotes()
    {
        var (context, dbPath) = await CreateTestContextAsync();
        try
        {
            var service = new CrmService(context);
            var biz = new Business
            {
                Name = "Prime Retail Store",
                Category = "Retail",
                City = "Lahore",
                PipelineStageId = "stage-discovered"
            };
            context.Businesses.Add(biz);
            await context.SaveChangesAsync();

            var note = new SalesNote
            {
                BusinessId = biz.Id,
                Content = "Spoke with store owner. Interested in custom e-commerce portal.",
                AuthorName = "Kashif"
            };

            await service.AddNoteAsync(note);

            var notes = await service.GetNotesForBusinessAsync(biz.Id);
            notes.Should().HaveCount(1);
            notes[0].Content.Should().Contain("custom e-commerce");
            notes[0].AuthorName.Should().Be("Kashif");
        }
        finally
        {
            await context.DisposeAsync();
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath)) File.Delete(dbPath);
        }
    }
}
