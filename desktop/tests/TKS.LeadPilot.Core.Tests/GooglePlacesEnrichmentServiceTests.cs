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

public class GooglePlacesEnrichmentServiceTests
{
    private class FakeSecretVault : ISecretVault
    {
        private readonly Dictionary<string, string> _secrets = new();

        public void SaveSecret(string key, string secret) => _secrets[key] = secret;
        public string? GetSecret(string key) => _secrets.TryGetValue(key, out var val) ? val : null;
        public void DeleteSecret(string key) => _secrets.Remove(key);
        public bool HasSecret(string key) => _secrets.ContainsKey(key);
    }

    private class MockHttpMessageHandler(string responseContent, HttpStatusCode statusCode = HttpStatusCode.OK) : HttpMessageHandler
    {
        public int CallCount { get; private set; } = 0;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            var response = new HttpResponseMessage(statusCode)
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

    private const string SamplePlacesJsonResponse = """
    {
       "html_attributions" : [],
       "results" : [
          {
             "business_status" : "OPERATIONAL",
             "formatted_address" : "Main Shahrah-e-Faisal, Block 6 PECHS, Karachi, Pakistan",
             "geometry" : {
                "location" : {
                   "lat" : 24.8614622,
                   "lng" : 67.0698188
                }
             },
             "name" : "The Indus Hospital & Health Network",
             "opening_hours" : {
                "open_now" : true,
                "weekday_text" : [
                   "Monday: Open 24 hours",
                   "Tuesday: Open 24 hours",
                   "Wednesday: Open 24 hours"
                ]
             },
             "place_id" : "ChIJ_3jP9E89sz4Rt_r3g1G-r0s",
             "rating" : 4.8,
             "user_ratings_total" : 542
          }
       ],
       "status" : "OK"
    }
    """;

