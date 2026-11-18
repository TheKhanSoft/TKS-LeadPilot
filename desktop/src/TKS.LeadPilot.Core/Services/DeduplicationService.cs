using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using TKS.LeadPilot.Core.Interfaces;
using TKS.LeadPilot.Core.Models;

namespace TKS.LeadPilot.Core.Services;

public partial class DeduplicationService : IDeduplicationService
{
    private static readonly Regex DigitRegex = new(@"[^\d]", RegexOptions.Compiled);
    private static readonly Regex NoiseWordRegex = new(@"\b(pvt|ltd|limited|inc|co|company|llc|services|corp|corporation|the|and)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex MultipleSpacesRegex = new(@"\s+", RegexOptions.Compiled);

    public string NormalizeDomain(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return string.Empty;

        var clean = url.Trim().ToLowerInvariant();

        // Strip protocol
        if (clean.StartsWith("https://")) clean = clean[8..];
        else if (clean.StartsWith("http://")) clean = clean[7..];

        // Strip www.
        if (clean.StartsWith("www.")) clean = clean[4..];

        // Strip path, query params, hash
        var slashIdx = clean.IndexOf('/');
        if (slashIdx >= 0) clean = clean[..slashIdx];

        var queryIdx = clean.IndexOf('?');
        if (queryIdx >= 0) clean = clean[..queryIdx];

        var hashIdx = clean.IndexOf('#');
        if (hashIdx >= 0) clean = clean[..hashIdx];

        // Strip port if present
        var colonIdx = clean.IndexOf(':');
        if (colonIdx >= 0) clean = clean[..colonIdx];

        return clean.Trim();
    }

    public string NormalizePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return string.Empty;

        var digits = DigitRegex.Replace(phone, "");
        if (string.IsNullOrEmpty(digits)) return string.Empty;

        // Strip international exit prefix "00" (e.g. 00923001234567 -> 923001234567)
        if (digits.StartsWith("00"))
        {
            digits = digits[2..];
        }
        // If local Pakistani format starting with 0 (e.g., 03001234567, 11 digits)
        else if (digits.StartsWith("0") && digits.Length >= 10)
        {
            digits = "92" + digits[1..];
        }
        // If local 10-digit number without leading 0 (e.g., 3001234567)
        else if (digits.Length == 10 && (digits.StartsWith("3") || digits.StartsWith("2") || digits.StartsWith("4") || digits.StartsWith("5")))
        {
            digits = "92" + digits;
        }

        return digits;
    }

    public string NormalizeName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return string.Empty;

        var clean = name.ToLowerInvariant();
        clean = clean.Replace("&", " and ")
                     .Replace(".", " ")
                     .Replace(",", " ")
                     .Replace("-", " ")
                     .Replace("'", "")
                     .Replace("\"", "");

        clean = NoiseWordRegex.Replace(clean, " ");
        clean = MultipleSpacesRegex.Replace(clean, " ").Trim();

