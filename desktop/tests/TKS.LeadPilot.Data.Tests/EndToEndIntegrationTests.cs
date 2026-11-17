using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TKS.LeadPilot.Core.Enums;
using TKS.LeadPilot.Core.Models;
using TKS.LeadPilot.Core.Services;
using TKS.LeadPilot.Data;
using TKS.LeadPilot.Data.Repositories;
using TKS.LeadPilot.Data.Services;
using Xunit;

namespace TKS.LeadPilot.Data.Tests;

public class EndToEndIntegrationTests
{
    [Fact]
    public async Task Scenario1_OfflineManualEntry_PersistsAcrossContextReloads()
    {
        var testDbPath = Path.Combine(Path.GetTempPath(), $"tks_e2e_{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={testDbPath}")
            .Options;

        try
        {
            var businessId = Guid.NewGuid().ToString();
            var stageId = "stage-qualified";

            // Stage 1: Write offline data
            using (var writeContext = new AppDbContext(options))
            {
                var initializer = new DatabaseInitializer(writeContext);
                await initializer.InitializeAsync();

                var newBiz = new Business
                {
                    Id = businessId,
                    Name = "Lahore Heart & Vascular Institute",
                    Category = "Healthcare & Hospital",
                    City = "Lahore",
                    AddressLine1 = "Jail Road, Shadman",
                    PhonePrimary = "+92 42 37428800",
                    EmailPrimary = "inquiries@lahoreheart.pk",
                    WebsiteUrl = "https://lahoreheart.pk",
                    WebsiteStatus = WebsiteStatus.ActiveWebsite,
                    PipelineStageId = stageId,
                    LeadStatus = LeadStatus.Qualified,
                    LeadScore = 79,
                    IsStarred = true,
                    SourceProvider = "Manual"
                };

                newBiz.Contacts.Add(new BusinessContact
                {
                    FullName = "Prof. Dr. Shehzad Tariq",
                    JobTitle = "Medical Superintendent",
                    Phone = "+92 300 4455667",
                    Email = "dr.shehzad@lahoreheart.pk",
                    IsPrimary = true
                });

                newBiz.Tasks.Add(new SalesTask
                {
                    Title = "Deliver HIPAA/Patient privacy SSL audit report",
                    Priority = TaskPriority.High,
                    DueDateUtc = DateTime.UtcNow.AddDays(2)
                });

                newBiz.Notes.Add(new SalesNote
                {
                    AuthorName = "Operator",
                    Content = "Initial consultation requested regarding hospital appointment portal."
                });

                await writeContext.Businesses.AddAsync(newBiz);
                await writeContext.SaveChangesAsync();
            }

            // Stage 2: Read from fresh independent DbContext
            using (var readContext = new AppDbContext(options))
            {
                var repo = new BusinessRepository(readContext);
                var loaded = await repo.GetByIdAsync(businessId);

                loaded.Should().NotBeNull();
                loaded!.Name.Should().Be("Lahore Heart & Vascular Institute");
                loaded.City.Should().Be("Lahore");
                loaded.Contacts.Should().HaveCount(1);
                loaded.Contacts.First().FullName.Should().Be("Prof. Dr. Shehzad Tariq");
                loaded.Tasks.Should().HaveCount(1);
                loaded.Tasks.First().Title.Should().Contain("HIPAA");
                loaded.Notes.Should().HaveCount(1);
                loaded.Notes.First().Content.Should().Contain("hospital appointment");
            }
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (File.Exists(testDbPath)) File.Delete(testDbPath);
        }
    }