    [Fact]
    public async Task EnrichBusinessAsync_WhenDisabled_BlocksAndReturnsErrorWithoutNetworkCall()
    {
        var mockHttp = new MockHttpMessageHandler("{}");
        var httpClient = new HttpClient(mockHttp);
        var vault = new FakeSecretVault();
        var repo = new InMemoryBusinessRepository();

        var service = new GooglePlacesEnrichmentService(httpClient, vault, repo);
        await service.SaveConfigAsync(new GooglePlacesConfig { IsEnabled = false });

        var business = new Business { Name = "Indus Hospital", City = "Karachi" };

        var result = await service.EnrichBusinessAsync(business);

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("disabled");
        mockHttp.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task EnrichBusinessAsync_WhenApiKeyMissing_BlocksAndReturnsError()
    {
        var mockHttp = new MockHttpMessageHandler("{}");
        var httpClient = new HttpClient(mockHttp);
        var vault = new FakeSecretVault(); // No key
        var repo = new InMemoryBusinessRepository();

        var service = new GooglePlacesEnrichmentService(httpClient, vault, repo);
        await service.SaveConfigAsync(new GooglePlacesConfig { IsEnabled = true });

        var business = new Business { Name = "Indus Hospital", City = "Karachi" };

        var result = await service.EnrichBusinessAsync(business);

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("API key is missing");
        mockHttp.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task EnrichBusinessAsync_WhenCachedWithin30Days_ReturnsCachedResultWithoutCallingApi()
    {
        var mockHttp = new MockHttpMessageHandler("{}");
        var httpClient = new HttpClient(mockHttp);
        var vault = new FakeSecretVault();
        vault.SaveSecret("GOOGLE_PLACES_API_KEY", "AIzaSyFakeKey123");
        var repo = new InMemoryBusinessRepository();

        var service = new GooglePlacesEnrichmentService(httpClient, vault, repo);
        await service.SaveConfigAsync(new GooglePlacesConfig { IsEnabled = true, DailyRequestCeiling = 50 });

        var business = new Business
        {
            Name = "Aga Khan Hospital",
            City = "Karachi",
            IsGoogleVerified = true,
            GooglePlaceId = "ChIJ_Cached123",
            GoogleRating = 4.9,
            GoogleReviewCount = 1200,
            LastEnrichedAtUtc = DateTime.UtcNow.AddDays(-10) // 10 days old (< 30)
        };

        var result = await service.EnrichBusinessAsync(business, forceRefresh: false);

        result.IsSuccess.Should().BeTrue();
        result.IsCached.Should().BeTrue();
        result.Rating.Should().Be(4.9);
        result.UserRatingsTotal.Should().Be(1200);
        mockHttp.CallCount.Should().Be(0); // $0 cost, zero network calls
    }

    [Fact]
    public async Task EnrichBusinessAsync_WhenDailyCeilingReached_BlocksCallAndReturnsError()
    {
        var mockHttp = new MockHttpMessageHandler(SamplePlacesJsonResponse);
        var httpClient = new HttpClient(mockHttp);
        var vault = new FakeSecretVault();
        vault.SaveSecret("GOOGLE_PLACES_API_KEY", "AIzaSyFakeKey123");
        var repo = new InMemoryBusinessRepository();

        var service = new GooglePlacesEnrichmentService(httpClient, vault, repo);
        // Set low ceiling: 2 requests and reset counters
        await service.SaveConfigAsync(new GooglePlacesConfig { IsEnabled = true, DailyRequestCeiling = 2, DailyRequestsUsed = 0, MonthlyRequestsUsed = 0 });

        var b1 = new Business { Name = "Hospital 1", City = "Karachi" };
        var b2 = new Business { Name = "Hospital 2", City = "Karachi" };
        var b3 = new Business { Name = "Hospital 3", City = "Karachi" };

        var r1 = await service.EnrichBusinessAsync(b1);
        r1.IsSuccess.Should().BeTrue();

        var r2 = await service.EnrichBusinessAsync(b2);
        r2.IsSuccess.Should().BeTrue();

        // 3rd call must be blocked by Cost Guard
        var r3 = await service.EnrichBusinessAsync(b3);
        r3.IsSuccess.Should().BeFalse();
        r3.ErrorMessage.Should().Contain("Daily hard ceiling");
        mockHttp.CallCount.Should().Be(2); // Never exceeded ceiling
    }

    [Fact]
    public void ParseGooglePlacesResponse_ExtractsPlaceDetailsAccurately()
    {
        var result = GooglePlacesEnrichmentService.ParseGooglePlacesResponse(SamplePlacesJsonResponse);

        result.IsSuccess.Should().BeTrue();
        result.PlaceId.Should().Be("ChIJ_3jP9E89sz4Rt_r3g1G-r0s");
        result.BusinessName.Should().Be("The Indus Hospital & Health Network");
        result.FormattedAddress.Should().Be("Main Shahrah-e-Faisal, Block 6 PECHS, Karachi, Pakistan");
        result.Rating.Should().Be(4.8);
        result.UserRatingsTotal.Should().Be(542);
        result.Latitude.Should().Be(24.8614622);
        result.Longitude.Should().Be(67.0698188);
        result.OpeningHours.Should().HaveCount(3);
        result.OpeningHours[0].Should().Be("Monday: Open 24 hours");
    }

    [Fact]
    public void ParseGooglePlacesResponse_HandlesZeroResultsGracefully()
    {
        var zeroResultsJson = """
        {
           "html_attributions" : [],
           "results" : [],
           "status" : "ZERO_RESULTS"
        }
        """;

        var result = GooglePlacesEnrichmentService.ParseGooglePlacesResponse(zeroResultsJson);

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("No matching place found");
    }

    [Fact]
    public async Task EnrichBusinessAsync_SuccessfulApiCall_UpdatesBusinessAndAddsTags()
    {
        var mockHttp = new MockHttpMessageHandler(SamplePlacesJsonResponse);
        var httpClient = new HttpClient(mockHttp);
        var vault = new FakeSecretVault();
        vault.SaveSecret("GOOGLE_PLACES_API_KEY", "AIzaSyValidKey_123");
        var repo = new InMemoryBusinessRepository();

        var service = new GooglePlacesEnrichmentService(httpClient, vault, repo);
        await service.SaveConfigAsync(new GooglePlacesConfig { IsEnabled = true, DailyRequestCeiling = 50 });

        var business = new Business
        {
            Id = Guid.NewGuid().ToString(),
            Name = "The Indus Hospital",
            City = "Karachi"
        };
        repo.Storage.Add(business);

        var result = await service.EnrichBusinessAsync(business);

        result.IsSuccess.Should().BeTrue();
        result.IsCached.Should().BeFalse();
        result.Rating.Should().Be(4.8);
        result.UserRatingsTotal.Should().Be(542);

        business.IsGoogleVerified.Should().BeTrue();
        business.GooglePlaceId.Should().Be("ChIJ_3jP9E89sz4Rt_r3g1G-r0s");
        business.GoogleRating.Should().Be(4.8);
        business.GoogleReviewCount.Should().Be(542);
        business.LastEnrichedAtUtc.Should().NotBeNull();
        business.HasTag("Google Verified").Should().BeTrue();
        business.HasTag("Rated 4.8★").Should().BeTrue();
        business.AddressLine1.Should().Be("Main Shahrah-e-Faisal, Block 6 PECHS, Karachi, Pakistan");
        business.BusinessHours.Should().Contain("Monday: Open 24 hours");
    }

    private const string SamplePlacesNewJsonResponse = """
    {
      "places": [
        {
          "id": "ChIJ_3jP9E89sz4Rt_r3g1G-r0s",
          "displayName": {
            "text": "The Indus Hospital & Health Network",
            "languageCode": "en"
          },
          "formattedAddress": "Main Shahrah-e-Faisal, Block 6 PECHS, Karachi, Pakistan",
          "location": {
            "latitude": 24.8614622,
            "longitude": 67.0698188
          },
          "rating": 4.8,
          "userRatingCount": 542,
          "businessStatus": "OPERATIONAL",
          "nationalPhoneNumber": "+92 21 111 111 880",
          "websiteUri": "https://indushospital.org.pk",
          "regularOpeningHours": {
            "weekdayDescriptions": [
              "Monday: Open 24 hours",
              "Tuesday: Open 24 hours",
              "Wednesday: Open 24 hours"
            ]
          }
        }
      ]
    }
    """;

    [Fact]
    public void ParseGooglePlacesResponse_PlacesApiNew_ParsesAllFieldsAccurately()
    {
        var result = GooglePlacesEnrichmentService.ParseGooglePlacesResponse(SamplePlacesNewJsonResponse);

        result.IsSuccess.Should().BeTrue();
        result.PlaceId.Should().Be("ChIJ_3jP9E89sz4Rt_r3g1G-r0s");
        result.BusinessName.Should().Be("The Indus Hospital & Health Network");
        result.FormattedAddress.Should().Be("Main Shahrah-e-Faisal, Block 6 PECHS, Karachi, Pakistan");
        result.Phone.Should().Be("+92 21 111 111 880");
        result.Website.Should().Be("https://indushospital.org.pk");
        result.Rating.Should().Be(4.8);
        result.UserRatingsTotal.Should().Be(542);
        result.Latitude.Should().Be(24.8614622);
        result.Longitude.Should().Be(67.0698188);
        result.BusinessStatus.Should().Be("OPERATIONAL");
        result.OpeningHours.Should().HaveCount(3);
    }

    [Fact]
    public void GetFieldMask_EnforcesStrictNonWildcardMasksBySkuTier()
    {
        var essentialsSearch = GooglePlacesEnrichmentService.GetSearchTextFieldMask(PlacesSkuTier.Essentials);
        var enterpriseSearch = GooglePlacesEnrichmentService.GetSearchTextFieldMask(PlacesSkuTier.EnterpriseWithAtmosphere);

        essentialsSearch.Should().NotContain("*");
        essentialsSearch.Should().Contain("places.id");
        essentialsSearch.Should().Contain("places.displayName");
        essentialsSearch.Should().NotContain("places.rating"); // Essentials excludes rating to protect 10k free tier

        enterpriseSearch.Should().NotContain("*");
        enterpriseSearch.Should().Contain("places.rating");
        enterpriseSearch.Should().Contain("places.userRatingCount");
    }

    [Fact]
    public async Task EnrichBusinessAsync_WhenMonthlyFreeCapReached_BlocksCallToPreventCloudBilling()
    {
        var mockHttp = new MockHttpMessageHandler(SamplePlacesNewJsonResponse);
        var httpClient = new HttpClient(mockHttp);
        var vault = new FakeSecretVault();
        vault.SaveSecret("GOOGLE_PLACES_API_KEY", "AIzaSyValidKey_123");
        var repo = new InMemoryBusinessRepository();

        var service = new GooglePlacesEnrichmentService(httpClient, vault, repo);
        await service.SaveConfigAsync(new GooglePlacesConfig
        {
            IsEnabled = true,
            SkuTier = PlacesSkuTier.EnterpriseWithAtmosphere,
            DailyRequestCeiling = 100
        });

        // Artificially simulate reaching the 1,000 monthly free tier limit
        var config = await service.GetConfigAsync();
        config.MonthlyRequestsUsed = 1000;
        await service.SaveConfigAsync(config);

        var b = new Business { Name = "Test Clinic", City = "Islamabad" };
        var result = await service.EnrichBusinessAsync(b);

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Monthly free tier cap");
        mockHttp.CallCount.Should().Be(0); // Zero cost: request was stopped BEFORE hitting network
    }

    [Fact]
    public async Task TestApiConnectionAsync_SendsIdsOnlyRequest_AtZeroCost()
    {
        var mockHttp = new MockHttpMessageHandler("""{ "places": [{ "id": "ChIJ2eUgeAK6j4ARMBk5bpBiMYo" }] }""");
        var httpClient = new HttpClient(mockHttp);
        var vault = new FakeSecretVault();
        vault.SaveSecret("GOOGLE_PLACES_API_KEY", "AIzaSyValidKey_123");
        var repo = new InMemoryBusinessRepository();

        var service = new GooglePlacesEnrichmentService(httpClient, vault, repo);
        var (isSuccess, msg) = await service.TestApiConnectionAsync();

        isSuccess.Should().BeTrue();
        msg.Should().Contain("$0.00 cost");
        mockHttp.CallCount.Should().Be(1);
    }
}
