using System.Collections.Generic;
using FluentAssertions;
using TKS.LeadPilot.Core.Models;
using TKS.LeadPilot.Core.Services;
using Xunit;

namespace TKS.LeadPilot.Core.Tests;

public class OverpassQueryBuilderTests
{
    [Fact]
    public void BuildQuery_WithKnownCity_UsesCityBoundingBox()
    {
        var query = new DiscoveryQuery
        {
            City = "Karachi",
            TargetAmenities = ["clinic", "dentist"],
            Limit = 50,
            TimeoutSeconds = 25
        };

        var ql = OverpassQueryBuilder.BuildQuery(query);

        ql.Should().Contain("[out:json][timeout:25];");
        ql.Should().Contain("node[\"amenity\"=\"clinic\"](24.7500,66.8500,25.1000,67.2500);");
        ql.Should().Contain("way[\"amenity\"=\"dentist\"](24.7500,66.8500,25.1000,67.2500);");
        ql.Should().Contain("out center 50;");
    }

    [Fact]
    public void BuildQuery_WithExplicitBoundingBox_OverridesCityDefault()
    {
        var query = new DiscoveryQuery
        {
            City = "Lahore",
            BBox = new BoundingBox(31.50, 74.30, 31.55, 74.35),
            TargetAmenities = ["pharmacy"],
            Limit = 100
        };

        var ql = OverpassQueryBuilder.BuildQuery(query);

        ql.Should().Contain("node[\"amenity\"=\"pharmacy\"](31.5000,74.3000,31.5500,74.3500);");
        ql.Should().Contain("out center 100;");
    }

    [Fact]
    public void BuildQuery_WithUnknownCity_FallsBackToAreaSearch()
    {
        var query = new DiscoveryQuery
        {
            City = "UnknownTown",
            TargetAmenities = ["hospital"]
        };

        var ql = OverpassQueryBuilder.BuildQuery(query);

        ql.Should().Contain("area[\"name\"=\"UnknownTown\"]->.searchArea;");
        ql.Should().Contain("node[\"amenity\"=\"hospital\"](area.searchArea);");
    }

    [Fact]
    public void CategoryPresets_ContainStandardPakistaniBusinessSectors()
    {
        var presets = OverpassQueryBuilder.CategoryPresets;

        presets.Should().ContainKey("Healthcare & Dental");
        presets["Healthcare & Dental"].Should().Contain(new[] { "clinic", "dentist", "pharmacy" });

        presets.Should().ContainKey("Restaurants & Food");
        presets["Restaurants & Food"].Should().Contain(new[] { "restaurant", "cafe" });

        presets.Should().ContainKey("Hotels & Lodging");
        presets["Hotels & Lodging"].Should().Contain("hotel");
    }
}
