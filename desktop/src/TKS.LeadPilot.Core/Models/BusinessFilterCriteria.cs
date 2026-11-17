namespace TKS.LeadPilot.Core.Models;

public enum BusinessSortOption
{
    ScoreDescending = 0,
    ScoreAscending = 1,
    NameAscending = 2,
    NameDescending = 3,
    CreatedDescending = 4,
    UpdatedDescending = 5
}

public enum QuickFilterType
{
    All = 0,
    HotLeads = 1,          // Score >= 70
    NoWebsite = 2,         // WebsiteUrl is empty
    GoogleVerified = 3,    // IsGoogleVerified == true
    TopRated = 4,          // GoogleRating >= 4.5
    NeedsAudit = 5,        // Audits is empty and has website
    MissingGmb = 6,        // No Google My Business account (IsGoogleVerified == false && GooglePlaceId is null/empty)
    StarredOnly = 7,       // IsStarred == true
    HasPhone = 8,          // PhonePrimary is not empty
    MissingContact = 9     // PhonePrimary and EmailPrimary are both empty
}

public class BusinessFilterCriteria
{
    public string? SearchQuery { get; set; }
    public string? Country { get; set; }
    public string? StateProvince { get; set; }
    public string? City { get; set; }
    public string? Category { get; set; }
    public string? PipelineStageId { get; set; }
    public bool? HasWebsite { get; set; }
    public bool? HasPhone { get; set; }
    public bool? HasEmail { get; set; }
    public bool? IsGoogleVerified { get; set; }
    public double? MinRating { get; set; }
    public int? MinScore { get; set; }
    public int? MaxScore { get; set; }
    public string? Tag { get; set; }
    public bool? IsStarred { get; set; }
    public QuickFilterType QuickFilter { get; set; } = QuickFilterType.All;
    public BusinessSortOption SortBy { get; set; } = BusinessSortOption.ScoreDescending;
    public int Skip { get; set; } = 0;
    public int Take { get; set; } = 200;

    public bool IsDefault =>
        string.IsNullOrWhiteSpace(SearchQuery) &&
        string.IsNullOrWhiteSpace(Country) &&
        string.IsNullOrWhiteSpace(StateProvince) &&
        string.IsNullOrWhiteSpace(City) &&
        string.IsNullOrWhiteSpace(Category) &&
        string.IsNullOrWhiteSpace(PipelineStageId) &&
        !HasWebsite.HasValue &&
        !IsGoogleVerified.HasValue &&
        !MinRating.HasValue &&
        !MinScore.HasValue &&
        !MaxScore.HasValue &&
        string.IsNullOrWhiteSpace(Tag) &&
        !IsStarred.HasValue &&
        QuickFilter == QuickFilterType.All;

    public void Reset()
    {
        SearchQuery = null;
        Country = null;
        StateProvince = null;
        City = null;
        Category = null;
        PipelineStageId = null;
        HasWebsite = null;
        IsGoogleVerified = null;
        MinRating = null;
        MinScore = null;
        MaxScore = null;
        Tag = null;
        IsStarred = null;
        QuickFilter = QuickFilterType.All;
        SortBy = BusinessSortOption.ScoreDescending;
        Skip = 0;
        Take = 200;
    }
}
