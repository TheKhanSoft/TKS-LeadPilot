using System;
using System.Collections.Generic;

namespace TKS.LeadPilot.Core.Models;

public class ContactEnrichmentResult
{
    public string BusinessId { get; set; } = string.Empty;
    public string BusinessName { get; set; } = string.Empty;
    public bool IsSuccess { get; set; }
    public bool HasNewData { get; set; }
    
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? WhatsAppNumber { get; set; }
    public string? WhatsAppUrl { get; set; }
    public string? ContactPerson { get; set; }
    public string? JobTitle { get; set; }
    
    public List<string> DiscoveredEmails { get; set; } = [];
    public List<string> DiscoveredPhones { get; set; } = [];
    public Dictionary<string, string> SocialLinks { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    
    public string MethodUsed { get; set; } = "Deep Scanner";
    public string? ErrorMessage { get; set; }
    public DateTime ProcessedAtUtc { get; set; } = DateTime.UtcNow;
}
