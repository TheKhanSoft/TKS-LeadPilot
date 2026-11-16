using TKS.LeadPilot.Core.Models;

namespace TKS.LeadPilot.Core.Interfaces;

public interface IBusinessRepository
{
    Task<List<Business>> GetAllAsync(string? searchQuery = null, string? city = null, string? category = null, string? stageId = null, CancellationToken ct = default);
    Task<Business?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<Business> AddAsync(Business business, CancellationToken ct = default);
    Task UpdateAsync(Business business, CancellationToken ct = default);
    Task DeleteAsync(string id, CancellationToken ct = default);
    Task<int> GetTotalCountAsync(CancellationToken ct = default);
    Task<int> GetWithWebsiteCountAsync(CancellationToken ct = default);
    Task<int> GetHighOpportunityCountAsync(int threshold = 70, CancellationToken ct = default);
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
