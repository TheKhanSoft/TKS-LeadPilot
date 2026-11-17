using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TKS.LeadPilot.Core.Models;

namespace TKS.LeadPilot.Core.Interfaces;

public interface IBusinessRepository
{
    Task<List<Business>> GetAllAsync(string? searchQuery = null, string? city = null, string? category = null, string? stageId = null, CancellationToken ct = default);
    Task<List<Business>> GetFilteredAsync(BusinessFilterCriteria criteria, CancellationToken ct = default);
    Task<int> GetFilteredCountAsync(BusinessFilterCriteria criteria, CancellationToken ct = default);
    Task<Business?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<Business> AddAsync(Business business, CancellationToken ct = default);
    Task AddBatchAsync(IEnumerable<Business> businesses, CancellationToken ct = default);
    Task UpdateAsync(Business business, CancellationToken ct = default);
    Task UpdateBatchAsync(IEnumerable<Business> businesses, CancellationToken ct = default);
    Task DeleteAsync(string id, CancellationToken ct = default);
    Task<int> GetTotalCountAsync(CancellationToken ct = default);
    Task<int> GetWithWebsiteCountAsync(CancellationToken ct = default);
    Task<int> GetHighOpportunityCountAsync(int threshold = 70, CancellationToken ct = default);
    Task<List<string>> GetDistinctCitiesAsync(CancellationToken ct = default);
    Task<List<string>> GetDistinctCategoriesAsync(CancellationToken ct = default);
    Task<List<string>> GetDistinctTagsAsync(CancellationToken ct = default);
    Task<List<Business>> FindPotentialDuplicatesAsync(string? domain, string? phone, string? name, string? city, CancellationToken ct = default);
    
    // Contact Sub-resources
    Task AddContactAsync(BusinessContact contact, CancellationToken ct = default);
    Task UpdateContactAsync(BusinessContact contact, CancellationToken ct = default);
    Task DeleteContactAsync(string contactId, CancellationToken ct = default);
}

public interface IPipelineStageRepository
{
    Task<List<PipelineStage>> GetAllAsync(CancellationToken ct = default);
    Task<PipelineStage?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<PipelineStage?> GetDefaultStageAsync(CancellationToken ct = default);
}

public interface ISecretVault
{
    void SaveSecret(string key, string secret);
    string? GetSecret(string key);
    void DeleteSecret(string key);
    bool HasSecret(string key);
}

public interface IDatabaseInitializer
{
    Task InitializeAsync(CancellationToken ct = default);
}

public interface IDeduplicationService
{
    string NormalizeDomain(string? url);
    string NormalizePhone(string? phone);
    string NormalizeName(string? name);
    DuplicateMatchResult CheckDuplicate(Business incoming, IEnumerable<Business> existing, DeduplicationOptions? options = null);
    List<DuplicateMatchResult> CheckDuplicatesBatch(IEnumerable<Business> incomingBatch, IEnumerable<Business> existingDatabase, DeduplicationOptions? options = null);
    void MergeBusinesses(Business targetExisting, Business sourceIncoming);
}

public interface ICsvImporterService
{
    CsvColumnMapping AutoDetectMapping(IReadOnlyList<string> headers);
    Task<ImportPreviewResult> PreviewCsvAsync(Stream stream, DeduplicationOptions? dedupOptions = null, CancellationToken ct = default);
    Task<ImportExecutionResult> ExecuteImportAsync(Stream stream, CsvColumnMapping? mapping = null, DeduplicationStrategy strategy = DeduplicationStrategy.Skip, DeduplicationOptions? dedupOptions = null, CancellationToken ct = default);
    Task<byte[]> ExportToCsvAsync(IEnumerable<Business> businesses, CancellationToken ct = default);
}

public interface IDiscoveryService
{
    Task<DiscoveryResult> DiscoverLeadsAsync(DiscoveryQuery query, CancellationToken ct = default);
    Task<ImportExecutionResult> ImportDiscoveredLeadsAsync(IEnumerable<DiscoveredLead> selectedLeads, DeduplicationStrategy strategy = DeduplicationStrategy.Skip, CancellationToken ct = default);
    IReadOnlyList<string> GetSupportedCities();
    IReadOnlyDictionary<string, List<string>> GetPresetCategories();
}

public interface IWebsiteAuditEngine
{
    Task<WebsiteAudit> RunAuditAsync(string businessId, string targetUrl, CancellationToken ct = default);
    Task<List<WebsiteAudit>> GetAuditsForBusinessAsync(string businessId, CancellationToken ct = default);
}

public interface IAuditRepository
{
    Task<WebsiteAudit> SaveAuditAsync(WebsiteAudit audit, CancellationToken ct = default);
    Task<WebsiteAudit?> GetLatestAuditForBusinessAsync(string businessId, CancellationToken ct = default);
    Task<List<WebsiteAudit>> GetAllAuditsForBusinessAsync(string businessId, CancellationToken ct = default);
    Task<List<WebsiteAudit>> GetRecentAuditsAsync(int take = 50, CancellationToken ct = default);
    Task<int> GetTotalAuditsCountAsync(CancellationToken ct = default);
}
