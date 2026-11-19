using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using TKS.LeadPilot.Core.Common;
using TKS.LeadPilot.Core.Enums;

namespace TKS.LeadPilot.Core.Models;

public class WebsiteAudit
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string BusinessId { get; set; } = string.Empty;
    public Business? Business { get; set; }
    
    public string TargetUrl { get; set; } = string.Empty;
    public AuditStatus Status { get; set; } = AuditStatus.Queued;
    
    // Scores
    public int? OverallScore { get; set; }
    public int? PerformanceScore { get; set; }
    public int? SeoScore { get; set; }
    public int? AccessibilityScore { get; set; }
    public int? SecurityScore { get; set; }

    // Diagnostics & Signals
    public bool HttpRedirectsToHttps { get; set; } = false;
    public bool SslValid { get; set; } = false;
    public string? SslIssuer { get; set; }
    public int? SslDaysUntilExpiry { get; set; }
    public double? TtfbMs { get; set; }
    public long? HtmlSizeBytes { get; set; }
    public bool HasRobotsTxt { get; set; } = false;
    public bool HasSitemapXml { get; set; } = false;
    public string? DetectedTechStack { get; set; }
    public string? PageTitle { get; set; }
    public string? MetaDescription { get; set; }
    
    // Advanced Technical, SEO & Security Diagnostics
    public string? CanonicalUrl { get; set; }
    public string? H1Text { get; set; }
    public int? H1Count { get; set; }
    public int? H2Count { get; set; }
    public int? ImagesTotalCount { get; set; }
    public int? ImagesMissingAltCount { get; set; }
    public int? InternalLinksCount { get; set; }
    public int? ExternalLinksCount { get; set; }
    public string? ServerBanner { get; set; }
    public string? CompressionType { get; set; }
    public string? SecurityHeadersGrade { get; set; }
    public string? AnalyticsTrackersDetected { get; set; }
    public bool ViewportConfigured { get; set; } = false;
    public bool OpenGraphConfigured { get; set; } = false;
    public bool TwitterCardConfigured { get; set; } = false;
    public bool HstsConfigured { get; set; } = false;
    public bool CspConfigured { get; set; } = false;
    public bool XFrameOptionsConfigured { get; set; } = false;
    
    public string? RawReportJson { get; set; }
    public string? ErrorMessage { get; set; }
    
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAtUtc { get; set; }
    
    public List<WebsiteAuditFinding> Findings { get; set; } = [];

    // UI Helpers (Transient, NotMapped, JsonIgnore)
    [NotMapped]
    [JsonIgnore]
    public string CreatedAtPktDisplay => TimeHelper.ToPktString(CreatedAtUtc, "yyyy-MM-dd HH:mm");

    [NotMapped]
    [JsonIgnore]
    public string CompletedAtPktDisplay => CompletedAtUtc.HasValue ? TimeHelper.ToPktString(CompletedAtUtc.Value, "yyyy-MM-dd HH:mm") : "-";

    [NotMapped]
    [JsonIgnore]
    public string OverallScoreDisplay => OverallScore.HasValue ? $"{OverallScore}%" : "--";

    [NotMapped]
    [JsonIgnore]
    public string SecurityScoreDisplay => SecurityScore.HasValue ? $"{SecurityScore}%" : "--";

    [NotMapped]
    [JsonIgnore]
    public string PerformanceScoreDisplay => PerformanceScore.HasValue ? $"{PerformanceScore}%" : "--";

    [NotMapped]
    [JsonIgnore]
    public string SeoScoreDisplay => SeoScore.HasValue ? $"{SeoScore}%" : "--";

    [NotMapped]
    [JsonIgnore]
    public string AccessibilityScoreDisplay => AccessibilityScore.HasValue ? $"{AccessibilityScore}%" : "--";

    [NotMapped]
    [JsonIgnore]
    public string TtfbDisplay => TtfbMs.HasValue ? $"{TtfbMs:F0}ms" : "--";

    [NotMapped]
    [JsonIgnore]
    public string PayloadSizeDisplay => HtmlSizeBytes.HasValue ? $"{HtmlSizeBytes.Value / 1024:N0} KB" : "--";

    [NotMapped]
    [JsonIgnore]
    public string SslStatusDisplay => SslValid ? $"Valid SSL ({SslDaysUntilExpiry ?? 0}d remaining)" : "Invalid / Missing SSL";

    [NotMapped]
    [JsonIgnore]
    public string SecurityGradeDisplay => !string.IsNullOrWhiteSpace(SecurityHeadersGrade) ? $"Grade {SecurityHeadersGrade}" : "Grade N/A";

    [NotMapped]
    [JsonIgnore]
    public string HstsDisplay => HstsConfigured ? "Enforced (Pass)" : "Missing (Vulnerable)";

    [NotMapped]
    [JsonIgnore]
    public string CspDisplay => CspConfigured ? "Configured (Pass)" : "Missing (No CSP)";

    [NotMapped]
    [JsonIgnore]
    public string XFrameDisplay => XFrameOptionsConfigured ? "Protected (Pass)" : "Missing (Clickjack Risk)";

    [NotMapped]
    [JsonIgnore]
    public string RobotsDisplay => HasRobotsTxt ? "Configured (Present)" : "Missing";

    [NotMapped]
    [JsonIgnore]
    public string SitemapDisplay => HasSitemapXml ? "Configured (Present)" : "Missing";

    [NotMapped]
    [JsonIgnore]
    public string ViewportDisplay => ViewportConfigured ? "Mobile-Friendly" : "Desktop Only (Non-Responsive)";

    [NotMapped]
    [JsonIgnore]
    public string OpenGraphDisplay => OpenGraphConfigured ? "Configured (Rich Previews)" : "Missing";

    [NotMapped]
    [JsonIgnore]
    public string TwitterCardDisplay => TwitterCardConfigured ? "Configured" : "Missing";

    [NotMapped]
    [JsonIgnore]
    public int TotalFindingsCount => Findings?.Count ?? 0;

    [NotMapped]
    [JsonIgnore]
    public int CriticalCount => Findings?.Count(f => f.Severity == AuditSeverity.Critical) ?? 0;

    [NotMapped]
    [JsonIgnore]
    public int HighCount => Findings?.Count(f => f.Severity == AuditSeverity.High) ?? 0;

    [NotMapped]
    [JsonIgnore]
    public int MediumCount => Findings?.Count(f => f.Severity == AuditSeverity.Medium) ?? 0;

    [NotMapped]
    [JsonIgnore]
    public int LowCount => Findings?.Count(f => f.Severity == AuditSeverity.Low || f.Severity == AuditSeverity.Informational) ?? 0;
}

