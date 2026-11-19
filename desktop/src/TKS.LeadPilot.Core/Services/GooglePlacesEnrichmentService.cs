using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
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
    private const string ConfigVaultKey = "GOOGLE_PLACES_CONFIG_JSON";

    private readonly HttpClient _httpClient;
    private readonly ISecretVault _secretVault;
    private readonly IBusinessRepository _businessRepository;

    private static GooglePlacesConfig _config = new();
    private static bool _isConfigLoaded = false;
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
            EnsureConfigLoaded();
            ResetIfNewPeriod();
            return CloneConfig(_config);
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
            EnsureConfigLoaded();
            _config.IsEnabled = config.IsEnabled;
            _config.SkuTier = config.SkuTier;
            _config.DailyRequestCeiling = Math.Max(1, Math.Min(500, config.DailyRequestCeiling));
            _config.DailyRequestsUsed = config.DailyRequestsUsed;
            _config.MonthlyRequestsUsed = config.MonthlyRequestsUsed;

            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                _secretVault.SaveSecret(ApiKeyVaultKey, apiKey.Trim());
            }

            PersistConfigToVault();
        }
        finally
        {
            ConfigLock.Release();
        }
    }

    public async Task<int> GetRemainingRequestsTodayAsync(CancellationToken ct = default)
    {
        var config = await GetConfigAsync(ct);
        return config.DailyRequestsRemaining;
    }

    public async Task<int> GetRemainingRequestsThisMonthAsync(CancellationToken ct = default)
    {
        var config = await GetConfigAsync(ct);
        return config.MonthlyRequestsRemaining;
    }

    public async Task ResetUsageCountersAsync(CancellationToken ct = default)
    {
        await ConfigLock.WaitAsync(ct);
        try
        {
            EnsureConfigLoaded();
            _config.DailyRequestsUsed = 0;
            _config.MonthlyRequestsUsed = 0;
            _config.LastResetDateUtc = DateTime.UtcNow.Date;
            _config.LastMonthlyResetDateUtc = DateTime.UtcNow.Date;
            PersistConfigToVault();
        }
        finally
        {
            ConfigLock.Release();
        }
    }

    public async Task<(bool IsSuccess, string Message)> TestApiConnectionAsync(CancellationToken ct = default)
    {
        var apiKey = _secretVault.GetSecret(ApiKeyVaultKey);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return (false, "Google Places API key is missing. Please save a valid API key in Settings & Vault.");
        }

        try
        {
            // Places API (New) Text Search with IDs Only FieldMask (Unlimited Free SKU Tier = $0.00 cost)
            var payload = new
            {
                textQuery = "Googleplex Mountain View CA",
                pageSize = 1
            };

            var jsonContent = JsonSerializer.Serialize(payload);
            using var reqMsg = new HttpRequestMessage(HttpMethod.Post, "https://places.googleapis.com/v1/places:searchText")
            {
                Content = new StringContent(jsonContent, Encoding.UTF8, "application/json")
            };

            reqMsg.Headers.Add("X-Goog-Api-Key", apiKey);
            reqMsg.Headers.Add("X-Goog-FieldMask", "places.id");

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(10));

            var response = await _httpClient.SendAsync(reqMsg, cts.Token);
            var responseBody = await response.Content.ReadAsStringAsync(ct);

            if (response.IsSuccessStatusCode)
            {
                return (true, "✅ Google Places API (New) verified active! Connection succeeded at $0.00 cost.");
            }

            // Extract Google Cloud error details if available
            var extractedError = ExtractGoogleErrorMessage(responseBody);
            return (false, $"❌ Google API Error (HTTP {(int)response.StatusCode}): {extractedError}");
        }
        catch (Exception ex)
        {
            return (false, $"Connection test failed: {ex.Message}");
        }
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
                DailyLimit = config.DailyRequestCeiling,
                MonthlyRequestsUsed = config.MonthlyRequestsUsed,
                MonthlyLimit = config.MonthlyFreeAllowance,
                SkuTier = config.SkuTier
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
                DailyLimit = config.DailyRequestCeiling,
                MonthlyRequestsUsed = config.MonthlyRequestsUsed,
                MonthlyLimit = config.MonthlyFreeAllowance,
                SkuTier = config.SkuTier
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
                    DailyLimit = config.DailyRequestCeiling,
                    MonthlyRequestsUsed = config.MonthlyRequestsUsed,
                    MonthlyLimit = config.MonthlyFreeAllowance,
                    SkuTier = config.SkuTier
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
                DailyLimit = config.DailyRequestCeiling,
                MonthlyRequestsUsed = config.MonthlyRequestsUsed,
                MonthlyLimit = config.MonthlyFreeAllowance,
                SkuTier = config.SkuTier
            };
        }

        // 5. Cost Guard: Monthly Free Cap Check (Zero-Cost Billing Protection)
        if (config.MonthlyRequestsUsed >= config.MonthlyFreeAllowance)
        {
            return new GooglePlacesEnrichmentResult
            {
                IsSuccess = false,
                ErrorMessage = $"Monthly free tier cap of {config.MonthlyFreeAllowance:N0} requests reached for {config.SkuTier} tier. Blocked to guarantee zero unexpected Cloud Billing charges.",
                RequestsUsedToday = config.DailyRequestsUsed,
                DailyLimit = config.DailyRequestCeiling,
                MonthlyRequestsUsed = config.MonthlyRequestsUsed,
                MonthlyLimit = config.MonthlyFreeAllowance,
                SkuTier = config.SkuTier
            };
        }

        // 6. Execute Google Places (New) Request
        try
        {
            HttpResponseMessage response;

            // If we already have a Place ID, query Place Details (New) directly
            if (!string.IsNullOrWhiteSpace(business.GooglePlaceId))
            {
                var detailsMask = GetPlaceDetailsFieldMask(config.SkuTier);
                var detailsUrl = $"https://places.googleapis.com/v1/places/{Uri.EscapeDataString(business.GooglePlaceId)}";

                using var req = new HttpRequestMessage(HttpMethod.Get, detailsUrl);
                req.Headers.Add("X-Goog-Api-Key", apiKey);
                req.Headers.Add("X-Goog-FieldMask", detailsMask);

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(12));
                response = await _httpClient.SendAsync(req, cts.Token);
            }
            else
            {
                // Otherwise use Text Search (New) with exact field mask
                var searchMask = GetSearchTextFieldMask(config.SkuTier);
                var locationSuffix = !string.IsNullOrWhiteSpace(business.City) ? $"{business.City} {business.Country}" : business.Country;
                var searchQuery = $"{business.Name} {locationSuffix}".Trim();

                var payload = new
                {
                    textQuery = searchQuery,
                    pageSize = 1
                };

                var jsonPayload = JsonSerializer.Serialize(payload);
                using var req = new HttpRequestMessage(HttpMethod.Post, "https://places.googleapis.com/v1/places:searchText")
                {
                    Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json")
                };
                req.Headers.Add("X-Goog-Api-Key", apiKey);
                req.Headers.Add("X-Goog-FieldMask", searchMask);

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(12));
                response = await _httpClient.SendAsync(req, cts.Token);
            }

            var json = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                var errDetail = ExtractGoogleErrorMessage(json);
                return new GooglePlacesEnrichmentResult
                {
                    IsSuccess = false,
                    ErrorMessage = $"Google Places API error (HTTP {(int)response.StatusCode}): {errDetail}",
                    RequestsUsedToday = config.DailyRequestsUsed,
                    DailyLimit = config.DailyRequestCeiling,
                    MonthlyRequestsUsed = config.MonthlyRequestsUsed,
                    MonthlyLimit = config.MonthlyFreeAllowance,
                    SkuTier = config.SkuTier
                };
            }

            var parseResult = ParseGooglePlacesResponse(json);

            // Increment usage counters under lock
            await ConfigLock.WaitAsync(ct);
            try
            {
                _config.DailyRequestsUsed++;
                _config.MonthlyRequestsUsed++;
                PersistConfigToVault();

                config.DailyRequestsUsed = _config.DailyRequestsUsed;
                config.MonthlyRequestsUsed = _config.MonthlyRequestsUsed;
            }
            finally
            {
                ConfigLock.Release();
            }

            parseResult.RequestsUsedToday = config.DailyRequestsUsed;
            parseResult.DailyLimit = config.DailyRequestCeiling;
            parseResult.MonthlyRequestsUsed = config.MonthlyRequestsUsed;
            parseResult.MonthlyLimit = config.MonthlyFreeAllowance;
            parseResult.SkuTier = config.SkuTier;

            if (!parseResult.IsSuccess)
            {
                return parseResult;
            }

            // 7. Update and Persist Business
            ApplyEnrichmentToBusiness(business, parseResult);
            await _businessRepository.UpdateAsync(business, ct);

            return parseResult;
        }
        catch (Exception ex)
        {
            return new GooglePlacesEnrichmentResult
            {
                IsSuccess = false,
                ErrorMessage = $"Google Places request error: {ex.Message}",
                RequestsUsedToday = config.DailyRequestsUsed,
                DailyLimit = config.DailyRequestCeiling,
                MonthlyRequestsUsed = config.MonthlyRequestsUsed,
                MonthlyLimit = config.MonthlyFreeAllowance,
                SkuTier = config.SkuTier
            };
        }
    }

    public async Task<List<Business>> DiscoverPlacesAsync(string query, string city, string country, int limit = 20, CancellationToken ct = default)
    {
        var results = new List<Business>();
        var config = await GetConfigAsync(ct);

        var apiKey = _secretVault.GetSecret(ApiKeyVaultKey)
                  ?? _secretVault.GetSecret("PLACES_API_KEY")
                  ?? _secretVault.GetSecret("GooglePlacesApiKey");
        if (string.IsNullOrWhiteSpace(apiKey)) return results;

        if (config.DailyRequestsUsed >= config.DailyRequestCeiling) return results;
        if (config.MonthlyRequestsUsed >= config.MonthlyFreeAllowance) return results;

        try
        {
            var searchMask = GetSearchTextFieldMask(config.SkuTier);
            var searchQuery = $"{query} in {city} {country}".Trim();

            var payload = new
            {
                textQuery = searchQuery,
                pageSize = Math.Clamp(limit, 1, 20)
            };

            var jsonPayload = JsonSerializer.Serialize(payload);
            using var req = new HttpRequestMessage(HttpMethod.Post, "https://places.googleapis.com/v1/places:searchText")
            {
                Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json")
            };
            req.Headers.Add("X-Goog-Api-Key", apiKey);
            req.Headers.Add("X-Goog-FieldMask", searchMask);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(15));
            var response = await _httpClient.SendAsync(req, cts.Token);

            if (!response.IsSuccessStatusCode) return results;

            var json = await response.Content.ReadAsStringAsync(ct);

            // Increment usage counters
            await ConfigLock.WaitAsync(ct);
            try
            {
                _config.DailyRequestsUsed++;
                _config.MonthlyRequestsUsed++;
                PersistConfigToVault();
            }
            finally
            {
                ConfigLock.Release();
            }

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Handle Places API (New) response array "places"
            if (root.TryGetProperty("places", out var placesArray) && placesArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var place in placesArray.EnumerateArray())
                {
                    var parsed = ParsePlaceElement(place);
                    if (string.IsNullOrWhiteSpace(parsed.BusinessName)) continue;

                    results.Add(CreateBusinessFromParsedResult(parsed, query, city, country));
                    if (results.Count >= limit) break;
                }
            }
            // Fallback for Legacy API "results" array
            else if (root.TryGetProperty("results", out var legacyArray) && legacyArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var place in legacyArray.EnumerateArray())
                {
                    var parsed = ParsePlaceElement(place);
                    if (string.IsNullOrWhiteSpace(parsed.BusinessName)) continue;

                    results.Add(CreateBusinessFromParsedResult(parsed, query, city, country));
                    if (results.Count >= limit) break;
                }
            }
        }
        catch { }

        return results;
    }

    public static GooglePlacesEnrichmentResult ParseGooglePlacesResponse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Check for API Error
            if (root.TryGetProperty("error", out var errorProp))
            {
                var message = errorProp.TryGetProperty("message", out var msg) ? msg.GetString() : "Google API Error";
                return new GooglePlacesEnrichmentResult
                {
                    IsSuccess = false,
                    ErrorMessage = message
                };
            }

            // Check for Legacy Status
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

            // Case A: Places API (New) Search -> { "places": [ ... ] }
            if (root.TryGetProperty("places", out var places) && places.ValueKind == JsonValueKind.Array)
            {
                if (places.GetArrayLength() == 0)
                {
                    return new GooglePlacesEnrichmentResult { IsSuccess = false, ErrorMessage = "No matching place found on Google Maps." };
                }
                return ParsePlaceElement(places[0]);
            }

            // Case B: Legacy Search -> { "results": [ ... ] }
            if (root.TryGetProperty("results", out var results) && results.ValueKind == JsonValueKind.Array)
            {
                if (results.GetArrayLength() == 0)
                {
                    return new GooglePlacesEnrichmentResult { IsSuccess = false, ErrorMessage = "No results returned." };
                }
                return ParsePlaceElement(results[0]);
            }

            // Case C: Places API (New) Place Details -> Root is the place object directly
            if (root.TryGetProperty("id", out _) || root.TryGetProperty("displayName", out _) || root.TryGetProperty("formattedAddress", out _))
            {
                return ParsePlaceElement(root);
            }

            // Empty or unrecognized payload
            return new GooglePlacesEnrichmentResult
            {
                IsSuccess = false,
                ErrorMessage = "No matching place found in Google Places response."
            };
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

    private static GooglePlacesEnrichmentResult ParsePlaceElement(JsonElement place)
    {
        var result = new GooglePlacesEnrichmentResult { IsSuccess = true };

        // 1. Place ID (Permanent storage permitted at zero cost)
        if (place.TryGetProperty("id", out var idProp)) result.PlaceId = idProp.GetString();
        else if (place.TryGetProperty("place_id", out var pidProp)) result.PlaceId = pidProp.GetString();

        // 2. Business Name
        if (place.TryGetProperty("displayName", out var dnProp) && dnProp.TryGetProperty("text", out var textProp))
        {
            result.BusinessName = textProp.GetString();
        }
        else if (place.TryGetProperty("name", out var nProp))
        {
            result.BusinessName = nProp.GetString();
        }

        // 3. Formatted Address
        if (place.TryGetProperty("formattedAddress", out var faProp)) result.FormattedAddress = faProp.GetString();
        else if (place.TryGetProperty("formatted_address", out var lfaProp)) result.FormattedAddress = lfaProp.GetString();

        // 4. Phone Number
        if (place.TryGetProperty("nationalPhoneNumber", out var npProp)) result.Phone = npProp.GetString();
        else if (place.TryGetProperty("internationalPhoneNumber", out var ipProp)) result.Phone = ipProp.GetString();
        else if (place.TryGetProperty("formatted_phone_number", out var lfpProp)) result.Phone = lfpProp.GetString();
        else if (place.TryGetProperty("international_phone_number", out var lipProp)) result.Phone = lipProp.GetString();

        // 5. Website URI
        if (place.TryGetProperty("websiteUri", out var wuProp)) result.Website = wuProp.GetString();
        else if (place.TryGetProperty("website", out var lwProp)) result.Website = lwProp.GetString();

        // 6. Rating & Review Count
        if (place.TryGetProperty("rating", out var rProp) && rProp.TryGetDouble(out var rVal)) result.Rating = rVal;
        if (place.TryGetProperty("userRatingCount", out var urcProp) && urcProp.TryGetInt32(out var urcVal)) result.UserRatingsTotal = urcVal;
        else if (place.TryGetProperty("user_ratings_total", out var urtProp) && urtProp.TryGetInt32(out var urtVal)) result.UserRatingsTotal = urtVal;

        // 7. Business Status
        if (place.TryGetProperty("businessStatus", out var bsProp)) result.BusinessStatus = bsProp.GetString();
        else if (place.TryGetProperty("business_status", out var lbsProp)) result.BusinessStatus = lbsProp.GetString();

        // 8. Location Coordinates
        if (place.TryGetProperty("location", out var locProp))
        {
            if (locProp.TryGetProperty("latitude", out var lat) && lat.TryGetDouble(out var latVal)) result.Latitude = latVal;
            if (locProp.TryGetProperty("longitude", out var lng) && lng.TryGetDouble(out var lngVal)) result.Longitude = lngVal;
        }
        else if (place.TryGetProperty("geometry", out var geomProp) && geomProp.TryGetProperty("location", out var glocProp))
        {
            if (glocProp.TryGetProperty("lat", out var glat) && glat.TryGetDouble(out var glatVal)) result.Latitude = glatVal;
            if (glocProp.TryGetProperty("lng", out var glng) && glng.TryGetDouble(out var glngVal)) result.Longitude = glngVal;
        }

        // 9. Regular Opening Hours
        if (place.TryGetProperty("regularOpeningHours", out var rohProp) && rohProp.TryGetProperty("weekdayDescriptions", out var wdProp) && wdProp.ValueKind == JsonValueKind.Array)
        {
            foreach (var line in wdProp.EnumerateArray())
            {
                var text = line.GetString();
                if (!string.IsNullOrWhiteSpace(text)) result.OpeningHours.Add(text);
            }
        }
        else if (place.TryGetProperty("opening_hours", out var ohProp) && ohProp.TryGetProperty("weekday_text", out var wtProp) && wtProp.ValueKind == JsonValueKind.Array)
        {
            foreach (var line in wtProp.EnumerateArray())
            {
                var text = line.GetString();
                if (!string.IsNullOrWhiteSpace(text)) result.OpeningHours.Add(text);
            }
        }

        return result;
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

        if (!string.IsNullOrWhiteSpace(result.Phone) && string.IsNullOrWhiteSpace(business.PhonePrimary))
        {
            business.PhonePrimary = result.Phone;
        }

        if (!string.IsNullOrWhiteSpace(result.Website) && string.IsNullOrWhiteSpace(business.WebsiteUrl))
        {
            business.WebsiteUrl = result.Website;
            business.WebsiteStatus = WebsiteStatus.ActiveWebsite;
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

    private static Business CreateBusinessFromParsedResult(GooglePlacesEnrichmentResult parsed, string query, string city, string country)
    {
        var b = new Business
        {
            Id = Guid.NewGuid().ToString(),
            Name = parsed.BusinessName?.Trim() ?? "Commercial Business",
            Category = CategoryResolver.ResolveEffectiveCategory(new DiscoveryQuery { CustomSearchTerm = query, City = city, Country = country }, parsed.BusinessName, null),
            City = city,
            Country = country,
            AddressLine1 = parsed.FormattedAddress ?? $"{city}, {country}",
            PhonePrimary = parsed.Phone,
            WebsiteUrl = parsed.Website,
            Latitude = parsed.Latitude,
            Longitude = parsed.Longitude,
            GooglePlaceId = parsed.PlaceId,
            GoogleRating = parsed.Rating,
            GoogleReviewCount = parsed.UserRatingsTotal,
            IsGoogleVerified = true,
            LastEnrichedAtUtc = DateTime.UtcNow,
            WebsiteStatus = !string.IsNullOrWhiteSpace(parsed.Website) ? WebsiteStatus.ActiveWebsite : WebsiteStatus.NoWebsite,
            PipelineStageId = "stage-discovered",
            LeadStatus = LeadStatus.Discovered,
            SourceProvider = "Google Places API (New)",
            SourceReferenceId = parsed.PlaceId ?? Guid.NewGuid().ToString("N"),
            Tags = $"Google Places, {query}" + (parsed.Rating.HasValue ? $", Rated {parsed.Rating.Value:F1}★" : ""),
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        if (parsed.OpeningHours.Count > 0)
        {
            b.BusinessHours = string.Join("; ", parsed.OpeningHours);
        }

        return b;
    }

    public static string GetSearchTextFieldMask(PlacesSkuTier tier)
    {
        // Essentials SKU: Free up to 10,000 requests / month
        var essentials = "places.id,places.displayName,places.formattedAddress,places.location,places.nationalPhoneNumber,places.internationalPhoneNumber,places.websiteUri,places.businessStatus,places.primaryType,places.types";

        if (tier == PlacesSkuTier.Essentials)
        {
            return essentials;
        }

        // Enterprise with Atmosphere SKU: Free up to 1,000 requests / month
        return $"{essentials},places.rating,places.userRatingCount,places.regularOpeningHours";
    }

    public static string GetPlaceDetailsFieldMask(PlacesSkuTier tier)
    {
        // Essentials SKU: Free up to 10,000 requests / month
        var essentials = "id,displayName,formattedAddress,location,nationalPhoneNumber,internationalPhoneNumber,websiteUri,businessStatus,primaryType,types";

        if (tier == PlacesSkuTier.Essentials)
        {
            return essentials;
        }

        // Enterprise with Atmosphere SKU: Free up to 1,000 requests / month
        return $"{essentials},rating,userRatingCount,regularOpeningHours";
    }

    private static string ExtractGoogleErrorMessage(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("error", out var err))
            {
                var msg = err.TryGetProperty("message", out var m) ? m.GetString() : null;
                var status = err.TryGetProperty("status", out var s) ? s.GetString() : null;
                if (!string.IsNullOrWhiteSpace(msg))
                {
                    return !string.IsNullOrWhiteSpace(status) ? $"{status}: {msg}" : msg;
                }
            }
        }
        catch { }

        return !string.IsNullOrWhiteSpace(json) ? json[..Math.Min(120, json.Length)] : "Unknown Google Cloud API error";
    }

    private void EnsureConfigLoaded()
    {
        if (_isConfigLoaded) return;

        try
        {
            var savedJson = _secretVault.GetSecret(ConfigVaultKey);
            if (!string.IsNullOrWhiteSpace(savedJson))
            {
                var loaded = JsonSerializer.Deserialize<GooglePlacesConfig>(savedJson);
                if (loaded != null)
                {
                    _config = loaded;
                }
            }
        }
        catch { }

        _isConfigLoaded = true;
    }

    private void PersistConfigToVault()
    {
        try
        {
            var json = JsonSerializer.Serialize(_config);
            _secretVault.SaveSecret(ConfigVaultKey, json);
        }
        catch { }
    }

    private static void ResetIfNewPeriod()
    {
        var today = DateTime.UtcNow.Date;

        // Daily Reset
        if (_config.LastResetDateUtc < today)
        {
            _config.DailyRequestsUsed = 0;
            _config.LastResetDateUtc = today;
        }

        // Monthly Reset (Google Cloud Billing Free Tier resets monthly)
        if (_config.LastMonthlyResetDateUtc.Year != today.Year || _config.LastMonthlyResetDateUtc.Month != today.Month)
        {
            _config.MonthlyRequestsUsed = 0;
            _config.LastMonthlyResetDateUtc = today;
        }
    }

    private static GooglePlacesConfig CloneConfig(GooglePlacesConfig source)
    {
        return new GooglePlacesConfig
        {
            IsEnabled = source.IsEnabled,
            SkuTier = source.SkuTier,
            DailyRequestCeiling = source.DailyRequestCeiling,
            DailyRequestsUsed = source.DailyRequestsUsed,
            MonthlyRequestsUsed = source.MonthlyRequestsUsed,
            LastResetDateUtc = source.LastResetDateUtc,
            LastMonthlyResetDateUtc = source.LastMonthlyResetDateUtc
        };
    }
}