        return clean;
    }

    public DuplicateMatchResult CheckDuplicate(Business incoming, IEnumerable<Business> existing, DeduplicationOptions? options = null)
    {
        options ??= new DeduplicationOptions();

        var incomingDomain = NormalizeDomain(incoming.WebsiteUrl);
        var incomingPhone = NormalizePhone(incoming.PhonePrimary);
        var incomingName = NormalizeName(incoming.Name);
        var incomingCity = (incoming.City ?? string.Empty).Trim().ToLowerInvariant();

        DuplicateMatchResult bestMatch = new()
        {
            IncomingBusiness = incoming,
            MatchType = DuplicateMatchType.None,
            ConfidenceScore = 0
        };

        foreach (var candidate in existing)
        {
            if (candidate.Id == incoming.Id) continue;

            // 1. Exact Website Match (Confidence: 100)
            if (options.MatchWebsite && !string.IsNullOrEmpty(incomingDomain))
            {
                var candidateDomain = NormalizeDomain(candidate.WebsiteUrl);
                if (!string.IsNullOrEmpty(candidateDomain) && string.Equals(incomingDomain, candidateDomain, StringComparison.OrdinalIgnoreCase))
                {
                    return new DuplicateMatchResult
                    {
                        IncomingBusiness = incoming,
                        ExistingBusiness = candidate,
                        MatchType = DuplicateMatchType.ExactWebsite,
                        ConfidenceScore = 100,
                        MatchReason = $"Identical website domain: '{incomingDomain}'"
                    };
                }
            }

            // 2. Exact Phone Match (Confidence: 95)
            if (options.MatchPhone && !string.IsNullOrEmpty(incomingPhone) && incomingPhone.Length >= 8)
            {
                var candidatePhone = NormalizePhone(candidate.PhonePrimary);
                if (!string.IsNullOrEmpty(candidatePhone) && string.Equals(incomingPhone, candidatePhone, StringComparison.Ordinal))
                {
                    if (bestMatch.ConfidenceScore < 95)
                    {
                        bestMatch = new DuplicateMatchResult
                        {
                            IncomingBusiness = incoming,
                            ExistingBusiness = candidate,
                            MatchType = DuplicateMatchType.ExactPhone,
                            ConfidenceScore = 95,
                            MatchReason = $"Identical primary phone: '{incomingPhone}'"
                        };
                    }
                }
            }

            // 3. Name & City Match
            if (options.MatchNameAndCity && !string.IsNullOrEmpty(incomingName) && !string.IsNullOrEmpty(incomingCity))
            {
                var candidateCity = (candidate.City ?? string.Empty).Trim().ToLowerInvariant();
                if (string.Equals(incomingCity, candidateCity, StringComparison.OrdinalIgnoreCase))
                {
                    var candidateName = NormalizeName(candidate.Name);
                    if (string.Equals(incomingName, candidateName, StringComparison.OrdinalIgnoreCase))
                    {
                        if (bestMatch.ConfidenceScore < 90)
                        {
                            bestMatch = new DuplicateMatchResult
                            {
                                IncomingBusiness = incoming,
                                ExistingBusiness = candidate,
                                MatchType = DuplicateMatchType.ExactNameAndCity,
                                ConfidenceScore = 90,
                                MatchReason = $"Identical normalized name '{incomingName}' in '{candidate.City}'"
                            };
                        }
                    }
                    else
                    {
                        // Fuzzy Name Comparison
                        var similarity = CalculateSimilarity(incomingName, candidateName);
                        if (similarity >= options.FuzzyNameThreshold)
                        {
                            var score = (int)(similarity * 100);
                            if (score > bestMatch.ConfidenceScore)
                            {
                                bestMatch = new DuplicateMatchResult
                                {
                                    IncomingBusiness = incoming,
                                    ExistingBusiness = candidate,
                                    MatchType = DuplicateMatchType.FuzzyNameAndCity,
                                    ConfidenceScore = score,
                                    MatchReason = $"High name similarity ({similarity:P0}) in '{candidate.City}' ('{incoming.Name}' vs '{candidate.Name}')"
                                };
                            }
                        }
                    }
                }
            }
        }

        return bestMatch;
    }

    public List<DuplicateMatchResult> CheckDuplicatesBatch(IEnumerable<Business> incomingBatch, IEnumerable<Business> existingDatabase, DeduplicationOptions? options = null)
    {
        var existingList = existingDatabase.ToList();
        var results = new List<DuplicateMatchResult>();

        foreach (var incoming in incomingBatch)
        {
            var match = CheckDuplicate(incoming, existingList, options);
            results.Add(match);
        }

        return results;
    }

    public void MergeBusinesses(Business targetExisting, Business sourceIncoming)
    {
        if (targetExisting == null || sourceIncoming == null) return;

        // Fill missing fields
        if (string.IsNullOrWhiteSpace(targetExisting.PhonePrimary) && !string.IsNullOrWhiteSpace(sourceIncoming.PhonePrimary))
            targetExisting.PhonePrimary = sourceIncoming.PhonePrimary;

        if (string.IsNullOrWhiteSpace(targetExisting.EmailPrimary) && !string.IsNullOrWhiteSpace(sourceIncoming.EmailPrimary))
            targetExisting.EmailPrimary = sourceIncoming.EmailPrimary;

        if (string.IsNullOrWhiteSpace(targetExisting.WebsiteUrl) && !string.IsNullOrWhiteSpace(sourceIncoming.WebsiteUrl))
        {
            targetExisting.WebsiteUrl = sourceIncoming.WebsiteUrl;
            targetExisting.WebsiteStatus = sourceIncoming.WebsiteStatus;
        }

        if (string.IsNullOrWhiteSpace(targetExisting.AddressLine1) && !string.IsNullOrWhiteSpace(sourceIncoming.AddressLine1))
            targetExisting.AddressLine1 = sourceIncoming.AddressLine1;

        if (string.IsNullOrWhiteSpace(targetExisting.Locality) && !string.IsNullOrWhiteSpace(sourceIncoming.Locality))
            targetExisting.Locality = sourceIncoming.Locality;

        if (string.IsNullOrWhiteSpace(targetExisting.PostalCode) && !string.IsNullOrWhiteSpace(sourceIncoming.PostalCode))
            targetExisting.PostalCode = sourceIncoming.PostalCode;

        if (string.IsNullOrWhiteSpace(targetExisting.Description) && !string.IsNullOrWhiteSpace(sourceIncoming.Description))
            targetExisting.Description = sourceIncoming.Description;

        if (string.IsNullOrWhiteSpace(targetExisting.Subcategory) && !string.IsNullOrWhiteSpace(sourceIncoming.Subcategory))
            targetExisting.Subcategory = sourceIncoming.Subcategory;

        if (!targetExisting.Latitude.HasValue && sourceIncoming.Latitude.HasValue)
            targetExisting.Latitude = sourceIncoming.Latitude;

        if (!targetExisting.Longitude.HasValue && sourceIncoming.Longitude.HasValue)
            targetExisting.Longitude = sourceIncoming.Longitude;

        // Merge provenance & discovery engines
        if (!string.IsNullOrWhiteSpace(sourceIncoming.SourceProvider))
        {
            targetExisting.SourceProvider = MergeSourceEngines(targetExisting.SourceProvider, sourceIncoming.SourceProvider);
        }

        // Merge tags
        foreach (var tag in sourceIncoming.GetTagList())
        {
            targetExisting.AddTag(tag);
        }

        // Merge contacts
        foreach (var contact in sourceIncoming.Contacts)
        {
            var exists = targetExisting.Contacts.Any(c => 
                (!string.IsNullOrWhiteSpace(c.Email) && string.Equals(c.Email, contact.Email, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(c.Phone) && string.Equals(c.Phone, contact.Phone, StringComparison.OrdinalIgnoreCase)) ||
                string.Equals(c.FullName, contact.FullName, StringComparison.OrdinalIgnoreCase));

            if (!exists)
            {
                contact.BusinessId = targetExisting.Id;
                targetExisting.Contacts.Add(contact);
            }
        }

        targetExisting.UpdatedAtUtc = DateTime.UtcNow;
        targetExisting.Version += 1;
    }

    private static double CalculateSimilarity(string s1, string s2)
    {
        if (string.IsNullOrEmpty(s1) && string.IsNullOrEmpty(s2)) return 1.0;
        if (string.IsNullOrEmpty(s1) || string.IsNullOrEmpty(s2)) return 0.0;
        if (string.Equals(s1, s2, StringComparison.OrdinalIgnoreCase)) return 1.0;

        // Levenshtein distance
        int n = s1.Length;
        int m = s2.Length;
        int[,] d = new int[n + 1, m + 1];

        for (int i = 0; i <= n; i++) d[i, 0] = i;
        for (int j = 0; j <= m; j++) d[0, j] = j;

        for (int i = 1; i <= n; i++)
        {
            for (int j = 1; j <= m; j++)
            {
                int cost = char.ToLowerInvariant(s1[i - 1]) == char.ToLowerInvariant(s2[j - 1]) ? 0 : 1;
                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost);
            }
        }

        int distance = d[n, m];
        int maxLength = Math.Max(n, m);
        return 1.0 - ((double)distance / maxLength);
    }

    public string MergeSourceEngines(string? existingSources, string? incomingSources)
    {
        if (string.IsNullOrWhiteSpace(existingSources)) return incomingSources?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(incomingSources)) return existingSources.Trim();

        var list = new List<string>();

        void AddTokens(string raw)
        {
            var tokens = raw.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var token in tokens)
            {
                if (!string.IsNullOrWhiteSpace(token) && !list.Any(x => string.Equals(x, token, StringComparison.OrdinalIgnoreCase)))
                {
                    list.Add(token);
                }
            }
        }

        AddTokens(existingSources);
        AddTokens(incomingSources);

        return string.Join(", ", list);
    }
}
