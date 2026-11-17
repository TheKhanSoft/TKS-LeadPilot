using System.Threading.Tasks;
using FluentAssertions;
using TKS.LeadPilot.Core.Enums;
using TKS.LeadPilot.Core.Models;
using TKS.LeadPilot.Core.Services;
using Xunit;

namespace TKS.LeadPilot.Core.Tests;

public class TksServiceMatcherTests
{
    [Fact]
    public async Task MatchServicesAsync_WhenNoWebsite_MatchesModernWebDevelopmentWithHighConfidence()
    {
        var matcher = new TksServiceMatcher();
        var business = new Business
        {
            Name = "Alpha Medical",
            Category = "Healthcare",
            City = "Karachi",
            WebsiteUrl = null,
            WebsiteStatus = WebsiteStatus.NoWebsite
        };

        var matches = await matcher.MatchServicesAsync(business, null, null);

        matches.Should().NotBeEmpty();
        var top = matches[0];
        top.Service.Name.Should().Contain("Modern Business Website");
        top.MatchConfidence.Should().Be(95);
        top.GeneratedPitch.Should().Contain("Alpha Medical");
    }

    [Fact]
    public async Task MatchServicesAsync_WhenLatencyHigh_MatchesSpeedOptimization()
    {
        var matcher = new TksServiceMatcher();
        var business = new Business
        {
            Name = "Royal Furnishings",
            Category = "Retail",
            City = "Lahore",
            WebsiteUrl = "https://royalfurnishings.pk",
            WebsiteStatus = WebsiteStatus.ActiveWebsite
        };

        var audit = new WebsiteAudit
        {
            TargetUrl = "https://royalfurnishings.pk",
            TtfbMs = 1750,
            PerformanceScore = 42,
            SslValid = true,
            HttpRedirectsToHttps = true
        };

        var matches = await matcher.MatchServicesAsync(business, audit, null);

        var speedMatch = matches.FirstOrDefault(m => m.Service.Name.Contains("Speed"));
        speedMatch.Should().NotBeNull();
        speedMatch!.MatchConfidence.Should().Be(95);
        speedMatch.GeneratedPitch.Should().Contain("1750ms");
    }

    [Fact]
    public async Task MatchServicesAsync_WhenSslInvalid_MatchesSecurityRetainer()
    {
        var matcher = new TksServiceMatcher();
        var business = new Business
        {
            Name = "Rawalpindi Dental",
            Category = "Healthcare",
            City = "Rawalpindi",
            WebsiteUrl = "http://rawalpindidental.pk",
            WebsiteStatus = WebsiteStatus.ActiveWebsite
        };

        var audit = new WebsiteAudit
        {
            TargetUrl = "http://rawalpindidental.pk",
            SslValid = false,
            HttpRedirectsToHttps = false
        };

        var matches = await matcher.MatchServicesAsync(business, audit, null);

        var secMatch = matches.FirstOrDefault(m => m.Service.Name.Contains("Security"));
        secMatch.Should().NotBeNull();
        secMatch!.MatchConfidence.Should().Be(98);
        secMatch.GeneratedPitch.Should().Contain("Not Secure");
    }

    [Fact]
    public async Task MatchServicesAsync_WhenMissingGmb_MatchesGmbLocalSeoService()
    {
        var matcher = new TksServiceMatcher();
        var business = new Business
        {
            Name = "Peshawar Carpet House",
            Category = "Wholesale & Retail",
            City = "Peshawar",
            IsGoogleVerified = false,
            GooglePlaceId = null,
            WebsiteUrl = "https://peshawarcarpets.pk",
            WebsiteStatus = WebsiteStatus.ActiveWebsite
        };

        var matches = await matcher.MatchServicesAsync(business, null, null);

        var gmbMatch = matches.FirstOrDefault(m => m.Service.Id == "svc-gmb-local-seo");
        gmbMatch.Should().NotBeNull();
        gmbMatch!.MatchConfidence.Should().Be(92);
        gmbMatch.GeneratedPitch.Should().Contain("Google Maps");
    }
}
