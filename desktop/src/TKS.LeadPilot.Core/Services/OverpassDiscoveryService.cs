using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using TKS.LeadPilot.Core.Interfaces;
using TKS.LeadPilot.Core.Models;

namespace TKS.LeadPilot.Core.Services;

public class OverpassDiscoveryService : IDiscoveryService
{
    private readonly HttpClient _httpClient;
    private readonly IBusinessRepository _repository;
    private readonly IDeduplicationService _dedupService;
    private readonly IGeoLocationService? _geoLocationService;
    private readonly ISecretVault? _secretVault;
    private readonly IGooglePlacesEnrichmentService? _googlePlacesService;
    private readonly IAiProposalSynthesizer? _aiSynthesizer;

    // Rate limiter state: minimum 1000ms between requests
    private static readonly SemaphoreSlim RateLimitLock = new(1, 1);
    private static DateTime _lastRequestTime = DateTime.MinValue;
    private const int MinRequestIntervalMs = 1000;

    // Public Overpass mirror endpoints
    private static readonly string[] Endpoints =
    [
        "https://overpass.kumi.systems/api/interpreter",
        "https://overpass-api.de/api/interpreter",
        "https://maps.mail.ru/osm/tools/overpass/api/interpreter",
        "https://overpass.private.coffee/api/interpreter"
    ];

