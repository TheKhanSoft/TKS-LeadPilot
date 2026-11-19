using System;
using System.Collections.Generic;
using System.Linq;
using TKS.LeadPilot.Core.Models;

namespace TKS.LeadPilot.Core.Services;

/// <summary>
/// Centralized service to resolve and normalize business categories.
/// Ensures search keywords, niches, business names, or raw metadata descriptions
/// do not corrupt the commercial category.
/// </summary>
public static class CategoryResolver
{
    private static readonly (string[] Keywords, string Category)[] CanonicalRules =
    [
        // Software & IT
        (["software", "it agency", "it service", "it company", "tech", "saas", "code", "digital", "developer", "cloud", "ai", "cyber", "programming", "web development", "web design", "app development", "soft", "nexa"], "Software Company & IT Services"),
        
        // Healthcare & Dental
        (["clinic", "hospital", "dentist", "dental", "doctor", "health", "pharmacy", "medical", "physio", "eyecare", "optician", "laboratory", "diagnostic", "wellness"], "Healthcare & Dental"),
        
        // Legal & Corporate Law
        (["lawyer", "legal", "advocate", "attorney", "court", "chambers", "notary", "barrister", "law"], "Legal & Corporate Law"),
        
        // Solar & Renewable Energy
        (["solar", "energy", "inverter", "renewable", "photovoltaic"], "Solar & Renewable Energy"),
        
        // Restaurants & Food
        (["restaurant", "cafe", "coffee", "food", "dining", "grill", "bakery", "fast food", "pizza", "burger", "bistro", "catering"], "Restaurants & Food"),
        
        // Hotels & Lodging
        (["hotel", "motel", "resort", "lodging", "guest house", "hostel", "inn"], "Hotels & Lodging"),
        
        // Education & Training
        (["school", "college", "university", "academy", "institute", "education", "tuition", "training"], "Education & Training"),
        
        // Real Estate & Builders
        (["real estate", "property", "builder", "developer", "estate agent", "housing", "apartments"], "Real Estate & Builders"),
        
        // Automotive & Repair
        (["car", "auto", "motors", "repair", "mechanic", "dealership", "workshop", "garage", "tyres"], "Automotive & Repair"),
        
        // Fitness & Wellness
        (["fitness", "gym", "workout", "crossfit", "sports"], "Fitness Centers & Gyms"),
        
        // Beauty & Spas
        (["salon", "beauty", "spa", "barber", "parlour", "hairdresser", "makeup"], "Beauty Salons & Spas"),
        
        // Retail & Shopping
        (["supermarket", "mart", "store", "shop", "grocery", "mall", "retail", "department store"], "Retail & Shopping"),
        
        // Finance & Accounting
        (["bank", "finance", "accounting", "accountant", "audit", "tax", "insurance", "exchange"], "Finance & Accounting"),
        
        // Logistics & Warehousing
        (["courier", "logistics", "cargo", "delivery", "transport", "freight", "storage"], "Logistics & Warehousing"),
        
        // Manufacturing & Industrial
        (["industrial", "factory", "manufacturing", "mill", "steel"], "Manufacturing & Industrial"),
        
        // Architecture & Construction
        (["architect", "architecture", "construction", "interior design", "contractor"], "Architecture & Construction"),
        
        // Travel & Tourism
        (["travel", "tourism", "tour", "visa", "airline", "ticketing"], "Travel & Tourism Agencies"),
        
        // Marketing & Media
        (["marketing", "advertising", "seo", "media studio", "photography", "design studio"], "Marketing & Media Studios"),
        
        // Event Planners & Halls
        (["events venue", "banquet hall", "marriage hall", "party hall", "marquee", "wedding"], "Event Planners & Marriage Halls"),
        
        // Printing & Packaging
        (["copyshop", "printer", "printing", "packaging", "signage", "flex"], "Printing, Packaging & Signage"),
        
        // Veterinary & Pets
        (["veterinary", "pet", "animal clinic"], "Pet Care & Veterinary Clinics"),
        
        // Textile & Fashion
        (["tailor", "boutique", "fashion", "fabric", "cloth", "garments", "textile"], "Textile, Apparel & Fashion")
    ];

