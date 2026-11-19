using System;
using TKS.LeadPilot.Core.Models;
using TKS.LeadPilot.Core.Services;
using Xunit;

namespace TKS.LeadPilot.Core.Tests;

public class CategoryResolverTests
{
    [Fact]
    public void ResolveEffectiveCategory_WithExplicitPreset_ReturnsPreset()
    {
        var query = new DiscoveryQuery
        {
            CategoryPreset = "Software & IT Agencies",
            CustomSearchTerm = "TheKhanSoft"
        };

        var result = CategoryResolver.ResolveEffectiveCategory(query, "TheKhanSoft", "TheKhanSoft");

        Assert.Equal("Software & IT Agencies", result);
    }

    [Fact]
    public void ResolveEffectiveCategory_WithAllCommercialAndKeyword_ResolvesCanonicalCategory()
    {
        var query = new DiscoveryQuery
        {
            CategoryPreset = "⭐ All Commercial Businesses & Enterprises",
            CustomSearchTerm = "dentist clinic"
        };

        var result = CategoryResolver.ResolveEffectiveCategory(query, "Dr. John Dental", "dentist clinic");

        Assert.Equal("Healthcare & Dental", result);
    }

    [Fact]
    public void NormalizeLeadCategory_WhenCategoryIsSearchKeyword_ReplacesWithCanonicalCategory()
    {
        var query = new DiscoveryQuery
        {
            CategoryPreset = "Software & IT Agencies",
            CustomSearchTerm = "TheKhanSoft"
        };

        var lead = new Business
        {
            Name = "TheKhanSoft",
            Category = "TheKhanSoft",
            City = "Peshawar",
            Country = "Pakistan"
        };

        CategoryResolver.NormalizeLeadCategory(lead, query);

        Assert.Equal("Software & IT Agencies", lead.Category);
    }

    [Fact]
    public void NormalizeLeadCategory_WhenCategoryIsWikidataNoise_ReplacesWithCanonicalCategory()
    {
        var query = new DiscoveryQuery
        {
            CategoryPreset = "Software & IT Agencies",
            CustomSearchTerm = "software"
        };

        var lead = new Business
        {
            Name = "Tech Horizons",
            Category = "scientific article published on 15 December 2021",
            City = "Islamabad",
            Country = "Pakistan"
        };

        CategoryResolver.NormalizeLeadCategory(lead, query);

        Assert.Equal("Software & IT Agencies", lead.Category);
    }

    [Fact]
    public void InferCommercialGmbData_WithoutGooglePlaceId_ReturnsUnverified()
    {
        var result = OverpassResultMapper.InferCommercialGmbData(
            name: "TheKhanSoft",
            category: "Software & IT Agencies",
            phone: "+92 300 1234567",
            website: "https://thekhansoft.com",
            existingStars: null,
            existingHours: null,
            existingPlaceId: null
        );

        Assert.False(result.HasGmb);
        Assert.Null(result.PlaceId);
        Assert.Null(result.Rating);
        Assert.Null(result.Reviews);
    }

    [Fact]
    public void InferCommercialGmbData_WithSyntheticPlaceId_RejectsFakeAndReturnsUnverified()
    {
        var result = OverpassResultMapper.InferCommercialGmbData(
            name: "Local Shop",
            category: "Retail",
            phone: "03001234567",
            website: null,
            existingStars: 4.2,
            existingHours: null,
            existingPlaceId: "gmb_1234abcd"
        );

        Assert.False(result.HasGmb);
        Assert.Null(result.PlaceId);
    }

    [Fact]
    public void InferCommercialGmbData_WithGenuineChIjPlaceId_ReturnsVerified()
    {
        var result = OverpassResultMapper.InferCommercialGmbData(
            name: "Apex Dental",
            category: "Healthcare & Dental",
            phone: "+1 555-1234",
            website: "https://apexdental.com",
            existingStars: 4.8,
            existingHours: "9:00 AM - 5:00 PM",
            existingPlaceId: "ChIJN1t_tDeuEmsRUsoyG83frY4"
        );

        Assert.True(result.HasGmb);
        Assert.Equal("ChIJN1t_tDeuEmsRUsoyG83frY4", result.PlaceId);
        Assert.Equal(4.8, result.Rating);
    }
}
