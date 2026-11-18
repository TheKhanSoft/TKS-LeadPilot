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

        var engine = query.SearchEngine ?? "Unified Multi-Engine Aggregator (All Active)";
        var isAggregator = engine.Contains("Aggregator", StringComparison.OrdinalIgnoreCase) ||
                           engine.Contains("All Active", StringComparison.OrdinalIgnoreCase) ||
                           engine.Contains("All Sources", StringComparison.OrdinalIgnoreCase);

        // 1. Photon Geosearch (100% Free, Komoot OpenStreetMap)
        if (isAggregator || engine.Contains("Photon", StringComparison.OrdinalIgnoreCase))
        {
            var photonLeads = await QueryPhotonAsync(query, ct);
            if (photonLeads.Count > 0)
            {
                combinedBusinesses.AddRange(photonLeads);
                activeSources.Add($"Photon OpenPOI ({photonLeads.Count})");
            }
        }

        // 2. OpenStreetMap Nominatim Live Search (100% Free)
        if (isAggregator || engine.Contains("Nominatim", StringComparison.OrdinalIgnoreCase))
        {
            var nomLeads = await QueryNominatimAsync(query, ct);
            if (nomLeads.Count > 0)
            {
                combinedBusinesses.AddRange(nomLeads);
                activeSources.Add($"OSM Nominatim ({nomLeads.Count})");
            }
        }

        // 3. OpenStreetMap Overpass Live Query (100% Free)
        if (isAggregator || engine.Contains("Overpass", StringComparison.OrdinalIgnoreCase) || engine.Contains("Geospatial", StringComparison.OrdinalIgnoreCase))
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

        // 4. DuckDuckGo Commercial Web Directory (100% Free)
        if (isAggregator || engine.Contains("DuckDuckGo", StringComparison.OrdinalIgnoreCase))
        {
            var ddgLeads = await QueryDuckDuckGoAsync(query, ct);
            if (ddgLeads.Count > 0)
            {
                combinedBusinesses.AddRange(ddgLeads);
                activeSources.Add($"DuckDuckGo Web ({ddgLeads.Count})");
            }
        }

        // 5. Wikidata Global Commercial Graph (100% Free - Verified Entities)
        if (isAggregator || engine.Contains("Wikidata", StringComparison.OrdinalIgnoreCase))
        {
            var wikiLeads = await QueryWikidataAsync(query, ct);
            if (wikiLeads.Count > 0)
            {
                combinedBusinesses.AddRange(wikiLeads);
                activeSources.Add($"Wikidata Global ({wikiLeads.Count})");
            }
        }

        // 6. Brave Search Local & Web Discovery (Free Tier - 2,000 Queries)
        if (isAggregator || engine.Contains("Brave", StringComparison.OrdinalIgnoreCase))
        {
            var braveLeads = await QueryBraveSearchAsync(query, ct);
            if (braveLeads.Count > 0)
            {
                combinedBusinesses.AddRange(braveLeads);
                activeSources.Add($"Brave Search ({braveLeads.Count})");
            }
        }

        // 7. Groq Cloud AI Engine (100% Free Fast Llama-3 via console.groq.com)
        if (isAggregator || engine.Contains("Groq", StringComparison.OrdinalIgnoreCase))
        {
            var groqLeads = await QueryGroqAiAsync(query, ct);
            if (groqLeads.Count > 0)
            {
                combinedBusinesses.AddRange(groqLeads);
                activeSources.Add($"Groq AI ({groqLeads.Count})");
            }
        }

        // 8. Serper.dev Google Places (2,500 Free Queries via serper.dev)
        if (isAggregator || engine.Contains("Serper", StringComparison.OrdinalIgnoreCase))
        {
            var serperLeads = await QuerySerperPlacesAsync(query, ct);
            if (serperLeads.Count > 0)
            {
                combinedBusinesses.AddRange(serperLeads);
                activeSources.Add($"Serper Google ({serperLeads.Count})");
            }
        }

        // 9. Google Places / Google My Business Discovery ($200 Monthly Free Credit)
        if (isAggregator || engine.Contains("Google Places", StringComparison.OrdinalIgnoreCase))
        {
            var gmbLeads = await QueryGooglePlacesAsync(query, ct);
            if (gmbLeads.Count > 0)
            {
                combinedBusinesses.AddRange(gmbLeads);
                activeSources.Add($"Google Places ({gmbLeads.Count})");
            }
        }

        // 10. Google Gemini Free Tier AI Discovery (1,500 req/day free)
        if (isAggregator || engine.Contains("Gemini", StringComparison.OrdinalIgnoreCase))
        {
            var aiLeads = await QueryGeminiAiAsync(query, ct);
            if (aiLeads.Count > 0)
            {
                combinedBusinesses.AddRange(aiLeads);
                activeSources.Add($"Gemini AI ({aiLeads.Count})");
            }
        }

        // 11. High-Density Verified Local Catalog Fallback (or explicit offline choice)
        if (combinedBusinesses.Count == 0 || engine.Contains("Offline", StringComparison.OrdinalIgnoreCase) || engine.Contains("Vault", StringComparison.OrdinalIgnoreCase))
        {
            var catalogLeads = OsmOfflineCache.GetCachedLeads(query.City, query.Country, query.TargetAmenities, Math.Max(query.Limit, 35));
            if (catalogLeads.Count > 0)
            {
                combinedBusinesses.AddRange(catalogLeads);
                activeSources.Add($"OpenStreetMap (Verified Local Cache / Offline Vault) ({catalogLeads.Count})");
            }
        }

        if (combinedBusinesses.Count == 0)
        {
            result.ErrorMessage = $"No matching businesses found in {query.City}, {query.Country}. Please verify city name or select All Commercial preset.";
            result.ExecutionDuration = sw.Elapsed;
            return result;
        }

        // Deduplicate gathered leads internally (across the different engines) & apply specificity filter
        var deduplicatedLeads = new List<Business>();
        var relevanceScores = new Dictionary<string, int>();

        bool hasSpecificNiche = !string.IsNullOrWhiteSpace(query.CustomSearchTerm) ||
                                (!string.IsNullOrWhiteSpace(query.CategoryPreset) && !query.CategoryPreset.Contains("All Commercial", StringComparison.OrdinalIgnoreCase));

        foreach (var candidate in combinedBusinesses)
        {
            if (string.IsNullOrWhiteSpace(candidate.Name)) continue;

            // Apply pre-scan filter constraints if requested
            if (query.RequirePhone && string.IsNullOrWhiteSpace(candidate.PhonePrimary)) continue;
            if (query.TargetMissingWebsiteOnly && !string.IsNullOrWhiteSpace(candidate.WebsiteUrl)) continue;
            if (query.TargetMissingGmbOnly && (candidate.IsGoogleVerified || !string.IsNullOrWhiteSpace(candidate.GooglePlaceId))) continue;

            // Strict specificity filter: if searching for specific keyword or niche, discard 0-relevance entries
            var relevance = CalculateRelevanceScore(candidate, query);
            if (query.StrictKeywordFilter && hasSpecificNiche && relevance <= 0)
            {
                continue;
            }

            var dupCheck = _dedupService.CheckDuplicate(candidate, deduplicatedLeads);
            if (!dupCheck.IsDuplicate)
            {
                deduplicatedLeads.Add(candidate);
                relevanceScores[candidate.Id] = relevance;
            }
            else if (dupCheck.ExistingBusiness != null)
            {
                _dedupService.MergeBusinesses(dupCheck.ExistingBusiness, candidate);
            }
        }

        if (deduplicatedLeads.Count == 0)
        {
            result.ErrorMessage = $"No matching businesses found matching \"{query.CustomSearchTerm ?? query.CategoryPreset}\" in {query.City}, {query.Country}. Try disabling strict match or broadening search keyword.";
            result.ExecutionDuration = sw.Elapsed;
            return result;
        }

        // Sort candidates by relevance descending, placing targeted leads first
        var sortedLeads = deduplicatedLeads
            .OrderByDescending(b => relevanceScores.GetValueOrDefault(b.Id, 50))
            .ThenByDescending(b => !string.IsNullOrEmpty(b.WebsiteUrl))
            .ToList();

        // Fetch existing database records to flag already imported leads
        var existing = await _repository.GetAllAsync(ct: ct);

        int newCount = 0;
        int dupCount = 0;

        foreach (var b in sortedLeads.Take(query.Limit))
        {
            var dedupMatch = _dedupService.CheckDuplicate(b, existing);
            var isDup = dedupMatch.IsDuplicate;

            if (isDup) dupCount++;
            else newCount++;

            var score = relevanceScores.GetValueOrDefault(b.Id, 75);

            result.Leads.Add(new DiscoveredLead
            {
                Business = b,
                DuplicateMatch = isDup ? dedupMatch : null,
                IsSelectedForImport = !isDup,
                RelevanceScore = score
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
            var term = query.CustomSearchTerm.Trim();
            searchKeywords.Add(term);

            // Deep Scan multi-pass query expansion
            if (query.DeepScan)
            {
                searchKeywords.Add($"{term} company");
                searchKeywords.Add($"{term} services");
            }
        }
        else if (isAllCommercial)
        {
            searchKeywords.AddRange(["hospital", "hotel", "bank", "restaurant", "clinic", "company"]);
        }
        else if (query.CategoryPreset != null && OverpassQueryBuilder.CategoryPresets.TryGetValue(query.CategoryPreset, out var presetKeywords))
        {
            var cleanKeywords = presetKeywords
                .Where(k => !k.Equals("all_commercial", StringComparison.OrdinalIgnoreCase) && !k.Equals("all", StringComparison.OrdinalIgnoreCase))
                .Select(k => k.Replace("_", " "))
                .Take(query.DeepScan ? 6 : 4)
                .ToList();

            if (cleanKeywords.Count > 0)
            {
                searchKeywords.AddRange(cleanKeywords);
            }
            else
            {
                searchKeywords.Add(query.CategoryPreset.Replace("&", "and").Trim());
            }
        }
        else
        {
            var cleanPreset = query.CategoryPreset?.Replace("&", "and").Trim() ?? "commercial business";
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

    private async Task<List<Business>> QueryPhotonAsync(DiscoveryQuery query, CancellationToken ct)
    {
        var list = new List<Business>();
        var searchTerm = !string.IsNullOrWhiteSpace(query.CustomSearchTerm)
            ? query.CustomSearchTerm.Trim()
            : (query.CategoryPreset?.Replace("&", "and") ?? "commercial");

        var queriesToExecute = new List<string> { $"{searchTerm} {query.City}" };
        if (query.DeepScan && !string.IsNullOrWhiteSpace(query.CustomSearchTerm))
        {
            queriesToExecute.Add($"{searchTerm} company {query.City}");
            queriesToExecute.Add($"{searchTerm} clinic {query.City}");
        }

        foreach (var qText in queriesToExecute)
        {
            try
            {
                var queryString = qText;
                if (!string.IsNullOrWhiteSpace(query.Country)) queryString += $", {query.Country}";

                var url = $"https://photon.komoot.io/api/?q={Uri.EscapeDataString(queryString)}&limit=30";
                if (query.BBox != null)
                {
                    var centerLat = (query.BBox.North + query.BBox.South) / 2.0;
                    var centerLon = (query.BBox.East + query.BBox.West) / 2.0;
                    url += $"&lat={centerLat:F4}&lon={centerLon:F4}";
                }

                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.Add("User-Agent", "TKS-LeadPilot/1.0 (contact: info@thekhansoft.com)");

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(8));

                var resp = await _httpClient.SendAsync(req, cts.Token);
                if (resp.IsSuccessStatusCode)
                {
                    var json = await resp.Content.ReadAsStringAsync(ct);
                    var mapped = OverpassResultMapper.MapPhotonToBusinesses(json, query.City, query.Country, query.StateProvince);
                    list.AddRange(mapped);
                }
            }
            catch { }
        }

        return list;
    }

    private async Task<List<Business>> QueryDuckDuckGoAsync(DiscoveryQuery query, CancellationToken ct)
    {
        var list = new List<Business>();
        var searchTerm = !string.IsNullOrWhiteSpace(query.CustomSearchTerm)
            ? query.CustomSearchTerm.Trim()
            : (query.CategoryPreset?.Replace("&", "and") ?? "businesses");

        try
        {
            var queryStr = $"{searchTerm} in {query.City} {query.Country} directory";
            var url = $"https://html.duckduckgo.com/html/?q={Uri.EscapeDataString(queryStr)}";

            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(8));

            var resp = await _httpClient.SendAsync(req, cts.Token);
            if (resp.IsSuccessStatusCode)
            {
                var html = await resp.Content.ReadAsStringAsync(ct);
                var mapped = OverpassResultMapper.MapDuckDuckGoToBusinesses(html, searchTerm, query.City, query.Country, query.StateProvince);
                list.AddRange(mapped);
            }
        }
        catch { }

        return list;
    }

    private async Task<List<Business>> QueryGroqAiAsync(DiscoveryQuery query, CancellationToken ct)
    {
        var apiKey = _secretVault?.GetSecret("GROQ_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey)) return [];

        var list = new List<Business>();
        var searchTerm = !string.IsNullOrWhiteSpace(query.CustomSearchTerm)
            ? query.CustomSearchTerm.Trim()
            : (query.CategoryPreset?.Replace("&", "and") ?? "Commercial Businesses");

        try
        {
            var prompt = $$"""
            You are an enterprise business intelligence investigator for TKS LeadPilot.
            List 25 real, operating commercial businesses, clinics, companies, stores, law firms, restaurants, or service providers located in {{query.City}}, {{query.Country}} under the category '{{searchTerm}}'.
            
            Return ONLY a valid JSON array of objects with the following keys:
            [
              {
                "name": "Commercial Name",
                "category": "Standard Business Category",
                "address": "Street / Area, City",
                "phone": "Phone with country code",
                "website": "https://... or null if unknown",
                "tags": "Commercial Niche, Keywords"
              }
            ]
            """;

            var model = _secretVault?.GetSecret("GROQ_MODEL") ?? "llama-3.3-70b-versatile";

            var requestBody = new
            {
                model = model,
                messages = new[]
                {
                    new { role = "system", content = "You are a professional business directory indexing engine. Output valid JSON arrays only." },
                    new { role = "user", content = prompt }
                },
                temperature = 0.2,
                max_tokens = 2048
            };

            var jsonPayload = System.Text.Json.JsonSerializer.Serialize(requestBody);
            using var reqMsg = new HttpRequestMessage(HttpMethod.Post, "https://api.groq.com/openai/v1/chat/completions")
            {
                Content = new StringContent(jsonPayload, System.Text.Encoding.UTF8, "application/json")
            };
            reqMsg.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(10));

            var resp = await _httpClient.SendAsync(reqMsg, cts.Token);
            if (resp.IsSuccessStatusCode)
            {
                var respJson = await resp.Content.ReadAsStringAsync(ct);
                var mapped = OverpassResultMapper.MapGroqAiToBusinesses(respJson, query.City, query.Country, query.StateProvince);
                list.AddRange(mapped);
            }
        }
        catch { }

        return list;
    }

    private async Task<List<Business>> QuerySerperPlacesAsync(DiscoveryQuery query, CancellationToken ct)
    {
        var apiKey = _secretVault?.GetSecret("SERPER_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey)) return [];

        var list = new List<Business>();
        var searchTerm = !string.IsNullOrWhiteSpace(query.CustomSearchTerm)
            ? query.CustomSearchTerm.Trim()
            : (query.CategoryPreset?.Replace("&", "and") ?? "businesses");

        try
        {
            var reqObj = new
            {
                q = $"{searchTerm} in {query.City} {query.Country}",
                gl = "us",
                hl = "en"
            };

            var jsonPayload = System.Text.Json.JsonSerializer.Serialize(reqObj);
            using var reqMsg = new HttpRequestMessage(HttpMethod.Post, "https://google.serper.dev/places")
            {
                Content = new StringContent(jsonPayload, System.Text.Encoding.UTF8, "application/json")
            };
            reqMsg.Headers.Add("X-API-KEY", apiKey);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(10));

            var resp = await _httpClient.SendAsync(reqMsg, cts.Token);
            if (resp.IsSuccessStatusCode)
            {
                var respJson = await resp.Content.ReadAsStringAsync(ct);
                var mapped = OverpassResultMapper.MapSerperPlacesToBusinesses(respJson, query.City, query.Country, query.StateProvince);
                list.AddRange(mapped);
            }
        }
        catch { }

        return list;
    }

    private async Task<List<Business>> QueryWikidataAsync(DiscoveryQuery query, CancellationToken ct)
    {
        var list = new List<Business>();
        var searchTerm = !string.IsNullOrWhiteSpace(query.CustomSearchTerm)
            ? query.CustomSearchTerm.Trim()
            : (query.CategoryPreset?.Replace("&", "and") ?? "company");

        if (searchTerm.Contains("All Commercial", StringComparison.OrdinalIgnoreCase))
        {
            searchTerm = "company";
        }

        try
        {
            var searchPhrase = $"{searchTerm} {query.City}";
            var searchUrl = $"https://www.wikidata.org/w/api.php?action=query&list=search&srsearch={Uri.EscapeDataString(searchPhrase)}&format=json&srlimit=25";

            using var searchReq = new HttpRequestMessage(HttpMethod.Get, searchUrl);
            searchReq.Headers.Add("User-Agent", "TKS-LeadPilot/1.0 (contact: info@thekhansoft.com)");

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(8));

            var searchResp = await _httpClient.SendAsync(searchReq, cts.Token);
            if (!searchResp.IsSuccessStatusCode) return list;

            var searchJson = await searchResp.Content.ReadAsStringAsync(ct);
            using var searchDoc = System.Text.Json.JsonDocument.Parse(searchJson);

            var qids = new List<string>();
            if (searchDoc.RootElement.TryGetProperty("query", out var queryObj) &&
                queryObj.TryGetProperty("search", out var searchArr) &&
                searchArr.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var item in searchArr.EnumerateArray())
                {
                    if (item.TryGetProperty("title", out var titleProp))
                    {
                        var t = titleProp.GetString();
                        if (!string.IsNullOrWhiteSpace(t) && t.StartsWith("Q", StringComparison.OrdinalIgnoreCase))
                        {
                            qids.Add(t);
                        }
                    }
                }
            }

            if (qids.Count == 0) return list;

            // Fetch entity claims in one batch
            var idsParam = string.Join("|", qids.Take(25));
            var entitiesUrl = $"https://www.wikidata.org/w/api.php?action=wbgetentities&ids={idsParam}&languages=en&props=labels|descriptions|claims&format=json";

            using var entitiesReq = new HttpRequestMessage(HttpMethod.Get, entitiesUrl);
            entitiesReq.Headers.Add("User-Agent", "TKS-LeadPilot/1.0 (contact: info@thekhansoft.com)");

            using var cts2 = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts2.CancelAfter(TimeSpan.FromSeconds(8));

            var entitiesResp = await _httpClient.SendAsync(entitiesReq, cts2.Token);
            if (entitiesResp.IsSuccessStatusCode)
            {
                var entitiesJson = await entitiesResp.Content.ReadAsStringAsync(ct);
                var mapped = OverpassResultMapper.MapWikidataToBusinesses(entitiesJson, searchTerm, query.City, query.Country, query.StateProvince);
                list.AddRange(mapped);
            }
        }
        catch { }

        return list;
    }

    private async Task<List<Business>> QueryBraveSearchAsync(DiscoveryQuery query, CancellationToken ct)
    {
        var apiKey = _secretVault?.GetSecret("BRAVE_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey)) return [];

        var list = new List<Business>();
        var searchTerm = !string.IsNullOrWhiteSpace(query.CustomSearchTerm)
            ? query.CustomSearchTerm.Trim()
            : (query.CategoryPreset?.Replace("&", "and") ?? "commercial businesses");

        try
        {
            var q = $"{searchTerm} in {query.City} {query.Country} contact phone address";
            var url = $"https://api.search.brave.com/res/v1/web/search?q={Uri.EscapeDataString(q)}&count=20";

            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Add("Accept", "application/json");
            req.Headers.Add("X-Subscription-Token", apiKey);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(8));

            var resp = await _httpClient.SendAsync(req, cts.Token);
            if (resp.IsSuccessStatusCode)
            {
                var json = await resp.Content.ReadAsStringAsync(ct);
                var mapped = OverpassResultMapper.MapBraveSearchToBusinesses(json, searchTerm, query.City, query.Country, query.StateProvince);
                list.AddRange(mapped);
            }
        }
        catch { }

        return list;
    }

    public static int CalculateRelevanceScore(Business candidate, DiscoveryQuery query)
    {
        var custom = query.CustomSearchTerm?.Trim();
        var preset = query.CategoryPreset?.Trim();
        bool isAllCommercial = string.IsNullOrWhiteSpace(preset) || preset.Contains("All Commercial", StringComparison.OrdinalIgnoreCase);

        // If no custom keyword and All Commercial preset, general commercial score
        if (string.IsNullOrWhiteSpace(custom) && isAllCommercial)
        {
            return 75;
        }

        int score = 0;
        var nameLower = (candidate.Name ?? string.Empty).ToLowerInvariant();
        var catLower = (candidate.Category ?? string.Empty).ToLowerInvariant();
        var subcatLower = (candidate.Subcategory ?? string.Empty).ToLowerInvariant();
        var tagsLower = (candidate.Tags ?? string.Empty).ToLowerInvariant();
        var descLower = (candidate.Description ?? string.Empty).ToLowerInvariant();
        var urlLower = (candidate.WebsiteUrl ?? string.Empty).ToLowerInvariant();

        if (!string.IsNullOrWhiteSpace(custom))
        {
            var customLower = custom.ToLowerInvariant();

            // Direct full phrase match in name
            if (nameLower.Contains(customLower))
            {
                score += 65;
            }

            var tokens = customLower.Split([' ', ',', '-', '/', '&'], StringSplitOptions.RemoveEmptyEntries)
                .Where(t => t.Length >= 3)
                .ToList();

            foreach (var tok in tokens)
            {
                if (nameLower.Contains(tok)) score += 30;
                if (catLower.Contains(tok) || subcatLower.Contains(tok)) score += 25;
                if (tagsLower.Contains(tok) || descLower.Contains(tok)) score += 20;
                if (urlLower.Contains(tok)) score += 15;
            }
        }

        if (!isAllCommercial && !string.IsNullOrWhiteSpace(preset))
        {
            var presetLower = preset.ToLowerInvariant();
            if (catLower.Contains(presetLower) || nameLower.Contains(presetLower))
            {
                score += 40;
            }

            var presetTokens = presetLower.Split([' ', ',', '-', '/', '&', '⭐'], StringSplitOptions.RemoveEmptyEntries)
                .Where(t => t.Length >= 4 && t != "commercial" && t != "businesses" && t != "enterprises")
                .ToList();

            foreach (var ptok in presetTokens)
            {
                if (catLower.Contains(ptok) || subcatLower.Contains(ptok)) score += 25;
                if (nameLower.Contains(ptok)) score += 20;
                if (tagsLower.Contains(ptok)) score += 15;
            }
        }

        return Math.Min(100, score);
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
                    // Even if skipping field overwrites, persist newly discovered engines to existing database record so provenance is never lost
                    if (!string.IsNullOrWhiteSpace(incoming.SourceProvider))
                    {
                        var merged = _dedupService.MergeSourceEngines(existing.SourceProvider, incoming.SourceProvider);
                        if (!string.Equals(merged, existing.SourceProvider, StringComparison.OrdinalIgnoreCase))
                        {
                            existing.SourceProvider = merged;
                            existing.UpdatedAtUtc = DateTime.UtcNow;
                            toUpdate.Add(existing);
                        }
                    }
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
