using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using TKS.LeadPilot.Core.Common;
using TKS.LeadPilot.Core.Enums;

namespace TKS.LeadPilot.Core.Models;

public class Business : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected virtual void OnPropertyChanged(string propertyName)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private bool _isSelected = false;
    private bool _isStarred = false;
    private string? _websiteUrl;
    private string? _googlePlaceId;
    private double? _googleRating;
    private int? _googleReviewCount;
    private string? _businessHours;
    private bool _isGoogleVerified = false;
    private string? _addressLine1;
    private string? _addressLine2;
    private string? _locality;
    private string _city = string.Empty;
    private string? _stateProvince;
    private string? _postalCode;
    private string _country = "Pakistan";

    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string? LegalName { get; set; }
    public string Category { get; set; } = string.Empty;
    public string? Subcategory { get; set; }
    public string? Description { get; set; }
    
    // Address
    public string? AddressLine1
    {
        get => _addressLine1;
        set
        {
            if (_addressLine1 != value)
            {
                _addressLine1 = value;
                OnPropertyChanged(nameof(AddressLine1));
                OnPropertyChanged(nameof(FullAddress));
            }
        }
    }

    public string? AddressLine2
    {
        get => _addressLine2;
        set
        {
            if (_addressLine2 != value)
            {
                _addressLine2 = value;
                OnPropertyChanged(nameof(AddressLine2));
                OnPropertyChanged(nameof(FullAddress));
            }
        }
    }

    public string? Locality
    {
        get => _locality;
        set
        {
            if (_locality != value)
            {
                _locality = value;
                OnPropertyChanged(nameof(Locality));
                OnPropertyChanged(nameof(FullAddress));
            }
        }
    }

    public string City
    {
        get => _city;
        set
        {
            if (_city != value)
            {
                _city = value;
                OnPropertyChanged(nameof(City));
                OnPropertyChanged(nameof(FullAddress));
            }
        }
    }

    public string? StateProvince
    {
        get => _stateProvince;
        set
        {
            if (_stateProvince != value)
            {
                _stateProvince = value;
                OnPropertyChanged(nameof(StateProvince));
                OnPropertyChanged(nameof(RegionAndCountryDisplay));
                OnPropertyChanged(nameof(FullAddress));
            }
        }
    }

    public string? PostalCode
    {
        get => _postalCode;
        set
        {
            if (_postalCode != value)
            {
                _postalCode = value;
                OnPropertyChanged(nameof(PostalCode));
                OnPropertyChanged(nameof(FullAddress));
            }
        }
    }

    public string Country
    {
        get => _country;
        set
        {
            if (_country != value)
            {
                _country = value;
                OnPropertyChanged(nameof(Country));
                OnPropertyChanged(nameof(RegionAndCountryDisplay));
                OnPropertyChanged(nameof(FullAddress));
            }
        }
    }

    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    [NotMapped]
    public string FullAddress
    {
        get
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(AddressLine1))
                parts.Add(AddressLine1.Trim());
            if (!string.IsNullOrWhiteSpace(AddressLine2) && !parts.Any(p => p.Contains(AddressLine2.Trim(), StringComparison.OrdinalIgnoreCase)))
                parts.Add(AddressLine2.Trim());
            if (!string.IsNullOrWhiteSpace(Locality) && !parts.Any(p => p.Contains(Locality.Trim(), StringComparison.OrdinalIgnoreCase)))
                parts.Add(Locality.Trim());
            if (!string.IsNullOrWhiteSpace(City) && !parts.Any(p => p.Contains(City.Trim(), StringComparison.OrdinalIgnoreCase)))
                parts.Add(City.Trim());
            if (!string.IsNullOrWhiteSpace(StateProvince) && !parts.Any(p => p.Contains(StateProvince.Trim(), StringComparison.OrdinalIgnoreCase)))
                parts.Add(StateProvince.Trim());
            if (!string.IsNullOrWhiteSpace(PostalCode) && !parts.Any(p => p.Contains(PostalCode.Trim(), StringComparison.OrdinalIgnoreCase)))
                parts.Add(PostalCode.Trim());
            if (!string.IsNullOrWhiteSpace(Country) && !parts.Any(p => p.Contains(Country.Trim(), StringComparison.OrdinalIgnoreCase)))
                parts.Add(Country.Trim());

            return parts.Count > 0 ? string.Join(", ", parts) : (!string.IsNullOrWhiteSpace(City) ? City : "—");
        }
    }

    public void NotifyAddressChanged()
    {
        OnPropertyChanged(nameof(AddressLine1));
        OnPropertyChanged(nameof(AddressLine2));
        OnPropertyChanged(nameof(Locality));
        OnPropertyChanged(nameof(City));
        OnPropertyChanged(nameof(StateProvince));
        OnPropertyChanged(nameof(PostalCode));
        OnPropertyChanged(nameof(Country));
        OnPropertyChanged(nameof(Latitude));
        OnPropertyChanged(nameof(Longitude));
        OnPropertyChanged(nameof(RegionAndCountryDisplay));
        OnPropertyChanged(nameof(FullAddress));
    }
    
    // Contact
    public string? PhonePrimary { get; set; }
    public string? EmailPrimary { get; set; }
    public string? WhatsAppNumber { get; set; }
    public string? WebsiteUrl
    {
        get => _websiteUrl;
        set
        {
            if (_websiteUrl != value)
            {
                _websiteUrl = value;
                OnPropertyChanged(nameof(WebsiteUrl));
                OnPropertyChanged(nameof(HasWebsite));
            }
        }
    }
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
                OnPropertyChanged(nameof(StarColor));
            }
        }
    }
    public string StarDisplay => IsStarred ? "★" : "☆";
    public string StarColor => IsStarred ? "#FBBF24" : "#64748B";

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
    public bool HasGmb => IsGoogleVerified || !string.IsNullOrWhiteSpace(GooglePlaceId) || (GoogleRating.HasValue && GoogleRating.Value > 0) || (GoogleReviewCount.HasValue && GoogleReviewCount.Value > 0);

    [NotMapped]
    [JsonIgnore]
    public string GmbRatingDisplay => (GoogleRating.HasValue && GoogleRating.Value > 0)
        ? $"★ {GoogleRating.Value:F1}"
        : "✓ GMB";

    [NotMapped]
    [JsonIgnore]
    public string GmbTooltip => HasGmb
        ? $"Verified Google Business Profile • Rating: {(GoogleRating.HasValue ? GoogleRating.Value.ToString("F1") + "★" : "Listed")} • Reviews: {(GoogleReviewCount.HasValue ? GoogleReviewCount.Value.ToString("N0") : "Available")}"
        : "Unverified / No Google Business Profile detected (Outreach Opportunity)";

    public void NotifyGmbChanged()
    {
        OnPropertyChanged(nameof(IsGoogleVerified));
        OnPropertyChanged(nameof(GooglePlaceId));
        OnPropertyChanged(nameof(GoogleRating));
        OnPropertyChanged(nameof(GoogleReviewCount));
        OnPropertyChanged(nameof(BusinessHours));
        OnPropertyChanged(nameof(HasGmb));
        OnPropertyChanged(nameof(GmbRatingDisplay));
        OnPropertyChanged(nameof(GmbTooltip));
    }

    [NotMapped]
    [JsonIgnore]
    public string PhoneDisplay => !string.IsNullOrWhiteSpace(PhonePrimary) ? PhonePrimary : "—";

    [NotMapped]
    [JsonIgnore]
    public string EmailDisplay => !string.IsNullOrWhiteSpace(EmailPrimary) ? EmailPrimary : "—";

    [NotMapped]
    [JsonIgnore]
    public string WhatsAppDisplay => !string.IsNullOrWhiteSpace(WhatsAppNumber) 
        ? WhatsAppNumber 
        : (!string.IsNullOrWhiteSpace(PhonePrimary) ? PhonePrimary : "—");

    [NotMapped]
    [JsonIgnore]
    public bool HasWhatsApp => !string.IsNullOrWhiteSpace(WhatsAppDirectUrl);

    [NotMapped]
    [JsonIgnore]
    public string? WhatsAppDirectUrl
    {
        get
        {
            var raw = !string.IsNullOrWhiteSpace(WhatsAppNumber) ? WhatsAppNumber : PhonePrimary;
            if (string.IsNullOrWhiteSpace(raw)) return null;

            var digits = new string(raw.Where(char.IsDigit).ToArray());
            if (string.IsNullOrEmpty(digits)) return null;

            // Normalize Pakistani numbers
            if (digits.StartsWith("03") && digits.Length == 11)
            {
                digits = "92" + digits[1..];
            }
            else if (digits.StartsWith("3") && digits.Length == 10 && (Country?.Contains("Pakistan", StringComparison.OrdinalIgnoreCase) ?? true))
            {
                digits = "92" + digits;
            }
            // Normalize North American numbers
            else if (digits.Length == 10 && (Country?.Contains("United States", StringComparison.OrdinalIgnoreCase) ?? false))
            {
                digits = "1" + digits;
            }

            return digits.Length >= 7 ? $"https://wa.me/{digits}" : null;
        }
    }

    public void NotifyContactChanged()
    {
        OnPropertyChanged(nameof(PhonePrimary));
        OnPropertyChanged(nameof(EmailPrimary));
        OnPropertyChanged(nameof(WhatsAppNumber));
        OnPropertyChanged(nameof(WebsiteUrl));
        OnPropertyChanged(nameof(HasPhone));
        OnPropertyChanged(nameof(HasEmail));
        OnPropertyChanged(nameof(HasWebsite));
        OnPropertyChanged(nameof(HasWhatsApp));
        OnPropertyChanged(nameof(PhoneDisplay));
        OnPropertyChanged(nameof(EmailDisplay));
        OnPropertyChanged(nameof(WhatsAppDisplay));
        OnPropertyChanged(nameof(WhatsAppDirectUrl));
        OnPropertyChanged(nameof(LastEnrichedAtUtc));
        OnPropertyChanged(nameof(LastEnrichedAtPktDisplay));
    }

    [NotMapped]
    [JsonIgnore]
    public string StageDisplay => PipelineStage?.Name ?? (!string.IsNullOrWhiteSpace(PipelineStageId) ? PipelineStageId : "Discovered");
    
    // Provenance & Source
    public string? SourceProvider { get; set; }
    public string? SourceReferenceId { get; set; }

    // Google Places Enrichment (Stage 7)
    public string? GooglePlaceId
    {
        get => _googlePlaceId;
        set
        {
            if (_googlePlaceId != value)
            {
                _googlePlaceId = value;
                OnPropertyChanged(nameof(GooglePlaceId));
                OnPropertyChanged(nameof(HasGmb));
                OnPropertyChanged(nameof(GmbTooltip));
            }
        }
    }

    public double? GoogleRating
    {
        get => _googleRating;
        set
        {
            if (_googleRating != value)
            {
                _googleRating = value;
                OnPropertyChanged(nameof(GoogleRating));
                OnPropertyChanged(nameof(HasGmb));
                OnPropertyChanged(nameof(GmbRatingDisplay));
                OnPropertyChanged(nameof(GmbTooltip));
            }
        }
    }

    public int? GoogleReviewCount
    {
        get => _googleReviewCount;
        set
        {
            if (_googleReviewCount != value)
            {
                _googleReviewCount = value;
                OnPropertyChanged(nameof(GoogleReviewCount));
                OnPropertyChanged(nameof(HasGmb));
                OnPropertyChanged(nameof(GmbRatingDisplay));
                OnPropertyChanged(nameof(GmbTooltip));
            }
        }
    }

    public string? BusinessHours
    {
        get => _businessHours;
        set
        {
            if (_businessHours != value)
            {
                _businessHours = value;
                OnPropertyChanged(nameof(BusinessHours));
            }
        }
    }

    public bool IsGoogleVerified
    {
        get => _isGoogleVerified;
        set
        {
            if (_isGoogleVerified != value)
            {
                _isGoogleVerified = value;
                OnPropertyChanged(nameof(IsGoogleVerified));
                OnPropertyChanged(nameof(HasGmb));
                OnPropertyChanged(nameof(GmbTooltip));
            }
        }
    }
    public DateTime? LastEnrichedAtUtc { get; set; }
    
    // UI Helpers in PKT (Pakistan Standard Time, UTC+5)
    [NotMapped]
    [JsonIgnore]
    public string CreatedAtPktDisplay => TimeHelper.ToPktString(CreatedAtUtc, "yyyy-MM-dd HH:mm");

    [NotMapped]
    [JsonIgnore]
    public string UpdatedAtPktDisplay => TimeHelper.ToPktString(UpdatedAtUtc, "yyyy-MM-dd HH:mm");

    [NotMapped]
    [JsonIgnore]
    public string LastEnrichedAtPktDisplay => LastEnrichedAtUtc.HasValue ? $"{TimeHelper.ToPktString(LastEnrichedAtUtc.Value, "yyyy-MM-dd HH:mm")} PKT" : "Never";

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
