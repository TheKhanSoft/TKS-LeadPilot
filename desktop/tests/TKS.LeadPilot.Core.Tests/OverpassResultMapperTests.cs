using System.Collections.Generic;
using FluentAssertions;
using TKS.LeadPilot.Core.Enums;
using TKS.LeadPilot.Core.Services;
using Xunit;

namespace TKS.LeadPilot.Core.Tests;

public class OverpassResultMapperTests
{
    [Fact]
    public void MapJsonToBusinesses_ParsesNodeWithFullTags()
    {
        var sampleJson = """
        {
          "version": 0.6,
          "elements": [
            {
              "type": "node",
              "id": 100234,
              "lat": 24.8607,
              "lon": 67.0011,
              "tags": {
                "name": "Dr. Tariq Dental Surgery",
                "amenity": "dentist",
                "website": "https://tariqdental.pk",
                "phone": "+92 21 34567890",
                "contact:email": "info@tariqdental.pk",
                "addr:housenumber": "14-C",
                "addr:street": "Main Clifton Road",
                "addr:city": "Karachi",
                "addr:postcode": "75600"
              }
            }
          ]
        }
        """;

        var businesses = OverpassResultMapper.MapJsonToBusinesses(sampleJson, "Karachi");

        businesses.Should().HaveCount(1);
        var b = businesses[0];
        b.Name.Should().Be("Dr. Tariq Dental Surgery");
        b.Category.Should().Be("Dentist & Dental Clinic");
        b.City.Should().Be("Karachi");
        b.AddressLine1.Should().Be("14-C Main Clifton Road");
        b.PhonePrimary.Should().Be("+92 21 34567890");
        b.EmailPrimary.Should().Be("info@tariqdental.pk");
        b.WebsiteUrl.Should().Be("https://tariqdental.pk");
        b.WebsiteStatus.Should().Be(WebsiteStatus.ActiveWebsite);
        b.Latitude.Should().Be(24.8607);
        b.Longitude.Should().Be(67.0011);
        b.SourceProvider.Should().Be("OpenStreetMap");
        b.SourceReferenceId.Should().Be("osm:node:100234");
        b.PipelineStageId.Should().Be("stage-discovered");
    }

    [Fact]
    public void MapJsonToBusinesses_ParsesWayWithCenterCoordinates()
    {
        var sampleJson = """
        {
          "version": 0.6,
          "elements": [
            {
              "type": "way",
              "id": 554433,
              "center": {
                "lat": 31.5204,
                "lon": 74.3587
              },
              "tags": {
                "name": "Careway Pharmacy",
                "amenity": "pharmacy",
                "addr:city": "Lahore"
              }
            }
          ]
        }
        """;

        var businesses = OverpassResultMapper.MapJsonToBusinesses(sampleJson, "Lahore");

        businesses.Should().HaveCount(1);
        var b = businesses[0];
        b.Name.Should().Be("Careway Pharmacy");
        b.Category.Should().Be("Pharmacy & Healthcare");
        b.City.Should().Be("Lahore");
        b.Latitude.Should().Be(31.5204);
        b.Longitude.Should().Be(74.3587);
        b.WebsiteStatus.Should().Be(WebsiteStatus.NoWebsite);
        b.SourceReferenceId.Should().Be("osm:way:554433");
    }

    [Fact]
    public void MapJsonToBusinesses_SkipsElementsWithoutNameOrTags()
    {
        var sampleJson = """
        {
          "version": 0.6,
          "elements": [
            {
              "type": "node",
              "id": 1,
              "lat": 24.86,
              "lon": 67.00
            },
            {
              "type": "node",
              "id": 2,
              "lat": 24.87,
              "lon": 67.01,
              "tags": {
                "amenity": "bench"
              }
            }
          ]
        }
        """;

        var businesses = OverpassResultMapper.MapJsonToBusinesses(sampleJson, "Karachi");
        businesses.Should().BeEmpty("unnamed points of interest should be discarded");
    }

    [Fact]
    public void MapJsonToBusinesses_ParsesExhaustiveOsmAddressTagsAndBuildsFullAddress()
    {
        var sampleJson = """
        {
          "version": 0.6,
          "elements": [
            {
              "type": "node",
              "id": 999888,
              "lat": 31.5204,
              "lon": 74.3587,
              "tags": {
                "name": "Al-Rehman Diagnostic Complex",
                "amenity": "clinic",
                "addr:housename": "Al-Rehman Center",
                "addr:housenumber": "Plot 45",
                "addr:street": "Main Boulevard",
                "addr:block": "Block H",
                "addr:suburb": "Gulberg II",
                "addr:city": "Lahore",
                "addr:state": "Punjab",
                "addr:postcode": "54660",
                "addr:country": "Pakistan"
              }
            }
          ]
        }
        """;

        var businesses = OverpassResultMapper.MapJsonToBusinesses(sampleJson, "Lahore");

        businesses.Should().HaveCount(1);
        var b = businesses[0];
        b.Name.Should().Be("Al-Rehman Diagnostic Complex");
        b.AddressLine1.Should().Contain("Al-Rehman Center");
        b.AddressLine1.Should().Contain("Plot 45 Main Boulevard");
        b.AddressLine1.Should().Contain("Block H");
        b.Locality.Should().Be("Gulberg II");
        b.PostalCode.Should().Be("54660");
        b.FullAddress.Should().Be("Al-Rehman Center, Plot 45 Main Boulevard, Block H, Gulberg II, Lahore, Punjab, 54660, Pakistan");
    }
}
