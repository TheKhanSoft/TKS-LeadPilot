using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TKS.LeadPilot.Core.Enums;
using TKS.LeadPilot.Core.Models;
using TKS.LeadPilot.Data;
using TKS.LeadPilot.Data.Repositories;
using Xunit;

namespace TKS.LeadPilot.Data.Tests;

public class AuditRepositoryTests
{
    private async Task<(AppDbContext Context, string DbPath)> CreateTestContextAsync()
    {
        var testDbPath = Path.Combine(Path.GetTempPath(), $"tks_audit_{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={testDbPath}")
            .Options;

        var context = new AppDbContext(options);
        var initializer = new DatabaseInitializer(context);
        await initializer.InitializeAsync();
        return (context, testDbPath);
    }

    [Fact]
    public async Task SaveAuditAsync_PersistsAuditWithFindingsAndUpdatesBusinessOpportunityScore()
    {
        var (context, dbPath) = await CreateTestContextAsync();
        try
        {
            var auditRepo = new AuditRepository(context);
            var bizRepo = new BusinessRepository(context);

            var biz = new Business
            {
                Name = "Cure Diagnostic Lab",
                Category = "Healthcare",
                City = "Karachi",
                WebsiteUrl = "https://curediagnostic.pk",
                PipelineStageId = "stage-discovered",
                LeadScore = 20
            };
            await bizRepo.AddAsync(biz);

            var audit = new WebsiteAudit
            {
                BusinessId = biz.Id,
                TargetUrl = biz.WebsiteUrl,
                Status = AuditStatus.Completed,
                OverallScore = 35, // Poor website quality (35%) -> high sales opportunity!
                PerformanceScore = 30,
                SecurityScore = 40,
                SeoScore = 35,
                DetectedTechStack = "WordPress, Apache",
                Findings =
                [
                    new WebsiteAuditFinding
                    {
                        Category = AuditCategory.Security,
                        Severity = AuditSeverity.Critical,
                        RuleCode = "SEC_NO_HTTPS",
                        Title = "Missing HTTPS/SSL Encryption"
                    },
                    new WebsiteAuditFinding
                    {
                        Category = AuditCategory.Performance,
                        Severity = AuditSeverity.High,
                        RuleCode = "PERF_SLOW_TTFB",
                        Title = "Slow TTFB (2400ms)"
                    }
                ]
            };

            await auditRepo.SaveAuditAsync(audit);

            // Verify retrieval
            var retrieved = await auditRepo.GetLatestAuditForBusinessAsync(biz.Id);
            retrieved.Should().NotBeNull();
            retrieved!.OverallScore.Should().Be(35);
            retrieved.Findings.Should().HaveCount(2);

            // Verify linked business opportunity score was automatically recalculated and boosted
            var updatedBiz = await bizRepo.GetByIdAsync(biz.Id);
            updatedBiz!.LeadScore.Should().Be(65, "opportunity score is 100 - overall website score (100 - 35 = 65)");
            updatedBiz.PipelineStageId.Should().Be("stage-qualified");
        }
        finally
        {
            context.Dispose();
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath)) File.Delete(dbPath);
        }
    }
}
