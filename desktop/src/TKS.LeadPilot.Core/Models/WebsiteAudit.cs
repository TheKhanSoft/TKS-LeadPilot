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
    public int? PerformanceScore { get; set; }
    public int? SeoScore { get; set; }
    public int? AccessibilityScore { get; set; }
    public int? SecurityScore { get; set; }
    
    public string? RawReportJson { get; set; }
    public string? ErrorMessage { get; set; }
    
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAtUtc { get; set; }
    
    public List<WebsiteAuditFinding> Findings { get; set; } = [];
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
}
