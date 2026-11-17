using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using TKS.LeadPilot.Core.Enums;
using TKS.LeadPilot.Core.Interfaces;
using TKS.LeadPilot.Core.Models;
using TKS.LeadPilot.Core.Services;
using Xunit;

namespace TKS.LeadPilot.Core.Tests;

public class LeadScoringEngineTests
{
    private class InMemoryBusinessRepository : IBusinessRepository
    {
        public List<Business> Storage { get; } = [];

        public Task<List<Business>> GetAllAsync(string? searchQuery = null, string? city = null, string? category = null, string? stageId = null, CancellationToken ct = default)
            => Task.FromResult(Storage.ToList());

        public Task<List<Business>> GetFilteredAsync(BusinessFilterCriteria criteria, CancellationToken ct = default)
            => Task.FromResult(Storage.ToList());

        public Task<int> GetFilteredCountAsync(BusinessFilterCriteria criteria, CancellationToken ct = default)
            => Task.FromResult(Storage.Count);

        public Task<Business?> GetByIdAsync(string id, CancellationToken ct = default)
            => Task.FromResult(Storage.FirstOrDefault(b => b.Id == id));

        public Task<Business> AddAsync(Business business, CancellationToken ct = default)
        {
            Storage.Add(business);
            return Task.FromResult(business);
        }

        public Task AddBatchAsync(IEnumerable<Business> businesses, CancellationToken ct = default)
        {
            Storage.AddRange(businesses);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Business business, CancellationToken ct = default)
        {
            var idx = Storage.FindIndex(b => b.Id == business.Id);
            if (idx >= 0) Storage[idx] = business;
            return Task.CompletedTask;
        }

        public Task UpdateBatchAsync(IEnumerable<Business> businesses, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(string id, CancellationToken ct = default) => Task.CompletedTask;
        public Task<int> GetTotalCountAsync(CancellationToken ct = default) => Task.FromResult(Storage.Count);
        public Task<int> GetWithWebsiteCountAsync(CancellationToken ct = default) => Task.FromResult(0);
        public Task<int> GetHighOpportunityCountAsync(int threshold = 70, CancellationToken ct = default) => Task.FromResult(0);
        public Task<List<string>> GetDistinctCitiesAsync(CancellationToken ct = default) => Task.FromResult(new List<string>());
        public Task<List<string>> GetDistinctCategoriesAsync(CancellationToken ct = default) => Task.FromResult(new List<string>());
        public Task<List<string>> GetDistinctTagsAsync(CancellationToken ct = default) => Task.FromResult(new List<string>());
        public Task<List<Business>> FindPotentialDuplicatesAsync(string? domain, string? phone, string? name, string? city, CancellationToken ct = default) => Task.FromResult(new List<Business>());
        public Task AddContactAsync(BusinessContact contact, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateContactAsync(BusinessContact contact, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteContactAsync(string contactId, CancellationToken ct = default) => Task.CompletedTask;
    }

    [Fact]
    public async Task CalculateScoreAsync_WhenNoWebsite_AwardsMaximumWebsiteOpportunityPoints()
    {
        var repo = new InMemoryBusinessRepository();
        var business = new Business
        {
            Id = "biz-1",
            Name = "Lahore Auto Care",
            City = "Lahore",
            WebsiteUrl = null,
            WebsiteStatus = WebsiteStatus.NoWebsite,
            IsGoogleVerified = false
        };
        repo.Storage.Add(business);

        var engine = new LeadScoringEngine(repo);
        var score = await engine.CalculateScoreAsync(business);

        score.TotalScore.Should().BeGreaterOrEqualTo(45); // 35 (no site) + 12 (unverified Google) = 47
        var webComponent = score.Components.FirstOrDefault(c => c.ComponentName.Contains("Website"));
        webComponent.Should().NotBeNull();
        webComponent!.PointsAwarded.Should().Be(35);
        business.LeadScore.Should().Be(score.TotalScore);
    }

    [Fact]
    public async Task CalculateScoreAsync_WhenSlowTtfbAndBadSecurity_AwardsOpportunityPoints()
    {
        var repo = new InMemoryBusinessRepository();
        var business = new Business
        {
            Id = "biz-2",
            Name = "Karachi Diagnostic Lab",
            City = "Karachi",
            WebsiteUrl = "https://kdl.pk",
            WebsiteStatus = WebsiteStatus.ActiveWebsite,
            IsGoogleVerified = true,
            GoogleRating = 3.5,
            GoogleReviewCount = 4
        };
        repo.Storage.Add(business);

        var audit = new WebsiteAudit
        {
            OverallScore = 38,
            PerformanceScore = 30,
            SecurityScore = 40,
            TtfbMs = 1850,
            SslValid = false,
            HttpRedirectsToHttps = false,
            HasRobotsTxt = true,
            HasSitemapXml = true,
            MetaDescription = "Karachi Diagnostic Lab Official Site",
            PageTitle = "Home | Karachi Diagnostic Lab"
        };

        var engine = new LeadScoringEngine(repo);
        var score = await engine.CalculateScoreAsync(business, audit);

        score.TotalScore.Should().BeGreaterOrEqualTo(65);
        // Comp 1: 20 (overall < 50)
        // Comp 2: 25 (ttfb > 1500)
        // Comp 3: 15 (ssl invalid)
        // Comp 5: 8 (Google rating < 4.0)
        // Total = 68
        score.TotalScore.Should().Be(68);
    }

    [Fact]
    public async Task CalculateScoreAsync_ComponentsSumToTotalScore()
    {
        var repo = new InMemoryBusinessRepository();
        var business = new Business
        {
            Id = "biz-3",
            Name = "Islamabad Tech Hub",
            City = "Islamabad",
            WebsiteUrl = "https://ith.pk",
            WebsiteStatus = WebsiteStatus.ActiveWebsite
        };
        repo.Storage.Add(business);

        var engine = new LeadScoringEngine(repo);
        var score = await engine.CalculateScoreAsync(business);

        var sum = score.Components.Sum(c => c.PointsAwarded);
        score.TotalScore.Should().Be(Math.Min(100, Math.Max(0, sum)));
    }

    [Fact]
    public async Task ApplyManualOverrideAsync_OverridesScoreAndSavesAuditReason()
    {
        var repo = new InMemoryBusinessRepository();
        var business = new Business
        {
            Id = "biz-4",
            Name = "Peshawar Carpet Weavers",
            City = "Peshawar",
            LeadScore = 30
        };
        repo.Storage.Add(business);

        var engine = new LeadScoringEngine(repo);
        var overrideScore = await engine.ApplyManualOverrideAsync(business.Id, 92, "VIP client with confirmed budget of $5,000");

        overrideScore.TotalScore.Should().Be(92);
        overrideScore.IsManualOverride.Should().BeTrue();
        overrideScore.OverrideReason.Should().Contain("VIP client");
        business.LeadScore.Should().Be(92);
    }
}
