using Microsoft.EntityFrameworkCore;
using TKS.LeadPilot.Core.Interfaces;
using TKS.LeadPilot.Core.Models;

namespace TKS.LeadPilot.Data.Repositories;

public class PipelineStageRepository : IPipelineStageRepository
{
    private readonly AppDbContext _context;

    public PipelineStageRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<PipelineStage>> GetAllAsync(CancellationToken ct = default)
    {
        return await _context.PipelineStages.OrderBy(s => s.DisplayOrder).ToListAsync(ct);
    }

    public async Task<PipelineStage?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        return await _context.PipelineStages.FirstOrDefaultAsync(s => s.Id == id, ct);
    }

    public async Task<PipelineStage?> GetDefaultStageAsync(CancellationToken ct = default)
    {
        return await _context.PipelineStages.OrderBy(s => s.DisplayOrder).FirstOrDefaultAsync(ct);
    }
}
