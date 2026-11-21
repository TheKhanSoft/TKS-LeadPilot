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
            SearchEngine = "OpenStreetMap Overpass (Free Live QL)",
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

    [Fact]
    public async Task ImportDiscoveredLeadsAsync_MergesSearchEnginesIntoExistingRecordOnSkipStrategy()
    {
        var fakeHandler = new MockHttpMessageHandler("{}");
        var httpClient = new HttpClient(fakeHandler);
        var fakeRepo = new InMemoryBusinessRepository();
        var dedupService = new DeduplicationService();
        var sut = new OverpassDiscoveryService(httpClient, fakeRepo, dedupService);

        var existingBusiness = new Business
        {
            Id = "existing-1",
            Name = "Apex Dental Clinic",
            City = "Lahore",
            PhonePrimary = "+92 42 35789000",
            SourceProvider = "OpenStreetMap"
        };
        fakeRepo.Storage.Add(existingBusiness);

        var leads = new List<DiscoveredLead>
        {
            new()
            {
                Business = new Business
                {
                    Name = "Apex Dental Clinic",
                    City = "Lahore",
                    PhonePrimary = "+92 42 35789000",
                    SourceProvider = "Photon (Komoot OpenPOI), Google Places (via Serper)"
                },
                DuplicateMatch = new DuplicateMatchResult
                {
                    ExistingBusiness = existingBusiness,
                    MatchType = DuplicateMatchType.ExactPhone,
                    ConfidenceScore = 95
                },
                IsSelectedForImport = true
            }
        };

        var result = await sut.ImportDiscoveredLeadsAsync(leads, DeduplicationStrategy.Skip);

        result.SkippedCount.Should().Be(1);
        result.CreatedCount.Should().Be(0);
        existingBusiness.SourceProvider.Should().Be("OpenStreetMap, Photon (Komoot OpenPOI), Google Places (via Serper)");
        fakeRepo.Storage.Should().HaveCount(1);
    }

    [Fact]
    public void MapNominatimToBusinesses_ExtractsAllPropertiesAccurately()
    {
        var nominatimJson = """
        [
          {
            "place_id": 998877,
            "osm_type": "node",
            "osm_id": 12345678,
            "lat": "34.0151",
            "lon": "71.5249",
            "name": "Rehman Medical Institute",
            "display_name": "Rehman Medical Institute, Phase 5, Hayatabad, Peshawar, Khyber Pakhtunkhwa, 25000, Pakistan",
            "category": "amenity",
            "type": "hospital",
            "address": {
              "hospital": "Rehman Medical Institute",
              "suburb": "Hayatabad",
              "city": "Peshawar",
              "state": "Khyber Pakhtunkhwa",
              "postcode": "25000",
              "country": "Pakistan"
            },
            "extratags": {
              "phone": "+92 91 5838000",
              "website": "https://rmi.edu.pk"
            }
          }
        ]
        """;

        var businesses = OverpassResultMapper.MapNominatimToBusinesses(nominatimJson, "Peshawar", "Pakistan", "Khyber Pakhtunkhwa");

        businesses.Should().HaveCount(1);
        var b = businesses[0];
        b.Name.Should().Be("Rehman Medical Institute");
        b.Category.Should().Contain("Hospital");
        b.City.Should().Be("Peshawar");
        b.Country.Should().Be("Pakistan");
        b.StateProvince.Should().Be("Khyber Pakhtunkhwa");
        b.PhonePrimary.Should().Be("+92 91 5838000");
        b.WebsiteUrl.Should().Be("https://rmi.edu.pk");
        b.SourceProvider.Should().Be("OpenStreetMap (Nominatim)");
    }

    [Fact]
    public void OverpassQueryBuilder_BuildsBroadCommercialTags_ForCityScan()
    {
        var query = new DiscoveryQuery
        {
            City = "Peshawar",
            Country = "Pakistan",
            TargetAmenities = ["all_commercial"],
            CategoryPreset = "⭐ All Commercial Businesses & Enterprises"
        };

        var ql = OverpassQueryBuilder.BuildQuery(query);

        ql.Should().Contain("node[\"office\"]");
        ql.Should().Contain("node[\"craft\"]");
        ql.Should().Contain("node[\"healthcare\"]");
        ql.Should().Contain("node[\"amenity\"]");
        ql.Should().Contain("node[\"shop\"]");
        ql.Should().Contain("33.9500,71.4500,34.0500,71.6000");
    }

    [Fact]
    public void OverpassQueryBuilder_WithCustomSearchKeyword_InjectsRegexTags()
    {
        var query = new DiscoveryQuery
        {
            City = "Lahore",
            Country = "Pakistan",
            CustomSearchTerm = "Solar Energy",
            DeepScan = true
        };

        var ql = OverpassQueryBuilder.BuildQuery(query);

        ql.Should().Contain("node[\"name\"~\"Solar Energy\",i]");
        ql.Should().Contain("node[\"shop\"~\"Solar Energy\",i]");
        ql.Should().Contain("node[\"office\"~\"Solar Energy\",i]");
    }

    [Fact]
    public void CalculateRelevanceScore_ScoresTargetMatchesAccurately()
    {
        var dentistBusiness = new Business
        {
            Name = "Apex Dental Implant Clinic",
            Category = "Healthcare & Dental",
            City = "Lahore",
            WebsiteUrl = "https://apexdental.pk",
            Tags = "dentist, teeth cleaning, implants"
        };

        var groceryBusiness = new Business
        {
            Name = "Al-Madina General Store",
            Category = "Retail & Grocery",
            City = "Lahore",
            WebsiteUrl = "https://almadinastore.pk",
            Tags = "grocery, fruits"
        };

        var query = new DiscoveryQuery
        {
            City = "Lahore",
            CustomSearchTerm = "dentist",
            CategoryPreset = "Healthcare & Dental",
            StrictKeywordFilter = true
        };

        var dentistScore = OverpassDiscoveryService.CalculateRelevanceScore(dentistBusiness, query);
        var groceryScore = OverpassDiscoveryService.CalculateRelevanceScore(groceryBusiness, query);

        dentistScore.Should().BeGreaterThanOrEqualTo(80);
        groceryScore.Should().Be(0, "unrelated grocery store should have 0 relevance for dentist search");
    }

    [Fact]
    public void MapWikidataToBusinesses_ParsesMediaWikiEntitiesFormat()
    {
        var wikidataJson = """
        {
          "entities": {
            "Q7455839": {
              "type": "item",
              "id": "Q7455839",
              "labels": {
                "en": { "language": "en", "value": "Services Hospital" }
              },
              "descriptions": {
                "en": { "language": "en", "value": "hospital in Lahore, Pakistan" }
              },
              "claims": {
                "P856": [
                  {
                    "mainsnak": {
                      "datavalue": { "value": "http://sims.edu.pk/shl.aspx" }
                    }
                  }
                ],
                "P625": [
                  {
                    "mainsnak": {
                      "datavalue": {
                        "value": { "latitude": 31.5412, "longitude": 74.3330 }
                      }
                    }
                  }
                ]
              }
            }
          }
        }
        """;

        var businesses = OverpassResultMapper.MapWikidataToBusinesses(wikidataJson, "Healthcare", "Lahore", "Pakistan");

        businesses.Should().HaveCount(1);
        var b = businesses[0];
        b.Name.Should().Be("Services Hospital");
        b.WebsiteUrl.Should().Be("http://sims.edu.pk/shl.aspx");
        b.Latitude.Should().BeApproximately(31.5412, 0.001);
        b.Longitude.Should().BeApproximately(74.3330, 0.001);
        b.SourceProvider.Should().Be("Wikidata Global Commercial Graph");
    }

    [Fact]
    public void MapBraveSearchToBusinesses_ParsesWebSearchResultsAndExtractsPhone()
    {
        var braveJson = """
        {
          "web": {
            "results": [
              {
                "title": "Ziauddin Hospital - Best Medical Services in Karachi",
                "url": "https://ziauddinhospital.com",
                "description": "Call us at +92 21 35862937 for 24/7 emergency and specialist appointments in Clifton Karachi."
              }
            ]
          }
        }
        """;

        var businesses = OverpassResultMapper.MapBraveSearchToBusinesses(braveJson, "Healthcare", "Karachi", "Pakistan");

        businesses.Should().HaveCount(1);
        var b = businesses[0];
        b.Name.Should().Be("Ziauddin Hospital");
        b.WebsiteUrl.Should().Be("https://ziauddinhospital.com");
        b.PhonePrimary.Should().Contain("92 21 35862937");
        b.SourceProvider.Should().Be("Brave Search Local Discovery");
    }

    [Fact]
    public void CityTerritoryHelper_EliminatesCrossCityLeakage_ForMardanSearch()
    {
        var query = new DiscoveryQuery
        {
            City = "Mardan",
            Country = "Pakistan",
            BBox = new BoundingBox(34.14, 71.97, 34.26, 72.11)
        };

        var nowsheraLead = new Business
        {
            Name = "Army Public School and College",
            City = "نوشہرہ",
            Latitude = 34.0604,
            Longitude = 71.9980
        };

        var dargaiLead = new Business
        {
            Name = "Army Public School Dargai Fort",
            City = "درگئ",
            Latitude = 34.5154,
            Longitude = 71.8961
        };

        var swabiLead = new Business
        {
            Name = "Frontier Public School Bamkhel (Swabi)",
            City = "بام خیل",
            Latitude = 34.0971,
            Longitude = 72.5323
        };

        var karachiLead = new Business
        {
            Name = "Hyderi Public School",
            City = "Karachi",
            Latitude = 24.93,
            Longitude = 67.04
        };

        var mardanLead1 = new Business
        {
            Name = "Aziz Bhatti Shaheed Army Public School & College",
            City = "مردان",
            Latitude = 34.2067,
            Longitude = 72.0389
        };

        var mardanLead2 = new Business
        {
            Name = "F.G. Public High School Mardan",
            City = "Mardan",
            Latitude = 34.2077,
            Longitude = 72.0381
        };

        var garhiKapuraLead = new Business
        {
            Name = "Government School Garhi Kapura",
            City = "گڑھی کپورہ",
            Latitude = 34.1983,
            Longitude = 72.1611
        };

        CityTerritoryHelper.IsGeographicallyContained(nowsheraLead, query, null).Should().BeFalse("Nowshera must never leak into Mardan");
        CityTerritoryHelper.IsGeographicallyContained(dargaiLead, query, null).Should().BeFalse("Dargai must never leak into Mardan");
        CityTerritoryHelper.IsGeographicallyContained(swabiLead, query, null).Should().BeFalse("Swabi must never leak into Mardan");
        CityTerritoryHelper.IsGeographicallyContained(karachiLead, query, null).Should().BeFalse("Karachi must never leak into Mardan");

        CityTerritoryHelper.IsGeographicallyContained(mardanLead1, query, null).Should().BeTrue("Mardan Urdu lead should be accepted");
        mardanLead1.City.Should().Be("Mardan", "Urdu city name should be normalized to canonical English Mardan");

        CityTerritoryHelper.IsGeographicallyContained(mardanLead2, query, null).Should().BeTrue("Mardan English lead should be accepted");
        mardanLead2.City.Should().Be("Mardan");

        CityTerritoryHelper.IsGeographicallyContained(garhiKapuraLead, query, null).Should().BeTrue("Garhi Kapura tehsil in Mardan should be accepted");
        garhiKapuraLead.City.Should().Be("Mardan");
        garhiKapuraLead.Locality.Should().Be("گڑھی کپورہ");
    }

    [Fact]
    public void OsmOfflineCache_ReturnsVerifiedSchools_ForMardan()
    {
        var leads = OsmOfflineCache.GetCachedLeads("Mardan", "Pakistan", ["school", "college", "university"], limit: 30);

        leads.Should().NotBeEmpty();
        leads.Should().Contain(b => b.Name.Contains("Army Public School"));
        leads.Should().Contain(b => b.Name.Contains("F.G. Public High School"));
        leads.All(b => b.City == "Mardan").Should().BeTrue("All returned offline leads must strictly have City == Mardan");
    }

    [Fact]
    public void CalculateRelevanceScore_GivesExactMatch100_ToPublicSchoolKeywords()
    {
        var query = new DiscoveryQuery
        {
            City = "Mardan",
            CategoryPreset = "Education & Training",
            CustomSearchTerm = "public school"
        };

        var apsLead = new Business
        {
            Name = "Aziz Bhatti Shaheed Army Public School & College",
            Category = "Education & Training"
        };

        var modelSchoolLead = new Business
        {
            Name = "Peshawar Model School Mardan Boys Campus",
            Category = "Education & Training"
        };

        var sugarMillLead = new Business
        {
            Name = "Premier Sugar Mills & Distillery",
            Category = "Manufacturing & Industrial"
        };

        var apsScore = OverpassDiscoveryService.CalculateRelevanceScore(apsLead, query);
        var pmsScore = OverpassDiscoveryService.CalculateRelevanceScore(modelSchoolLead, query);
        var millScore = OverpassDiscoveryService.CalculateRelevanceScore(sugarMillLead, query);

        apsScore.Should().Be(100, "Exact public school phrase in educational category should be 100%");
        pmsScore.Should().BeGreaterThanOrEqualTo(70, "Related educational schools should have high relevance");
        millScore.Should().BeLessThan(15, "Unrelated industrial sugar mill should have negligible relevance");
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
        public Task DeleteAsync(string id, CancellationToken ct = default)
        {
            Storage.RemoveAll(b => b.Id == id);
            return Task.CompletedTask;
        }
        public Task DeleteBatchAsync(IEnumerable<string> ids, CancellationToken ct = default)
        {
            var idSet = ids.ToHashSet();
            Storage.RemoveAll(b => idSet.Contains(b.Id));
            return Task.CompletedTask;
        }
        public Task DeleteAllAsync(CancellationToken ct = default)
        {
            Storage.Clear();
            return Task.CompletedTask;
        }
        public Task<int> GetTotalCountAsync(CancellationToken ct = default) => Task.FromResult(Storage.Count);
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
}
