using System;
using System.Collections.Generic;

namespace TKS.LeadPilot.Core.Models;

public enum PlacesSkuTier
{
    Essentials = 0,               // 10,000 Free Calls / Month (Basic Place Data, Name, Address, Phone, Website, Location)
    EnterpriseWithAtmosphere = 1  // 1,000 Free Calls / Month (Includes Ratings, Reviews, Business Hours, Atmosphere)
}

public class GooglePlacesConfig
{
    public bool IsEnabled { get; set; } = false;
    public PlacesSkuTier SkuTier { get; set; } = PlacesSkuTier.EnterpriseWithAtmosphere;
    public int DailyRequestCeiling { get; set; } = 50;
    public int DailyRequestsUsed { get; set; } = 0;
    public int MonthlyRequestsUsed { get; set; } = 0;
    public DateTime LastResetDateUtc { get; set; } = DateTime.UtcNow.Date;
    public DateTime LastMonthlyResetDateUtc { get; set; } = DateTime.UtcNow.Date;

    // Per-SKU Free Monthly Usage Caps (Google Places API New)
    public int MonthlyFreeAllowance => SkuTier == PlacesSkuTier.Essentials ? 10000 : 1000;
    public int DailyRequestsRemaining => Math.Max(0, DailyRequestCeiling - DailyRequestsUsed);
    public int MonthlyRequestsRemaining => Math.Max(0, MonthlyFreeAllowance - MonthlyRequestsUsed);
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
    public int MonthlyRequestsUsed { get; set; }
    public int MonthlyLimit { get; set; }
    public PlacesSkuTier SkuTier { get; set; }
}
