using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using TKS.LeadPilot.Core.Interfaces;
using TKS.LeadPilot.Core.Models;

namespace TKS.LeadPilot.Core.Services;

public class ContactEnrichmentService : IContactEnrichmentService
{
    private readonly HttpClient _httpClient;
    private readonly ISsrfValidator _ssrfValidator;
    private readonly IBusinessRepository _repository;

    private static readonly Regex EmailRegex = new(
        @"\b[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex WaMeRegex = new(
        @"https?://(?:api\.)?whatsapp\.com/send\?(?:[^""'>]*&)?phone=([0-9\+]+)|https?://wa\.me/([0-9\+]+)|whatsapp://send\?phone=([0-9\+]+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex TelRegex = new(
        @"href=[""']tel:([0-9\+\-\s\(\)]{7,20})[""']",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly string[] ExcludedEmailExtensions =
    [
        ".png", ".jpg", ".jpeg", ".gif", ".svg", ".webp", ".bmp", ".ico",
        ".js", ".css", ".woff", ".woff2", ".ttf", ".eot", ".mp4", ".pdf"
    ];

    private static readonly string[] DummyEmailPatterns =
    [
        "example.com", "domain.com", "email.com", "test.com", "yoursite.com",
        "yourdomain.com", "sentry.io", "wix.com", "wordpress.org", "w3.org",
        "schema.org", "github.com", "cloudflare.com", "bootstrap.com"
    ];

    public ContactEnrichmentService(
        HttpClient httpClient,
        ISsrfValidator ssrfValidator,
        IBusinessRepository repository)
    {
        _httpClient = httpClient;
        _ssrfValidator = ssrfValidator;
        _repository = repository;
    }

    public string? CleanPhoneNumber(string? phone, string? country = null)
    {
        if (string.IsNullOrWhiteSpace(phone)) return null;

        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (string.IsNullOrEmpty(digits)) return null;

        var isPakistan = country == null || country.Contains("Pakistan", StringComparison.OrdinalIgnoreCase);

        // Pakistani mobile formats (0300 1234567 -> 923001234567)
        if (isPakistan)
        {
            if (digits.StartsWith("03") && digits.Length == 11)
            {
                return "92" + digits[1..];
            }
            if (digits.StartsWith("3") && digits.Length == 10)
            {
                return "92" + digits;
            }
        }

        // US/Canada 10-digit formats (518 555 0123 -> 15185550123)
        if (digits.Length == 10 && (country?.Contains("United States", StringComparison.OrdinalIgnoreCase) ?? false))
        {
            return "1" + digits;
        }

        return digits.Length >= 7 ? digits : null;
    }

    public string? FormatWhatsAppUrl(string? phone, string? country = null)
    {
        var clean = CleanPhoneNumber(phone, country);
        return !string.IsNullOrWhiteSpace(clean) ? $"https://wa.me/{clean}" : null;
    }

    public async Task<ContactEnrichmentResult> EnrichBusinessContactsAsync(Business business, CancellationToken ct = default)
    {
        var result = new ContactEnrichmentResult
        {
            BusinessId = business.Id,
            BusinessName = business.Name,
            IsSuccess = true
        };

        bool hasNewData = false;

        // 1. WhatsApp normalization from existing phone
        if (!string.IsNullOrWhiteSpace(business.PhonePrimary))
        {
            var clean = CleanPhoneNumber(business.PhonePrimary, business.Country);
            if (!string.IsNullOrWhiteSpace(clean))
            {
                if (string.IsNullOrWhiteSpace(business.WhatsAppNumber))
                {
                    business.WhatsAppNumber = clean;
                    hasNewData = true;
                }
                result.WhatsAppNumber = business.WhatsAppNumber;
                result.WhatsAppUrl = business.WhatsAppDirectUrl;
            }
        }

        // 2. Website Deep Crawl for Emails, WhatsApp, Phones, and Socials
        if (!string.IsNullOrWhiteSpace(business.WebsiteUrl))
        {
            var crawled = await CrawlWebsiteForContactsAsync(business.WebsiteUrl, ct);
            if (crawled.Emails.Count > 0)
            {
                result.DiscoveredEmails.AddRange(crawled.Emails);
                if (string.IsNullOrWhiteSpace(business.EmailPrimary))
                {
                    business.EmailPrimary = crawled.Emails[0];
                    hasNewData = true;
                }
            }

            if (!string.IsNullOrWhiteSpace(crawled.WhatsAppNumber))
            {
                result.WhatsAppNumber = crawled.WhatsAppNumber;
                result.WhatsAppUrl = FormatWhatsAppUrl(crawled.WhatsAppNumber, business.Country);
                if (string.IsNullOrWhiteSpace(business.WhatsAppNumber))
                {
                    business.WhatsAppNumber = crawled.WhatsAppNumber;
                    hasNewData = true;
                }
            }

            if (crawled.Phones.Count > 0)
            {
                result.DiscoveredPhones.AddRange(crawled.Phones);
                if (string.IsNullOrWhiteSpace(business.PhonePrimary))
                {
                    business.PhonePrimary = crawled.Phones[0];
                    hasNewData = true;
                }
            }

            foreach (var kvp in crawled.SocialLinks)
            {
                result.SocialLinks[kvp.Key] = kvp.Value;
            }

            result.MethodUsed = "Website Deep Crawl";
        }
        // 3. Fallback Web Search Discovery if no website or email
        else if (string.IsNullOrWhiteSpace(business.EmailPrimary))
        {
            var searchResults = await SearchWebForContactsAsync(business.Name, business.City, business.Country, ct);
            if (searchResults.Emails.Count > 0)
            {
                result.DiscoveredEmails.AddRange(searchResults.Emails);
                if (string.IsNullOrWhiteSpace(business.EmailPrimary))
                {
                    business.EmailPrimary = searchResults.Emails[0];
                    hasNewData = true;
                }
            }

            if (searchResults.Phones.Count > 0 && string.IsNullOrWhiteSpace(business.PhonePrimary))
            {
                business.PhonePrimary = searchResults.Phones[0];
                hasNewData = true;
            }

            result.MethodUsed = "Web Search Engine";
        }

        // 4. Auto-create primary BusinessContact entry if contact was found
        if (!string.IsNullOrWhiteSpace(business.EmailPrimary) || !string.IsNullOrWhiteSpace(business.WhatsAppNumber))
        {
            var existingContact = business.Contacts.FirstOrDefault(c => c.IsPrimary) 
                               ?? business.Contacts.FirstOrDefault();

            if (existingContact == null)
            {
                business.Contacts.Add(new BusinessContact
                {
                    Id = Guid.NewGuid().ToString(),
                    BusinessId = business.Id,
                    FullName = $"{business.Name} Commercial Contact",
                    JobTitle = "Inquiry & Sales Desk",
                    Email = business.EmailPrimary,
                    Phone = business.PhonePrimary,
                    WhatsAppNumber = business.WhatsAppNumber,
                    IsPrimary = true,
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                });
                hasNewData = true;
            }
            else
            {
                if (string.IsNullOrWhiteSpace(existingContact.Email) && !string.IsNullOrWhiteSpace(business.EmailPrimary))
                {
                    existingContact.Email = business.EmailPrimary;
                    hasNewData = true;
                }
                if (string.IsNullOrWhiteSpace(existingContact.WhatsAppNumber) && !string.IsNullOrWhiteSpace(business.WhatsAppNumber))
                {
                    existingContact.WhatsAppNumber = business.WhatsAppNumber;
                    hasNewData = true;
                }
            }
        }

        result.HasNewData = hasNewData;
        result.Email = business.EmailPrimary;
        result.Phone = business.PhonePrimary;
        result.WhatsAppNumber = business.WhatsAppNumber;
        result.WhatsAppUrl = business.WhatsAppDirectUrl;

        business.LastEnrichedAtUtc = DateTime.UtcNow;
        business.UpdatedAtUtc = DateTime.UtcNow;
        business.NotifyContactChanged();

        try
        {
            await _repository.UpdateAsync(business, ct);
        }
        catch { }

        return result;
    }

    public async Task<int> BatchEnrichContactsAsync(
        IEnumerable<Business> businesses, 
        Action<int, int>? progressCallback = null, 
        CancellationToken ct = default)
    {
        var list = businesses.ToList();
        if (list.Count == 0) return 0;

        int enrichedCount = 0;
        int total = list.Count;

        // Process in bounded concurrent batches of 4 to avoid overwhelming local network
        using var semaphore = new SemaphoreSlim(4, 4);
        var tasks = list.Select(async (biz, idx) =>
        {
            await semaphore.WaitAsync(ct);
            try
            {
                if (ct.IsCancellationRequested) return;
                var res = await EnrichBusinessContactsAsync(biz, ct);
                if (res.HasNewData)
                {
                    Interlocked.Increment(ref enrichedCount);
                }
                progressCallback?.Invoke(idx + 1, total);
            }
            catch { }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
        return enrichedCount;
    }

    private async Task<(List<string> Emails, string? WhatsAppNumber, List<string> Phones, Dictionary<string, string> SocialLinks)> CrawlWebsiteForContactsAsync(
        string rawUrl, CancellationToken ct)
    {
        var emails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string? waNumber = null;
        var phones = new HashSet<string>();
        var socials = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var targetUrl = rawUrl.Trim();
        if (!targetUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !targetUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            targetUrl = "https://" + targetUrl;
        }

        var ssrf = await _ssrfValidator.ValidateUrlAsync(targetUrl, ct);
        if (!ssrf.IsSafe) return ([], null, [], socials);

        // Pages to inspect: Homepage, /contact, /about
        var pagesToFetch = new List<string> { targetUrl };

        try
        {
            var uri = new Uri(targetUrl);
            var baseHost = uri.GetLeftPart(UriPartial.Authority);
            pagesToFetch.Add($"{baseHost.TrimEnd('/')}/contact");
            pagesToFetch.Add($"{baseHost.TrimEnd('/')}/contact-us");
        }
        catch { }

        foreach (var pageUrl in pagesToFetch.Distinct())
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(6));

                using var req = new HttpRequestMessage(HttpMethod.Get, pageUrl);
                req.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36 TKS-LeadPilot/1.0");

                var resp = await _httpClient.SendAsync(req, cts.Token);
                if (!resp.IsSuccessStatusCode) continue;

                var html = await resp.Content.ReadAsStringAsync(ct);
                if (string.IsNullOrWhiteSpace(html)) continue;

                // Extract emails
                foreach (Match m in EmailRegex.Matches(html))
                {
                    var email = m.Value.Trim().ToLowerInvariant();
                    if (IsValidEmail(email))
                    {
                        emails.Add(email);
                    }
                }

                // Extract mailto: links specifically
                var mailtoMatches = Regex.Matches(html, @"href=[""']mailto:([^""'?]+)", RegexOptions.IgnoreCase);
                foreach (Match m in mailtoMatches)
                {
                    if (m.Groups.Count > 1)
                    {
                        var e = m.Groups[1].Value.Trim().ToLowerInvariant();
                        if (IsValidEmail(e)) emails.Add(e);
                    }
                }

                // Extract WhatsApp
                var waMatches = WaMeRegex.Matches(html);
                foreach (Match m in waMatches)
                {
                    for (int i = 1; i < m.Groups.Count; i++)
                    {
                        if (m.Groups[i].Success && !string.IsNullOrWhiteSpace(m.Groups[i].Value))
                        {
                            var num = new string(m.Groups[i].Value.Where(char.IsDigit).ToArray());
                            if (num.Length >= 8)
                            {
                                waNumber ??= num;
                                break;
                            }
                        }
                    }
                }

                // Extract Phones
                foreach (Match m in TelRegex.Matches(html))
                {
                    if (m.Groups.Count > 1)
                    {
                        var p = m.Groups[1].Value.Trim();
                        if (p.Length >= 7) phones.Add(p);
                    }
                }

                // Extract Socials
                ExtractSocialLink(html, @"https?://(?:www\.)?facebook\.com/[a-zA-Z0-9.\-_]+", "Facebook", socials);
                ExtractSocialLink(html, @"https?://(?:www\.)?instagram\.com/[a-zA-Z0-9.\-_]+", "Instagram", socials);
                ExtractSocialLink(html, @"https?://(?:www\.)?linkedin\.com/(?:company|in)/[a-zA-Z0-9.\-_]+", "LinkedIn", socials);
            }
            catch { }
        }

        return (emails.ToList(), waNumber, phones.ToList(), socials);
    }

