using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TKS.LeadPilot.Core.Enums;
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
    Task DeleteBatchAsync(IEnumerable<string> ids, CancellationToken ct = default);
    Task DeleteAllAsync(CancellationToken ct = default);
    Task<int> GetTotalCountAsync(CancellationToken ct = default);
    Task<int> GetWithWebsiteCountAsync(CancellationToken ct = default);
    Task<int> GetHighOpportunityCountAsync(int threshold = 70, CancellationToken ct = default);
    Task<List<string>> GetDistinctCountriesAsync(CancellationToken ct = default);
    Task<List<string>> GetDistinctStatesAsync(string? country = null, CancellationToken ct = default);
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
    string MergeSourceEngines(string? existingSources, string? incomingSources);
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

public interface IGooglePlacesEnrichmentService
{
    Task<GooglePlacesConfig> GetConfigAsync(CancellationToken ct = default);
    Task SaveConfigAsync(GooglePlacesConfig config, string? apiKey = null, CancellationToken ct = default);
    Task<GooglePlacesEnrichmentResult> EnrichBusinessAsync(Business business, bool forceRefresh = false, CancellationToken ct = default);
    Task<int> GetRemainingRequestsTodayAsync(CancellationToken ct = default);
    Task<int> GetRemainingRequestsThisMonthAsync(CancellationToken ct = default);
    Task<List<Business>> DiscoverPlacesAsync(string query, string city, string country, int limit = 20, CancellationToken ct = default);
    Task<(bool IsSuccess, string Message)> TestApiConnectionAsync(CancellationToken ct = default);
    Task ResetUsageCountersAsync(CancellationToken ct = default);
}

public interface IAiProposalSynthesizer
{
    Task<GeminiConfig> GetConfigAsync(CancellationToken ct = default);
    Task SaveConfigAsync(GeminiConfig config, string? apiKey = null, CancellationToken ct = default);
    Task<AuditProposalSynthesisResult> SynthesizeProposalAsync(AuditProposalSynthesisRequest request, CancellationToken ct = default);
    AuditProposalSynthesisResult SynthesizeDeterministicProposal(AuditProposalSynthesisRequest request);
    Task<int> GetRemainingRequestsTodayAsync(CancellationToken ct = default);
    Task<List<Business>> DiscoverBusinessesAiAsync(string city, string country, string category, int count = 25, CancellationToken ct = default);
    Task<(bool HasGmb, double? Rating, int? ReviewCount, string? Hours)> VerifyBusinessGmbAiAsync(string businessName, string city, string country, CancellationToken ct = default);
}

public interface ILandingPageGeneratorService
{
    Task<LandingPageGenerationResult> GenerateLandingPageAsync(LandingPageGenerationRequest request, CancellationToken ct = default);
    LandingPageGenerationResult GenerateDeterministicLandingPage(LandingPageGenerationRequest request);
    IReadOnlyList<LandingPageModuleOption> GetAvailableModules();
}

public interface IAuditComparisonService
{
    AuditComparisonResult CompareAudits(WebsiteAudit baseline, WebsiteAudit target);
}

public interface ILeadScoringEngine
{
    Task<LeadScore> CalculateScoreAsync(Business business, WebsiteAudit? latestAudit = null, CancellationToken ct = default);
    Task<LeadScore> SaveScoreAsync(LeadScore score, CancellationToken ct = default);
    Task<LeadScore?> GetLatestScoreForBusinessAsync(string businessId, CancellationToken ct = default);
    Task<LeadScore> ApplyManualOverrideAsync(string businessId, int overriddenScore, string reason, CancellationToken ct = default);
}

public interface ITksServiceMatcher
{
    Task<List<TksServiceMatch>> MatchServicesAsync(Business business, WebsiteAudit? audit, LeadScore? score, CancellationToken ct = default);
}

public interface ICrmService
{
    Task<List<PipelineStage>> GetPipelineStagesAsync(CancellationToken ct = default);
    Task<Dictionary<string, List<Business>>> GetPipelineBoardAsync(CancellationToken ct = default);
    Task<bool> MoveLeadStageAsync(string businessId, string targetStageId, CancellationToken ct = default);
    
    // Tasks
    Task<List<SalesTask>> GetTasksForBusinessAsync(string businessId, CancellationToken ct = default);
    Task<List<SalesTask>> GetPendingTasksAsync(CancellationToken ct = default);
    Task<SalesTask> CreateTaskAsync(SalesTask task, CancellationToken ct = default);
    Task<bool> CompleteTaskAsync(string taskId, CancellationToken ct = default);
    Task<bool> DeleteTaskAsync(string taskId, CancellationToken ct = default);
    
    // Notes
    Task<List<SalesNote>> GetNotesForBusinessAsync(string businessId, CancellationToken ct = default);
    Task<SalesNote> AddNoteAsync(SalesNote note, CancellationToken ct = default);
    Task<bool> DeleteNoteAsync(string noteId, CancellationToken ct = default);
}

public interface IOutreachService
{
    Task<List<OutreachTemplate>> GetTemplatesAsync(OutreachChannel? channel = null, CancellationToken ct = default);
    Task<OutreachMessage> CreateDraftAsync(string businessId, string templateId, string? contactId = null, CancellationToken ct = default);
    Task<OutreachMessage> CreateCustomDraftAsync(OutreachMessage message, CancellationToken ct = default);
    Task<List<OutreachMessage>> GetApprovalQueueAsync(CancellationToken ct = default);
    Task<List<OutreachMessage>> GetMessagesForBusinessAsync(string businessId, CancellationToken ct = default);
    Task<bool> ApproveMessageAsync(string messageId, CancellationToken ct = default);
    Task<bool> RejectMessageAsync(string messageId, CancellationToken ct = default);
    Task<bool> MarkAsSentAsync(string messageId, CancellationToken ct = default);
    string RenderTemplate(string template, Business business, BusinessContact? contact, WebsiteAudit? audit, string? serviceName = null);
    string BuildMailtoUri(OutreachMessage message);
    string BuildWhatsAppUri(OutreachMessage message);
}

public interface ISyncService
{
    Task<SyncConfig> GetConfigAsync(CancellationToken ct = default);
    Task SaveConfigAsync(SyncConfig config, CancellationToken ct = default);
    Task<SyncStatusResult> GetSyncStatusAsync(CancellationToken ct = default);
    Task<SyncPushResult> PushLocalChangesAsync(CancellationToken ct = default);
    Task<SyncPullResult> PullRemoteChangesAsync(CancellationToken ct = default);
    Task<FullSyncResult> SynchronizeAsync(CancellationToken ct = default);
}

public interface IGeoLocationService
{
    IReadOnlyList<CountryInfo> GetAllCountries();
    CountryInfo? GetCountry(string countryNameOrCode);
    IReadOnlyList<StateProvinceInfo> GetStatesForCountry(string countryNameOrCode);
    IReadOnlyList<CityInfo> GetCitiesForState(string countryNameOrCode, string stateNameOrCode);
    CityInfo? FindCity(string cityName, string? country = null);
    BoundingBox CalculateProximityBoundingBox(double centerLat, double centerLon, double radiusKm);
    Task<NearbyLocationResult> DetectNearbyLocationAsync(double radiusKm = 15.0, CancellationToken ct = default);
    IReadOnlyList<string> GetSuggestedNiches(string? country = null, string? city = null);
}
