using TKS.LeadPilot.Core.Enums;

namespace TKS.LeadPilot.Core.Models;

public class Business
{
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
    public bool IsStarred { get; set; } = false;
    
    // Provenance & Source
    public string? SourceProvider { get; set; }
    public string? SourceReferenceId { get; set; }
    
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
