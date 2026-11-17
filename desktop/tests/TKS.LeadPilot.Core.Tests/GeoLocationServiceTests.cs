using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using TKS.LeadPilot.Core.Services;
using Xunit;

namespace TKS.LeadPilot.Core.Tests;

public class GeoLocationServiceTests
{
    private readonly GeoLocationService _service = new();

    [Fact]
    public void GetAllCountries_ReturnsGlobalTierOneAndRegionalCountries()
    {
        var countries = _service.GetAllCountries();

        countries.Should().NotBeNullOrEmpty();
        countries.Count.Should().BeGreaterOrEqualTo(30);
        countries.Select(c => c.Name).Should().Contain(new[]
        {
            "Pakistan",
            "United States",
            "United Kingdom",
            "United Arab Emirates",
            "Canada",
            "Saudi Arabia",
            "Australia",
            "Germany",
            "Switzerland",
            "France",
            "Sweden",
            "Denmark",
            "Norway",
            "New Zealand",
            "Spain",
            "Italy",
            "Japan",
            "South Korea",
            "Hong Kong",
            "Estonia",
            "Poland",
            "South Africa",
            "India"
        });
    }

    [Theory]
    [InlineData("Pakistan", "Punjab", "Lahore")]
    [InlineData("Pakistan", "Khyber Pakhtunkhwa", "Peshawar")]
    [InlineData("Pakistan", "Khyber Pakhtunkhwa", "Haripur")]
    [InlineData("United States", "California", "Los Angeles")]
    [InlineData("United States", "New York", "New York City")]
    [InlineData("United Kingdom", "England", "London")]
    [InlineData("United Arab Emirates", "Dubai", "Dubai")]
    [InlineData("Switzerland", "Zurich", "Zurich")]
    [InlineData("Switzerland", "Zug", "Zug")]
    [InlineData("France", "Île-de-France", "Paris")]
    [InlineData("Sweden", "Stockholm County", "Stockholm")]
    [InlineData("Japan", "Tokyo Metropolis", "Tokyo")]
    [InlineData("Estonia", "Harju County", "Tallinn")]
    [InlineData("India", "Karnataka", "Bengaluru")]
    public void CascadingHierarchies_CountryStateCity_BindCorrectly(string countryName, string stateName, string expectedCity)
    {
        var states = _service.GetStatesForCountry(countryName);
        states.Should().NotBeEmpty();
        states.Select(s => s.Name).Should().Contain(stateName);

        var cities = _service.GetCitiesForState(countryName, stateName);
        cities.Should().NotBeEmpty();
        cities.Select(c => c.Name).Should().Contain(expectedCity);
    }

    [Fact]
    public void FindCity_ReturnsCityDetailsWithBoundingBox()
    {
        var city = _service.FindCity("Karachi", "Pakistan");

        city.Should().NotBeNull();
        city!.Name.Should().Be("Karachi");
        city.Latitude.Should().BeApproximately(24.86, 0.1);
        city.Longitude.Should().BeApproximately(67.00, 0.1);
        city.BoundingBox.Should().NotBeNull();
        city.BoundingBox.South.Should().BeLessThan(city.BoundingBox.North);
        city.BoundingBox.West.Should().BeLessThan(city.BoundingBox.East);
    }

    [Fact]
    public void CalculateProximityBoundingBox_GeneratesAccurateCoordinates()
    {
        // Lahore center ~ (31.5204, 74.3587)
        double centerLat = 31.5204;
        double centerLon = 74.3587;
        double radiusKm = 10.0;

        var bbox = _service.CalculateProximityBoundingBox(centerLat, centerLon, radiusKm);

        bbox.Should().NotBeNull();
        bbox.South.Should().BeLessThan(centerLat);
        bbox.North.Should().BeGreaterThan(centerLat);
        bbox.West.Should().BeLessThan(centerLon);
        bbox.East.Should().BeGreaterThan(centerLon);

        // Bounding box dimensions should correspond to roughly 2 * 10km = 20km width/height
        (bbox.North - bbox.South).Should().BeApproximately(20.0 / 111.0, 0.05);
    }

    [Theory]
    [InlineData("Karachi", "Textile & Apparel Exporters")]
    [InlineData("Lahore", "Software & IT Agencies")]
    [InlineData("Dubai", "Luxury Real Estate Brokers")]
    [InlineData("London", "FinTech & Wealth Management")]
    public void GetSuggestedNiches_ReturnsRelevantCommercialNiches(string city, string expectedNiche)
    {
        var niches = _service.GetSuggestedNiches(city: city);

        niches.Should().NotBeNullOrEmpty();
        niches.Should().Contain(expectedNiche);
    }

    [Fact]
    public async Task DetectNearbyLocationAsync_ReturnsFallbackAnchorWhenOffline()
    {
        // Service with null HttpClient should return reliable regional anchor
        var result = await _service.DetectNearbyLocationAsync(15.0);

        result.Should().NotBeNull();
        result.City.Should().NotBeNullOrWhiteSpace();
        result.Country.Should().NotBeNullOrWhiteSpace();
        result.BoundingBox.Should().NotBeNull();
        result.RadiusKm.Should().Be(15.0);
    }
}
