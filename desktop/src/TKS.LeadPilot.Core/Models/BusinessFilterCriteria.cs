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

public class BusinessFilterCriteria
{
    public string? SearchQuery { get; set; }
    public string? City { get; set; }
    public string? Category { get; set; }
    public string? PipelineStageId { get; set; }
    public bool? HasWebsite { get; set; }
    public int? MinScore { get; set; }
    public int? MaxScore { get; set; }
    public string? Tag { get; set; }
    public bool? IsStarred { get; set; }
    public BusinessSortOption SortBy { get; set; } = BusinessSortOption.ScoreDescending;
    public int Skip { get; set; } = 0;
    public int Take { get; set; } = 200;

    public bool IsDefault =>
        string.IsNullOrWhiteSpace(SearchQuery) &&
        string.IsNullOrWhiteSpace(City) &&
        string.IsNullOrWhiteSpace(Category) &&
        string.IsNullOrWhiteSpace(PipelineStageId) &&
        !HasWebsite.HasValue &&
        !MinScore.HasValue &&
        !MaxScore.HasValue &&
        string.IsNullOrWhiteSpace(Tag) &&
        !IsStarred.HasValue;

    public void Reset()
    {
        SearchQuery = null;
        City = null;
        Category = null;
        PipelineStageId = null;
        HasWebsite = null;
        MinScore = null;
        MaxScore = null;
        Tag = null;
        IsStarred = null;
        SortBy = BusinessSortOption.ScoreDescending;
        Skip = 0;
        Take = 200;
    }
}
