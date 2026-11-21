using System;
using System.Collections.Generic;
using System.Linq;
using TKS.LeadPilot.Core.Interfaces;
using TKS.LeadPilot.Core.Models;

namespace TKS.LeadPilot.Core.Services;

/// <summary>
/// High-precision geographic containment and transliteration engine.
/// Enforces strict city boundaries, handles Urdu and English city names/aliases,
/// and eliminates cross-city leakage (e.g. Karachi or Nowshera leads appearing in Mardan).
/// </summary>
public static class CityTerritoryHelper
{
    private const double DefaultMaxCityRadiusKm = 22.0;

    // Known English canonical city names mapped to their recognized Urdu / local transliterations and aliases
    private static readonly Dictionary<string, string[]> CityAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Mardan"] = ["mardan", "مردان", "تحصیل مردان", "ضلع مردان", "mardan cantt"],
        ["Peshawar"] = ["peshawar", "پشاور", "تحصیل پشاور", "ضلع پشاور", "peshawar cantt"],
        ["Nowshera"] = ["nowshera", "نوشہرہ", "نوشهره", "تحصیل نوشہرہ", "ضلع نوشہرہ", "nowshera cantt"],
        ["Charsadda"] = ["charsadda", "چارسدہ", "چارسده", "تحصیل چارسدہ"],
        ["Swabi"] = ["swabi", "صوابی", "بام خیل", "topi", "ٹ Were", "تحصیل صوابی"],
        ["Abbottabad"] = ["abbottabad", "ایبٹ آباد", "ایبٹ اباد", "abbottabad cantt"],
        ["Swat"] = ["swat", "سوات", "mingora", "مینگورہ", "سیدو شریف", "saidu sharif"],
        ["Kohat"] = ["kohat", "کوہاٹ", "kohat cantt"],
        ["Dera Ismail Khan"] = ["dera ismail khan", "d.i. khan", "dikhan", "ڈیرہ اسماعیل خان"],
        ["Bannu"] = ["bannu", "بنوں", "bannu cantt"],
        ["Haripur"] = ["haripur", "ہری پور", "ہریپور"],
        ["Mansehra"] = ["mansehra", "مانسہرہ"],
        ["Dargai"] = ["dargai", "درگئی", "درگئ", "malakand", "مالاکنڈ", "بٹ خیلہ", "batkhela"],
        ["Karachi"] = ["karachi", "کراچی", "کراچی شرقی", "کراچی غربی", "کراچی جنوبی", "کراچی وسطی", "ملیر", "کورنگی"],
        ["Lahore"] = ["lahore", "لاہور", "لا هور", "lahore cantt"],
        ["Islamabad"] = ["islamabad", "اسلام آباد", "اسلام اباد"],
        ["Rawalpindi"] = ["rawalpindi", "راولپنڈی", "راولپندی", "rawalpindi cantt"],
        ["Faisalabad"] = ["faisalabad", "فیصل آباد", "فیصل اباد", "lyallpur"],
        ["Multan"] = ["multan", "ملتان", "multan cantt"],
        ["Gujranwala"] = ["gujranwala", "گوجرانوالہ", "گوجرانواله", "gujranwala cantt"],
        ["Sialkot"] = ["sialkot", "سیالکوٹ", "sialkot cantt"],
        ["Hyderabad"] = ["hyderabad", "حیدرآباد", "حیدراباد", "hyderabad cantt"],
        ["Quetta"] = ["quetta", "کوئٹہ", "کوئٹه", "quetta cantt"],
        ["Sukkur"] = ["sukkur", "سکھر"],
        ["Larkana"] = ["larkana", "لاڑکانہ", "لاڑکانه"],
        ["Sargodha"] = ["sargodha", "سرگودھا", "sargodha cantt"],
        ["Bahawalpur"] = ["bahawalpur", "بہاولپور", "bahawalpur cantt"],
        ["Sahiwal"] = ["sahiwal", "ساہیوال"],
        ["Gujrat"] = ["gujrat", "گجرات"],
        ["Sheikhupura"] = ["sheikhupura", "شیخوپورہ"],
        ["Jhelum"] = ["jhelum", "جہلم", "jhelum cantt"],
        ["Kasur"] = ["kasur", "قصور"],
        ["Rahim Yar Khan"] = ["rahim yar khan", "رحیم یار خان"],
        ["Muzaffarabad"] = ["muzaffarabad", "مظفرآباد", "مظفراباد"],
        ["Mirpur"] = ["mirpur", "میرپور"],
        ["Gilgit"] = ["gilgit", "گلگت"],
        ["Skardu"] = ["skardu", "سکردو"]
    };

    // Sub-localities, tehsils, and union councils of Mardan that are inside Mardan District
    private static readonly HashSet<string> MardanTehsilsAndLocalities = new(StringComparer.OrdinalIgnoreCase)
    {
        "mardan", "مردان", "cantt", "mardan cantt",
        "گڑھی کپورہ", "garhi kapura", "garhi kapoora",
        "رستم", "rustam",
        "کاٹلنگ", "katlang",
        "تخت بھائی", "takht bhai", "takht-i-bahi",
        "فضل آباد", "fazal abad",
        "چترال کلے", "takkar", "ٹکر",
        "تورو", "toru",
        "ہوتی", "hoti", "par hoti",
        "شیخان", "sheikhan",
        "بگدادہ", "baghdada", "bagh-dada",
        "گجرات", "gujrat mardan",
        "مایار", "mayar",
        "شامسی روڈ", "shamsi road",
        "مال روڈ", "mall road",
        "بینک روڈ", "bank road",
        "چار سدہ روڈ", "charsadda road",
        "نوشہرہ روڈ", "nowshera road mardan"
    };

    /// <summary>
    /// Resolves canonical English city name from any localized, Urdu, or variant city string.
    /// </summary>
    public static string? ResolveCanonicalCity(string? rawCity)
    {
        if (string.IsNullOrWhiteSpace(rawCity)) return null;

        var clean = rawCity.Trim();

        foreach (var (canonical, aliases) in CityAliases)
        {
            if (clean.Equals(canonical, StringComparison.OrdinalIgnoreCase)) return canonical;
            if (aliases.Any(a => clean.Equals(a, StringComparison.OrdinalIgnoreCase) || clean.Contains(a, StringComparison.OrdinalIgnoreCase)))
            {
                return canonical;
            }
        }

        return clean;
    }

    /// <summary>
    /// Determines whether a business candidate strictly belongs to the queried city/territory.
    /// Eliminates cross-city leakage via coordinates (Haversine distance) and city text matching.
    /// Also normalizes the candidate's City to the clean English canonical name.
    /// </summary>
    public static bool IsGeographicallyContained(
        Business candidate,
        DiscoveryQuery query,
        IGeoLocationService? geoLocationService)
    {
        if (string.IsNullOrWhiteSpace(query.City) || query.City.Equals("All Cities", StringComparison.OrdinalIgnoreCase))
        {
            return true; // No specific city boundary constraint
        }

        var targetCityClean = query.City.Trim();
        var targetCanonical = ResolveCanonicalCity(targetCityClean) ?? targetCityClean;

        // 1. Resolve Target City Center Coordinates
        double? targetLat = null;
        double? targetLon = null;

        if (query.BBox != null)
        {
            targetLat = (query.BBox.North + query.BBox.South) / 2.0;
            targetLon = (query.BBox.East + query.BBox.West) / 2.0;
        }

        if ((!targetLat.HasValue || !targetLon.HasValue) && geoLocationService != null)
        {
            var cityInfo = geoLocationService.FindCity(targetCityClean, query.Country);
            if (cityInfo != null)
            {
                targetLat = cityInfo.Latitude;
                targetLon = cityInfo.Longitude;
            }
        }

        if ((!targetLat.HasValue || !targetLon.HasValue) &&
            OverpassQueryBuilder.CityBoundingBoxes.TryGetValue(targetCityClean, out var staticBbox))
        {
            targetLat = (staticBbox.North + staticBbox.South) / 2.0;
            targetLon = (staticBbox.East + staticBbox.West) / 2.0;
        }

        // 2. High-Precision Coordinate Boundary Check
        double maxRadiusKm = DefaultMaxCityRadiusKm;

        if (candidate.Latitude.HasValue && candidate.Longitude.HasValue && targetLat.HasValue && targetLon.HasValue)
        {
            var distKm = CalculateHaversineDistanceKm(
                candidate.Latitude.Value, candidate.Longitude.Value,
                targetLat.Value, targetLon.Value);

            // Candidate is located far outside target city boundary (e.g. Karachi vs Mardan: 1,120km, Swabi: 47km, Dargai: 38km)
            if (distKm > maxRadiusKm)
            {
                return false;
            }
        }

        // 3. City Text Conflict & Alias Analysis
        var candidateCityRaw = candidate.City?.Trim();
        var candidateAddressRaw = candidate.AddressLine1?.Trim() ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(candidateCityRaw))
        {
            var candidateCanonical = ResolveCanonicalCity(candidateCityRaw);

            // Special handling for Mardan sub-tehsils (e.g. Garhi Kapura, Rustam, Katlang, Takht Bhai)
            if (targetCanonical.Equals("Mardan", StringComparison.OrdinalIgnoreCase) &&
                MardanTehsilsAndLocalities.Contains(candidateCityRaw))
            {
                if (candidate.Locality == null && !candidateCityRaw.Equals("Mardan", StringComparison.OrdinalIgnoreCase) && !candidateCityRaw.Equals("مردان", StringComparison.OrdinalIgnoreCase))
                {
                    candidate.Locality = candidateCityRaw;
                }
                candidate.City = "Mardan";
                return true;
            }

            // If the candidate city resolves to a known DIFFERENT city (e.g. Nowshera, Swabi, Dargai, Karachi when searching Mardan)
            if (candidateCanonical != null &&
                !candidateCanonical.Equals(targetCanonical, StringComparison.OrdinalIgnoreCase))
            {
                // Explicit contradiction: reject immediately
                return false;
            }

            // If candidate city matches target canonical
            if (candidateCanonical != null && candidateCanonical.Equals(targetCanonical, StringComparison.OrdinalIgnoreCase))
            {
                candidate.City = targetCanonical;
                return true;
            }
        }

        // 4. If candidate city was empty or unparsed, check address text or fallback to coordinate proximity
        if (string.IsNullOrWhiteSpace(candidateCityRaw))
        {
            // If coordinates were verified inside city radius, adopt target city
            if (candidate.Latitude.HasValue && candidate.Longitude.HasValue && targetLat.HasValue && targetLon.HasValue)
            {
                candidate.City = targetCanonical;
                return true;
            }

            // Check if address mentions target city or aliases
            if (CityAliases.TryGetValue(targetCanonical, out var targetAliases))
            {
                if (targetAliases.Any(a => candidateAddressRaw.Contains(a, StringComparison.OrdinalIgnoreCase)))
                {
                    candidate.City = targetCanonical;
                    return true;
                }
            }

            // If strict keyword filter is on and location cannot be verified, reject to prevent leakage
            if (query.StrictKeywordFilter)
            {
                return false;
            }

            candidate.City = targetCanonical;
            return true;
        }

        return true;
    }

    /// <summary>
    /// Calculates the great-circle distance between two geographic coordinates using the Haversine formula.
    /// </summary>
    public static double CalculateHaversineDistanceKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371.0; // Earth's mean radius in kilometers
        var dLat = (lat2 - lat1) * Math.PI / 180.0;
        var dLon = (lon2 - lon1) * Math.PI / 180.0;

        var a = Math.Sin(dLat / 2.0) * Math.Sin(dLat / 2.0) +
                Math.Cos(lat1 * Math.PI / 180.0) * Math.Cos(lat2 * Math.PI / 180.0) *
                Math.Sin(dLon / 2.0) * Math.Sin(dLon / 2.0);

        var c = 2.0 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1.0 - a));
        return R * c;
    }
}
