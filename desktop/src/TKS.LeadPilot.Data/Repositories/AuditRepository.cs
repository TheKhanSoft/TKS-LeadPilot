using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TKS.LeadPilot.Core.Interfaces;
using TKS.LeadPilot.Core.Models;

namespace TKS.LeadPilot.Data.Repositories;

public class AuditRepository : IAuditRepository
{
    private readonly AppDbContext _context;

    public AuditRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<WebsiteAudit> SaveAuditAsync(WebsiteAudit audit, CancellationToken ct = default)
    {
        audit.CreatedAtUtc = DateTime.UtcNow;
        _context.WebsiteAudits.Add(audit);

        // Also update business lead score & status if business exists
        var business = await _context.Businesses.FirstOrDefaultAsync(b => b.Id == audit.BusinessId, ct);
        if (business != null)
        {
            if (audit.OverallScore.HasValue)
            {
                // Invert website quality to compute opportunity score: lower site quality = higher opportunity score!
                // Example: website score of 30% -> opportunity score of 70-85!
                var opportunityScore = Math.Max(10, 100 - audit.OverallScore.Value);
                business.LeadScore = opportunityScore;
                business.LeadStatus = Core.Enums.LeadStatus.Qualified;
                business.PipelineStageId = "stage-qualified";
            }
            business.UpdatedAtUtc = DateTime.UtcNow;
            business.Version += 1;
        }

        await _context.SaveChangesAsync(ct);
        return audit;
    }

    public async Task<WebsiteAudit?> GetLatestAuditForBusinessAsync(string businessId, CancellationToken ct = default)
    {
        return await _context.WebsiteAudits
            .Include(a => a.Findings)
            .Include(a => a.Business)
            .Where(a => a.BusinessId == businessId)
            .OrderByDescending(a => a.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<List<WebsiteAudit>> GetAllAuditsForBusinessAsync(string businessId, CancellationToken ct = default)
    {
        return await _context.WebsiteAudits
            .Include(a => a.Findings)
            .Where(a => a.BusinessId == businessId)
            .OrderByDescending(a => a.CreatedAtUtc)
            .ToListAsync(ct);
    }

    public async Task<List<WebsiteAudit>> GetRecentAuditsAsync(int take = 50, CancellationToken ct = default)
    {
        return await _context.WebsiteAudits
            .Include(a => a.Findings)
            .Include(a => a.Business)
            .OrderByDescending(a => a.CreatedAtUtc)
            .Take(take)
            .ToListAsync(ct);
    }

    public async Task<int> GetTotalAuditsCountAsync(CancellationToken ct = default)
    {
        return await _context.WebsiteAudits.CountAsync(ct);
    }
}
