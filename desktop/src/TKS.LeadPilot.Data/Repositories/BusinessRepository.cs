using Microsoft.EntityFrameworkCore;
using TKS.LeadPilot.Core.Interfaces;
using TKS.LeadPilot.Core.Models;

namespace TKS.LeadPilot.Data.Repositories;

public class BusinessRepository : IBusinessRepository
{
    private readonly AppDbContext _context;

    public BusinessRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<Business>> GetAllAsync(string? searchQuery = null, string? city = null, string? category = null, string? stageId = null, CancellationToken ct = default)
    {
        var query = _context.Businesses
            .Include(b => b.PipelineStage)
            .Include(b => b.Contacts)
            .Where(b => !b.IsDeleted)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(searchQuery))
        {
            var search = searchQuery.Trim().ToLower();
            query = query.Where(b => b.Name.ToLower().Contains(search) || 
                                     b.City.ToLower().Contains(search) || 
                                     (b.WebsiteUrl != null && b.WebsiteUrl.ToLower().Contains(search)));
        }

        if (!string.IsNullOrWhiteSpace(city))
        {
            query = query.Where(b => b.City == city);
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(b => b.Category == category);
        }

        if (!string.IsNullOrWhiteSpace(stageId))
        {
            query = query.Where(b => b.PipelineStageId == stageId);
        }

        return await query.OrderByDescending(b => b.LeadScore).ThenByDescending(b => b.CreatedAtUtc).ToListAsync(ct);
    }

    public async Task<Business?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        return await _context.Businesses
            .Include(b => b.PipelineStage)
            .Include(b => b.Contacts)
            .Include(b => b.Audits)
                .ThenInclude(a => a.Findings)
            .Include(b => b.Tasks)
            .Include(b => b.Notes)
            .FirstOrDefaultAsync(b => b.Id == id && !b.IsDeleted, ct);
    }

    public async Task<Business> AddAsync(Business business, CancellationToken ct = default)
    {
        business.CreatedAtUtc = DateTime.UtcNow;
        business.UpdatedAtUtc = DateTime.UtcNow;
        _context.Businesses.Add(business);
        await _context.SaveChangesAsync(ct);
        return business;
    }

    public async Task UpdateAsync(Business business, CancellationToken ct = default)
    {
        business.UpdatedAtUtc = DateTime.UtcNow;
        business.Version += 1;
        _context.Businesses.Update(business);
        await _context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        var business = await _context.Businesses.FirstOrDefaultAsync(b => b.Id == id, ct);
        if (business != null)
        {
            business.IsDeleted = true;
            business.DeletedAtUtc = DateTime.UtcNow;
            business.Version += 1;
            await _context.SaveChangesAsync(ct);
        }
    }

    public async Task<int> GetTotalCountAsync(CancellationToken ct = default)
    {
        return await _context.Businesses.CountAsync(b => !b.IsDeleted, ct);
    }

    public async Task<int> GetWithWebsiteCountAsync(CancellationToken ct = default)
    {
        return await _context.Businesses.CountAsync(b => !b.IsDeleted && !string.IsNullOrWhiteSpace(b.WebsiteUrl), ct);
    }

    public async Task<int> GetHighOpportunityCountAsync(int threshold = 70, CancellationToken ct = default)
    {
        return await _context.Businesses.CountAsync(b => !b.IsDeleted && b.LeadScore >= threshold, ct);
    }
}