    /// <summary>
    /// Resolves the intended category for a discovery query.
    /// If user selected a specific CategoryPreset, that preset takes precedence.
    /// Otherwise, infers the canonical category from search terms.
    /// </summary>
    public static string ResolveEffectiveCategory(DiscoveryQuery query, string? businessName = null, string? rawCategory = null)
    {
        // 1. If user explicitly selected a concrete CategoryPreset (other than All Commercial), that wins
        if (!string.IsNullOrWhiteSpace(query.CategoryPreset) &&
            !query.CategoryPreset.Contains("All Commercial", StringComparison.OrdinalIgnoreCase) &&
            !query.CategoryPreset.StartsWith("⭐", StringComparison.OrdinalIgnoreCase))
        {
            return query.CategoryPreset.Trim();
        }

        // 2. Try inferring from the raw category if it's already a clean known commercial type
        if (!string.IsNullOrWhiteSpace(rawCategory) && !IsCorruptOrNoise(rawCategory, query.CustomSearchTerm, businessName))
        {
            var matched = MatchCanonicalCategory(rawCategory);
            if (!string.IsNullOrWhiteSpace(matched)) return matched;
        }

        // 3. Try inferring from custom search term / niche
        if (!string.IsNullOrWhiteSpace(query.CustomSearchTerm))
        {
            var matched = MatchCanonicalCategory(query.CustomSearchTerm);
            if (!string.IsNullOrWhiteSpace(matched)) return matched;
        }

        // 4. Try inferring from business name
        if (!string.IsNullOrWhiteSpace(businessName))
        {
            var matched = MatchCanonicalCategory(businessName);
            if (!string.IsNullOrWhiteSpace(matched)) return matched;
        }

        // 5. Fallback
        return "⭐ All Commercial Businesses & Enterprises";
    }

    /// <summary>
    /// Normalizes a business candidate's category in place so search keywords,
    /// niches, or noisy scraper fragments never contaminate the Category field.
    /// </summary>
    public static void NormalizeLeadCategory(Business candidate, DiscoveryQuery query)
    {
        if (candidate == null) return;

        var effectiveCategory = ResolveEffectiveCategory(query, candidate.Name, candidate.Category);

        // Check if current category is corrupt or equals the search term / business name / noise
        var isCorrupt = IsCorruptOrNoise(candidate.Category, query.CustomSearchTerm, candidate.Name);

        if (isCorrupt)
        {
            candidate.Category = !effectiveCategory.Contains("All Commercial", StringComparison.OrdinalIgnoreCase)
                ? effectiveCategory
                : "Commercial Business";
        }
        else if (!string.IsNullOrWhiteSpace(query.CategoryPreset) &&
                 !query.CategoryPreset.Contains("All Commercial", StringComparison.OrdinalIgnoreCase) &&
                 !query.CategoryPreset.StartsWith("⭐", StringComparison.OrdinalIgnoreCase))
        {
            // If the user selected a specific preset, verify if candidate's category belongs to that sector
            if (!IsCategoryCompatibleWithPreset(candidate.Category, query.CategoryPreset))
            {
                candidate.Category = query.CategoryPreset.Trim();
            }
        }

        // Ensure the search keyword / niche is preserved in Tags
        if (!string.IsNullOrWhiteSpace(query.CustomSearchTerm))
        {
            var tag = query.CustomSearchTerm.Trim();
            if (string.IsNullOrWhiteSpace(candidate.Tags))
            {
                candidate.Tags = tag;
            }
            else if (!candidate.Tags.Contains(tag, StringComparison.OrdinalIgnoreCase))
            {
                candidate.Tags = $"{candidate.Tags}, {tag}";
            }
        }
    }

