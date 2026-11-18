using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using TKS.LeadPilot.Core.Enums;

namespace TKS.LeadPilot.Core.Models;

public class Business : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected virtual void OnPropertyChanged(string propertyName)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private bool _isSelected = false;
    private bool _isStarred = false;

    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string? LegalName { get; set; }
    public string Category { get; set; } = string.Empty;
    public string? Subcategory { get; set; }
    public string? Description { get; set; }
    
    // Address
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? Locality { get; set; }
    public string City { get; set; } = string.Empty;
    public string? StateProvince { get; set; }
    public string? PostalCode { get; set; }
    public string Country { get; set; } = "Pakistan";
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    
    // Contact
    public string? PhonePrimary { get; set; }
    public string? EmailPrimary { get; set; }
    public string? WebsiteUrl { get; set; }
    public WebsiteStatus WebsiteStatus { get; set; } = WebsiteStatus.NoWebsite;
    
    // CRM & Scoring
    public string PipelineStageId { get; set; } = string.Empty;
    public PipelineStage? PipelineStage { get; set; }
    public int LeadScore { get; set; } = 0;
    public LeadStatus LeadStatus { get; set; } = LeadStatus.Discovered;
    public string? Tags { get; set; }
    
    public bool IsStarred
    {
        get => _isStarred;
        set
        {
            if (_isStarred != value)
            {
                _isStarred = value;
                OnPropertyChanged(nameof(IsStarred));
                OnPropertyChanged(nameof(StarDisplay));
            }
        }
    }
    public string StarDisplay => IsStarred ? "★" : "☆";

    // Transient UI Selection (Not stored in DB, ignored in JSON)
    [NotMapped]
    [JsonIgnore]
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                OnPropertyChanged(nameof(IsSelected));
            }
        }
    }

    // UI Helpers (Transient, NotMapped, JsonIgnored)
    [NotMapped]
    [JsonIgnore]
    public string RegionAndCountryDisplay
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(StateProvince) && !string.IsNullOrWhiteSpace(Country))
                return $"{StateProvince}, {Country}";
            if (!string.IsNullOrWhiteSpace(StateProvince))
                return StateProvince;
            return Country ?? string.Empty;
        }
    }

    [NotMapped]
    [JsonIgnore]
    public bool HasWebsite => !string.IsNullOrWhiteSpace(WebsiteUrl);

    [NotMapped]
    [JsonIgnore]
    public bool HasPhone => !string.IsNullOrWhiteSpace(PhonePrimary);

    [NotMapped]
    [JsonIgnore]
    public bool HasEmail => !string.IsNullOrWhiteSpace(EmailPrimary);

    [NotMapped]
    [JsonIgnore]
    public bool HasGmb => IsGoogleVerified || !string.IsNullOrWhiteSpace(GooglePlaceId);

    [NotMapped]
    [JsonIgnore]
    public string PhoneDisplay => !string.IsNullOrWhiteSpace(PhonePrimary) ? PhonePrimary : "—";

    [NotMapped]
    [JsonIgnore]
    public string EmailDisplay => !string.IsNullOrWhiteSpace(EmailPrimary) ? EmailPrimary : "—";

    [NotMapped]
    [JsonIgnore]
    public string StageDisplay => PipelineStage?.Name ?? (!string.IsNullOrWhiteSpace(PipelineStageId) ? PipelineStageId : "Discovered");
    
    // Provenance & Source
    public string? SourceProvider { get; set; }
    public string? SourceReferenceId { get; set; }

    // Google Places Enrichment (Stage 7)
    public string? GooglePlaceId { get; set; }
    public double? GoogleRating { get; set; }
    public int? GoogleReviewCount { get; set; }
    public string? BusinessHours { get; set; }
    public bool IsGoogleVerified { get; set; } = false;
    public DateTime? LastEnrichedAtUtc { get; set; }
    
    // Sync & Soft Delete
    public bool IsDeleted { get; set; } = false;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAtUtc { get; set; }
    public int Version { get; set; } = 1;
    
    // Navigation collections
    public List<BusinessContact> Contacts { get; set; } = [];
    public List<WebsiteAudit> Audits { get; set; } = [];
    public List<SalesTask> Tasks { get; set; } = [];
    public List<SalesNote> Notes { get; set; } = [];

    // Tagging Helpers
    public IReadOnlyList<string> GetTagList()
    {
        if (string.IsNullOrWhiteSpace(Tags)) return [];
        return Tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    public void AddTag(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return;
        var list = GetTagList().ToList();
        if (!list.Any(t => string.Equals(t, tag.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            list.Add(tag.Trim());
            Tags = string.Join(", ", list);
        }
    }

    public void RemoveTag(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag) || string.IsNullOrWhiteSpace(Tags)) return;
        var list = GetTagList().Where(t => !string.Equals(t, tag.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
        Tags = list.Count > 0 ? string.Join(", ", list) : null;
    }

    public bool HasTag(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag) || string.IsNullOrWhiteSpace(Tags)) return false;
        return GetTagList().Any(t => string.Equals(t, tag.Trim(), StringComparison.OrdinalIgnoreCase));
    }
}