    [Fact]
    public async Task Scenario2_CompleteLeadLifecycle_FromDiscoveryToOutreach()
    {
        var testDbPath = Path.Combine(Path.GetTempPath(), $"tks_e2e_{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={testDbPath}")
            .Options;

        try
        {
            using var context = new AppDbContext(options);
            var initializer = new DatabaseInitializer(context);
            await initializer.InitializeAsync();

            var businessRepo = new BusinessRepository(context);
            var crmService = new CrmService(context);
            var scoringEngine = new LeadScoringEngine(businessRepo);
            var serviceMatcher = new TksServiceMatcher();
            var outreachService = new OutreachService(context);

            // Step A: Load seeded business and its audit
            var leads = await businessRepo.GetAllAsync();
            var targetLead = leads.First(b => b.Name.Contains("Apex Dental"));
            var audit = await context.WebsiteAudits
                .Include(a => a.Findings)
                .FirstOrDefaultAsync(a => a.BusinessId == targetLead.Id);

            // Step B: Calculate Lead Score with Audit signals
            var score = await scoringEngine.CalculateScoreAsync(targetLead, audit);
            score.TotalScore.Should().BeGreaterThan(50);
            score.Components.Should().NotBeEmpty();

            // Step C: Match Recommended Services
            var matchedServices = await serviceMatcher.MatchServicesAsync(targetLead, audit, score);
            matchedServices.Should().NotBeEmpty();
            matchedServices.First().Service.Name.Should().NotBeNullOrWhiteSpace();

            // Step D: Create Outreach Draft
            var draft = await outreachService.CreateDraftAsync(
                targetLead.Id,
                "tpl-audit-email",
                targetLead.Contacts.FirstOrDefault()?.Id);

            draft.Should().NotBeNull();
            draft.ApprovalStatus.Should().Be(OutreachApprovalStatus.PendingApproval);
            draft.Subject.Should().Contain(targetLead.Name);
            draft.RenderedBody.Should().Contain(targetLead.City);

            // Step E: Create Follow-up Task in CRM
            var newTask = new SalesTask
            {
                BusinessId = targetLead.Id,
                Title = "Review outreach approval queue",
                Priority = TaskPriority.High,
                DueDateUtc = DateTime.UtcNow.AddDays(1)
            };
            var task = await crmService.CreateTaskAsync(newTask);
            task.Should().NotBeNull();
            task.BusinessId.Should().Be(targetLead.Id);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (File.Exists(testDbPath)) File.Delete(testDbPath);
        }
    }

    [Fact]
    public async Task Scenario3_SsrfGuard_RejectsPrivateAndDangerousNetworks()
    {
        var validator = new SsrfValidator();
        var dangerousUrls = new[]
        {
            "http://127.0.0.1",
            "http://127.0.0.1:8080/admin",
            "http://localhost",
            "http://10.0.0.1",
            "http://172.16.0.1",
            "http://192.168.1.1",
            "http://169.254.169.254/latest/meta-data/",
            "http://0.0.0.0",
            "ftp://example.com/file",
            "file:///etc/passwd"
        };

        foreach (var url in dangerousUrls)
        {
            var result = await validator.ValidateUrlAsync(url);
            result.IsSafe.Should().BeFalse($"URL '{url}' must be blocked by SSRF validator, reason: {result.Reason}");
        }

        // Public IP address must pass without requiring external DNS network resolution
        var publicResult = await validator.ValidateUrlAsync("https://1.1.1.1");
        publicResult.IsSafe.Should().BeTrue($"Public IP URL should pass SSRF check, failed with: {publicResult.Reason}");
    }

    [Fact]
    public async Task Scenario4_SyncConfiguration_AndLocalChangesTracking()
    {
        var testDbPath = Path.Combine(Path.GetTempPath(), $"tks_e2e_{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={testDbPath}")
            .Options;

        try
        {
            using var context = new AppDbContext(options);
            var initializer = new DatabaseInitializer(context);
            await initializer.InitializeAsync();

            var syncService = new SyncService(context);
            var config = await syncService.GetConfigAsync();

            config.DeviceId.Should().NotBeNullOrWhiteSpace();
            config.ApiBaseUrl.Should().Be("http://127.0.0.1:8000/api/v1");

            var status = await syncService.GetSyncStatusAsync();
            status.IsConfigured.Should().BeTrue();
            status.PendingLocalChangesCount.Should().BeGreaterThan(0, "All newly seeded businesses have not been pushed yet");
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (File.Exists(testDbPath)) File.Delete(testDbPath);
        }
    }
}
