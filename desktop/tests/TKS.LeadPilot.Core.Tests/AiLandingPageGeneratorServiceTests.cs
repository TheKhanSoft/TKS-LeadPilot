using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using TKS.LeadPilot.Core.Interfaces;
using TKS.LeadPilot.Core.Models;
using TKS.LeadPilot.Core.Services;
using Xunit;

namespace TKS.LeadPilot.Core.Tests;

public class AiLandingPageGeneratorServiceTests
{
    private class MockSecretVault : ISecretVault
    {
        private readonly Dictionary<string, string> _vault = [];

        public void SaveSecret(string key, string secret) => _vault[key] = secret;
        public string? GetSecret(string key) => _vault.TryGetValue(key, out var val) ? val : null;
        public bool HasSecret(string key) => _vault.ContainsKey(key);
        public void DeleteSecret(string key) => _vault.Remove(key);
    }

    [Fact]
    public void GetAvailableModules_ReturnsExpectedDefaultModules()
    {
        var vault = new MockSecretVault();
        var sut = new AiLandingPageGeneratorService(new HttpClient(), vault);

        var modules = sut.GetAvailableModules();

        modules.Should().NotBeEmpty();
        modules.Should().Contain(m => m.Id == "mod-hero");
        modules.Should().Contain(m => m.Id == "mod-services");
        modules.Should().Contain(m => m.Id == "mod-faq");
        modules.Should().Contain(m => m.Id == "mod-whatsapp");
        modules.Should().Contain(m => m.Id == "mod-lead-form");
    }

    [Fact]
    public void GenerateDeterministicLandingPage_ProducesCompleteHtmlAndBlueprint()
    {
        var vault = new MockSecretVault();
        var sut = new AiLandingPageGeneratorService(new HttpClient(), vault);

        var request = new LandingPageGenerationRequest
        {
            BusinessName = "Peshawar Advanced Orthopedic Clinic",
            Category = "Healthcare & Dental",
            City = "Peshawar",
            Country = "Pakistan",
            PhonePrimary = "+92 312 9876543",
            Archetype = LandingPageArchetype.LocalServiceBooking,
            DesignStyle = LandingPageDesignStyle.TrustworthyEmerald,
            SelectedModules = ["mod-hero", "mod-services", "mod-faq", "mod-lead-form", "mod-whatsapp", "mod-seo"],
            CustomPromptInstructions = "Highlight joint replacement and 24/7 emergency response"
        };

        var result = sut.GenerateDeterministicLandingPage(request);

        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.BusinessName.Should().Be("Peshawar Advanced Orthopedic Clinic");
        result.Headline.Should().NotBeNullOrWhiteSpace();
        result.Subheadline.Should().NotBeNullOrWhiteSpace();
        result.GeneratedHtml.Should().Contain("<!DOCTYPE html>");
        result.GeneratedHtml.Should().Contain("Peshawar Advanced Orthopedic Clinic");
        result.GeneratedHtml.Should().Contain("Peshawar");
        result.GeneratedHtml.Should().Contain("https://cdn.tailwindcss.com");
        result.GeneratedHtml.Should().Contain("+92 312 9876543");
        result.GeneratedHtml.Should().Contain("Schema.org");
        result.GeneratedHtml.Should().Contain("fa-whatsapp");
        result.GeneratedHtml.Should().Contain("</html>");

        result.ArchitectureBlueprintMarkdown.Should().Contain("Executive Strategic Positioning");
        result.ArchitectureBlueprintMarkdown.Should().Contain("Information Architecture");
        result.ArchitectureBlueprintMarkdown.Should().Contain("Highlight joint replacement and 24/7 emergency response");

        result.MidjourneyPrompts.Should().NotBeEmpty();
        result.DeveloperPrompts.Should().Contain("Next.js");
        result.AdCopyPrompts.Should().Contain("Google Search Ads");
        result.ConversionScore.Should().BeGreaterThan(85);
    }

    [Fact]
    public async Task GenerateLandingPageAsync_WithoutApiKey_GracefullyFallsBackToDeterministic()
    {
        var vault = new MockSecretVault(); // Empty vault
        var sut = new AiLandingPageGeneratorService(new HttpClient(), vault);

        var request = new LandingPageGenerationRequest
        {
            BusinessName = "Lahore Luxury Interiors",
            Category = "Architecture & Interior Design",
            City = "Lahore",
            Country = "Pakistan",
            Archetype = LandingPageArchetype.LuxuryBrandShowcase,
            DesignStyle = LandingPageDesignStyle.MidnightLuxuryGold
        };

        var result = await sut.GenerateLandingPageAsync(request);

        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.IsLocalFallback.Should().BeTrue();
        result.Provider.Should().Contain("Local");
        result.GeneratedHtml.Should().Contain("Lahore Luxury Interiors");
        result.GeneratedHtml.Should().Contain("<!DOCTYPE html>");
        result.ArchitectureBlueprintMarkdown.Should().Contain("Lahore");
    }

