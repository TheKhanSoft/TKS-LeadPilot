using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TKS.LeadPilot.Core.Enums;
using TKS.LeadPilot.Core.Interfaces;
using TKS.LeadPilot.Core.Models;

namespace TKS.LeadPilot.Core.Services;

public class GooglePlacesEnrichmentService : IGooglePlacesEnrichmentService
{
    private const string ApiKeyVaultKey = "GOOGLE_PLACES_API_KEY";
    private readonly HttpClient _httpClient;
    private readonly ISecretVault _secretVault;
    private readonly IBusinessRepository _businessRepository;

    private static GooglePlacesConfig _config = new();
    private static readonly SemaphoreSlim ConfigLock = new(1, 1);

    public GooglePlacesEnrichmentService(
        HttpClient httpClient,
        ISecretVault secretVault,
        IBusinessRepository businessRepository)
    {
        _httpClient = httpClient;
        _secretVault = secretVault;
        _businessRepository = businessRepository;
    }

    public async Task<GooglePlacesConfig> GetConfigAsync(CancellationToken ct = default)
    {
        await ConfigLock.WaitAsync(ct);
        try
        {
            ResetIfNewDay();
            return new GooglePlacesConfig
            {
                IsEnabled = _config.IsEnabled,
                DailyRequestCeiling = _config.DailyRequestCeiling,
                DailyRequestsUsed = _config.DailyRequestsUsed,
                LastResetDateUtc = _config.LastResetDateUtc
            };
        }
        finally
        {
            ConfigLock.Release();
        }
    }

