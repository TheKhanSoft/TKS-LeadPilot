namespace TKS.LeadPilot.Core.Models;

public enum DuplicateMatchType
{
    None = 0,
    ExactWebsite = 1,
    ExactPhone = 2,
    ExactNameAndCity = 3,
    FuzzyNameAndCity = 4
}

public enum DeduplicationStrategy
{
    Skip = 0,
    Merge = 1,
    Overwrite = 2
}

public class DuplicateMatchResult
{
    public Business IncomingBusiness { get; set; } = null!;
    public Business? ExistingBusiness { get; set; }
    public string? MatchedExistingId => ExistingBusiness?.Id;
    public DuplicateMatchType MatchType { get; set; } = DuplicateMatchType.None;
    public int ConfidenceScore { get; set; } = 0;
    public string MatchReason { get; set; } = string.Empty;
    public bool IsDuplicate => ConfidenceScore >= 80 && ExistingBusiness != null;
}

public class DeduplicationOptions
{
    public int ConfidenceThreshold { get; set; } = 80;
    public bool MatchWebsite { get; set; } = true;
    public bool MatchPhone { get; set; } = true;
    public bool MatchNameAndCity { get; set; } = true;
    public double FuzzyNameThreshold { get; set; } = 0.82;
}