    [Theory]
    [InlineData(LandingPageDesignStyle.ModernSlateCyan)]
    [InlineData(LandingPageDesignStyle.MidnightLuxuryGold)]
    [InlineData(LandingPageDesignStyle.TrustworthyEmerald)]
    [InlineData(LandingPageDesignStyle.BoldVibrantIndigo)]
    [InlineData(LandingPageDesignStyle.MinimalistMonochrome)]
    public void GenerateDeterministicLandingPage_SupportsAllColorPalettes(LandingPageDesignStyle style)
    {
        var vault = new MockSecretVault();
        var sut = new AiLandingPageGeneratorService(new HttpClient(), vault);

        var request = new LandingPageGenerationRequest
        {
            BusinessName = "Palette Test Studio",
            DesignStyle = style
        };

        var result = sut.GenerateDeterministicLandingPage(request);

        result.GeneratedHtml.Should().Contain("<!DOCTYPE html>");
        result.GeneratedHtml.Should().Contain("Palette Test Studio");
    }

    [Fact]
    public void GenerateDeterministicLandingPage_GeneratesDistinctBespokeContentAcrossCategories()
    {
        var vault = new MockSecretVault();
        var sut = new AiLandingPageGeneratorService(new HttpClient(), vault);

        var healthReq = new LandingPageGenerationRequest
        {
            BusinessName = "Healthhelpzone",
            Category = "Healthcare & Urgent Care",
            City = "Albany",
            Country = "United States"
        };

        var techReq = new LandingPageGenerationRequest
        {
            BusinessName = "TheKhanSoft",
            Category = "Software Company & IT Services",
            City = "Charsadda",
            Country = "Pakistan"
        };

        var healthResult = sut.GenerateDeterministicLandingPage(healthReq);
        var techResult = sut.GenerateDeterministicLandingPage(techReq);

        // Different headlines
        healthResult.Headline.Should().NotBe(techResult.Headline);
        healthResult.Headline.Should().Contain("Health");
        techResult.Headline.Should().Contain("Software");

        // Healthcare specific services & testimonials
        healthResult.GeneratedHtml.Should().Contain("Walk-In Care");
        healthResult.GeneratedHtml.Should().Contain("X-Ray");
        healthResult.GeneratedHtml.Should().Contain("Sarah Jenkins");

        // Tech specific services & testimonials
        techResult.GeneratedHtml.Should().Contain("Custom SaaS");
        techResult.GeneratedHtml.Should().Contain("DevOps");
        techResult.GeneratedHtml.Should().Contain("Alexander Wright");

        // Ensure neither has static generic placeholders
        healthResult.GeneratedHtml.Should().NotContain("Premium Healthcare & Urgent Care Delivery");
        techResult.GeneratedHtml.Should().NotContain("Premium Software Company & IT Services Delivery");
    }

    [Fact]
    public void GenerateDeterministicLandingPage_SupportsArchetypeSpecificLayoutsAndAll12Modules()
    {
        var vault = new MockSecretVault();
        var sut = new AiLandingPageGeneratorService(new HttpClient(), vault);

        // 1. LocalServiceBooking Archetype
        var clinicReq = new LandingPageGenerationRequest
        {
            BusinessName = "Capital Urgent Care",
            Category = "Healthcare & Walk-In Clinic",
            City = "Albany",
            Archetype = LandingPageArchetype.LocalServiceBooking,
            SelectedModules = ["mod-hero", "mod-services", "mod-pricing", "mod-team", "mod-before-after", "mod-calculator", "mod-faq", "mod-lead-form"]
        };

        var clinicResult = sut.GenerateDeterministicLandingPage(clinicReq);
        clinicResult.GeneratedHtml.Should().Contain("Priority Appointment");
        clinicResult.GeneratedHtml.Should().Contain("Open Now");
        clinicResult.GeneratedHtml.Should().Contain("Preferred Time Window");
        clinicResult.GeneratedHtml.Should().Contain("Monday – Sunday: 8:00 AM – 9:00 PM");

        // Verify all advanced modules rendered
        clinicResult.GeneratedHtml.Should().Contain("Case Studies &amp; Transformation");
        clinicResult.GeneratedHtml.Should().Contain("Measurable Results Delivered Across Albany");
        clinicResult.GeneratedHtml.Should().Contain("Leadership &amp; Professional Authority");
        clinicResult.GeneratedHtml.Should().Contain("Pricing &amp; Packages");
        clinicResult.GeneratedHtml.Should().Contain("updateCalculator");

        // 2. ModernSaaSTech Archetype
        var saasReq = new LandingPageGenerationRequest
        {
            BusinessName = "CloudStream AI",
            Category = "Software & IT Services",
            City = "Seattle",
            Archetype = LandingPageArchetype.ModernSaaSTech
        };

        var saasResult = sut.GenerateDeterministicLandingPage(saasReq);
        saasResult.GeneratedHtml.Should().Contain("api.thekhansoft.io");
        saasResult.GeneratedHtml.Should().Contain("99.99% SLA");
        saasResult.GeneratedHtml.Should().Contain("EnterpriseCluster");

        // 3. LuxuryBrandShowcase Archetype
        var luxuryReq = new LandingPageGenerationRequest
        {
            BusinessName = "Aura Private Concierge",
            Category = "Luxury Real Estate & Advisory",
            City = "Geneva",
            Archetype = LandingPageArchetype.LuxuryBrandShowcase,
            DesignStyle = LandingPageDesignStyle.MidnightLuxuryGold
        };

        var luxuryResult = sut.GenerateDeterministicLandingPage(luxuryReq);
        luxuryResult.GeneratedHtml.Should().Contain("Playfair Display");
        luxuryResult.GeneratedHtml.Should().Contain("Exclusive Bespoke Experience");
        luxuryResult.GeneratedHtml.Should().Contain("Explore Collection");
    }
}
