using System;
using System.Collections.Generic;

namespace TKS.LeadPilot.Core.Models;

public class BoundingBox
{
    public double South { get; set; }
    public double West { get; set; }
    public double North { get; set; }
    public double East { get; set; }

    public BoundingBox() { }

    public BoundingBox(double south, double west, double north, double east)
    {
        South = south;
        West = west;
        North = north;
        East = east;
    }

    public override string ToString() => $"{South:F4},{West:F4},{North:F4},{East:F4}";
}

public class DiscoveryQuery
{
    public string City { get; set; } = "Karachi";
    public string Country { get; set; } = "Pakistan";
    public string? StateProvince { get; set; }
    public List<string> TargetAmenities { get; set; } = ["clinic", "dentist", "pharmacy", "hospital"];
    public BoundingBox? BBox { get; set; }
    public int Limit { get; set; } = 150;
    public int TimeoutSeconds { get; set; } = 30;
}

public class DiscoveredLead
{
    public Business Business { get; set; } = null!;
    public DuplicateMatchResult? DuplicateMatch { get; set; }
    public bool IsDuplicate => DuplicateMatch?.IsDuplicate ?? false;
    public bool IsSelectedForImport { get; set; } = true;
}

public class DiscoveryResult
{
    public int TotalFetched => Leads.Count;
    public int NewCount { get; set; }
    public int DuplicateCount { get; set; }
    public List<DiscoveredLead> Leads { get; set; } = [];
    public string QueryExecuted { get; set; } = string.Empty;
    public string Provider { get; set; } = "OpenStreetMap Overpass";
    public TimeSpan ExecutionDuration { get; set; }
    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
    public string? ErrorMessage { get; set; }
}
