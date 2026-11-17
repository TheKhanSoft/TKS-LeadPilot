using System;
using System.Collections.Generic;

namespace TKS.LeadPilot.Core.Models;

public class GooglePlacesConfig
{
    public bool IsEnabled { get; set; } = false;
    public int DailyRequestCeiling { get; set; } = 50;
    public int DailyRequestsUsed { get; set; } = 0;
    public DateTime LastResetDateUtc { get; set; } = DateTime.UtcNow.Date;
}

public class GooglePlacesEnrichmentResult
{
    public bool IsSuccess { get; set; }
    public string? PlaceId { get; set; }
    public string? BusinessName { get; set; }
    public string? FormattedAddress { get; set; }
    public string? Phone { get; set; }
    public string? Website { get; set; }
    public double? Rating { get; set; }
    public int? UserRatingsTotal { get; set; }
    public List<string> OpeningHours { get; set; } = [];
    public string? BusinessStatus { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public bool IsCached { get; set; } = false;
    public string? ErrorMessage { get; set; }
    public int RequestsUsedToday { get; set; }
    public int DailyLimit { get; set; }
}
