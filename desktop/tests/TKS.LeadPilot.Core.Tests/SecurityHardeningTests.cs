using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using TKS.LeadPilot.Core.Enums;
using TKS.LeadPilot.Core.Interfaces;
using TKS.LeadPilot.Core.Models;
using TKS.LeadPilot.Core.Services;
using Xunit;

namespace TKS.LeadPilot.Core.Tests;

public class SecurityHardeningTests
{
    private readonly SsrfValidator _ssrfValidator = new();

    [Theory]
    [InlineData("http://127.0.0.1")]
    [InlineData("http://127.0.0.1:8080/admin")]
    [InlineData("http://127.1")]
    [InlineData("http://127.0.1.1")]
    [InlineData("http://localhost")]
    [InlineData("http://sub.localhost")]
    [InlineData("http://app.local")]
    [InlineData("http://internal.service.lan")]
    [InlineData("http://0.0.0.0")]
    [InlineData("http://255.255.255.255")]
    public async Task SsrfValidator_BlocksLoopbackAndLocalAddresses(string url)
    {
        var result = await _ssrfValidator.ValidateUrlAsync(url);
        result.IsSafe.Should().BeFalse($"'{url}' must be recognized as local/loopback/internal and blocked.");
        result.Reason.Should().NotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData("http://10.0.0.1")]
    [InlineData("http://10.254.1.100:9200")]
    [InlineData("http://172.16.0.1")]
    [InlineData("http://172.31.255.254")]
    [InlineData("http://192.168.0.1")]
    [InlineData("http://192.168.1.254:8080")]
    public async Task SsrfValidator_BlocksRfc1918PrivateRanges(string url)
    {
        var result = await _ssrfValidator.ValidateUrlAsync(url);
        result.IsSafe.Should().BeFalse($"'{url}' belongs to RFC 1918 private IP subnets and must be blocked.");
    }

    [Theory]
    [InlineData("http://169.254.169.254")]
    [InlineData("http://169.254.169.254/latest/meta-data/")]
    [InlineData("http://169.254.1.1")]
    public async Task SsrfValidator_BlocksCloudMetadataAndLinkLocal(string url)
    {
        var result = await _ssrfValidator.ValidateUrlAsync(url);
        result.IsSafe.Should().BeFalse($"'{url}' is a link-local cloud metadata address and must be blocked.");
    }

    [Theory]
    [InlineData("ftp://thekhansoft.com/file.txt")]
    [InlineData("file:///etc/passwd")]
    [InlineData("file:///C:/Windows/win.ini")]
    [InlineData("gopher://127.0.0.1:70")]
    [InlineData("dict://127.0.0.1:11211")]
    [InlineData("ldap://127.0.0.1")]
    public async Task SsrfValidator_BlocksNonHttpHttpsProtocols(string url)
    {
        var result = await _ssrfValidator.ValidateUrlAsync(url);
        result.IsSafe.Should().BeFalse($"Non-HTTP protocol '{url}' must be rejected.");
    }

    [Theory]
    [InlineData("https://1.1.1.1")]
    [InlineData("https://8.8.8.8")]
    public async Task SsrfValidator_PermitsLegitimatePublicIpEndpoints(string url)
    {
        var result = await _ssrfValidator.ValidateUrlAsync(url);
        result.IsSafe.Should().BeTrue($"Public IP '{url}' should be allowed.");
    }

    [Fact]
    public void TechStackDetector_DoesNotCrashOnMaliciousOrMalformedHtml()
    {
        var maliciousHtml = "<script>alert(1)</script><img src=x onerror=alert(2)><a href='javascript:void(0)'>link</a>" +
                            new string('A', 50000);

        var action = () => TechStackDetector.Detect(maliciousHtml, null, null);
        action.Should().NotThrow("HTML detector must be memory-safe and resist malformed payloads");
    }

    [Fact]
    public void OsmOfflineCache_ReturnsVerifiedRealNodesForMetropolitanCities()
    {
        var cities = new[] { "Islamabad", "Lahore", "Karachi", "Rawalpindi", "Faisalabad", "Peshawar", "Multan" };

        foreach (var city in cities)
        {
            var nodes = OsmOfflineCache.GetCachedLeads(city, limit: 10);
            nodes.Should().NotBeEmpty($"OsmOfflineCache must provide verified fallback nodes for {city}");
            nodes.All(n => n.City == city).Should().BeTrue();
            nodes.All(n => !string.IsNullOrWhiteSpace(n.Name)).Should().BeTrue();
            nodes.All(n => n.SourceProvider.Contains("OpenStreetMap")).Should().BeTrue();
        }
    }

