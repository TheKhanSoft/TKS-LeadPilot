using TKS.LeadPilot.Core.Enums;

namespace TKS.LeadPilot.Core.Models;

public class LeadScore
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string BusinessId { get; set; } = string.Empty;
    public int TotalScore { get; set; }
    public string RuleVersion { get; set; } = "1.0.0";
    public bool IsManualOverride { get; set; } = false;
    public string? OverrideReason { get; set; }
    public DateTime CalculatedAtUtc { get; set; } = DateTime.UtcNow;
    
    public List<LeadScoreComponent> Components { get; set; } = [];
}

public class LeadScoreComponent
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string LeadScoreId { get; set; } = string.Empty;
    public string ComponentName { get; set; } = string.Empty;
    public int PointsAwarded { get; set; }
    public int MaxPointsPossible { get; set; }
    public string Explanation { get; set; } = string.Empty;
}

public class TksService
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal? StartingPriceUsd { get; set; }
    public decimal? StartingPricePkr { get; set; }
    public string PitchTemplate { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

public class TksServiceMatch
{
    public TksService Service { get; set; } = new();
    public int MatchConfidence { get; set; } // 0-100%
    public string MatchReason { get; set; } = string.Empty;
    public string GeneratedPitch { get; set; } = string.Empty;
}

public class SalesTask
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string BusinessId { get; set; } = string.Empty;
    public Business? Business { get; set; }
    
    public string Title { get; set; } = string.Empty;
    public DateTime? DueDateUtc { get; set; }
    public TaskPriority Priority { get; set; } = TaskPriority.Medium;
    public bool IsCompleted { get; set; } = false;
    public DateTime? CompletedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class SalesNote
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string BusinessId { get; set; } = string.Empty;
    public Business? Business { get; set; }
    
    public string Content { get; set; } = string.Empty;
    public string AuthorName { get; set; } = "Operator";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class AppSetting
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
