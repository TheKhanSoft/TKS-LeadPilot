using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
        var criteria = new BusinessFilterCriteria
        {
            SearchQuery = searchQuery,
            City = city,
            Category = category,
            PipelineStageId = stageId,
            Take = 500
        };

        return await GetFilteredAsync(criteria, ct);
    }

    public async Task<List<Business>> GetFilteredAsync(BusinessFilterCriteria criteria, CancellationToken ct = default)
    {
        var query = BuildFilteredQuery(criteria);

        // Sorting
        query = criteria.SortBy switch
        {
            BusinessSortOption.ScoreDescending => query.OrderByDescending(b => b.LeadScore).ThenByDescending(b => b.CreatedAtUtc),
            BusinessSortOption.ScoreAscending => query.OrderBy(b => b.LeadScore).ThenByDescending(b => b.CreatedAtUtc),
            BusinessSortOption.NameAscending => query.OrderBy(b => b.Name),
            BusinessSortOption.NameDescending => query.OrderByDescending(b => b.Name),
            BusinessSortOption.CreatedDescending => query.OrderByDescending(b => b.CreatedAtUtc),
            BusinessSortOption.UpdatedDescending => query.OrderByDescending(b => b.UpdatedAtUtc),
            _ => query.OrderByDescending(b => b.LeadScore).ThenByDescending(b => b.CreatedAtUtc)
        };

        if (criteria.Skip > 0)
        {
            query = query.Skip(criteria.Skip);
        }

        if (criteria.Take > 0)
        {
            query = query.Take(criteria.Take);
        }

        return await query.ToListAsync(ct);
    }

    public async Task<int> GetFilteredCountAsync(BusinessFilterCriteria criteria, CancellationToken ct = default)
    {
        var query = BuildFilteredQuery(criteria);
        return await query.CountAsync(ct);
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

    public async Task AddBatchAsync(IEnumerable<Business> businesses, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        foreach (var b in businesses)
        {
            b.CreatedAtUtc = now;
            b.UpdatedAtUtc = now;
        }
        _context.Businesses.AddRange(businesses);
        await _context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Business business, CancellationToken ct = default)
    {
        business.UpdatedAtUtc = DateTime.UtcNow;
        business.Version += 1;

        var tracked = _context.ChangeTracker.Entries<Business>().FirstOrDefault(e => e.Entity.Id == business.Id);
        if (tracked != null)
        {
            if (tracked.Entity != business)
            {
                tracked.CurrentValues.SetValues(business);
            }
        }
        else
        {
            _context.Businesses.Update(business);
        }

        await _context.SaveChangesAsync(ct);
    }

    public async Task UpdateBatchAsync(IEnumerable<Business> businesses, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        foreach (var b in businesses)
        {
            b.UpdatedAtUtc = now;
            b.Version += 1;

            var tracked = _context.ChangeTracker.Entries<Business>().FirstOrDefault(e => e.Entity.Id == b.Id);
            if (tracked != null)
            {
                if (tracked.Entity != b)
                {
                    tracked.CurrentValues.SetValues(b);
                }
            }
            else
            {
                _context.Businesses.Update(b);
            }
        }
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

    public async Task<List<string>> GetDistinctCitiesAsync(CancellationToken ct = default)
    {
        return await _context.Businesses
            .Where(b => !b.IsDeleted && !string.IsNullOrWhiteSpace(b.City))
            .Select(b => b.City)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync(ct);
    }

    public async Task<List<string>> GetDistinctCategoriesAsync(CancellationToken ct = default)
    {
        return await _context.Businesses
            .Where(b => !b.IsDeleted && !string.IsNullOrWhiteSpace(b.Category))
            .Select(b => b.Category)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync(ct);
    }

    public async Task<List<string>> GetDistinctTagsAsync(CancellationToken ct = default)
    {
        var allTagStrings = await _context.Businesses
            .Where(b => !b.IsDeleted && !string.IsNullOrWhiteSpace(b.Tags))
            .Select(b => b.Tags!)
            .ToListAsync(ct);

        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var ts in allTagStrings)
        {
            var parts = ts.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var p in parts)
            {
                set.Add(p);
            }
        }

        return set.OrderBy(t => t).ToList();
    }

    public async Task<List<Business>> FindPotentialDuplicatesAsync(string? domain, string? phone, string? name, string? city, CancellationToken ct = default)
    {
        var query = _context.Businesses
            .Include(b => b.Contacts)
            .Where(b => !b.IsDeleted)
            .AsNoTracking();

        var conditions = new List<System.Linq.Expressions.Expression<Func<Business, bool>>>();

        if (!string.IsNullOrWhiteSpace(domain))
        {
            var d = domain.Trim().ToLowerInvariant();
            query = query.Where(b => b.WebsiteUrl != null && b.WebsiteUrl.ToLower().Contains(d));
        }
        else if (!string.IsNullOrWhiteSpace(phone))
        {
            var p = phone.Trim();
            query = query.Where(b => b.PhonePrimary != null && b.PhonePrimary.Contains(p));
        }
        else if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(city))
        {
            var n = name.Trim().ToLowerInvariant();
            var c = city.Trim().ToLowerInvariant();
            query = query.Where(b => b.City.ToLower() == c && b.Name.ToLower().Contains(n));
        }
        else
        {
            return [];
        }

        return await query.Take(20).ToListAsync(ct);
    }

    // Contact Sub-resources
    public async Task AddContactAsync(BusinessContact contact, CancellationToken ct = default)
    {
        contact.CreatedAtUtc = DateTime.UtcNow;
        contact.UpdatedAtUtc = DateTime.UtcNow;
        _context.BusinessContacts.Add(contact);
        await _context.SaveChangesAsync(ct);
    }

    public async Task UpdateContactAsync(BusinessContact contact, CancellationToken ct = default)
    {
        contact.UpdatedAtUtc = DateTime.UtcNow;
        _context.BusinessContacts.Update(contact);
        await _context.SaveChangesAsync(ct);
    }

    public async Task DeleteContactAsync(string contactId, CancellationToken ct = default)
    {
        var contact = await _context.BusinessContacts.FirstOrDefaultAsync(c => c.Id == contactId, ct);
        if (contact != null)
        {
            _context.BusinessContacts.Remove(contact);
            await _context.SaveChangesAsync(ct);
        }
    }

    private IQueryable<Business> BuildFilteredQuery(BusinessFilterCriteria criteria)
    {
        var query = _context.Businesses
            .Include(b => b.PipelineStage)
            .Include(b => b.Contacts)
            .Where(b => !b.IsDeleted)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(criteria.SearchQuery))
        {
            var search = criteria.SearchQuery.Trim().ToLower();
            query = query.Where(b =>
                b.Name.ToLower().Contains(search) ||
                b.City.ToLower().Contains(search) ||
                (b.Locality != null && b.Locality.ToLower().Contains(search)) ||
                b.Category.ToLower().Contains(search) ||
                (b.WebsiteUrl != null && b.WebsiteUrl.ToLower().Contains(search)) ||
                (b.PhonePrimary != null && b.PhonePrimary.ToLower().Contains(search)) ||
                (b.EmailPrimary != null && b.EmailPrimary.ToLower().Contains(search)) ||
                (b.Tags != null && b.Tags.ToLower().Contains(search)));
        }

        if (!string.IsNullOrWhiteSpace(criteria.City))
        {
            query = query.Where(b => b.City == criteria.City);
        }

        if (!string.IsNullOrWhiteSpace(criteria.Category))
        {
            query = query.Where(b => b.Category == criteria.Category);
        }

        if (!string.IsNullOrWhiteSpace(criteria.PipelineStageId))
        {
            query = query.Where(b => b.PipelineStageId == criteria.PipelineStageId);
        }

        if (criteria.HasWebsite.HasValue)
        {
            if (criteria.HasWebsite.Value)
            {
                query = query.Where(b => !string.IsNullOrWhiteSpace(b.WebsiteUrl));
            }
            else
            {
                query = query.Where(b => string.IsNullOrWhiteSpace(b.WebsiteUrl));
            }
        }

        if (criteria.MinScore.HasValue)
        {
            query = query.Where(b => b.LeadScore >= criteria.MinScore.Value);
        }

        if (criteria.MaxScore.HasValue)
        {
            query = query.Where(b => b.LeadScore <= criteria.MaxScore.Value);
        }

        if (criteria.IsStarred.HasValue)
        {
            query = query.Where(b => b.IsStarred == criteria.IsStarred.Value);
        }

        if (!string.IsNullOrWhiteSpace(criteria.Tag))
        {
            var tag = criteria.Tag.Trim().ToLower();
            query = query.Where(b => b.Tags != null && b.Tags.ToLower().Contains(tag));
        }

        return query;
    }
}
