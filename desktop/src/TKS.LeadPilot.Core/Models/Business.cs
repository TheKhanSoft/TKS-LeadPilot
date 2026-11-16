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
}
