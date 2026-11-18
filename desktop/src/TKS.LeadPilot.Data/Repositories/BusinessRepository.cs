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
        if (string.IsNullOrWhiteSpace(business.PipelineStageId))
        {
            business.PipelineStageId = "stage-discovered";
        }
        business.CreatedAtUtc = DateTime.UtcNow;
        business.UpdatedAtUtc = DateTime.UtcNow;
        _context.Businesses.Add(business);
        await _context.SaveChangesAsync(ct);
        return business;
    }

    public async Task AddBatchAsync(IEnumerable<Business> businesses, CancellationToken ct = default)
    {
        var list = businesses.ToList();
        if (list.Count == 0) return;

        // Fetch existing records for duplicate safety
        var existing = await _context.Businesses
            .AsNoTracking()
            .Where(b => !b.IsDeleted)
            .Select(b => new { b.Id, b.Name, b.City, b.WebsiteUrl, b.PhonePrimary })
            .ToListAsync(ct);

        var existingDomains = new HashSet<string>(
            existing.Where(b => !string.IsNullOrWhiteSpace(b.WebsiteUrl))
                    .Select(b => CleanDomain(b.WebsiteUrl!)),
            StringComparer.OrdinalIgnoreCase);

        var existingPhones = new HashSet<string>(
            existing.Where(b => !string.IsNullOrWhiteSpace(b.PhonePrimary))
                    .Select(b => CleanPhone(b.PhonePrimary!)),
            StringComparer.OrdinalIgnoreCase);

        var existingNameCity = new HashSet<string>(
            existing.Select(b => $"{CleanName(b.Name)}|{b.City.Trim().ToLowerInvariant()}"),
            StringComparer.OrdinalIgnoreCase);

        var now = DateTime.UtcNow;
        var filteredToInsert = new List<Business>();

        foreach (var b in list)
        {
            if (string.IsNullOrWhiteSpace(b.PipelineStageId))
            {
                b.PipelineStageId = "stage-discovered";
            }

            // Check if domain duplicate
            var d = CleanDomain(b.WebsiteUrl);
            if (!string.IsNullOrEmpty(d) && existingDomains.Contains(d))
            {
                continue; // Skip duplicate domain
            }

            // Check if phone duplicate
            var p = CleanPhone(b.PhonePrimary);
            if (!string.IsNullOrEmpty(p) && p.Length >= 8 && existingPhones.Contains(p))
            {
                continue; // Skip duplicate phone
            }

            // Check if name + city duplicate
            var nc = $"{CleanName(b.Name)}|{(b.City ?? "").Trim().ToLowerInvariant()}";
            if (existingNameCity.Contains(nc))
            {
                continue; // Skip duplicate name + city
            }

            b.CreatedAtUtc = now;
            b.UpdatedAtUtc = now;
            filteredToInsert.Add(b);

            // Track within batch so duplicates within the same batch are also caught
            if (!string.IsNullOrEmpty(d)) existingDomains.Add(d);
            if (!string.IsNullOrEmpty(p)) existingPhones.Add(p);
            existingNameCity.Add(nc);
        }

        if (filteredToInsert.Count > 0)
        {
            _context.Businesses.AddRange(filteredToInsert);
            await _context.SaveChangesAsync(ct);
        }
    }

    private static string CleanDomain(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return string.Empty;
        var u = url.Trim().ToLowerInvariant();
        if (u.StartsWith("https://")) u = u[8..];
        else if (u.StartsWith("http://")) u = u[7..];
        if (u.StartsWith("www.")) u = u[4..];
        var slash = u.IndexOf('/');
        if (slash >= 0) u = u[..slash];
        return u.Trim();
    }

    private static string CleanPhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return string.Empty;
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.StartsWith("00")) digits = digits[2..];
        else if (digits.StartsWith("0") && digits.Length >= 10) digits = "92" + digits[1..];
        return digits;
    }

    private static string CleanName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return string.Empty;
        return System.Text.RegularExpressions.Regex.Replace(name.ToLowerInvariant(), @"[^\w\d]", "").Trim();
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
            var stageNav = business.PipelineStage;
            try
            {
                var trackedStage = _context.ChangeTracker.Entries<PipelineStage>().FirstOrDefault(e => e.Entity.Id == business.PipelineStageId);
                if (trackedStage != null && stageNav != null && !ReferenceEquals(trackedStage.Entity, stageNav))
                {
                    business.PipelineStage = null;
                }
                _context.Businesses.Update(business);
            }
            finally
            {
                business.PipelineStage = stageNav;
            }
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
                var stageNav = b.PipelineStage;
                try
                {
                    var trackedStage = _context.ChangeTracker.Entries<PipelineStage>().FirstOrDefault(e => e.Entity.Id == b.PipelineStageId);
                    if (trackedStage != null && stageNav != null && !ReferenceEquals(trackedStage.Entity, stageNav))
                    {
                        b.PipelineStage = null;
                    }
                    _context.Businesses.Update(b);
                }
                finally
                {
                    b.PipelineStage = stageNav;
                }
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

    public async Task DeleteBatchAsync(IEnumerable<string> ids, CancellationToken ct = default)
    {
        var idList = ids.ToList();
        if (idList.Count == 0) return;

        var businesses = await _context.Businesses
            .Where(b => idList.Contains(b.Id) && !b.IsDeleted)
            .ToListAsync(ct);

        if (businesses.Count == 0) return;

        var now = DateTime.UtcNow;
        foreach (var b in businesses)
        {
            b.IsDeleted = true;
            b.DeletedAtUtc = now;
            b.Version += 1;
        }

        await _context.SaveChangesAsync(ct);
    }

    public async Task DeleteAllAsync(CancellationToken ct = default)
    {
        var businesses = await _context.Businesses
            .Where(b => !b.IsDeleted)
            .ToListAsync(ct);

        if (businesses.Count == 0) return;

        var now = DateTime.UtcNow;
        foreach (var b in businesses)
        {
            b.IsDeleted = true;
            b.DeletedAtUtc = now;
            b.Version += 1;
        }

        await _context.SaveChangesAsync(ct);
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

    public async Task<List<string>> GetDistinctCountriesAsync(CancellationToken ct = default)
    {
        return await _context.Businesses
            .Where(b => !b.IsDeleted && !string.IsNullOrWhiteSpace(b.Country))
            .Select(b => b.Country)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync(ct);
    }

    public async Task<List<string>> GetDistinctStatesAsync(string? country = null, CancellationToken ct = default)
    {
        var query = _context.Businesses
            .Where(b => !b.IsDeleted && !string.IsNullOrWhiteSpace(b.StateProvince));

        if (!string.IsNullOrWhiteSpace(country))
        {
            query = query.Where(b => b.Country == country);
        }

        return await query
            .Select(b => b.StateProvince!)
            .Distinct()
            .OrderBy(s => s)
            .ToListAsync(ct);
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

        if (!string.IsNullOrWhiteSpace(criteria.Country))
        {
            query = query.Where(b => b.Country == criteria.Country);
        }

        if (!string.IsNullOrWhiteSpace(criteria.StateProvince))
        {
            query = query.Where(b => b.StateProvince == criteria.StateProvince);
        }

        if (!string.IsNullOrWhiteSpace(criteria.City))
        {
            var cityLower = criteria.City.Trim().ToLower();
            query = query.Where(b => b.City.ToLower() == cityLower);
        }

        if (!string.IsNullOrWhiteSpace(criteria.Category))
        {
            var catLower = criteria.Category.Trim().ToLower();

            // Broad sector & semantic preset expansion:
            if (catLower.Contains("software") || catLower.Contains(" it ") || catLower.EndsWith(" it") || catLower.StartsWith("it ") || catLower.Contains("agency") || catLower.Contains("tech") || catLower.Contains("saas"))
            {
                query = query.Where(b =>
                    b.Category.ToLower() == catLower ||
                    b.Category.ToLower().Contains("software") ||
                    b.Category.ToLower().Contains(" it") ||
                    b.Category.ToLower().Contains("it ") ||
                    b.Category.ToLower().Contains("tech") ||
                    b.Category.ToLower().Contains("saas") ||
                    b.Category.ToLower().Contains("developer") ||
                    b.Category.ToLower().Contains("computer") ||
                    b.Category.ToLower().Contains("web"));
            }
            else if (catLower.Contains("health") || catLower.Contains("dent") || catLower.Contains("clinic") || catLower.Contains("medic") || catLower.Contains("hospital") || catLower.Contains("pharm"))
            {
                query = query.Where(b =>
                    b.Category.ToLower() == catLower ||
                    b.Category.ToLower().Contains("health") ||
                    b.Category.ToLower().Contains("dent") ||
                    b.Category.ToLower().Contains("clinic") ||
                    b.Category.ToLower().Contains("medic") ||
                    b.Category.ToLower().Contains("hospital") ||
                    b.Category.ToLower().Contains("doctor") ||
                    b.Category.ToLower().Contains("pharm"));
            }
            else if (catLower.Contains("law") || catLower.Contains("legal"))
            {
                query = query.Where(b =>
                    b.Category.ToLower() == catLower ||
                    b.Category.ToLower().Contains("law") ||
                    b.Category.ToLower().Contains("legal") ||
                    b.Category.ToLower().Contains("attorney") ||
                    b.Category.ToLower().Contains("advocate") ||
                    b.Category.ToLower().Contains("notary") ||
                    b.Category.ToLower().Contains("barrister"));
            }
            else if (catLower.Contains("rest") || catLower.Contains("food") || catLower.Contains("cafe") || catLower.Contains("dining"))
            {
                query = query.Where(b =>
                    b.Category.ToLower() == catLower ||
                    b.Category.ToLower().Contains("rest") ||
                    b.Category.ToLower().Contains("food") ||
                    b.Category.ToLower().Contains("cafe") ||
                    b.Category.ToLower().Contains("dining") ||
                    b.Category.ToLower().Contains("bakery") ||
                    b.Category.ToLower().Contains("fast food"));
            }
            else if (catLower.Contains("hotel") || catLower.Contains("lodg") || catLower.Contains("hospitality"))
            {
                query = query.Where(b =>
                    b.Category.ToLower() == catLower ||
                    b.Category.ToLower().Contains("hotel") ||
                    b.Category.ToLower().Contains("lodg") ||
                    b.Category.ToLower().Contains("resort") ||
                    b.Category.ToLower().Contains("guest house") ||
                    b.Category.ToLower().Contains("motel"));
            }
            else if (catLower.Contains("estate") || catLower.Contains("propert") || catLower.Contains("builder"))
            {
                query = query.Where(b =>
                    b.Category.ToLower() == catLower ||
                    b.Category.ToLower().Contains("estate") ||
                    b.Category.ToLower().Contains("propert") ||
                    b.Category.ToLower().Contains("builder") ||
                    b.Category.ToLower().Contains("housing") ||
                    b.Category.ToLower().Contains("developer"));
            }
            else if (catLower.Contains("auto") || catLower.Contains("repair") || catLower.Contains("car") || catLower.Contains("mechanic"))
            {
                query = query.Where(b =>
                    b.Category.ToLower() == catLower ||
                    b.Category.ToLower().Contains("auto") ||
                    b.Category.ToLower().Contains("car") ||
                    b.Category.ToLower().Contains("repair") ||
                    b.Category.ToLower().Contains("mechanic") ||
                    b.Category.ToLower().Contains("dealership"));
            }
            else if (catLower.Contains("solar") || catLower.Contains("renew") || catLower.Contains("energy"))
            {
                query = query.Where(b =>
                    b.Category.ToLower() == catLower ||
                    b.Category.ToLower().Contains("solar") ||
                    b.Category.ToLower().Contains("energy") ||
                    b.Category.ToLower().Contains("renew") ||
                    b.Category.ToLower().Contains("photovoltaic"));
            }
            else if (catLower.Contains("textile") || catLower.Contains("apparel") || catLower.Contains("fashion") || catLower.Contains("cloth") || catLower.Contains("garment"))
            {
                query = query.Where(b =>
                    b.Category.ToLower() == catLower ||
                    b.Category.ToLower().Contains("textile") ||
                    b.Category.ToLower().Contains("apparel") ||
                    b.Category.ToLower().Contains("fashion") ||
                    b.Category.ToLower().Contains("garment") ||
                    b.Category.ToLower().Contains("fabric") ||
                    b.Category.ToLower().Contains("tailor"));
            }
            else
            {
                // General flexible category match
                query = query.Where(b =>
                    b.Category.ToLower() == catLower ||
                    b.Category.ToLower().Contains(catLower) ||
                    catLower.Contains(b.Category.ToLower()));
            }
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

        if (criteria.IsGoogleVerified.HasValue)
        {
            query = query.Where(b => b.IsGoogleVerified == criteria.IsGoogleVerified.Value);
        }

        if (criteria.MinRating.HasValue)
        {
            query = query.Where(b => b.GoogleRating != null && b.GoogleRating >= criteria.MinRating.Value);
        }

        if (criteria.MinScore.HasValue)
        {
            query = query.Where(b => b.LeadScore >= criteria.MinScore.Value);
        }

        if (criteria.MaxScore.HasValue)
        {
            query = query.Where(b => b.LeadScore <= criteria.MaxScore.Value);
        }

        if (criteria.HasPhone.HasValue)
        {
            if (criteria.HasPhone.Value)
            {
                query = query.Where(b => !string.IsNullOrWhiteSpace(b.PhonePrimary));
            }
            else
            {
                query = query.Where(b => string.IsNullOrWhiteSpace(b.PhonePrimary));
            }
        }

        if (criteria.HasEmail.HasValue)
        {
            if (criteria.HasEmail.Value)
            {
                query = query.Where(b => !string.IsNullOrWhiteSpace(b.EmailPrimary));
            }
            else
            {
                query = query.Where(b => string.IsNullOrWhiteSpace(b.EmailPrimary));
            }
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

        // Quick Filter Preset Processing
        switch (criteria.QuickFilter)
        {
            case QuickFilterType.HotLeads:
                query = query.Where(b => b.LeadScore >= 70);
                break;
            case QuickFilterType.NoWebsite:
                query = query.Where(b => string.IsNullOrWhiteSpace(b.WebsiteUrl));
                break;
            case QuickFilterType.GoogleVerified:
                query = query.Where(b => b.IsGoogleVerified);
                break;
            case QuickFilterType.TopRated:
                query = query.Where(b => b.GoogleRating != null && b.GoogleRating >= 4.5);
                break;
            case QuickFilterType.NeedsAudit:
                query = query.Where(b => !string.IsNullOrWhiteSpace(b.WebsiteUrl) && !b.Audits.Any());
                break;
            case QuickFilterType.MissingGmb:
                query = query.Where(b => !b.IsGoogleVerified && (b.GooglePlaceId == null || b.GooglePlaceId == ""));
                break;
            case QuickFilterType.StarredOnly:
                query = query.Where(b => b.IsStarred);
                break;
            case QuickFilterType.HasPhone:
                query = query.Where(b => !string.IsNullOrWhiteSpace(b.PhonePrimary));
                break;
            case QuickFilterType.MissingContact:
                query = query.Where(b => string.IsNullOrWhiteSpace(b.PhonePrimary) && string.IsNullOrWhiteSpace(b.EmailPrimary));
                break;
        }

        return query;
    }
}
