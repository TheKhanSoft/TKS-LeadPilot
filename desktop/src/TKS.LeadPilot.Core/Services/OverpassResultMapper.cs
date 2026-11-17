using System;
using System.Collections.Generic;
using System.Text.Json;
using TKS.LeadPilot.Core.Enums;
using TKS.LeadPilot.Core.Models;

namespace TKS.LeadPilot.Core.Services;

public static class OverpassResultMapper
{
    public static List<Business> MapJsonToBusinesses(string jsonContent, string defaultCity = "Karachi", string defaultCountry = "Pakistan", string? defaultState = null)
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
                var state = GetTagValue(tags, "addr:state", "addr:province");
                var country = GetTagValue(tags, "addr:country");
                var postcode = GetTagValue(tags, "addr:postcode");

                if (string.IsNullOrWhiteSpace(city))
                {
                    city = defaultCity;
                }
                if (string.IsNullOrWhiteSpace(state))
                {
                    state = defaultState;
                }
                if (string.IsNullOrWhiteSpace(country))
                {
                    country = defaultCountry;
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
                    StateProvince = string.IsNullOrWhiteSpace(state) ? null : state.Trim(),
                    Country = country.Trim(),
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
            "veterinary" => "Veterinary Clinic",
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
            "car_dealership" => "Automotive Dealership",
            "school" => "School & Education",
            "college" => "College & Higher Education",
            "university" => "University & Academic",
            "it" or "software" => "Software & IT Agency",
            "lawyer" or "legal" => "Legal & Corporate Law",
            "notary" => "Notary & Legal Services",
            "accountant" or "tax" => "Accounting & Tax Services",
            "bank" or "financial" => "Finance & Banking",
            "insurance" => "Insurance Agency",
            "estate_agent" or "real_estate" => "Real Estate Agency",
            "fitness_centre" or "gym" => "Fitness & Gym",
            "hairdresser" or "beauty" => "Beauty Salon & Spa",
            "spa" => "Spa & Wellness",
            "courier" or "logistics" => "Logistics & Courier",
            "industrial" or "factory" => "Manufacturing & Industrial",
            "architect" => "Architecture & Design",
            "travel_agency" => "Travel & Tourism Agency",
            "advertising_agency" => "Marketing & Advertising",
            "solar" or "photovoltaic" => "Solar & Renewable Energy",
            "events_venue" or "banquet_hall" => "Event Planners & Marriage Halls",
            "copyshop" or "printer" => "Printing, Packaging & Signage",
            "agrarian" or "fertilizer" => "Agricultural & Agro-Chemical",
            "tailor" or "fabrics" => "Textile, Apparel & Fashion",
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

    public static List<Business> MapNominatimToBusinesses(string jsonContent, string defaultCity = "Karachi", string defaultCountry = "Pakistan", string? defaultState = null)
    {
        var results = new List<Business>();
        if (string.IsNullOrWhiteSpace(jsonContent)) return results;

        try
        {
            using var doc = JsonDocument.Parse(jsonContent);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return results;

            foreach (var elem in doc.RootElement.EnumerateArray())
            {
                var name = string.Empty;
                if (elem.TryGetProperty("name", out var nameProp) && !string.IsNullOrWhiteSpace(nameProp.GetString()))
                {
                    name = nameProp.GetString()!;
                }
                else if (elem.TryGetProperty("display_name", out var dnProp) && !string.IsNullOrWhiteSpace(dnProp.GetString()))
                {
                    name = dnProp.GetString()!.Split(',')[0].Trim();
                }

                if (string.IsNullOrWhiteSpace(name)) continue;

                var type = elem.TryGetProperty("type", out var typeProp) ? typeProp.GetString() : "commercial";
                var category = FormatCategory(type);

                double? lat = null;
                double? lon = null;
                if (elem.TryGetProperty("lat", out var latProp) && double.TryParse(latProp.GetString(), System.Globalization.CultureInfo.InvariantCulture, out var lVal)) lat = lVal;
                if (elem.TryGetProperty("lon", out var lonProp) && double.TryParse(lonProp.GetString(), System.Globalization.CultureInfo.InvariantCulture, out var loVal)) lon = loVal;

                string? phone = null;
                string? website = null;
                string? email = null;

                if (elem.TryGetProperty("extratags", out var extra) && extra.ValueKind == JsonValueKind.Object)
                {
                    phone = GetTagValue(extra, "phone", "contact:phone", "mobile");
                    website = GetTagValue(extra, "website", "contact:website", "url");
                    email = GetTagValue(extra, "email", "contact:email");
                }

                var address = string.Empty;
                var city = defaultCity;
                var state = defaultState;
                var country = defaultCountry;

                if (elem.TryGetProperty("address", out var addr) && addr.ValueKind == JsonValueKind.Object)
                {
                    var road = GetTagValue(addr, "road", "street");
                    var suburb = GetTagValue(addr, "suburb", "neighbourhood", "residential");
                    address = CombineAddress(road, suburb) ?? string.Empty;

                    var addrCity = GetTagValue(addr, "city", "town", "county");
                    if (!string.IsNullOrWhiteSpace(addrCity)) city = addrCity;

                    var addrState = GetTagValue(addr, "state", "province");
                    if (!string.IsNullOrWhiteSpace(addrState)) state = addrState;

                    var addrCountry = GetTagValue(addr, "country");
                    if (!string.IsNullOrWhiteSpace(addrCountry)) country = addrCountry;
                }

                if (string.IsNullOrWhiteSpace(address) && elem.TryGetProperty("display_name", out var fullDn))
                {
                    address = fullDn.GetString() ?? string.Empty;
                }

                results.Add(new Business
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = name.Trim(),
                    Category = category,
                    City = city.Trim(),
                    StateProvince = state,
                    Country = country.Trim(),
                    AddressLine1 = string.IsNullOrWhiteSpace(address) ? null : address.Trim(),
                    PhonePrimary = phone,
                    WebsiteUrl = website,
                    EmailPrimary = email,
                    WebsiteStatus = !string.IsNullOrWhiteSpace(website) ? WebsiteStatus.ActiveWebsite : WebsiteStatus.NoWebsite,
                    Latitude = lat,
                    Longitude = lon,
                    SourceProvider = "OpenStreetMap (Nominatim)",
                    SourceReferenceId = elem.TryGetProperty("place_id", out var pid) ? pid.ToString() : null,
                    Tags = $"OSM Nominatim, {category}",
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                });
            }
        }
        catch { }

        return results;
    }
}
