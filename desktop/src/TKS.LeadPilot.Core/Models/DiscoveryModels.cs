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
    public string SearchEngine { get; set; } = "All Sources (Unified Aggregator)";
    public string? CategoryPreset { get; set; }
    public string? CustomSearchTerm { get; set; }
    public bool RequirePhone { get; set; } = false;
    public bool TargetMissingWebsiteOnly { get; set; } = false;
    public bool TargetMissingGmbOnly { get; set; } = false;
    public bool DeepScan { get; set; } = true;
    public bool StrictKeywordFilter { get; set; } = true;
}

public class DiscoveredLead : System.ComponentModel.INotifyPropertyChanged
{
    private bool _isSelectedForImport = true;
    private Business _business = null!;
    private DuplicateMatchResult? _duplicateMatch;

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    public Business Business
    {
        get => _business;
        set
        {
            _business = value;
            OnPropertyChanged(nameof(Business));
        }
    }

    public DuplicateMatchResult? DuplicateMatch
    {
        get => _duplicateMatch;
        set
        {
            _duplicateMatch = value;
            OnPropertyChanged(nameof(DuplicateMatch));
            OnPropertyChanged(nameof(IsDuplicate));
            OnPropertyChanged(nameof(DuplicateStatusText));
        }
    }

    public bool IsDuplicate => DuplicateMatch?.IsDuplicate ?? false;

    public string DuplicateStatusText => IsDuplicate
        ? $"⚠️ Duplicate ({DuplicateMatch?.MatchReason ?? "Already in database"})"
        : "✅ New Lead";

    private int _relevanceScore = 100;
    public int RelevanceScore
    {
        get => _relevanceScore;
        set
        {
            if (_relevanceScore != value)
            {
                _relevanceScore = value;
                OnPropertyChanged(nameof(RelevanceScore));
                OnPropertyChanged(nameof(RelevanceBadge));
            }
        }
    }

    public string RelevanceBadge => RelevanceScore >= 85
        ? $"🎯 Exact Match ({RelevanceScore}%)"
        : RelevanceScore >= 50
            ? $"⭐ Niche Match ({RelevanceScore}%)"
            : $"🏢 Commercial ({RelevanceScore}%)";

    public bool IsSelectedForImport
    {
        get => _isSelectedForImport;
        set
        {
            if (_isSelectedForImport != value)
            {
                _isSelectedForImport = value;
                OnPropertyChanged(nameof(IsSelectedForImport));
            }
        }
    }

    protected void OnPropertyChanged(string propertyName)
        => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
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
