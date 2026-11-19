using System;
using System.Collections.Generic;

namespace TKS.LeadPilot.Core.Models;

public class CountryInfo
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string FlagEmoji { get; set; } = string.Empty;
    public List<StateProvinceInfo> States { get; set; } = [];

    public string DisplayName => $"{FlagEmoji} {Name}".Trim();

    public override string ToString() => DisplayName;
}

public class StateProvinceInfo
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public List<CityInfo> Cities { get; set; } = [];

    public override string ToString() => Name;
}

public class CityInfo
{
    public string Name { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public BoundingBox BoundingBox { get; set; } = new();
    public List<string> SuggestedNiches { get; set; } = [];

    public override string ToString() => Name;
}

public class NearbyLocationResult
{
    public string Country { get; set; } = "Pakistan";
    public string StateProvince { get; set; } = "Punjab";
    public string City { get; set; } = "Lahore";
    public double Latitude { get; set; } = 31.5204;
    public double Longitude { get; set; } = 74.3587;
    public double RadiusKm { get; set; } = 15.0;
    public BoundingBox BoundingBox { get; set; } = new(31.40, 74.15, 31.65, 74.45);
    public bool IsDetectedFromIp { get; set; } = false;
    public bool Success { get; set; } = true;
    public string Message { get; set; } = string.Empty;
}

public class DetailedAddressResult
{
    public string? FullAddress { get; set; }
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? Locality { get; set; }
    public string? City { get; set; }
    public string? StateProvince { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? PlaceId { get; set; }
    public string? DisplayName { get; set; }
    public bool Success { get; set; } = true;
    public string? Source { get; set; }
}
