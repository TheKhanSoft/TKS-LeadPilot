using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using TKS.LeadPilot.Core.Enums;
using TKS.LeadPilot.Core.Interfaces;
using TKS.LeadPilot.Core.Models;
using TKS.LeadPilot.Core.Services;
using Xunit;

namespace TKS.LeadPilot.Core.Tests;

public class OverpassDiscoveryServiceTests
{
    [Fact]
    public async Task DiscoverLeadsAsync_ReturnsMappedLeadsAndFlagsDuplicates()
    {
        var osmResponseJson = """
        {
          "version": 0.6,
          "elements": [
            {
              "type": "node",
              "id": 1001,
              "lat": 24.86,
              "lon": 67.00,
              "tags": {
                "name": "Apex Dental Clinic",
                "amenity": "dentist",
                "website": "https://apexdental.pk",
                "addr:city": "Karachi"
              }
            },
            {
              "type": "node",
              "id": 1002,
              "lat": 24.87,
              "lon": 67.01,
              "tags": {
                "name": "Ziauddin Diagnostic Lab",
                "amenity": "clinic",
                "website": "https://ziauddindiagnostic.pk",
                "addr:city": "Karachi"
              }
            }
          ]
        }
        """;

        var fakeHandler = new MockHttpMessageHandler(osmResponseJson);
        var httpClient = new HttpClient(fakeHandler);
        var fakeRepo = new InMemoryBusinessRepository();

        // Seed an existing business that will trigger duplicate detection for Apex Dental
        fakeRepo.Storage.Add(new Business
        {
            Id = "b-existing",
            Name = "Apex Dental",
            City = "Karachi",
            WebsiteUrl = "https://www.apexdental.pk"
        });

        var dedupService = new DeduplicationService();
        var sut = new OverpassDiscoveryService(httpClient, fakeRepo, dedupService);

        var query = new DiscoveryQuery
        {
            City = "Karachi",
            TargetAmenities = ["dentist", "clinic"]
        };

        var result = await sut.DiscoverLeadsAsync(query);

        result.HasError.Should().BeFalse();
        result.TotalFetched.Should().Be(2);
        result.NewCount.Should().Be(1);
        result.DuplicateCount.Should().Be(1);

        var apexLead = result.Leads.First(l => l.Business.Name.Contains("Apex Dental"));
        apexLead.IsDuplicate.Should().BeTrue();
        apexLead.DuplicateMatch!.MatchType.Should().Be(DuplicateMatchType.ExactWebsite);
        apexLead.IsSelectedForImport.Should().BeFalse("duplicates should be unselected by default");

        var ziauddinLead = result.Leads.First(l => l.Business.Name.Contains("Ziauddin"));
        ziauddinLead.IsDuplicate.Should().BeFalse();
        ziauddinLead.IsSelectedForImport.Should().BeTrue("new leads should be selected by default");
    }

    [Fact]
    public async Task ImportDiscoveredLeadsAsync_InsertsSelectedLeadsIntoRepository()
    {
        var fakeHandler = new MockHttpMessageHandler("{}");
        var httpClient = new HttpClient(fakeHandler);
        var fakeRepo = new InMemoryBusinessRepository();
        var dedupService = new DeduplicationService();
        var sut = new OverpassDiscoveryService(httpClient, fakeRepo, dedupService);

        var leads = new List<DiscoveredLead>
        {
            new()
            {
                Business = new Business
                {
                    Name = "Prime Diagnostics",
                    Category = "Healthcare",
                    City = "Lahore"
                },
                IsSelectedForImport = true
            },
            new()
            {
                Business = new Business
                {
                    Name = "Unselected Shop",
                    Category = "Retail",
                    City = "Lahore"
                },
                IsSelectedForImport = false
            }
        };

        var importResult = await sut.ImportDiscoveredLeadsAsync(leads);

        importResult.TotalProcessed.Should().Be(1);
        importResult.CreatedCount.Should().Be(1);
        fakeRepo.Storage.Should().HaveCount(1);
        fakeRepo.Storage[0].Name.Should().Be("Prime Diagnostics");
    }

    private class MockHttpMessageHandler(string responseContent) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseContent, Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }

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

        public Task UpdateAsync(Business business, CancellationToken ct = default) => Task.CompletedTask;
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
}