    private async Task<(List<string> Emails, List<string> Phones)> SearchWebForContactsAsync(
        string businessName, string city, string country, CancellationToken ct)
    {
        var emails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var phones = new HashSet<string>();

        try
        {
            var query = $"\"{businessName}\" \"{city}\" contact email";
            var url = $"https://html.duckduckgo.com/html/?q={Uri.EscapeDataString(query)}";

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(5));

            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");

            var resp = await _httpClient.SendAsync(req, cts.Token);
            if (!resp.IsSuccessStatusCode) return ([], []);

            var html = await resp.Content.ReadAsStringAsync(ct);

            foreach (Match m in EmailRegex.Matches(html))
            {
                var email = m.Value.Trim().ToLowerInvariant();
                if (IsValidEmail(email) && !email.Contains("duckduckgo"))
                {
                    emails.Add(email);
                }
            }
        }
        catch { }

        return (emails.ToList(), phones.ToList());
    }

    private static bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email) || email.Length < 6 || email.Length > 80)
            return false;

        foreach (var ext in ExcludedEmailExtensions)
        {
            if (email.EndsWith(ext, StringComparison.OrdinalIgnoreCase)) return false;
        }

        foreach (var dummy in DummyEmailPatterns)
        {
            if (email.Contains(dummy, StringComparison.OrdinalIgnoreCase)) return false;
        }

        return email.Contains('@') && email.Contains('.');
    }

    private static void ExtractSocialLink(string html, string pattern, string networkName, Dictionary<string, string> dict)
    {
        if (dict.ContainsKey(networkName)) return;

        var m = Regex.Match(html, pattern, RegexOptions.IgnoreCase);
        if (m.Success)
        {
            var link = m.Value.Trim();
            if (!link.Contains("share", StringComparison.OrdinalIgnoreCase) && 
                !link.Contains("sharer", StringComparison.OrdinalIgnoreCase))
            {
                dict[networkName] = link;
            }
        }
    }
}