    [Fact]
    public async Task OverpassDiscoveryService_GracefullyFallsBackToOfflineCache_WhenMirrorsFail()
    {
        var repo = new FakeBusinessRepo();
        var dedup = new FakeDedupService();
        var failingHandler = new FailingHttpMessageHandler();
        var client = new HttpClient(failingHandler);

        var discoveryService = new OverpassDiscoveryService(client, repo, dedup);

        var query = new DiscoveryQuery
        {
            City = "Islamabad",
            TargetAmenities = ["clinic", "dentist", "pharmacy", "hospital"],
            Limit = 10,
            TimeoutSeconds = 1 // Fast timeout in tests
        };

        var result = await discoveryService.DiscoverLeadsAsync(query);

        result.Should().NotBeNull();
        result.HasError.Should().BeFalse("Discovery service should seamlessly activate verified offline cache rather than returning error");
        result.Leads.Should().NotBeEmpty("Offline cache should supply verified OpenStreetMap leads for Islamabad");
        result.Provider.Should().Contain("Verified Local Cache");
        result.Leads.First().Business.City.Should().Be("Islamabad");
    }

    private class FailingHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            throw new HttpRequestException("Simulated Overpass 502 Bad Gateway / server timeout");
        }
    }

    private class FakeBusinessRepo : IBusinessRepository
    {
        public Task<List<Business>> GetAllAsync(string? searchQuery = null, string? city = null, string? category = null, string? stageId = null, CancellationToken ct = default) => Task.FromResult(new List<Business>());
        public Task<List<Business>> GetFilteredAsync(BusinessFilterCriteria criteria, CancellationToken ct = default) => Task.FromResult(new List<Business>());
        public Task<int> GetFilteredCountAsync(BusinessFilterCriteria criteria, CancellationToken ct = default) => Task.FromResult(0);
        public Task<Business?> GetByIdAsync(string id, CancellationToken ct = default) => Task.FromResult<Business?>(null);
        public Task<Business> AddAsync(Business business, CancellationToken ct = default) => Task.FromResult(business);
        public Task AddBatchAsync(IEnumerable<Business> businesses, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateAsync(Business business, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateBatchAsync(IEnumerable<Business> businesses, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(string id, CancellationToken ct = default) => Task.CompletedTask;
        public Task<int> GetTotalCountAsync(CancellationToken ct = default) => Task.FromResult(0);
        public Task<int> GetWithWebsiteCountAsync(CancellationToken ct = default) => Task.FromResult(0);
        public Task<int> GetHighOpportunityCountAsync(int threshold = 70, CancellationToken ct = default) => Task.FromResult(0);
        public Task<List<string>> GetDistinctCountriesAsync(CancellationToken ct = default) => Task.FromResult(new List<string>());
        public Task<List<string>> GetDistinctStatesAsync(string? country = null, CancellationToken ct = default) => Task.FromResult(new List<string>());
        public Task<List<string>> GetDistinctCitiesAsync(CancellationToken ct = default) => Task.FromResult(new List<string>());
        public Task<List<string>> GetDistinctCategoriesAsync(CancellationToken ct = default) => Task.FromResult(new List<string>());
        public Task<List<string>> GetDistinctTagsAsync(CancellationToken ct = default) => Task.FromResult(new List<string>());
        public Task<List<Business>> FindPotentialDuplicatesAsync(string? domain, string? phone, string? name, string? city, CancellationToken ct = default) => Task.FromResult(new List<Business>());
        public Task AddContactAsync(BusinessContact contact, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateContactAsync(BusinessContact contact, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteContactAsync(string contactId, CancellationToken ct = default) => Task.CompletedTask;
    }

    private class FakeDedupService : IDeduplicationService
    {
        public string NormalizeDomain(string? url) => string.Empty;
        public string NormalizePhone(string? phone) => string.Empty;
        public string NormalizeName(string? name) => string.Empty;
        public DuplicateMatchResult CheckDuplicate(Business incoming, IEnumerable<Business> existing, DeduplicationOptions? options = null)
            => new() { IncomingBusiness = incoming, ConfidenceScore = 0 };
        public List<DuplicateMatchResult> CheckDuplicatesBatch(IEnumerable<Business> incomingBatch, IEnumerable<Business> existingDatabase, DeduplicationOptions? options = null)
            => [];
        public void MergeBusinesses(Business targetExisting, Business sourceIncoming) { }
        public string MergeSourceEngines(string? existingSources, string? incomingSources) => existingSources ?? string.Empty;
    }
}
