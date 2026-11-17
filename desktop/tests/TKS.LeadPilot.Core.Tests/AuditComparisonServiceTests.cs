using System;
using System.Collections.Generic;
using FluentAssertions;
using TKS.LeadPilot.Core.Enums;
using TKS.LeadPilot.Core.Models;
using TKS.LeadPilot.Core.Services;
using Xunit;

namespace TKS.LeadPilot.Core.Tests;

public class AuditComparisonServiceTests
{
    [Fact]
    public void CompareAudits_CalculatesScoreDeltasAccurately()
    {
        var baseline = new WebsiteAudit
        {
            Id = "audit-1",
            CreatedAtUtc = DateTime.UtcNow.AddDays(-30),
            OverallScore = 45,
            PerformanceScore = 40,
            SecurityScore = 50,
            SeoScore = 45
        };

        var target = new WebsiteAudit
        {
            Id = "audit-2",
            CreatedAtUtc = DateTime.UtcNow,
            OverallScore = 78,
            PerformanceScore = 70,
            SecurityScore = 85,
            SeoScore = 80
        };

        var service = new AuditComparisonService();
        var result = service.CompareAudits(baseline, target);

        result.OverallScoreDelta.Should().Be(33); // +33%
        result.PerformanceScoreDelta.Should().Be(30); // +30%
        result.SecurityScoreDelta.Should().Be(35); // +35%
        result.SeoScoreDelta.Should().Be(35); // +35%
    }

    [Fact]
    public void CompareAudits_ClassifiesResolvedNewAndPersistentFindings()
    {
        var baseline = new WebsiteAudit
        {
            Id = "audit-baseline",
            CreatedAtUtc = DateTime.UtcNow.AddDays(-14),
            OverallScore = 50,
            Findings =
            [
                new WebsiteAuditFinding
                {
                    RuleCode = "SEC_NO_HTTPS",
                    Title = "Website does not redirect HTTP to HTTPS",
                    Category = AuditCategory.Security,
                    Severity = AuditSeverity.Critical
                },
                new WebsiteAuditFinding
                {
                    RuleCode = "PERF_SLOW_TTFB",
                    Title = "Time to First Byte exceeds 1200ms",
                    Category = AuditCategory.Performance,
                    Severity = AuditSeverity.Medium
                }
            ]
        };

        var target = new WebsiteAudit
        {
            Id = "audit-target",
            CreatedAtUtc = DateTime.UtcNow,
            OverallScore = 75,
            Findings =
            [
                // SEC_NO_HTTPS is resolved (missing in target)
                // PERF_SLOW_TTFB is persistent (still in target)
                new WebsiteAuditFinding
                {
                    RuleCode = "PERF_SLOW_TTFB",
                    Title = "Time to First Byte exceeds 1200ms",
                    Category = AuditCategory.Performance,
                    Severity = AuditSeverity.Medium
                },
                // SEO_NO_SITEMAP is a newly detected finding
                new WebsiteAuditFinding
                {
                    RuleCode = "SEO_NO_SITEMAP",
                    Title = "Missing sitemap.xml",
                    Category = AuditCategory.Seo,
                    Severity = AuditSeverity.Informational
                }
            ]
        };

        var service = new AuditComparisonService();
        var result = service.CompareAudits(baseline, target);

        // Resolved
        result.ResolvedFindings.Should().HaveCount(1);
        result.ResolvedFindings[0].RuleCode.Should().Be("SEC_NO_HTTPS");

        // Persistent
        result.PersistentFindings.Should().HaveCount(1);
        result.PersistentFindings[0].RuleCode.Should().Be("PERF_SLOW_TTFB");

        // New
        result.NewFindings.Should().HaveCount(1);
        result.NewFindings[0].RuleCode.Should().Be("SEO_NO_SITEMAP");

        // Markdown Report
        result.ComparisonSummaryMarkdown.Should().Contain("Resolved Issues (1)");
        result.ComparisonSummaryMarkdown.Should().Contain("Persistent Opportunities (1)");
        result.ComparisonSummaryMarkdown.Should().Contain("New Issues Detected (1)");
        result.ComparisonSummaryMarkdown.Should().Contain("Scorecard Deltas");
    }
}
