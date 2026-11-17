using System;
using System.Collections.Generic;
using System.Text.Json;
using TKS.LeadPilot.Core.Enums;
using TKS.LeadPilot.Core.Models;

namespace TKS.LeadPilot.Core.Services;

public static class OverpassResultMapper
{
    public static List<Business> MapJsonToBusinesses(string jsonContent, string defaultCity = "Karachi")
    {
        var results = new List<Business>();
        if (string.IsNullOrWhiteSpace(jsonContent)) return results;

        try
        {
            using var doc = JsonDocument.Parse(jsonContent);
            if (!doc.RootElement.TryGetProperty("elements", out var elements) || elements.ValueKind != JsonValueKind.Array)
            {
                return results;
            }

            foreach (var elem in elements.EnumerateArray())
            {
                if (!elem.TryGetProperty("tags", out var tags) || tags.ValueKind != JsonValueKind.Object)
                {
                    continue; // Skip elements with no tags
                }

                // Name is mandatory for a valid business lead
                var name = GetTagValue(tags, "name", "name:en", "brand", "operator");
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                // Coordinates
                double? lat = null;
                double? lon = null;
                if (elem.TryGetProperty("lat", out var latProp) && latProp.TryGetDouble(out var lVal))
                {
                    lat = lVal;
                }
                if (elem.TryGetProperty("lon", out var lonProp) && lonProp.TryGetDouble(out var lonVal))
                {
                    lon = lonVal;
                }

                if (!lat.HasValue && elem.TryGetProperty("center", out var center) && center.ValueKind == JsonValueKind.Object)
                {
                    if (center.TryGetProperty("lat", out var cLat) && cLat.TryGetDouble(out var clVal)) lat = clVal;
                    if (center.TryGetProperty("lon", out var cLon) && cLon.TryGetDouble(out var cloVal)) lon = cloVal;
                }

                // Category & Amenity
                var rawCategory = GetTagValue(tags, "amenity", "shop", "tourism", "office", "craft", "healthcare");
                var category = FormatCategory(rawCategory);

                // Contact info
                var website = GetTagValue(tags, "website", "contact:website", "url");
                var phone = GetTagValue(tags, "phone", "contact:phone", "contact:mobile", "mobile");
                var email = GetTagValue(tags, "email", "contact:email");

                // Address
                var house = GetTagValue(tags, "addr:housenumber", "addr:unit");
                var street = GetTagValue(tags, "addr:street");
                var locality = GetTagValue(tags, "addr:suburb", "addr:neighbourhood", "addr:district");
                var city = GetTagValue(tags, "addr:city");
                var postcode = GetTagValue(tags, "addr:postcode");

                if (string.IsNullOrWhiteSpace(city))
                {
                    city = defaultCity;
                }

                var address = CombineAddress(house, street);

                var elemType = elem.TryGetProperty("type", out var tProp) ? tProp.GetString() ?? "node" : "node";
                var elemId = elem.TryGetProperty("id", out var idProp) ? idProp.ToString() : Guid.NewGuid().ToString("N");

                var business = new Business
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = name.Trim(),
                    Category = category,
                    City = city.Trim(),
                    AddressLine1 = string.IsNullOrWhiteSpace(address) ? null : address.Trim(),
                    Locality = string.IsNullOrWhiteSpace(locality) ? null : locality.Trim(),
                    PostalCode = string.IsNullOrWhiteSpace(postcode) ? null : postcode.Trim(),
                    PhonePrimary = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim(),
                    EmailPrimary = string.IsNullOrWhiteSpace(email) ? null : email.Trim(),
                    WebsiteUrl = string.IsNullOrWhiteSpace(website) ? null : website.Trim(),
                    WebsiteStatus = !string.IsNullOrWhiteSpace(website) ? WebsiteStatus.ActiveWebsite : WebsiteStatus.NoWebsite,
                    Latitude = lat,
                    Longitude = lon,
                    PipelineStageId = "stage-discovered",
                    LeadStatus = LeadStatus.Discovered,
                    SourceProvider = "OpenStreetMap",
                    SourceReferenceId = $"osm:{elemType}:{elemId}",
                    Tags = !string.IsNullOrWhiteSpace(rawCategory) ? $"OSM, {rawCategory}" : "OSM"
                };

                results.Add(business);
            }
        }
        catch
        {
            // Fail gracefully on corrupted JSON
        }

        return results;
    }

    private static string? GetTagValue(JsonElement tags, params string[] tagKeys)
    {
        foreach (var key in tagKeys)
        {
            if (tags.TryGetProperty(key, out var val) && val.ValueKind == JsonValueKind.String)
            {
                var str = val.GetString();
                if (!string.IsNullOrWhiteSpace(str)) return str;
            }
        }
        return null;
    }

    private static string FormatCategory(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "General Business";

        return raw.ToLowerInvariant() switch
        {
            "clinic" => "Clinic & Healthcare",
            "dentist" => "Dentist & Dental Clinic",
            "pharmacy" => "Pharmacy & Healthcare",
            "hospital" => "Hospital & Medical Center",
            "doctors" => "Medical Practice",
            "restaurant" => "Restaurant & Dining",
            "fast_food" => "Fast Food & Dining",
            "cafe" => "Cafe & Coffee Shop",
            "hotel" => "Hotels & Hospitality",
            "guest_house" => "Guest House & Lodging",
            "motel" => "Motel & Lodging",
            "supermarket" => "Supermarket & Grocery",
            "clothes" => "Apparel & Retail",
            "electronics" => "Electronics & Appliances",
            "car_repair" => "Automotive Repair & Services",
            "school" => "School & Education",
            "college" => "College & Higher Education",
            "university" => "University & Academic",
            _ => char.ToUpperInvariant(raw[0]) + raw[1..].Replace("_", " ")
        };
    }

    private static string? CombineAddress(string? house, string? street)
    {
        if (!string.IsNullOrWhiteSpace(house) && !string.IsNullOrWhiteSpace(street))
            return $"{house} {street}";
        if (!string.IsNullOrWhiteSpace(street))
            return street;
        if (!string.IsNullOrWhiteSpace(house))
            return house;
        return null;
    }
}