public class WebsiteAuditFinding
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string AuditId { get; set; } = string.Empty;
    public WebsiteAudit? Audit { get; set; }
    
    public AuditCategory Category { get; set; }
    public AuditSeverity Severity { get; set; }
    public string RuleCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? EvidenceText { get; set; }
    public string? RecommendedFix { get; set; }
    public string? BusinessImpact { get; set; }
    public string? SuggestedServiceId { get; set; }
    
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    // UI Helpers (Transient, NotMapped, JsonIgnore)
    [NotMapped]
    [JsonIgnore]
    public string SeverityText => Severity.ToString();

    [NotMapped]
    [JsonIgnore]
    public string SeverityIcon => Severity switch
    {
        AuditSeverity.Critical => "🚨",
        AuditSeverity.High => "⚠️",
        AuditSeverity.Medium => "⚡",
        AuditSeverity.Low => "ℹ️",
        _ => "💡"
    };

    [NotMapped]
    [JsonIgnore]
    public string SeverityBadgeBg => Severity switch
    {
        AuditSeverity.Critical => "#450A0A",
        AuditSeverity.High => "#431407",
        AuditSeverity.Medium => "#451A03",
        AuditSeverity.Low => "#082F49",
        _ => "#1E293B"
    };

    [NotMapped]
    [JsonIgnore]
    public string SeverityBadgeBorder => Severity switch
    {
        AuditSeverity.Critical => "#991B1B",
        AuditSeverity.High => "#C2410C",
        AuditSeverity.Medium => "#D97706",
        AuditSeverity.Low => "#0284C7",
        _ => "#334155"
    };

    [NotMapped]
    [JsonIgnore]
    public string SeverityBadgeFg => Severity switch
    {
        AuditSeverity.Critical => "#FCA5A5",
        AuditSeverity.High => "#FDBA74",
        AuditSeverity.Medium => "#FDE047",
        AuditSeverity.Low => "#7DD3FC",
        _ => "#94A3B8"
    };

    [NotMapped]
    [JsonIgnore]
    public string CategoryText => Category.ToString();

    [NotMapped]
    [JsonIgnore]
    public string CategoryIcon => Category switch
    {
        AuditCategory.Security => "🛡️",
        AuditCategory.Seo => "📈",
        AuditCategory.Performance => "⚡",
        AuditCategory.Accessibility => "♿",
        AuditCategory.Conversion => "🎯",
        _ => "🔍"
    };

    [NotMapped]
    [JsonIgnore]
    public string CategoryBadgeBg => Category switch
    {
        AuditCategory.Security => "#1E1B4B",
        AuditCategory.Seo => "#2E1065",
        AuditCategory.Performance => "#064E3B",
        AuditCategory.Accessibility => "#172554",
        AuditCategory.Conversion => "#3B1C08",
        _ => "#1E293B"
    };

    [NotMapped]
    [JsonIgnore]
    public string CategoryBadgeFg => Category switch
    {
        AuditCategory.Security => "#A5B4FC",
        AuditCategory.Seo => "#D8B4FE",
        AuditCategory.Performance => "#6EE7B7",
        AuditCategory.Accessibility => "#93C5FD",
        AuditCategory.Conversion => "#FDBA74",
        _ => "#CBD5E1"
    };

    [NotMapped]
    [JsonIgnore]
    public string DisplayRuleCode => !string.IsNullOrWhiteSpace(RuleCode) ? RuleCode : "DIAG-01";
}