    public OverpassDiscoveryService(
        HttpClient httpClient,
        IBusinessRepository repository,
        IDeduplicationService dedupService,
        IGeoLocationService? geoLocationService = null,
        ISecretVault? secretVault = null,
        IGooglePlacesEnrichmentService? googlePlacesService = null,
        IAiProposalSynthesizer? aiSynthesizer = null)
    {
        _httpClient = httpClient;
        _repository = repository;
        _dedupService = dedupService;
        _geoLocationService = geoLocationService;
        _secretVault = secretVault;
        _googlePlacesService = googlePlacesService;
        _aiSynthesizer = aiSynthesizer;

        if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "TKS-LeadPilot/1.0 (contact: info@thekhansoft.com)");
        }
    }

    public IReadOnlyList<string> GetSupportedCities()
    {
        return OverpassQueryBuilder.CityBoundingBoxes.Keys.OrderBy(c => c).ToList();
    }

    public IReadOnlyDictionary<string, List<string>> GetPresetCategories()
    {
        return OverpassQueryBuilder.CategoryPresets;
    }

    public async Task<DiscoveryResult> DiscoverLeadsAsync(DiscoveryQuery query, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        // Ensure city has a bounding box if GeoLocationService is available
        if (query.BBox == null && _geoLocationService != null && !string.IsNullOrWhiteSpace(query.City))
        {
            var cityInfo = _geoLocationService.FindCity(query.City, query.Country);
            if (cityInfo != null)
            {
                query.BBox = _geoLocationService.CalculateProximityBoundingBox(cityInfo.Latitude, cityInfo.Longitude, 18.0);
            }
        }

        var result = new DiscoveryResult
        {
            QueryExecuted = $"Engine: {query.SearchEngine}, City: {query.City}, Category: {query.CategoryPreset ?? "All Commercial"}",
            Provider = "Universal Multi-Source Engine"
        };

        // Enforce rate limit
        await RateLimitLock.WaitAsync(ct);
        try
        {
            var elapsedSinceLast = (DateTime.UtcNow - _lastRequestTime).TotalMilliseconds;
            if (elapsedSinceLast < MinRequestIntervalMs)
            {
                var delay = (int)(MinRequestIntervalMs - elapsedSinceLast);
                await Task.Delay(delay, ct);
            }
            _lastRequestTime = DateTime.UtcNow;
        }
        finally
        {
            RateLimitLock.Release();
        }

        var combinedBusinesses = new List<Business>();
        var activeSources = new List<string>();

        var engine = query.SearchEngine ?? "All Sources (Unified Aggregator)";
        var isAggregator = engine.Contains("Aggregator", StringComparison.OrdinalIgnoreCase) ||
                           engine.Contains("All Sources", StringComparison.OrdinalIgnoreCase);

        // 1. OpenStreetMap Nominatim Live Search
        if (isAggregator || engine.Contains("Nominatim", StringComparison.OrdinalIgnoreCase) || engine.Contains("OpenStreetMap", StringComparison.OrdinalIgnoreCase))
        {
            var nomLeads = await QueryNominatimAsync(query, ct);
            if (nomLeads.Count > 0)
            {
                combinedBusinesses.AddRange(nomLeads);
                activeSources.Add($"OSM Nominatim ({nomLeads.Count})");
            }
        }

        // 2. OpenStreetMap Overpass Live Query (if mirrors available)
        if (isAggregator || engine.Contains("Overpass", StringComparison.OrdinalIgnoreCase) || engine.Contains("OpenStreetMap", StringComparison.OrdinalIgnoreCase))
        {
            var ql = OverpassQueryBuilder.BuildQuery(query);
            result.QueryExecuted = ql;
            var osmLeads = await QueryLiveOverpassAsync(query, ql, ct);
            if (osmLeads.Count > 0)
            {
                combinedBusinesses.AddRange(osmLeads);
                activeSources.Add($"OSM Overpass ({osmLeads.Count})");
            }
        }

        // 3. Google Places / Google My Business Discovery
        if (isAggregator || engine.Contains("Google", StringComparison.OrdinalIgnoreCase))
        {
            var gmbLeads = await QueryGooglePlacesAsync(query, ct);
            if (gmbLeads.Count > 0)
            {
                combinedBusinesses.AddRange(gmbLeads);
                activeSources.Add($"Google My Business ({gmbLeads.Count})");
            }
        }

        // 4. Google Gemini Free Tier AI Discovery
        if (isAggregator || engine.Contains("Gemini", StringComparison.OrdinalIgnoreCase) || engine.Contains("AI", StringComparison.OrdinalIgnoreCase))
        {
            var aiLeads = await QueryGeminiAiAsync(query, ct);
            if (aiLeads.Count > 0)
            {
                combinedBusinesses.AddRange(aiLeads);
                activeSources.Add($"Gemini AI ({aiLeads.Count})");
            }
        }

        // 5. High-Density Verified Local Catalog Fallback
        // Seamlessly activates verified local cache when live network queries return no results
        if (combinedBusinesses.Count == 0)
        {
            var catalogLeads = OsmOfflineCache.GetCachedLeads(query.City, query.Country, query.TargetAmenities, Math.Max(query.Limit, 35));
            if (catalogLeads.Count > 0)
            {
                combinedBusinesses.AddRange(catalogLeads);
                activeSources.Add($"OpenStreetMap (Verified Local Cache) ({catalogLeads.Count})");
            }
        }

        if (combinedBusinesses.Count == 0)
        {
            result.ErrorMessage = $"No matching businesses found in {query.City}, {query.Country}. Please verify city name or select All Commercial preset.";
            result.ExecutionDuration = sw.Elapsed;
            return result;
        }

        // Deduplicate gathered leads internally (across the different engines)
        var deduplicatedLeads = new List<Business>();
        foreach (var candidate in combinedBusinesses)
        {
            if (string.IsNullOrWhiteSpace(candidate.Name)) continue;
            var dupCheck = _dedupService.CheckDuplicate(candidate, deduplicatedLeads);
            if (!dupCheck.IsDuplicate)
            {
                deduplicatedLeads.Add(candidate);
            }
        }

        // Fetch existing database records to flag already imported leads
        var existing = await _repository.GetAllAsync(ct: ct);

        int newCount = 0;
        int dupCount = 0;

        foreach (var b in deduplicatedLeads.Take(query.Limit))
        {
            var dedupMatch = _dedupService.CheckDuplicate(b, existing);
            var isDup = dedupMatch.IsDuplicate;

            if (isDup) dupCount++;
            else newCount++;

            result.Leads.Add(new DiscoveredLead
            {
                Business = b,
                DuplicateMatch = isDup ? dedupMatch : null,
                IsSelectedForImport = !isDup
            });
        }

        result.NewCount = newCount;
        result.DuplicateCount = dupCount;
        result.ExecutionDuration = sw.Elapsed;
        result.Provider = activeSources.Count > 0 ? string.Join(" + ", activeSources) : "Universal Lead Engine";

        return result;
    }

    private async Task<List<Business>> QueryNominatimAsync(DiscoveryQuery query, CancellationToken ct)
    {
        var list = new List<Business>();
        var isAllCommercial = string.IsNullOrWhiteSpace(query.CategoryPreset) ||
                              query.CategoryPreset.Contains("All Commercial", StringComparison.OrdinalIgnoreCase);

        var searchKeywords = new List<string>();

        if (!string.IsNullOrWhiteSpace(query.CustomSearchTerm))
        {
            searchKeywords.Add(query.CustomSearchTerm.Trim());
        }
        else if (isAllCommercial)
        {
            searchKeywords.AddRange(["hospital", "hotel", "bank", "restaurant"]);
        }
        else
        {
            var cleanPreset = query.CategoryPreset!.Replace("&", "and").Trim();
            searchKeywords.Add(cleanPreset);
        }

        foreach (var kw in searchKeywords)
        {
            try
            {
                var queryStr = $"{kw} in {query.City}";
                if (!string.IsNullOrWhiteSpace(query.Country)) queryStr += $", {query.Country}";

                var url = $"https://nominatim.openstreetmap.org/search?q={Uri.EscapeDataString(queryStr)}&format=json&addressdetails=1&extratags=1&limit=20";

                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.Add("User-Agent", "TKS-LeadPilot/1.0 (contact: info@thekhansoft.com)");

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(6));

                var resp = await _httpClient.SendAsync(req, cts.Token);
                if (resp.IsSuccessStatusCode)
                {
                    var json = await resp.Content.ReadAsStringAsync(ct);
                    var mapped = OverpassResultMapper.MapNominatimToBusinesses(json, query.City, query.Country, query.StateProvince);
                    list.AddRange(mapped);
                }
            }
            catch { }
        }

        return list;
    }

    private async Task<List<Business>> QueryLiveOverpassAsync(DiscoveryQuery query, string ql, CancellationToken ct)
    {
        var list = new List<Business>();

        foreach (var endpoint in Endpoints)
        {
            try
            {
                using var content = new FormUrlEncodedContent([
                    new KeyValuePair<string, string>("data", ql)
                ]);

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(10));

                var response = await _httpClient.PostAsync(endpoint, content, cts.Token);
                if (response.IsSuccessStatusCode)
                {
                    var text = await response.Content.ReadAsStringAsync(ct);
                    if (!string.IsNullOrWhiteSpace(text) && text.Contains("\"elements\":"))
                    {
                        var mapped = OverpassResultMapper.MapJsonToBusinesses(text, query.City, query.Country, query.StateProvince);
                        if (mapped.Count > 0)
                        {
                            list.AddRange(mapped);
                            break;
                        }
                    }
                }
            }
            catch { }
        }

        return list;
    }

    private async Task<List<Business>> QueryGooglePlacesAsync(DiscoveryQuery query, CancellationToken ct)
    {
        if (_googlePlacesService == null) return [];

        try
        {
            var categoryTerm = query.CategoryPreset ?? "Commercial Businesses";
            if (categoryTerm.Contains("All Commercial", StringComparison.OrdinalIgnoreCase))
            {
                categoryTerm = "businesses";
            }

            var searchTerm = !string.IsNullOrWhiteSpace(query.CustomSearchTerm)
                ? query.CustomSearchTerm.Trim()
                : categoryTerm;

            return await _googlePlacesService.DiscoverPlacesAsync(searchTerm, query.City, query.Country, limit: 30, ct: ct);
        }
        catch { }

        return [];
    }

    private async Task<List<Business>> QueryGeminiAiAsync(DiscoveryQuery query, CancellationToken ct)
    {
        if (_aiSynthesizer == null) return [];

        try
        {
            var categoryTerm = query.CategoryPreset ?? "Commercial Businesses";
            var searchTerm = !string.IsNullOrWhiteSpace(query.CustomSearchTerm)
                ? query.CustomSearchTerm.Trim()
                : categoryTerm;

            return await _aiSynthesizer.DiscoverBusinessesAiAsync(query.City, query.Country, searchTerm, count: 25, ct: ct);
        }
        catch { }

        return [];
    }

    public async Task<ImportExecutionResult> ImportDiscoveredLeadsAsync(
        IEnumerable<DiscoveredLead> selectedLeads,
        DeduplicationStrategy strategy = DeduplicationStrategy.Skip,
        CancellationToken ct = default)
    {
        var result = new ImportExecutionResult();
        var existingBusinesses = await _repository.GetAllAsync(ct: ct);
        var existingMap = existingBusinesses.ToDictionary(b => b.Id);

        var toAdd = new List<Business>();
        var toUpdate = new List<Business>();

        foreach (var lead in selectedLeads)
        {
            if (!lead.IsSelectedForImport) continue;

            result.TotalProcessed++;
            var incoming = lead.Business;
            var isDup = lead.IsDuplicate;

            if (isDup && lead.DuplicateMatch?.MatchedExistingId != null &&
                existingMap.TryGetValue(lead.DuplicateMatch.MatchedExistingId, out var existing))
            {
                if (strategy == DeduplicationStrategy.Skip)
                {
                    result.SkippedCount++;
                    continue;
                }
                else if (strategy == DeduplicationStrategy.Overwrite || strategy == DeduplicationStrategy.Merge)
                {
                    _dedupService.MergeBusinesses(existing, incoming);
                    toUpdate.Add(existing);
                    result.UpdatedCount++;
                }
            }
            else
            {
                if (isDup)
                {
                    // Duplicates must never be added to database when strategy is Skip
                    result.SkippedCount++;
                    continue;
                }

                if (string.IsNullOrWhiteSpace(incoming.Id))
                {
                    incoming.Id = Guid.NewGuid().ToString();
                }
                if (string.IsNullOrWhiteSpace(incoming.PipelineStageId))
                {
                    incoming.PipelineStageId = "stage-discovered";
                }
                toAdd.Add(incoming);
                result.CreatedCount++;
            }
        }

        if (toAdd.Count > 0)
        {
            await _repository.AddBatchAsync(toAdd, ct);
        }

        if (toUpdate.Count > 0)
        {
            await _repository.UpdateBatchAsync(toUpdate, ct);
        }

        result.TotalProcessed = result.CreatedCount + result.UpdatedCount + result.SkippedCount;

        return result;
    }
}
