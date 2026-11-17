using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TKS.LeadPilot.Core.Models;
using TKS.LeadPilot.Core.Services;

namespace TKS.LeadPilot.Data.Services;

public class EfLeadScoreStore : LeadScoringEngine.ILeadScoreStore
{
    private readonly AppDbContext _context;

    public EfLeadScoreStore(AppDbContext context)
    {
        _context = context;
    }

    public async Task SaveAsync(LeadScore score, CancellationToken ct = default)
    {
        _context.LeadScores.Add(score);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<LeadScore?> GetLatestByBusinessIdAsync(string businessId, CancellationToken ct = default)
    {
        return await _context.LeadScores
            .Include(s => s.Components)
            .Where(s => s.BusinessId == businessId)
            .OrderByDescending(s => s.CalculatedAtUtc)
            .FirstOrDefaultAsync(ct);
    }
}