    /// <summary>
    /// Detects whether a string is noise (Wikidata sentence, patent, article, raw boolean tag, etc.)
    /// or improperly equals the search keyword or business name.
    /// </summary>
    public static bool IsCorruptOrNoise(string? category, string? searchKeyword = null, string? businessName = null)
    {
        if (string.IsNullOrWhiteSpace(category)) return true;

        var cat = category.Trim();

        // Exact match with search term (user typed "TheKhanSoft" or "software company" -> Category became that keyword)
        if (!string.IsNullOrWhiteSpace(searchKeyword) && string.Equals(cat, searchKeyword.Trim(), StringComparison.OrdinalIgnoreCase))
            return true;

        // Exact match with business name (business is "The Khan Soft" -> Category became "The Khan Soft")
        if (!string.IsNullOrWhiteSpace(businessName) && string.Equals(cat, businessName.Trim(), StringComparison.OrdinalIgnoreCase))
            return true;

        // Generic placeholders from OSM or scrapers
        var lower = cat.ToLowerInvariant();
        if (lower is "yes" or "no" or "true" or "false" or "general business" or "commercial" or "commercial business"
            or "commercial businesses" or "business" or "businesses" or "company" or "office" or "shop" or "store"
            or "all commercial" or "all commercial businesses & enterprises" or "village" or "playground" or "hospice")
        {
            return true;
        }

        // Wikidata noise: sentences, patents, articles, descriptions
        if (lower.StartsWith("scientific article") || lower.StartsWith("article published") ||
            lower.StartsWith("us patent") || lower.StartsWith("patent") ||
            lower.StartsWith("defunct government") || lower.StartsWith("allows agencies") ||
            lower.StartsWith("free software license") || lower.StartsWith("computer program that") ||
            lower.StartsWith("campaign management software for") || lower.StartsWith("creator and talent") ||
            lower.StartsWith("cloud-based home care") || lower.StartsWith("capacity planning") ||
            lower.StartsWith("agency management software") || lower.StartsWith("business management software") ||
            cat.Length > 45) // Categories longer than 45 chars are usually sentences/descriptions
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Matches any input text (keyword, niche, name, or description) against canonical categories.
    /// </summary>
    public static string? MatchCanonicalCategory(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        var lower = text.ToLowerInvariant();

        foreach (var (keywords, category) in CanonicalRules)
        {
            if (keywords.Any(k => lower.Contains(k)))
            {
                return category;
            }
        }

        return null;
    }

    /// <summary>
    /// Checks if a candidate's category is compatible with the selected category preset.
    /// </summary>
    public static bool IsCategoryCompatibleWithPreset(string category, string preset)
    {
        if (string.IsNullOrWhiteSpace(category) || string.IsNullOrWhiteSpace(preset)) return false;

        var cLower = category.ToLowerInvariant();
        var pLower = preset.ToLowerInvariant();

        if (cLower.Contains(pLower) || pLower.Contains(cLower)) return true;

        if (pLower.Contains("software") || pLower.Contains("it"))
            return cLower.Contains("software") || cLower.Contains("it") || cLower.Contains("tech") || cLower.Contains("digital");

        if (pLower.Contains("health") || pLower.Contains("dental"))
            return cLower.Contains("clinic") || cLower.Contains("dentist") || cLower.Contains("doctor") || cLower.Contains("hospital") || cLower.Contains("health");

        if (pLower.Contains("food") || pLower.Contains("restaurant"))
            return cLower.Contains("restaurant") || cLower.Contains("cafe") || cLower.Contains("dining") || cLower.Contains("food");

        if (pLower.Contains("law") || pLower.Contains("legal"))
            return cLower.Contains("law") || cLower.Contains("legal") || cLower.Contains("advocate");

        if (pLower.Contains("solar") || pLower.Contains("energy"))
            return cLower.Contains("solar") || cLower.Contains("energy");

        if (pLower.Contains("real estate") || pLower.Contains("builder"))
            return cLower.Contains("real estate") || cLower.Contains("property") || cLower.Contains("builder");

        if (pLower.Contains("school") || pLower.Contains("education"))
            return cLower.Contains("school") || cLower.Contains("college") || cLower.Contains("education") || cLower.Contains("academy");

        return false;
    }
}
