using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TKS.LeadPilot.Core.Interfaces;
using TKS.LeadPilot.Core.Models;

namespace TKS.LeadPilot.Core.Services;

public class AuditComparisonService : IAuditComparisonService
{
    public AuditComparisonResult CompareAudits(WebsiteAudit baseline, WebsiteAudit target)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(target);

        var result = new AuditComparisonResult
        {
            BaselineAuditId = baseline.Id,
            TargetAuditId = target.Id,
            BaselineDateUtc = baseline.CreatedAtUtc,
            TargetDateUtc = target.CreatedAtUtc,
            OverallScoreDelta = (target.OverallScore ?? 0) - (baseline.OverallScore ?? 0),
            PerformanceScoreDelta = (target.PerformanceScore ?? 0) - (baseline.PerformanceScore ?? 0),
            SecurityScoreDelta = (target.SecurityScore ?? 0) - (baseline.SecurityScore ?? 0),
            SeoScoreDelta = (target.SeoScore ?? 0) - (baseline.SeoScore ?? 0)
        };

        var baselineKeys = baseline.Findings
            .ToLookup(f => !string.IsNullOrWhiteSpace(f.RuleCode) ? f.RuleCode : f.Title);

        var targetKeys = target.Findings
            .ToLookup(f => !string.IsNullOrWhiteSpace(f.RuleCode) ? f.RuleCode : f.Title);

        // Resolved = Was in baseline, but absent in target
        foreach (var baselineFinding in baseline.Findings)
        {
            var key = !string.IsNullOrWhiteSpace(baselineFinding.RuleCode) ? baselineFinding.RuleCode : baselineFinding.Title;
            if (!targetKeys.Contains(key))
            {
                result.ResolvedFindings.Add(baselineFinding);
            }
        }

        // New vs Persistent in target
        foreach (var targetFinding in target.Findings)
        {
            var key = !string.IsNullOrWhiteSpace(targetFinding.RuleCode) ? targetFinding.RuleCode : targetFinding.Title;
            if (!baselineKeys.Contains(key))
            {
                result.NewFindings.Add(targetFinding);
            }
            else
            {
                result.PersistentFindings.Add(targetFinding);
            }
        }

        result.ComparisonSummaryMarkdown = GenerateComparisonMarkdown(baseline, target, result);
        return result;
    }

    private static string GenerateComparisonMarkdown(WebsiteAudit baseline, WebsiteAudit target, AuditComparisonResult result)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Audit Comparison Analysis: {target.TargetUrl}");
        sb.AppendLine($"**Baseline Audit:** {baseline.CreatedAtUtc.ToLocalTime():yyyy-MM-dd HH:mm} | **Re-Audit:** {target.CreatedAtUtc.ToLocalTime():yyyy-MM-dd HH:mm}");
        sb.AppendLine();
        sb.AppendLine("## Scorecard Deltas");
        sb.AppendLine("| Health Metric | Baseline Score | Current Score | Net Change | Status |");
        sb.AppendLine("| :--- | :---: | :---: | :---: | :--- |");
        sb.AppendLine($"| **Overall Health** | {baseline.OverallScore ?? 0}% | {target.OverallScore ?? 0}% | {FormatDelta(result.OverallScoreDelta)} | {GetStatusPill(result.OverallScoreDelta)} |");
        sb.AppendLine($"| **Performance** | {baseline.PerformanceScore ?? 0}% | {target.PerformanceScore ?? 0}% | {FormatDelta(result.PerformanceScoreDelta)} | {GetStatusPill(result.PerformanceScoreDelta)} |");
        sb.AppendLine($"| **Security** | {baseline.SecurityScore ?? 0}% | {target.SecurityScore ?? 0}% | {FormatDelta(result.SecurityScoreDelta)} | {GetStatusPill(result.SecurityScoreDelta)} |");
        sb.AppendLine($"| **SEO Hygiene** | {baseline.SeoScore ?? 0}% | {target.SeoScore ?? 0}% | {FormatDelta(result.SeoScoreDelta)} | {GetStatusPill(result.SeoScoreDelta)} |");
        sb.AppendLine();

        if (result.ResolvedFindings.Count > 0)
        {
            sb.AppendLine($"## ✅ Resolved Issues ({result.ResolvedFindings.Count})");
            foreach (var f in result.ResolvedFindings)
            {
                sb.AppendLine($"- **[{f.Category}] {f.Title}**: Remediated successfully.");
            }
            sb.AppendLine();
        }

        if (result.NewFindings.Count > 0)
        {
            sb.AppendLine($"## ⚠️ New Issues Detected ({result.NewFindings.Count})");
            foreach (var f in result.NewFindings)
            {
                sb.AppendLine($"- **[{f.Severity}] {f.Title}**: {f.Description}");
            }
            sb.AppendLine();
        }

        if (result.PersistentFindings.Count > 0)
        {
            sb.AppendLine($"## 📌 Persistent Opportunities ({result.PersistentFindings.Count})");
            foreach (var f in result.PersistentFindings)
            {
                sb.AppendLine($"- **[{f.Severity}] {f.Title}**: {f.RecommendedFix ?? f.Description}");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string FormatDelta(int delta) => delta > 0 ? $"+{delta}%" : $"{delta}%";

    private static string GetStatusPill(int delta)
    {
        if (delta > 0) return "🟢 Improved";
        if (delta < 0) return "🔴 Degraded";
        return "⚪ Unchanged";
    }
}
