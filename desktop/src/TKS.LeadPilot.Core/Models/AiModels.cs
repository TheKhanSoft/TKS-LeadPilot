using System;
using System.Collections.Generic;

namespace TKS.LeadPilot.Core.Models;

public enum AiTone
{
    Consultative, // "Helpful Local Consultant" - friendly, educational, relationship-focused
    RoiFocused,   // "Direct ROI Problem-Solver" - commercial impact, lost sales, search drop-off
    Technical     // "Technical Specialist" - precise architecture, compliance, performance bottlenecks
}

public class GeminiConfig
{
    public bool IsEnabled { get; set; } = true;
    public string ModelName { get; set; } = "gemini-3.5-flash-lite";
    public int DailyRequestCeiling { get; set; } = 1200; // Free tier ceiling
    public int DailyRequestsUsed { get; set; } = 0;
    public DateTime LastResetDateUtc { get; set; } = DateTime.UtcNow.Date;
    public int RpmLimit { get; set; } = 12; // Free tier has 15 RPM max; local hard cap is 12 RPM
}

public class AuditProposalSynthesisRequest
{
    public Business Business { get; set; } = new();
    public WebsiteAudit Audit { get; set; } = new();
    public AiTone Tone { get; set; } = AiTone.Consultative;
    public string? CustomInstructions { get; set; }
    public bool IncludeTksServicePackages { get; set; } = true;
}

public class AuditProposalSynthesisResult
{
    public bool IsSuccess { get; set; }
    public bool IsLocalFallback { get; set; } = false;
    public AiTone Tone { get; set; } = AiTone.Consultative;
    public string ExecutiveSummary { get; set; } = string.Empty;
    public string KeyFindingsSummary { get; set; } = string.Empty;
    public string RecommendedActions { get; set; } = string.Empty;
    public string FullProposalMarkdown { get; set; } = string.Empty;
    public List<string> MatchedServices { get; set; } = [];
    public string? ErrorMessage { get; set; }
    public int? TokensUsed { get; set; }
    public int RequestsUsedToday { get; set; }
    public int DailyLimit { get; set; }
}

public class AuditComparisonResult
{
    public string BaselineAuditId { get; set; } = string.Empty;
    public string TargetAuditId { get; set; } = string.Empty;
    public DateTime BaselineDateUtc { get; set; }
    public DateTime TargetDateUtc { get; set; }
    
    public int OverallScoreDelta { get; set; }
    public int PerformanceScoreDelta { get; set; }
    public int SecurityScoreDelta { get; set; }
    public int SeoScoreDelta { get; set; }

    public List<WebsiteAuditFinding> ResolvedFindings { get; set; } = [];
    public List<WebsiteAuditFinding> NewFindings { get; set; } = [];
    public List<WebsiteAuditFinding> PersistentFindings { get; set; } = [];

    public string ComparisonSummaryMarkdown { get; set; } = string.Empty;
}
