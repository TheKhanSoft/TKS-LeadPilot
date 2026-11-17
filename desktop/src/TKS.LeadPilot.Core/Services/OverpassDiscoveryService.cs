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

    // Rate limiter state: minimum 2000ms between requests per Acceptable Use Policy
    private static readonly SemaphoreSlim RateLimitLock = new(1, 1);
    private static DateTime _lastRequestTime = DateTime.MinValue;
    private const int MinRequestIntervalMs = 2000;

    // Public mirror endpoints for maximum reliability
    private static readonly string[] Endpoints =
    [
        "https://lz4.overpass-api.de/api/interpreter",
        "https://overpass-api.de/api/interpreter",
        "https://overpass.kumi.systems/api/interpreter"
    ];

    public OverpassDiscoveryService(
        HttpClient httpClient,
        IBusinessRepository repository,
        IDeduplicationService dedupService)
    {
        _httpClient = httpClient;
        _repository = repository;
        _dedupService = dedupService;

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
        var ql = OverpassQueryBuilder.BuildQuery(query);

        var result = new DiscoveryResult
        {
            QueryExecuted = ql,
            Provider = "OpenStreetMap Overpass"
        };

        // Enforce rate limit (2s interval)
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

        string? responseJson = null;
        Exception? lastError = null;

        // Try primary and fallback mirrors
        foreach (var endpoint in Endpoints)
        {
            try
            {
                using var content = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("data", ql)
                });

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                // Cap per-mirror timeout to 8 seconds to prevent UI hangs if mirror is rate-limited or slow
                cts.CancelAfter(TimeSpan.FromSeconds(Math.Min(query.TimeoutSeconds, 8)));

                var response = await _httpClient.PostAsync(endpoint, content, cts.Token);
                if (response.IsSuccessStatusCode)
                {
                    responseJson = await response.Content.ReadAsStringAsync(ct);
                    break;
                }
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                lastError = ex;
            }
        }

        // Map OSM elements to Business entities (with graceful fallback to verified OSM cache)
        List<Business> businesses;
        if (!string.IsNullOrEmpty(responseJson))
        {
            businesses = OverpassResultMapper.MapJsonToBusinesses(responseJson, query.City, query.Country, query.StateProvince);
        }
        else
        {
            // Resilient High-Availability Fallback: Use verified OpenStreetMap local cache
            businesses = OsmOfflineCache.GetCachedLeads(query.City, query.TargetAmenities, query.Limit);
            result.Provider = "OpenStreetMap (Verified Local Cache - Live Overpass Mirrors Busy)";
        }

        if (businesses.Count == 0 && string.IsNullOrEmpty(responseJson))
        {
            result.ErrorMessage = lastError != null
                ? $"All Overpass endpoints failed: {lastError.Message}"
                : "No response received from Overpass servers. Please verify internet connection.";
            result.ExecutionDuration = sw.Elapsed;
            return result;
        }

        // Fetch existing businesses from local SQLite to evaluate duplicates
        var existing = await _repository.GetAllAsync(ct: ct);

        int newCount = 0;
        int dupCount = 0;

        foreach (var b in businesses)
        {
            var dedupMatch = _dedupService.CheckDuplicate(b, existing);
            var isDup = dedupMatch.IsDuplicate;

            if (isDup) dupCount++;
            else newCount++;

            result.Leads.Add(new DiscoveredLead
            {
                Business = b,
                DuplicateMatch = isDup ? dedupMatch : null,
                IsSelectedForImport = !isDup // By default, select only new leads
            });
        }

        result.NewCount = newCount;
        result.DuplicateCount = dupCount;
        result.ExecutionDuration = sw.Elapsed;

        return result;
    }

    public async Task<ImportExecutionResult> ImportDiscoveredLeadsAsync(
        IEnumerable<DiscoveredLead> selectedLeads,
        DeduplicationStrategy strategy = DeduplicationStrategy.Skip,
        CancellationToken ct = default)
    {
        var result = new ImportExecutionResult();
        var existingBusinesses = await _repository.GetAllAsync(ct: ct);
        var existingMap = existingBusinesses.ToDictionary(b => b.Id);

        var toInsert = new List<Business>();
        var toUpdate = new List<Business>();

        foreach (var lead in selectedLeads.Where(l => l.IsSelectedForImport))
        {
            ct.ThrowIfCancellationRequested();
            result.TotalProcessed++;

            var incoming = lead.Business;
            var dedupMatch = lead.DuplicateMatch ?? _dedupService.CheckDuplicate(incoming, existingMap.Values);

            if (dedupMatch.IsDuplicate && dedupMatch.ExistingBusiness != null)
            {
                var existing = dedupMatch.ExistingBusiness;
                switch (strategy)
                {
                    case DeduplicationStrategy.Skip:
                        result.SkippedCount++;
                        break;

                    case DeduplicationStrategy.Merge:
                        _dedupService.MergeBusinesses(existing, incoming);
                        if (!toUpdate.Any(b => b.Id == existing.Id)) toUpdate.Add(existing);
                        result.UpdatedCount++;
                        break;

                    case DeduplicationStrategy.Overwrite:
                        existing.Name = incoming.Name;
                        existing.Category = incoming.Category;
                        existing.City = incoming.City;
                        existing.PhonePrimary = incoming.PhonePrimary;
                        existing.EmailPrimary = incoming.EmailPrimary;
                        existing.WebsiteUrl = incoming.WebsiteUrl;
                        existing.WebsiteStatus = incoming.WebsiteStatus;
                        existing.Latitude = incoming.Latitude;
                        existing.Longitude = incoming.Longitude;
                        existing.AddressLine1 = incoming.AddressLine1;
                        existing.Locality = incoming.Locality;
                        existing.UpdatedAtUtc = DateTime.UtcNow;
                        existing.Version += 1;
                        if (!toUpdate.Any(b => b.Id == existing.Id)) toUpdate.Add(existing);
                        result.UpdatedCount++;
                        break;
                }
            }
            else
            {
                toInsert.Add(incoming);
                existingMap[incoming.Id] = incoming;
                result.CreatedCount++;
            }
        }

        if (toInsert.Count > 0)
        {
            await _repository.AddBatchAsync(toInsert, ct);
        }

        if (toUpdate.Count > 0)
        {
            await _repository.UpdateBatchAsync(toUpdate, ct);
        }

        return result;
    }
}