    public async Task SaveConfigAsync(GooglePlacesConfig config, string? apiKey = null, CancellationToken ct = default)
    {
        await ConfigLock.WaitAsync(ct);
        try
        {
            _config.IsEnabled = config.IsEnabled;
            _config.DailyRequestCeiling = Math.Max(1, Math.Min(500, config.DailyRequestCeiling));

            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                _secretVault.SaveSecret(ApiKeyVaultKey, apiKey.Trim());
            }
        }
        finally
        {
            ConfigLock.Release();
        }
    }

    public async Task<int> GetRemainingRequestsTodayAsync(CancellationToken ct = default)
    {
        var config = await GetConfigAsync(ct);
        return Math.Max(0, config.DailyRequestCeiling - config.DailyRequestsUsed);
    }

    public async Task<GooglePlacesEnrichmentResult> EnrichBusinessAsync(Business business, bool forceRefresh = false, CancellationToken ct = default)
    {
        var config = await GetConfigAsync(ct);

        // 1. Cost Guard: Enabled Check
        if (!config.IsEnabled)
        {
            return new GooglePlacesEnrichmentResult
            {
                IsSuccess = false,
                ErrorMessage = "Google Places enrichment is disabled. Enable it in Settings to allow commercial place lookups.",
                RequestsUsedToday = config.DailyRequestsUsed,
                DailyLimit = config.DailyRequestCeiling
            };
        }

        // 2. Cost Guard: API Key Check
        var apiKey = _secretVault.GetSecret(ApiKeyVaultKey);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return new GooglePlacesEnrichmentResult
            {
                IsSuccess = false,
                ErrorMessage = "Google Places API key is missing. Store your key in Settings & Vault.",
                RequestsUsedToday = config.DailyRequestsUsed,
                DailyLimit = config.DailyRequestCeiling
            };
        }

        // 3. Cost Guard: Cache Check (Prevents duplicate calls within 30 days)
        if (!forceRefresh && business.IsGoogleVerified && business.LastEnrichedAtUtc.HasValue)
        {
            var ageDays = (DateTime.UtcNow - business.LastEnrichedAtUtc.Value).TotalDays;
            if (ageDays < 30)
            {
                return new GooglePlacesEnrichmentResult
                {
                    IsSuccess = true,
                    IsCached = true,
                    PlaceId = business.GooglePlaceId,
                    BusinessName = business.Name,
                    FormattedAddress = business.AddressLine1,
                    Phone = business.PhonePrimary,
                    Website = business.WebsiteUrl,
                    Rating = business.GoogleRating,
                    UserRatingsTotal = business.GoogleReviewCount,
                    Latitude = business.Latitude,
                    Longitude = business.Longitude,
                    RequestsUsedToday = config.DailyRequestsUsed,
                    DailyLimit = config.DailyRequestCeiling
                };
            }
        }

        // 4. Cost Guard: Daily Ceiling Check
        if (config.DailyRequestsUsed >= config.DailyRequestCeiling)
        {
            return new GooglePlacesEnrichmentResult
            {
                IsSuccess = false,
                ErrorMessage = $"Daily hard ceiling of {config.DailyRequestCeiling} Google Places requests reached today. Resets at midnight UTC to prevent unexpected billing.",
                RequestsUsedToday = config.DailyRequestsUsed,
                DailyLimit = config.DailyRequestCeiling
            };
        }

        // 5. Execute Google Places Text Search
        try
        {
            var searchQuery = $"{business.Name} {business.City} Pakistan";
            var url = $"https://maps.googleapis.com/maps/api/place/textsearch/json?query={Uri.EscapeDataString(searchQuery)}&key={apiKey}";

            var response = await _httpClient.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
            {
                return new GooglePlacesEnrichmentResult
                {
                    IsSuccess = false,
                    ErrorMessage = $"Google Places API returned HTTP {response.StatusCode}.",
                    RequestsUsedToday = config.DailyRequestsUsed,
                    DailyLimit = config.DailyRequestCeiling
                };
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            var parseResult = ParseGooglePlacesResponse(json);

            // Increment usage counter under lock
            await ConfigLock.WaitAsync(ct);
            try
            {
                _config.DailyRequestsUsed++;
                config.DailyRequestsUsed = _config.DailyRequestsUsed;
            }
            finally
            {
                ConfigLock.Release();
            }

            if (!parseResult.IsSuccess)
            {
                parseResult.RequestsUsedToday = config.DailyRequestsUsed;
                parseResult.DailyLimit = config.DailyRequestCeiling;
                return parseResult;
            }

            // 6. Update and Persist Business
            ApplyEnrichmentToBusiness(business, parseResult);
            await _businessRepository.UpdateAsync(business, ct);

            parseResult.RequestsUsedToday = config.DailyRequestsUsed;
            parseResult.DailyLimit = config.DailyRequestCeiling;
            return parseResult;
        }
        catch (Exception ex)
        {
            return new GooglePlacesEnrichmentResult
            {
                IsSuccess = false,
                ErrorMessage = $"Google Places request error: {ex.Message}",
                RequestsUsedToday = config.DailyRequestsUsed,
                DailyLimit = config.DailyRequestCeiling
            };
        }
    }

    public static GooglePlacesEnrichmentResult ParseGooglePlacesResponse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("status", out var statusProp))
            {
                var status = statusProp.GetString();
                if (status == "ZERO_RESULTS")
                {
                    return new GooglePlacesEnrichmentResult
                    {
                        IsSuccess = false,
                        ErrorMessage = "No matching place found on Google Maps."
                    };
                }
                if (status != "OK")
                {
                    return new GooglePlacesEnrichmentResult
                    {
                        IsSuccess = false,
                        ErrorMessage = $"Google Places API Status: {status}"
                    };
                }
            }

            if (!root.TryGetProperty("results", out var results) || results.GetArrayLength() == 0)
            {
                return new GooglePlacesEnrichmentResult { IsSuccess = false, ErrorMessage = "No results returned." };
            }

            var place = results[0];
            var result = new GooglePlacesEnrichmentResult { IsSuccess = true };

            if (place.TryGetProperty("place_id", out var pid)) result.PlaceId = pid.GetString();
            if (place.TryGetProperty("name", out var n)) result.BusinessName = n.GetString();
            if (place.TryGetProperty("formatted_address", out var addr)) result.FormattedAddress = addr.GetString();
            if (place.TryGetProperty("business_status", out var bs)) result.BusinessStatus = bs.GetString();

            if (place.TryGetProperty("rating", out var r) && r.TryGetDouble(out var rVal))
                result.Rating = rVal;

            if (place.TryGetProperty("user_ratings_total", out var urt) && urt.TryGetInt32(out var urtVal))
                result.UserRatingsTotal = urtVal;

            if (place.TryGetProperty("geometry", out var geom) && geom.TryGetProperty("location", out var loc))
            {
                if (loc.TryGetProperty("lat", out var latProp) && latProp.TryGetDouble(out var latVal)) result.Latitude = latVal;
                if (loc.TryGetProperty("lng", out var lngProp) && lngProp.TryGetDouble(out var lngVal)) result.Longitude = lngVal;
            }

            if (place.TryGetProperty("opening_hours", out var oh) && oh.TryGetProperty("weekday_text", out var wt) && wt.ValueKind == JsonValueKind.Array)
            {
                foreach (var line in wt.EnumerateArray())
                {
                    var text = line.GetString();
                    if (!string.IsNullOrWhiteSpace(text)) result.OpeningHours.Add(text);
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            return new GooglePlacesEnrichmentResult
            {
                IsSuccess = false,
                ErrorMessage = $"Failed to parse Google Places JSON: {ex.Message}"
            };
        }
    }

    private static void ApplyEnrichmentToBusiness(Business business, GooglePlacesEnrichmentResult result)
    {
        business.GooglePlaceId = result.PlaceId;
        business.GoogleRating = result.Rating;
        business.GoogleReviewCount = result.UserRatingsTotal;
        business.IsGoogleVerified = true;
        business.LastEnrichedAtUtc = DateTime.UtcNow;

        if (result.Latitude.HasValue && !business.Latitude.HasValue) business.Latitude = result.Latitude;
        if (result.Longitude.HasValue && !business.Longitude.HasValue) business.Longitude = result.Longitude;

        if (!string.IsNullOrWhiteSpace(result.FormattedAddress) && string.IsNullOrWhiteSpace(business.AddressLine1))
        {
            business.AddressLine1 = result.FormattedAddress;
        }

        if (result.OpeningHours.Count > 0)
        {
            business.BusinessHours = string.Join("; ", result.OpeningHours);
        }

        business.AddTag("Google Verified");
        if (result.Rating.HasValue)
        {
            business.AddTag($"Rated {result.Rating.Value:F1}★");
        }
    }

    private static void ResetIfNewDay()
    {
        var today = DateTime.UtcNow.Date;
        if (_config.LastResetDateUtc < today)
        {
            _config.DailyRequestsUsed = 0;
            _config.LastResetDateUtc = today;
        }
    }
}
