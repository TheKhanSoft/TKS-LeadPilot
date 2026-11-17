using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TKS.LeadPilot.Core.Interfaces;
using TKS.LeadPilot.Core.Models;

namespace TKS.LeadPilot.Core.Services;

public class GeoLocationService : IGeoLocationService
{
    private readonly HttpClient? _httpClient;
    private static readonly List<CountryInfo> CountriesList = BuildCatalog();

    public GeoLocationService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient;
    }

    public IReadOnlyList<CountryInfo> GetAllCountries() => CountriesList;

    public CountryInfo? GetCountry(string countryNameOrCode)
    {
        if (string.IsNullOrWhiteSpace(countryNameOrCode)) return null;

        var clean = countryNameOrCode.Trim();
        return CountriesList.FirstOrDefault(c =>
            string.Equals(c.Name, clean, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(c.Code, clean, StringComparison.OrdinalIgnoreCase) ||
            clean.Contains(c.Name, StringComparison.OrdinalIgnoreCase));
    }

    public IReadOnlyList<StateProvinceInfo> GetStatesForCountry(string countryNameOrCode)
    {
        var country = GetCountry(countryNameOrCode);
        return country?.States ?? [];
    }

    public IReadOnlyList<CityInfo> GetCitiesForState(string countryNameOrCode, string stateNameOrCode)
    {
        var country = GetCountry(countryNameOrCode);
        if (country == null) return [];

        var state = country.States.FirstOrDefault(s =>
            string.Equals(s.Name, stateNameOrCode, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(s.Code, stateNameOrCode, StringComparison.OrdinalIgnoreCase));

        return state?.Cities ?? [];
    }

    public CityInfo? FindCity(string cityName, string? country = null)
    {
        if (string.IsNullOrWhiteSpace(cityName)) return null;

        var cleanCity = cityName.Trim();

        if (!string.IsNullOrWhiteSpace(country))
        {
            var c = GetCountry(country);
            if (c != null)
            {
                foreach (var state in c.States)
                {
                    var match = state.Cities.FirstOrDefault(city =>
                        string.Equals(city.Name, cleanCity, StringComparison.OrdinalIgnoreCase));
                    if (match != null) return match;
                }
            }
        }

        // Global search
        foreach (var c in CountriesList)
        {
            foreach (var state in c.States)
            {
                var match = state.Cities.FirstOrDefault(city =>
                    string.Equals(city.Name, cleanCity, StringComparison.OrdinalIgnoreCase));
                if (match != null) return match;
            }
        }

        return null;
    }

    public BoundingBox CalculateProximityBoundingBox(double centerLat, double centerLon, double radiusKm)
    {
        // 1 deg latitude ~= 111 km
        // 1 deg longitude ~= 111 * cos(lat) km
        var clampedRadius = Math.Clamp(radiusKm, 1.0, 100.0);
        var deltaLat = clampedRadius / 111.0;
        var radLat = centerLat * Math.PI / 180.0;
        var cosLat = Math.Cos(radLat);
        if (Math.Abs(cosLat) < 0.0001) cosLat = 0.0001;
        var deltaLon = clampedRadius / (111.0 * cosLat);

        return new BoundingBox(
            Math.Round(centerLat - deltaLat, 4),
            Math.Round(centerLon - deltaLon, 4),
            Math.Round(centerLat + deltaLat, 4),
            Math.Round(centerLon + deltaLon, 4));
    }

    public async Task<NearbyLocationResult> DetectNearbyLocationAsync(double radiusKm = 15.0, CancellationToken ct = default)
    {
        var result = new NearbyLocationResult
        {
            Country = "Pakistan",
            StateProvince = "Khyber Pakhtunkhwa",
            City = "Peshawar",
            Latitude = 34.0151,
            Longitude = 71.5249,
            RadiusKm = radiusKm,
            BoundingBox = CalculateProximityBoundingBox(34.0151, 71.5249, radiusKm),
            IsDetectedFromIp = false,
            Message = "Detecting location..."
        };

        var client = _httpClient ?? new HttpClient();
        bool detected = false;

        // 1. Primary: ipwho.is (fast, highly accurate, returns city, region, country, lat, lon)
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(4));

            using var req = new HttpRequestMessage(HttpMethod.Get, "https://ipwho.is/");
            req.Headers.Add("User-Agent", "TKS-LeadPilot/1.0");
            var resp = await client.SendAsync(req, cts.Token);
            if (resp.IsSuccessStatusCode)
            {
                var json = await resp.Content.ReadAsStringAsync(ct);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.TryGetProperty("success", out var succ) && succ.GetBoolean() &&
                    root.TryGetProperty("latitude", out var latProp) && latProp.TryGetDouble(out var lat) &&
                    root.TryGetProperty("longitude", out var lonProp) && lonProp.TryGetDouble(out var lon))
                {
                    result.Latitude = lat;
                    result.Longitude = lon;
                    result.RadiusKm = radiusKm;
                    result.BoundingBox = CalculateProximityBoundingBox(lat, lon, radiusKm);
                    result.IsDetectedFromIp = true;

                    if (root.TryGetProperty("city", out var cp) && !string.IsNullOrWhiteSpace(cp.GetString()))
                        result.City = cp.GetString()!.Trim();
                    if (root.TryGetProperty("region", out var rp) && !string.IsNullOrWhiteSpace(rp.GetString()))
                        result.StateProvince = rp.GetString()!.Trim();
                    if (root.TryGetProperty("country", out var ctryp) && !string.IsNullOrWhiteSpace(ctryp.GetString()))
                        result.Country = ctryp.GetString()!.Trim();

                    detected = true;
                }
            }
        }
        catch { }

        // 2. Secondary fallback: ip-api.com
        if (!detected)
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(4));

                using var req = new HttpRequestMessage(HttpMethod.Get, "http://ip-api.com/json/");
                req.Headers.Add("User-Agent", "TKS-LeadPilot/1.0");
                var resp = await client.SendAsync(req, cts.Token);
                if (resp.IsSuccessStatusCode)
                {
                    var json = await resp.Content.ReadAsStringAsync(ct);
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("status", out var st) && st.GetString() == "success" &&
                        root.TryGetProperty("lat", out var latProp) && latProp.TryGetDouble(out var lat) &&
                        root.TryGetProperty("lon", out var lonProp) && lonProp.TryGetDouble(out var lon))
                    {
                        result.Latitude = lat;
                        result.Longitude = lon;
                        result.RadiusKm = radiusKm;
                        result.BoundingBox = CalculateProximityBoundingBox(lat, lon, radiusKm);
                        result.IsDetectedFromIp = true;

                        if (root.TryGetProperty("city", out var cp) && !string.IsNullOrWhiteSpace(cp.GetString()))
                            result.City = cp.GetString()!.Trim();
                        if (root.TryGetProperty("regionName", out var rp) && !string.IsNullOrWhiteSpace(rp.GetString()))
                            result.StateProvince = rp.GetString()!.Trim();
                        if (root.TryGetProperty("country", out var ctryp) && !string.IsNullOrWhiteSpace(ctryp.GetString()))
                            result.Country = ctryp.GetString()!.Trim();

                        detected = true;
                    }
                }
            }
            catch { }
        }

        // 3. Tertiary fallback: freeipapi.com
        if (!detected)
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(4));

                using var req = new HttpRequestMessage(HttpMethod.Get, "https://freeipapi.com/api/json");
                req.Headers.Add("User-Agent", "TKS-LeadPilot/1.0");
                var resp = await client.SendAsync(req, cts.Token);
                if (resp.IsSuccessStatusCode)
                {
                    var json = await resp.Content.ReadAsStringAsync(ct);
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("latitude", out var latProp) && latProp.TryGetDouble(out var lat) &&
                        root.TryGetProperty("longitude", out var lonProp) && lonProp.TryGetDouble(out var lon))
                    {
                        result.Latitude = lat;
                        result.Longitude = lon;
                        result.RadiusKm = radiusKm;
                        result.BoundingBox = CalculateProximityBoundingBox(lat, lon, radiusKm);
                        result.IsDetectedFromIp = true;

                        if (root.TryGetProperty("cityName", out var cp) && !string.IsNullOrWhiteSpace(cp.GetString()))
                            result.City = cp.GetString()!.Trim();
                        if (root.TryGetProperty("regionName", out var rp) && !string.IsNullOrWhiteSpace(rp.GetString()))
                            result.StateProvince = rp.GetString()!.Trim();
                        if (root.TryGetProperty("countryName", out var ctryp) && !string.IsNullOrWhiteSpace(ctryp.GetString()))
                            result.Country = ctryp.GetString()!.Trim();

                        detected = true;
                    }
                }
            }
            catch { }
        }

        // Align detected raw values to catalog entities
        AlignToCatalog(result);

        result.Message = detected
            ? $"📍 Accurate location detected: {result.City}, {result.StateProvince}, {result.Country} (GPS: {result.Latitude:F4}, {result.Longitude:F4})"
            : "Could not resolve live network location; using regional anchor.";

        return result;
    }

    private static void AlignToCatalog(NearbyLocationResult result)
    {
        var country = CountriesList.FirstOrDefault(c =>
            string.Equals(c.Name, result.Country, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(c.Code, result.Country, StringComparison.OrdinalIgnoreCase) ||
            result.Country.Contains(c.Name, StringComparison.OrdinalIgnoreCase));

        if (country == null) return;
        result.Country = country.Name;

        var state = country.States.FirstOrDefault(s =>
            string.Equals(s.Name, result.StateProvince, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(s.Code, result.StateProvince, StringComparison.OrdinalIgnoreCase) ||
            result.StateProvince.Contains(s.Name, StringComparison.OrdinalIgnoreCase));

        if (state == null && country.States.Count > 0)
        {
            state = country.States.FirstOrDefault(s => s.Cities.Any(c =>
                string.Equals(c.Name, result.City, StringComparison.OrdinalIgnoreCase)));
            state ??= country.States[0];
        }

        if (state != null)
        {
            result.StateProvince = state.Name;

            var city = state.Cities.FirstOrDefault(c =>
                string.Equals(c.Name, result.City, StringComparison.OrdinalIgnoreCase));

            if (city != null)
            {
                result.City = city.Name;
            }
            else if (state.Cities.Count > 0)
            {
                var closest = state.Cities
                    .OrderBy(c => DistanceSquared(result.Latitude, result.Longitude, c.Latitude, c.Longitude))
                    .First();
                result.City = closest.Name;
            }
        }
    }

    private static double DistanceSquared(double lat1, double lon1, double lat2, double lon2)
    {
        var dLat = lat1 - lat2;
        var dLon = lon1 - lon2;
        return (dLat * dLat) + (dLon * dLon);
    }

    public IReadOnlyList<string> GetSuggestedNiches(string? country = null, string? city = null)
    {
        if (!string.IsNullOrWhiteSpace(city))
        {
            var c = FindCity(city, country);
            if (c != null && c.SuggestedNiches.Count > 0)
            {
                return c.SuggestedNiches;
            }
        }

        if (!string.IsNullOrWhiteSpace(country))
        {
            var c = GetCountry(country);
            if (c != null && c.States.Count > 0)
            {
                var combined = c.States.SelectMany(s => s.Cities).SelectMany(ci => ci.SuggestedNiches).Distinct().Take(8).ToList();
                if (combined.Count > 0) return combined;
            }
        }

        // Global defaults
        return
        [
            "Private Dental & Medical Clinics",
            "Modern Web Development & Redesign",
            "Corporate Law & Advisory Chambers",
            "Specialty E-Commerce & Retail",
            "Real Estate & Property Agencies",
            "Hospitality & Fine Dining"
        ];
    }

    private static List<CountryInfo> BuildCatalog()
    {
        return
        [
            // 1. Pakistan
            new()
            {
                Code = "PK",
                Name = "Pakistan",
                FlagEmoji = "🇵🇰",
                States =
                [
                    new()
                    {
                        Name = "Punjab",
                        Code = "PB",
                        Cities =
                        [
                            new() { Name = "Lahore", Latitude = 31.5204, Longitude = 74.3587, BoundingBox = new(31.40, 74.15, 31.65, 74.45), SuggestedNiches = ["Software & IT Agencies", "Specialty Dental & Aesthetics", "Boutique Hospitality & Cafes", "Law & Advisory Chambers", "Private Colleges & Academies"] },
                            new() { Name = "Faisalabad", Latitude = 31.4504, Longitude = 73.1350, BoundingBox = new(31.35, 73.00, 31.50, 73.18), SuggestedNiches = ["Textile Mills & Weaving", "Industrial Chemicals", "Apparel Manufacturers", "Private Hospitals"] },
                            new() { Name = "Rawalpindi", Latitude = 33.5651, Longitude = 73.0169, BoundingBox = new(33.50, 73.00, 33.65, 73.15), SuggestedNiches = ["Diagnostic Centers & Hospitals", "Real Estate Agencies", "Commercial Transport", "Private Schools"] },
                            new() { Name = "Multan", Latitude = 30.1575, Longitude = 71.5249, BoundingBox = new(30.12, 71.40, 30.25, 71.55), SuggestedNiches = ["Agro Chemical & Seed Companies", "Citrus & Mango Exporters", "Handicrafts & Blue Pottery", "Fertilizer Traders"] },
                            new() { Name = "Gujranwala", Latitude = 32.1877, Longitude = 74.1945, BoundingBox = new(32.12, 74.12, 32.25, 74.26), SuggestedNiches = ["Sanitary Fittings & Metalwork", "Ceramics & Tiles", "Culinary Appliances", "Plastics Manufacturing"] },
                            new() { Name = "Sialkot", Latitude = 32.4945, Longitude = 74.5229, BoundingBox = new(32.44, 74.46, 32.56, 74.58), SuggestedNiches = ["Surgical Instruments Exporters", "Sportswear & Leather Goods", "Sports Equipment Manufacturers", "Musical Instruments"] },
                            new() { Name = "Sargodha", Latitude = 32.0836, Longitude = 72.6711, BoundingBox = new(32.02, 72.60, 32.15, 72.75), SuggestedNiches = ["Citrus & Kinnow Exporters", "Agricultural Machinery", "Medical Clinics", "Wholesale Grain Trading"] },
                            new() { Name = "Bahawalpur", Latitude = 29.3956, Longitude = 71.6836, BoundingBox = new(29.34, 71.62, 29.46, 71.76), SuggestedNiches = ["Cotton Ginning Mills", "Higher Education Institutions", "Heritage Tourism & Hotels", "Agri Trading"] },
                            new() { Name = "Sheikhupura", Latitude = 31.7131, Longitude = 73.9783, BoundingBox = new(31.65, 73.90, 31.78, 74.05), SuggestedNiches = ["Rice Processing & Export", "Petrochemical Plants", "Steel Mills", "Industrial Engineering"] },
                            new() { Name = "Gujrat", Latitude = 32.5742, Longitude = 74.0754, BoundingBox = new(32.52, 74.01, 32.64, 74.15), SuggestedNiches = ["Electric Fan Manufacturing", "Ceramic Tiles & Sanitaryware", "Modern Furniture Design", "Private Clinics"] },
                            new() { Name = "Jhang", Latitude = 31.2681, Longitude = 72.3181, BoundingBox = new(31.21, 72.25, 31.33, 72.39), SuggestedNiches = ["Cotton Weaving & Yarn", "Sugar Refining", "Agricultural Livestock"] },
                            new() { Name = "Sahiwal", Latitude = 30.6682, Longitude = 73.1114, BoundingBox = new(30.61, 73.04, 30.73, 73.18), SuggestedNiches = ["Dairy & Livestock Processing", "Cold Storage & Potato Export", "Textile Garments"] },
                            new() { Name = "Rahim Yar Khan", Latitude = 28.4195, Longitude = 70.3025, BoundingBox = new(28.36, 70.24, 28.48, 70.37), SuggestedNiches = ["Sugar Mills & Byproducts", "Cotton Seed & Oil Mills", "Fertilizer Trading", "Commercial Transport"] },
                            new() { Name = "Kasur", Latitude = 31.1179, Longitude = 74.4507, BoundingBox = new(31.06, 74.39, 31.18, 74.52), SuggestedNiches = ["Leather Tanneries & Export", "Embroidery & Textiles", "Traditional Sweets & Food"] },
                            new() { Name = "Okara", Latitude = 30.8081, Longitude = 73.4458, BoundingBox = new(30.75, 73.38, 30.87, 73.51), SuggestedNiches = ["Dairy Farms & Milk Processing", "Seed Corporation Trading", "Agricultural Storage"] },
                            new() { Name = "Dera Ghazi Khan", Latitude = 30.0561, Longitude = 70.6403, BoundingBox = new(30.00, 70.57, 30.12, 70.71), SuggestedNiches = ["Cement Manufacturing", "Cotton Processing", "Mineral & Stone Crushing"] },
                            new() { Name = "Attock", Latitude = 33.7667, Longitude = 72.3598, BoundingBox = new(33.72, 72.30, 33.82, 72.42), SuggestedNiches = ["Oil Refining & Petroleum", "Textile Weaving", "Heavy Transport"] },
                            new() { Name = "Chakwal", Latitude = 32.9328, Longitude = 72.8631, BoundingBox = new(32.88, 72.80, 32.98, 72.92), SuggestedNiches = ["Cement Manufacturing", "Poultry Farming", "Mineral Mining"] },
                            new() { Name = "Jhelum", Latitude = 32.9405, Longitude = 73.7276, BoundingBox = new(32.89, 73.68, 32.99, 73.78), SuggestedNiches = ["Tobacco Processing", "Flour Mills", "Overseas Expatriate Real Estate"] },
                            new() { Name = "Taxila", Latitude = 33.7463, Longitude = 72.8397, BoundingBox = new(33.70, 72.78, 33.80, 72.90), SuggestedNiches = ["Heavy Mechanical Complexes", "Heritage Tourism", "Engineering Colleges"] },
                            new() { Name = "Wah Cantt", Latitude = 33.7744, Longitude = 72.7523, BoundingBox = new(33.72, 72.70, 33.82, 72.80), SuggestedNiches = ["Defense Engineering", "Industrial Tools", "Educational Institutes"] },
                            new() { Name = "Mandi Bahauddin", Latitude = 32.5870, Longitude = 73.4912, BoundingBox = new(32.53, 73.44, 32.64, 73.54), SuggestedNiches = ["Agri Farming & Rice Mills", "Flour Mills", "Private Healthcare"] },
                            new() { Name = "Hafizabad", Latitude = 32.0719, Longitude = 73.6874, BoundingBox = new(32.02, 73.63, 32.12, 73.74), SuggestedNiches = ["Rice Husking & Processing", "Agricultural Trading", "Power Looms"] },
                            new() { Name = "Chiniot", Latitude = 31.7200, Longitude = 72.9789, BoundingBox = new(31.67, 72.92, 31.77, 73.03), SuggestedNiches = ["Handcrafted Wooden Furniture", "Brass Inlay Work", "Private Clinics"] },
                            new() { Name = "Toba Tek Singh", Latitude = 30.9709, Longitude = 72.4828, BoundingBox = new(30.92, 72.43, 31.02, 72.53), SuggestedNiches = ["Citrus Orchards & Trading", "Poultry Feed", "Textile Ginning"] },
                            new() { Name = "Vehari", Latitude = 30.0419, Longitude = 72.3489, BoundingBox = new(29.99, 72.29, 30.09, 72.40), SuggestedNiches = ["Cotton Ginning", "Seed Extraction", "Agro Pesticides"] },
                            new() { Name = "Khanewal", Latitude = 30.3017, Longitude = 71.9321, BoundingBox = new(30.25, 71.88, 30.35, 71.98), SuggestedNiches = ["Railway Freight Hub", "Cotton Trading", "Agricultural Machinery"] },
                            new() { Name = "Lodhran", Latitude = 29.5405, Longitude = 71.6336, BoundingBox = new(29.49, 71.58, 29.59, 71.69), SuggestedNiches = ["Cotton Seed Processing", "Agri Transport", "Fertilizer Wholesalers"] },
                            new() { Name = "Pakpattan", Latitude = 30.3410, Longitude = 73.3866, BoundingBox = new(30.29, 73.33, 30.39, 73.44), SuggestedNiches = ["Heritage Pilgrimage Lodging", "Dairy & Milk Chilling", "Agri Produce"] },
                            new() { Name = "Bahawalnagar", Latitude = 29.9987, Longitude = 73.2536, BoundingBox = new(29.94, 73.20, 30.05, 73.31), SuggestedNiches = ["Cotton Ginning Mills", "Flour Mills", "Grain Markets"] },
                            new() { Name = "Muzaffargarh", Latitude = 30.0754, Longitude = 71.1921, BoundingBox = new(30.02, 71.14, 30.13, 71.25), SuggestedNiches = ["Thermal Power Generation", "Sugar Mills", "Jute Manufacturing"] },
                            new() { Name = "Layyah", Latitude = 30.9613, Longitude = 70.9416, BoundingBox = new(30.91, 70.89, 31.01, 71.00), SuggestedNiches = ["Sugar Mills", "Cotton Processing", "Agri Trading"] },
                            new() { Name = "Bhakkar", Latitude = 31.6253, Longitude = 71.0657, BoundingBox = new(31.57, 71.01, 31.68, 71.12), SuggestedNiches = ["Gram Pulse Processing", "Textile Garments", "Livestock Market"] },
                            new() { Name = "Mianwali", Latitude = 32.5853, Longitude = 71.5436, BoundingBox = new(32.53, 71.49, 32.64, 71.60), SuggestedNiches = ["Hydropower & Energy", "Minerals & Silica Sand", "Transport"] },
                            new() { Name = "Khushab", Latitude = 32.2967, Longitude = 72.3525, BoundingBox = new(32.24, 72.30, 32.35, 72.41), SuggestedNiches = ["Salt & Mineral Mining", "Traditional Confectionery", "Cement Plants"] },
                            new() { Name = "Murree", Latitude = 33.9070, Longitude = 73.3903, BoundingBox = new(33.86, 73.34, 33.96, 73.44), SuggestedNiches = ["Resort Hotels & Lodging", "Tourism & Restaurants", "Handicrafts"] },
                            new() { Name = "Kot Addu", Latitude = 30.4688, Longitude = 70.9644, BoundingBox = new(30.41, 70.91, 30.52, 71.02), SuggestedNiches = ["Thermal Power Generation", "Refinery Services", "Sugar Mills"] },
                            new() { Name = "Taunsa", Latitude = 30.7042, Longitude = 70.6508, BoundingBox = new(30.65, 70.60, 30.76, 70.70), SuggestedNiches = ["Barrage Irrigation Works", "Agri Produce", "Transport Logistics"] }
                        ]
                    },
                    new()
                    {
                        Name = "Sindh",
                        Code = "SD",
                        Cities =
                        [
                            new() { Name = "Karachi", Latitude = 24.8607, Longitude = 67.0011, BoundingBox = new(24.75, 66.85, 25.10, 67.25), SuggestedNiches = ["Textile & Apparel Exporters", "Private Diagnostic Clinics", "Corporate Law & Tax Firms", "Freight Forwarding & Logistics", "E-Commerce D2C Brands"] },
                            new() { Name = "Hyderabad", Latitude = 25.3960, Longitude = 68.3578, BoundingBox = new(25.32, 68.28, 25.45, 68.42), SuggestedNiches = ["Glass & Bangle Manufacturers", "Medical Centers & Pharmacies", "Auto Parts Wholesalers", "Textile Weaving"] },
                            new() { Name = "Sukkur", Latitude = 27.7052, Longitude = 68.8574, BoundingBox = new(27.65, 68.80, 27.76, 68.92), SuggestedNiches = ["Agri Trading & Dates Exporters", "Wholesale Distribution", "River Transport & Storage"] },
                            new() { Name = "Larkana", Latitude = 27.5589, Longitude = 68.2120, BoundingBox = new(27.50, 68.15, 27.62, 68.28), SuggestedNiches = ["Rice Mills & Grain Trading", "Guava & Fruit Orchards", "Medical Services"] },
                            new() { Name = "Nawabshah", Latitude = 26.2483, Longitude = 68.4096, BoundingBox = new(26.19, 68.35, 26.31, 68.48), SuggestedNiches = ["Sugar Mills & Distilleries", "Cotton Processing", "Heavy Engineering Colleges"] },
                            new() { Name = "Mirpur Khas", Latitude = 25.5276, Longitude = 69.0125, BoundingBox = new(25.47, 68.95, 25.59, 69.08), SuggestedNiches = ["Mango Farming & Export", "Sugar Mills", "Commercial Agriculture"] },
                            new() { Name = "Jacobabad", Latitude = 28.2810, Longitude = 68.4376, BoundingBox = new(28.22, 68.37, 28.34, 68.50), SuggestedNiches = ["Grain Storage & Distribution", "Cross-Provincial Logistics", "Agricultural Equipment"] },
                            new() { Name = "Thatta", Latitude = 24.7475, Longitude = 67.9236, BoundingBox = new(24.69, 67.87, 24.80, 67.98), SuggestedNiches = ["Cement Plants", "Wind Energy Farms", "Heritage Tourism"] },
                            new() { Name = "Badin", Latitude = 24.6560, Longitude = 68.8378, BoundingBox = new(24.60, 68.78, 24.71, 68.89), SuggestedNiches = ["Oil & Gas Exploration", "Rice Mills", "Sugar Manufacturing"] },
                            new() { Name = "Tando Allahyar", Latitude = 25.4605, Longitude = 68.7176, BoundingBox = new(25.41, 68.66, 25.51, 68.77), SuggestedNiches = ["Sugar Mills", "Mango Orchards", "Cotton Processing"] },
                            new() { Name = "Tando Muhammad Khan", Latitude = 25.1239, Longitude = 68.5369, BoundingBox = new(25.07, 68.48, 25.18, 68.59), SuggestedNiches = ["Sugar Refining", "Agri Warehousing", "Livestock Trading"] },
                            new() { Name = "Matiari", Latitude = 25.5971, Longitude = 68.4467, BoundingBox = new(25.54, 68.39, 25.65, 68.50), SuggestedNiches = ["Ajrak & Sindhi Cap Handicrafts", "Agri Produce", "Educational Colleges"] },
                            new() { Name = "Jamshoro", Latitude = 25.4283, Longitude = 68.2811, BoundingBox = new(25.38, 68.23, 25.48, 68.34), SuggestedNiches = ["Higher Education Universities", "Thermal Power", "Healthcare"] },
                            new() { Name = "Dadu", Latitude = 26.7329, Longitude = 67.7769, BoundingBox = new(26.68, 67.72, 26.78, 67.83), SuggestedNiches = ["Agri Produce Trading", "Oil & Gas Services", "Wholesale Markets"] },
                            new() { Name = "Sanghar", Latitude = 26.0466, Longitude = 68.9482, BoundingBox = new(25.99, 68.89, 26.10, 69.00), SuggestedNiches = ["Oil & Natural Gas Fields", "Cotton Ginning", "Dairy Farming"] },
                            new() { Name = "Naushahro Feroze", Latitude = 26.8401, Longitude = 68.1211, BoundingBox = new(26.79, 68.07, 26.89, 68.18), SuggestedNiches = ["Grain Trading", "Flour Mills", "Agri Transport"] },
                            new() { Name = "Ghotki", Latitude = 28.0060, Longitude = 69.3161, BoundingBox = new(27.95, 69.26, 28.06, 69.37), SuggestedNiches = ["Fertilizer Plants", "Gas Exploration", "Sugar Processing"] },
                            new() { Name = "Shikarpur", Latitude = 27.9556, Longitude = 68.6382, BoundingBox = new(27.90, 68.58, 28.01, 68.69), SuggestedNiches = ["Traditional Pickles & Sweets", "Brass Handicrafts", "Rice Mills"] },
                            new() { Name = "Kandhkot", Latitude = 28.2439, Longitude = 69.1824, BoundingBox = new(28.19, 69.13, 28.30, 69.24), SuggestedNiches = ["Gas Field Services", "Grain Markets", "Cross-River Logistics"] },
                            new() { Name = "Kashmore", Latitude = 28.4333, Longitude = 69.5833, BoundingBox = new(28.38, 69.53, 28.49, 69.64), SuggestedNiches = ["Power Stations", "Rice Processing", "Transport Hub"] },
                            new() { Name = "Kambar Shahdadkot", Latitude = 27.5912, Longitude = 67.9984, BoundingBox = new(27.54, 67.94, 27.65, 68.05), SuggestedNiches = ["Rice Mills & Export", "Agricultural Supplies", "Wholesale Markets"] },
                            new() { Name = "Umerkot", Latitude = 25.3616, Longitude = 69.7362, BoundingBox = new(25.31, 69.68, 25.42, 69.79), SuggestedNiches = ["Red Chilli Export", "Agri Commodities", "Historical Tourism"] },
                            new() { Name = "Mithi", Latitude = 24.7438, Longitude = 69.8000, BoundingBox = new(24.69, 69.75, 24.80, 69.85), SuggestedNiches = ["Coal Energy Projects", "Handicrafts & Wool", "Solar Power Plants"] }
                        ]
                    },
                    new()
                    {
                        Name = "Islamabad Capital Territory",
                        Code = "IS",
                        Cities =
                        [
                            new() { Name = "Islamabad", Latitude = 33.6844, Longitude = 73.0479, BoundingBox = new(33.60, 72.95, 33.78, 73.18), SuggestedNiches = ["IT & SaaS Companies", "Diplomatic & Visa Consultants", "Boutique Real Estate", "High-End Aesthetic Clinics", "Corporate Law Firms"] }
                        ]
                    },
                    new()
                    {
                        Name = "Khyber Pakhtunkhwa",
                        Code = "KP",
                        Cities =
                        [
                            new() { Name = "Peshawar", Latitude = 34.0151, Longitude = 71.5249, BoundingBox = new(33.95, 71.45, 34.05, 71.60), SuggestedNiches = ["Dry Fruits & Spices Traders", "Car Dealerships & Import", "Traditional Restaurants & Hospitality", "Cross-Border Logistics"] },
                            new() { Name = "Abbottabad", Latitude = 34.1688, Longitude = 73.2215, BoundingBox = new(34.12, 73.18, 34.22, 73.28), SuggestedNiches = ["Tourism & Resort Hotels", "Private Medical Colleges", "Boarding Schools", "Real Estate Development"] },
                            new() { Name = "Mardan", Latitude = 34.1986, Longitude = 72.0404, BoundingBox = new(34.14, 71.97, 34.26, 72.11), SuggestedNiches = ["Sugar Refining", "Tobacco Processing & Export", "Wholesale Commercial Trade"] },
                            new() { Name = "Swat", Latitude = 34.7717, Longitude = 72.3602, BoundingBox = new(34.70, 72.30, 34.84, 72.43), SuggestedNiches = ["Tourism Resorts & Hotels", "Gemstones & Emerald Trading", "Natural Honey & Dry Fruits", "Handicrafts & Woodwork"] },
                            new() { Name = "Kohat", Latitude = 33.5869, Longitude = 71.4414, BoundingBox = new(33.52, 71.37, 33.65, 71.51), SuggestedNiches = ["Cement Manufacturing", "Oil & Gas Field Services", "Logistics & Transport"] },
                            new() { Name = "Dera Ismail Khan", Latitude = 31.8314, Longitude = 70.9017, BoundingBox = new(31.77, 70.83, 31.89, 70.97), SuggestedNiches = ["Dhakki Date Export", "Transport Hub", "Agro Food Processing"] },
                            new() { Name = "Haripur", Latitude = 33.9999, Longitude = 72.9341, BoundingBox = new(33.93, 72.86, 34.06, 73.00), SuggestedNiches = ["Hattar Industrial Estate Manufacturers", "Heavy Engineering", "Pharmaceutical Labs"] },
                            new() { Name = "Mansehra", Latitude = 34.3302, Longitude = 73.1968, BoundingBox = new(34.27, 73.13, 34.39, 73.26), SuggestedNiches = ["Northern Tourism Gateway", "Pine Timber & Wood Mills", "Stone Crushing & Mining"] },
                            new() { Name = "Swabi", Latitude = 34.1202, Longitude = 72.4698, BoundingBox = new(34.07, 72.41, 34.18, 72.53), SuggestedNiches = ["Tobacco Growing & Curing", "Topi Engineering Colleges", "Agri Produce"] },
                            new() { Name = "Nowshera", Latitude = 34.0153, Longitude = 71.9747, BoundingBox = new(33.96, 71.92, 34.07, 72.03), SuggestedNiches = ["Textile & Paper Mills", "Industrial Estates", "Army Logistics Hub"] },
                            new() { Name = "Charsadda", Latitude = 34.1453, Longitude = 71.7308, BoundingBox = new(34.09, 71.67, 34.20, 71.79), SuggestedNiches = ["Handmade Chappals & Leather", "Sugar Mills", "Gur & Traditional Sweets"] },
                            new() { Name = "Bannu", Latitude = 32.9861, Longitude = 70.6042, BoundingBox = new(32.93, 70.55, 33.04, 70.66), SuggestedNiches = ["Spices & Agri Trading", "Livestock Markets", "Transport Freight"] },
                            new() { Name = "Karak", Latitude = 33.1114, Longitude = 71.0917, BoundingBox = new(33.06, 71.04, 33.17, 71.15), SuggestedNiches = ["Oil & Gas Exploration", "Uranium & Salt Mining", "Heavy Transport"] },
                            new() { Name = "Hangu", Latitude = 33.5319, Longitude = 71.0594, BoundingBox = new(33.48, 71.00, 33.59, 71.11), SuggestedNiches = ["Agri Farming", "Cross-Border Logistics", "Mineral Extraction"] },
                            new() { Name = "Malakand", Latitude = 34.5667, Longitude = 71.9167, BoundingBox = new(34.51, 71.86, 34.62, 71.97), SuggestedNiches = ["Hydropower Generation", "Tourism Transit Lodges", "Agri Produce"] },
                            new() { Name = "Dir", Latitude = 35.2074, Longitude = 71.8757, BoundingBox = new(35.15, 71.82, 35.26, 71.93), SuggestedNiches = ["Timber & Woodwork", "Walnut & Pine Nuts", "Mountain Tourism"] },
                            new() { Name = "Chitral", Latitude = 35.8510, Longitude = 71.7864, BoundingBox = new(35.80, 71.73, 35.91, 71.84), SuggestedNiches = ["Eco-Tourism & Resorts", "Handwoven Patti Fabric", "Gemstones & Mineral Trade"] },
                            new() { Name = "Batagram", Latitude = 34.6772, Longitude = 73.0242, BoundingBox = new(34.62, 72.97, 34.73, 73.08), SuggestedNiches = ["Highway Logistics", "Pine Timber", "Mountain Hospitality"] },
                            new() { Name = "Shangla", Latitude = 34.8872, Longitude = 72.7570, BoundingBox = new(34.83, 72.70, 34.94, 72.81), SuggestedNiches = ["Coal Mining Labor Trade", "Hydel Power", "Tourism"] },
                            new() { Name = "Buner", Latitude = 34.3942, Longitude = 72.6150, BoundingBox = new(34.34, 72.56, 34.45, 72.67), SuggestedNiches = ["Marble & Granite Mining", "Tobacco Processing", "Agri Produce"] },
                            new() { Name = "Bajaur", Latitude = 34.7867, Longitude = 71.5247, BoundingBox = new(34.73, 71.47, 34.84, 71.58), SuggestedNiches = ["Marble Extraction", "Cross-Border Commerce", "Agri Farming"] },
                            new() { Name = "Mohmand", Latitude = 34.5367, Longitude = 71.4328, BoundingBox = new(34.48, 71.38, 34.59, 71.49), SuggestedNiches = ["Marble Industrial City", "Hydroelectric Dam Projects", "Mineral Extraction"] },
                            new() { Name = "Khyber", Latitude = 34.0044, Longitude = 71.1578, BoundingBox = new(33.95, 71.10, 34.06, 71.21), SuggestedNiches = ["Torkham Cross-Border Trade", "Customs Clearing Agencies", "Transport Fleets"] },
                            new() { Name = "Kurram", Latitude = 33.9000, Longitude = 70.1000, BoundingBox = new(33.85, 70.05, 33.95, 70.15), SuggestedNiches = ["Fresh Fruit Orchards", "Medicinal Herbs Trade", "Dry Fruit Trading"] },
                            new() { Name = "Tank", Latitude = 32.2217, Longitude = 70.3794, BoundingBox = new(32.17, 70.32, 32.28, 70.43), SuggestedNiches = ["Grain Storage", "Agri Logistics", "Date Processing"] }
                        ]
                    },
                    new()
                    {
                        Name = "Balochistan",
                        Code = "BA",
                        Cities =
                        [
                            new() { Name = "Quetta", Latitude = 30.1798, Longitude = 66.9750, BoundingBox = new(30.12, 66.92, 30.24, 67.04), SuggestedNiches = ["Mineral & Mining Traders", "Dry Fruit Distributors", "Transport & Warehousing", "Fruit Orchards"] },
                            new() { Name = "Gwadar", Latitude = 25.1264, Longitude = 62.3225, BoundingBox = new(25.08, 62.28, 25.18, 62.38), SuggestedNiches = ["Deep Sea Port Logistics", "Coastal Real Estate & Development", "Fisheries & Marine Export"] },
                            new() { Name = "Turbat", Latitude = 26.0031, Longitude = 63.0544, BoundingBox = new(25.95, 62.98, 26.07, 63.12), SuggestedNiches = ["Date Palm Processing", "Cross-Border Trade", "Agricultural Irrigation"] },
                            new() { Name = "Khuzdar", Latitude = 27.8000, Longitude = 66.6167, BoundingBox = new(27.74, 66.55, 27.86, 66.68), SuggestedNiches = ["Mining & Barytes Processing", "Highway Freight Logistics", "Livestock Trading"] },
                            new() { Name = "Hub", Latitude = 24.9000, Longitude = 66.8833, BoundingBox = new(24.84, 66.82, 24.96, 66.95), SuggestedNiches = ["Automobile Assembly", "Chemical Manufacturing", "Power & Energy Plants"] },
                            new() { Name = "Chaman", Latitude = 30.9236, Longitude = 66.4512, BoundingBox = new(30.87, 66.40, 30.98, 66.51), SuggestedNiches = ["Border Transit Commerce", "Customs Brokerage", "Dry Fruits Trade"] },
                            new() { Name = "Zhob", Latitude = 31.3417, Longitude = 69.4486, BoundingBox = new(31.29, 69.39, 31.40, 69.50), SuggestedNiches = ["Chromite & Mineral Mining", "Apple & Almond Orchards", "Livestock"] },
                            new() { Name = "Loralai", Latitude = 30.3705, Longitude = 68.5979, BoundingBox = new(30.32, 68.54, 30.43, 68.65), SuggestedNiches = ["Almond & Pomegranate Export", "Marble Quarries", "Education Colleges"] },
                            new() { Name = "Pishin", Latitude = 30.5803, Longitude = 67.0028, BoundingBox = new(30.53, 66.95, 30.64, 67.06), SuggestedNiches = ["Apple Orchards", "Commercial Cold Storage", "Vegetable Trading"] },
                            new() { Name = "Killa Abdullah", Latitude = 30.7289, Longitude = 66.6611, BoundingBox = new(30.67, 66.61, 30.78, 66.72), SuggestedNiches = ["Agri Farming", "Border Logistics", "Horticulture"] },
                            new() { Name = "Sibi", Latitude = 29.5448, Longitude = 67.8764, BoundingBox = new(29.49, 67.82, 29.60, 67.93), SuggestedNiches = ["Historical Horse & Cattle Shows", "Railway Junction", "Agri Produce"] },
                            new() { Name = "Dera Bugti", Latitude = 29.0304, Longitude = 69.1839, BoundingBox = new(28.98, 69.13, 29.09, 69.24), SuggestedNiches = ["Sui Natural Gas Production", "Petroleum Services", "Mineral Extraction"] },
                            new() { Name = "Kohlu", Latitude = 29.8965, Longitude = 69.2532, BoundingBox = new(29.84, 69.20, 29.95, 69.31), SuggestedNiches = ["Coal Mining", "Gypsum Extraction", "Livestock Herding"] },
                            new() { Name = "Jaffarabad", Latitude = 28.4312, Longitude = 68.2514, BoundingBox = new(28.38, 68.20, 28.49, 68.31), SuggestedNiches = ["Rice Cultivation & Husking", "Wheat Storage", "Canal Agri Trading"] },
                            new() { Name = "Nasirabad", Latitude = 28.5833, Longitude = 68.1833, BoundingBox = new(28.53, 68.13, 28.64, 68.24), SuggestedNiches = ["Canal Agriculture", "Grain Warehousing", "Agri Equipment"] },
                            new() { Name = "Kharan", Latitude = 28.5833, Longitude = 65.4167, BoundingBox = new(28.53, 65.36, 28.64, 65.47), SuggestedNiches = ["Date Processing", "Camel Livestock Trading", "Desert Agriculture"] },
                            new() { Name = "Panjgur", Latitude = 26.9644, Longitude = 64.0903, BoundingBox = new(26.91, 64.04, 27.02, 64.15), SuggestedNiches = ["Premium Dates Export", "Solar Installations", "Cross-Border Trade"] },
                            new() { Name = "Lasbela", Latitude = 25.7500, Longitude = 66.6000, BoundingBox = new(25.70, 66.55, 25.80, 66.65), SuggestedNiches = ["Zinc & Lead Mining", "Industrial Processing", "Coastal Fisheries"] },
                            new() { Name = "Uthal", Latitude = 25.8072, Longitude = 66.6219, BoundingBox = new(25.75, 66.57, 25.86, 66.67), SuggestedNiches = ["University Education", "Highway Hospitality", "Minerals"] },
                            new() { Name = "Kalat", Latitude = 29.0225, Longitude = 66.5916, BoundingBox = new(28.97, 66.54, 29.08, 66.65), SuggestedNiches = ["Apple Orchards", "Cold Weather Agriculture", "Heritage Tourism"] },
                            new() { Name = "Mastung", Latitude = 29.7997, Longitude = 66.8455, BoundingBox = new(29.74, 66.79, 29.85, 66.90), SuggestedNiches = ["Almonds & Melons Farming", "Agri Warehousing", "Highway Transport"] },
                            new() { Name = "Ziarat", Latitude = 30.3824, Longitude = 67.7256, BoundingBox = new(30.33, 67.67, 30.44, 67.78), SuggestedNiches = ["Juniper Heritage Tourism", "Resort Hotels", "Cherry Orchards"] },
                            new() { Name = "Harnai", Latitude = 30.1008, Longitude = 67.9372, BoundingBox = new(30.05, 67.88, 30.16, 67.99), SuggestedNiches = ["Coal Mining & Freight", "Wool Production", "Agri Farming"] },
                            new() { Name = "Nushki", Latitude = 29.5542, Longitude = 66.0215, BoundingBox = new(29.50, 65.97, 29.61, 66.08), SuggestedNiches = ["Desert Transit Freight", "Cross-Border Trade", "Agri Livestock"] }
                        ]
                    },
                    new()
                    {
                        Name = "Gilgit-Baltistan",
                        Code = "GB",
                        Cities =
                        [
                            new() { Name = "Gilgit", Latitude = 35.9208, Longitude = 74.3144, BoundingBox = new(35.86, 74.25, 35.98, 74.38), SuggestedNiches = ["Karakoram Highway Logistics", "Trout Fish Farms", "Eco-Tourism & Expeditions", "Gemstones & Minerals"] },
                            new() { Name = "Skardu", Latitude = 35.2971, Longitude = 75.6333, BoundingBox = new(35.24, 75.56, 35.36, 75.70), SuggestedNiches = ["High-Altitude Mountaineering Tourism", "Cherry & Apricot Export", "Resort Hotels"] },
                            new() { Name = "Hunza", Latitude = 36.3167, Longitude = 74.6500, BoundingBox = new(36.26, 74.58, 36.38, 74.72), SuggestedNiches = ["Luxury Boutique Hotels", "Organic Foods & Dry Fruits", "Cultural Heritage Tours"] },
                            new() { Name = "Chilas", Latitude = 35.4206, Longitude = 74.0967, BoundingBox = new(35.37, 74.04, 35.48, 74.15), SuggestedNiches = ["Diamer Bhasha Dam Services", "Highway Transit Hotels", "Agri Produce"] },
                            new() { Name = "Gahkuch", Latitude = 36.1736, Longitude = 73.7667, BoundingBox = new(36.12, 73.71, 36.23, 73.82), SuggestedNiches = ["Trout Aquaculture", "Fruit Dehydration", "Eco-Lodging"] },
                            new() { Name = "Astore", Latitude = 35.3667, Longitude = 74.8500, BoundingBox = new(35.31, 74.80, 35.42, 74.91), SuggestedNiches = ["Deosai Plains Tourism Gateway", "Pine Timber", "Medicinal Plants"] },
                            new() { Name = "Khaplu", Latitude = 35.1558, Longitude = 76.3353, BoundingBox = new(35.10, 76.28, 35.21, 76.39), SuggestedNiches = ["Heritage Palace Tourism", "Apricot Oil Processing", "Wood Carving"] },
                            new() { Name = "Shigar", Latitude = 35.4267, Longitude = 75.7289, BoundingBox = new(35.37, 75.67, 35.48, 75.78), SuggestedNiches = ["K2 Base Camp Gateway", "Historical Fort Lodging", "Gemstone Trade"] },
                            new() { Name = "Nagar", Latitude = 36.2500, Longitude = 74.6667, BoundingBox = new(36.20, 74.61, 36.31, 74.72), SuggestedNiches = ["Ruby & Gemstone Mining", "Alpine Tourism", "Glacial Water Bottling"] },
                            new() { Name = "Kharmang", Latitude = 34.9333, Longitude = 76.2167, BoundingBox = new(34.88, 76.16, 34.99, 76.27), SuggestedNiches = ["Manthokha Waterfall Tourism", "Organic Farming", "Dry Fruit Trade"] }
                        ]
                    },
                    new()
                    {
                        Name = "Azad Jammu & Kashmir",
                        Code = "AJK",
                        Cities =
                        [
                            new() { Name = "Muzaffarabad", Latitude = 34.3700, Longitude = 73.4708, BoundingBox = new(34.31, 73.41, 34.43, 73.54), SuggestedNiches = ["Corporate & Legal Advisory", "Tourism & Lodging", "Handicrafts & Kashmiri Shawls"] },
                            new() { Name = "Mirpur", Latitude = 33.1484, Longitude = 73.7519, BoundingBox = new(33.09, 73.68, 33.21, 73.82), SuggestedNiches = ["Overseas Expatriate Real Estate", "Modern Shopping Plazas", "Textile & Garment Mills", "Private Healthcare"] },
                            new() { Name = "Rawalakot", Latitude = 33.8584, Longitude = 73.7604, BoundingBox = new(33.80, 73.70, 33.92, 73.83), SuggestedNiches = ["Hill Station Resorts", "Educational Institutions", "Agri Farming & Poultry"] },
                            new() { Name = "Kotli", Latitude = 33.5156, Longitude = 73.9019, BoundingBox = new(33.46, 73.85, 33.57, 73.96), SuggestedNiches = ["Overseas Remittance Investments", "Commercial Retail", "Private Hospitals"] },
                            new() { Name = "Bhimber", Latitude = 32.9744, Longitude = 74.0786, BoundingBox = new(32.92, 74.02, 33.03, 74.13), SuggestedNiches = ["Industrial Estate Units", "Agri Produce", "Flour & Oil Mills"] },
                            new() { Name = "Bagh", Latitude = 33.9803, Longitude = 73.7747, BoundingBox = new(33.93, 73.72, 34.03, 73.83), SuggestedNiches = ["Mountain Resorts", "Agro Forestry", "Handmade Kashmiri Embroidery"] },
                            new() { Name = "Haveli", Latitude = 33.9000, Longitude = 74.1000, BoundingBox = new(33.85, 74.05, 33.95, 74.15), SuggestedNiches = ["Timber Mills", "Eco-Tourism", "High Altitude Farming"] },
                            new() { Name = "Pallandri", Latitude = 33.7297, Longitude = 73.6894, BoundingBox = new(33.67, 73.63, 33.78, 73.74), SuggestedNiches = ["Educational Academies", "Pine Wood Craft", "Healthcare Clinics"] },
                            new() { Name = "Athmuqam", Latitude = 34.5878, Longitude = 73.9075, BoundingBox = new(34.53, 73.85, 34.64, 73.96), SuggestedNiches = ["Neelum Valley Tourism", "River Lodges & Hotels", "Trout Fish Farms"] },
                            new() { Name = "Hattian Bala", Latitude = 34.1694, Longitude = 73.7431, BoundingBox = new(34.12, 73.69, 34.22, 73.80), SuggestedNiches = ["Highway Hospitality", "Fruit Orchards", "Small Hydro Installations"] }
                        ]
                    }
                
                ]
            },
            // 2. United States
            new()
            {
                Code = "US",
                Name = "United States",
                FlagEmoji = "🇺🇸",
                States =
                [
                    new()
                    {
                        Name = "California",
                        Code = "CA",
                        Cities =
                        [
                            new() { Name = "Los Angeles", Latitude = 34.0522, Longitude = -118.2437, BoundingBox = new(33.90, -118.50, 34.20, -118.15), SuggestedNiches = ["Entertainment & Media Studios", "Aesthetic Clinics & Plastic Surgery", "E-Commerce Brands", "Tech Startups"] },
                            new() { Name = "San Francisco", Latitude = 37.7749, Longitude = -122.4194, BoundingBox = new(37.70, -122.52, 37.83, -122.35), SuggestedNiches = ["B2B SaaS & AI Startups", "Venture Capital & Advisory", "Specialty Coffee Roasters"] },
                            new() { Name = "San Diego", Latitude = 32.7157, Longitude = -117.1611, BoundingBox = new(32.60, -117.28, 32.85, -117.05), SuggestedNiches = ["Biotech & Life Sciences", "Craft Breweries", "Tourism & Lodging"] },
                            new() { Name = "San Jose", Latitude = 37.3382, Longitude = -121.8863, BoundingBox = new(37.25, -122.00, 37.45, -121.75), SuggestedNiches = ["Semiconductors", "Hardware Engineering", "Cloud Infrastructure"] },
                            new() { Name = "Sacramento", Latitude = 38.5816, Longitude = -121.4944, BoundingBox = new(38.50, -121.58, 38.65, -121.40), SuggestedNiches = ["Government Advisory & Legal", "Healthcare Networks", "Clean Energy Startups"] },
                            new() { Name = "Irvine", Latitude = 33.6846, Longitude = -117.8265, BoundingBox = new(33.62, -117.90, 33.75, -117.75), SuggestedNiches = ["Medical Device Engineering", "Semiconductors", "B2B SaaS"] },
                            new() { Name = "Oakland", Latitude = 37.8044, Longitude = -122.2712, BoundingBox = new(37.74, -122.34, 37.86, -122.20), SuggestedNiches = ["Port & Maritime Trade", "Creative Agencies", "Green Logistics"] },
                            new() { Name = "Long Beach", Latitude = 33.7701, Longitude = -118.1937, BoundingBox = new(33.72, -118.25, 33.84, -118.12), SuggestedNiches = ["Container Shipping & Logistics", "Aerospace Design", "Hospitality"] },
                            new() { Name = "Fresno", Latitude = 36.7468, Longitude = -119.7726, BoundingBox = new(36.68, -119.85, 36.82, -119.70), SuggestedNiches = ["Agribusiness & Packing", "Food Processing", "Commercial Transport"] },
                            new() { Name = "Anaheim", Latitude = 33.8366, Longitude = -117.9143, BoundingBox = new(33.78, -117.98, 33.90, -117.84), SuggestedNiches = ["Tourism & Theme Parks", "Convention Services", "Industrial Manufacturing"] },
                            new() { Name = "Riverside", Latitude = 33.9806, Longitude = -117.3755, BoundingBox = new(33.92, -117.45, 34.04, -117.30), SuggestedNiches = ["Logistics & Warehousing", "Higher Education", "Agri-Tech"] },
                            new() { Name = "Bakersfield", Latitude = 35.3733, Longitude = -119.0187, BoundingBox = new(35.30, -119.10, 35.45, -118.94), SuggestedNiches = ["Oilfield & Energy Services", "Commercial Agriculture", "Food Packaging"] },
                            new() { Name = "Santa Ana", Latitude = 33.7455, Longitude = -117.8677, BoundingBox = new(33.70, -117.92, 33.80, -117.82), SuggestedNiches = ["Corporate Legal Services", "Aerospace Manufacturing", "Healthcare Clinics"] }
                        ]
                    },
                    new()
                    {
                        Name = "New York",
                        Code = "NY",
                        Cities =
                        [
                            new() { Name = "New York City", Latitude = 40.7128, Longitude = -74.0060, BoundingBox = new(40.58, -74.15, 40.85, -73.85), SuggestedNiches = ["Corporate Law & Litigation", "Investment Advisory & FinTech", "Commercial Real Estate", "Fine Dining & Bistros"] },
                            new() { Name = "Buffalo", Latitude = 42.8864, Longitude = -78.8784, BoundingBox = new(42.82, -78.93, 42.96, -78.78), SuggestedNiches = ["Advanced Manufacturing", "Healthcare Providers", "Educational Services"] },
                            new() { Name = "Rochester", Latitude = 43.1566, Longitude = -77.6088, BoundingBox = new(43.10, -77.68, 43.22, -77.53), SuggestedNiches = ["Optics & Imaging Tech", "Health Sciences", "Educational Services"] },
                            new() { Name = "Albany", Latitude = 42.6526, Longitude = -73.7562, BoundingBox = new(42.60, -73.83, 42.70, -73.70), SuggestedNiches = ["Nanotech & Semiconductor R&D", "Public Policy Law", "Healthcare"] },
                            new() { Name = "Syracuse", Latitude = 43.0481, Longitude = -76.1474, BoundingBox = new(43.00, -76.22, 43.10, -76.08), SuggestedNiches = ["Defense Radar & Sensors", "Logistics Hubs", "Higher Education"] },
                            new() { Name = "Yonkers", Latitude = 40.9312, Longitude = -73.8987, BoundingBox = new(40.88, -73.94, 40.98, -73.84), SuggestedNiches = ["Biotech Startups", "Film Production Studios", "Commercial Retail"] },
                            new() { Name = "White Plains", Latitude = 41.0340, Longitude = -73.7629, BoundingBox = new(40.98, -73.82, 41.08, -73.70), SuggestedNiches = ["Corporate HQ Services", "Wealth Management", "Medical Spas"] }
                        ]
                    },
                    new()
                    {
                        Name = "Texas",
                        Code = "TX",
                        Cities =
                        [
                            new() { Name = "Houston", Latitude = 29.7604, Longitude = -95.3698, BoundingBox = new(29.60, -95.55, 29.90, -95.20), SuggestedNiches = ["Energy & Cleantech Services", "Medical Centers & Specialists", "Industrial Logistics & Freight"] },
                            new() { Name = "Austin", Latitude = 30.2672, Longitude = -97.7431, BoundingBox = new(30.15, -97.88, 30.40, -97.62), SuggestedNiches = ["Tech Software Agencies", "Music & Creative Agencies", "Real Estate Developers"] },
                            new() { Name = "Dallas", Latitude = 32.7767, Longitude = -96.7970, BoundingBox = new(32.65, -96.95, 32.90, -96.65), SuggestedNiches = ["Financial Services & Wealth Management", "Telecom & Data Solutions", "Corporate HQ Services"] },
                            new() { Name = "San Antonio", Latitude = 29.4241, Longitude = -98.4936, BoundingBox = new(29.30, -98.65, 29.55, -98.35), SuggestedNiches = ["Cybersecurity & Defense", "Healthcare Networks", "Hospitality & Heritage"] },
                            new() { Name = "Fort Worth", Latitude = 32.7555, Longitude = -97.3308, BoundingBox = new(32.68, -97.42, 32.84, -97.24), SuggestedNiches = ["Aviation & Defense", "Logistics & Distribution", "Agribusiness"] },
                            new() { Name = "El Paso", Latitude = 31.7619, Longitude = -106.4850, BoundingBox = new(31.70, -106.56, 31.84, -106.38), SuggestedNiches = ["Cross-Border Manufacturing", "Bilingual Medical Services", "Logistics"] },
                            new() { Name = "Arlington", Latitude = 32.7357, Longitude = -97.1081, BoundingBox = new(32.68, -97.18, 32.80, -97.02), SuggestedNiches = ["Sports & Entertainment", "Automotive Manufacturing", "Medical Centers"] },
                            new() { Name = "Plano", Latitude = 33.0198, Longitude = -96.6989, BoundingBox = new(32.95, -96.78, 33.09, -96.62), SuggestedNiches = ["Corporate Headquarters", "FinTech & Payments", "Enterprise Software"] },
                            new() { Name = "Corpus Christi", Latitude = 27.8006, Longitude = -97.3964, BoundingBox = new(27.72, -97.48, 27.88, -97.32), SuggestedNiches = ["Deepwater Petrochemical Port", "Marine Science", "Aviation Maintenance"] },
                            new() { Name = "Frisco", Latitude = 33.1507, Longitude = -96.8236, BoundingBox = new(33.10, -96.88, 33.20, -96.76), SuggestedNiches = ["Sports Technology Hub", "Healthtech & Wellness", "Corporate Relocations"] }
                        ]
                    },
                    new()
                    {
                        Name = "Florida",
                        Code = "FL",
                        Cities =
                        [
                            new() { Name = "Miami", Latitude = 25.7617, Longitude = -80.1918, BoundingBox = new(25.68, -80.30, 25.88, -80.12), SuggestedNiches = ["Luxury Real Estate", "Aesthetic Clinics & Wellness", "Hospitality & Yachting"] },
                            new() { Name = "Orlando", Latitude = 28.5383, Longitude = -81.3792, BoundingBox = new(28.40, -81.50, 28.65, -81.25), SuggestedNiches = ["Tourism & Entertainment", "Aviation & Simulation", "Digital Media"] },
                            new() { Name = "Tampa", Latitude = 27.9506, Longitude = -82.4572, BoundingBox = new(27.85, -82.58, 28.08, -82.35), SuggestedNiches = ["Marine Shipping", "Financial Services", "Specialty Healthcare"] },
                            new() { Name = "Jacksonville", Latitude = 30.3322, Longitude = -81.6557, BoundingBox = new(30.22, -81.76, 30.42, -81.55), SuggestedNiches = ["Intermodal Freight Logistics", "Financial Services", "Biomedical Tech"] },
                            new() { Name = "Fort Lauderdale", Latitude = 26.1224, Longitude = -80.1373, BoundingBox = new(26.06, -80.20, 26.20, -80.08), SuggestedNiches = ["Marine Yachting Services", "Aviation Services", "Hospitality & Tourism"] },
                            new() { Name = "St. Petersburg", Latitude = 27.7676, Longitude = -82.6403, BoundingBox = new(27.70, -82.72, 27.84, -82.58), SuggestedNiches = ["Marine Science", "Financial Advisory", "Creative Arts & Tourism"] },
                            new() { Name = "Tallahassee", Latitude = 30.4383, Longitude = -84.2807, BoundingBox = new(30.38, -84.35, 30.50, -84.20), SuggestedNiches = ["State Government Affairs", "Higher Education R&D", "Legal Firms"] }
                        ]
                    },
                    new()
                    {
                        Name = "Illinois",
                        Code = "IL",
                        Cities =
                        [
                            new() { Name = "Chicago", Latitude = 41.8781, Longitude = -87.6298, BoundingBox = new(41.70, -87.80, 42.02, -87.55), SuggestedNiches = ["Logistics & Warehousing", "Commercial Law Practices", "Financial Trading & FinTech"] },
                            new() { Name = "Naperville", Latitude = 41.7508, Longitude = -88.1535, BoundingBox = new(41.70, -88.22, 41.80, -88.08), SuggestedNiches = ["R&D Technology Labs", "Financial Planning", "Corporate Legal"] },
                            new() { Name = "Rockford", Latitude = 42.2711, Longitude = -89.0940, BoundingBox = new(42.20, -89.18, 42.34, -89.00), SuggestedNiches = ["Aerospace Components", "Heavy Tool Manufacturing", "Healthcare"] },
                            new() { Name = "Aurora", Latitude = 41.7606, Longitude = -88.3201, BoundingBox = new(41.70, -88.38, 41.82, -88.24), SuggestedNiches = ["Advanced Manufacturing", "Healthcare Services", "Distribution Centers"] }
                        ]
                    },
                    new()
                    {
                        Name = "Washington",
                        Code = "WA",
                        Cities =
                        [
                            new() { Name = "Seattle", Latitude = 47.6062, Longitude = -122.3321, BoundingBox = new(47.50, -122.45, 47.72, -122.22), SuggestedNiches = ["Cloud Computing & Big Data", "E-Commerce Infrastructure", "Biotechnology"] },
                            new() { Name = "Bellevue", Latitude = 47.6101, Longitude = -122.2015, BoundingBox = new(47.55, -122.26, 47.68, -122.12), SuggestedNiches = ["Cloud Computing & Gaming", "B2B SaaS Ventures", "Wealth Advisory"] },
                            new() { Name = "Spokane", Latitude = 47.6588, Longitude = -117.4260, BoundingBox = new(47.60, -117.50, 47.72, -117.34), SuggestedNiches = ["Health Sciences & Biotech", "Agribusiness Services", "Clean Energy"] },
                            new() { Name = "Tacoma", Latitude = 47.2529, Longitude = -122.4443, BoundingBox = new(47.18, -122.52, 47.30, -122.36), SuggestedNiches = ["Deepwater Port Shipping", "Healthcare Systems", "Wood & Paper Products"] }
                        ]
                    },
                    new()
                    {
                        Name = "Massachusetts",
                        Code = "MA",
                        Cities =
                        [
                            new() { Name = "Boston", Latitude = 42.3601, Longitude = -71.0589, BoundingBox = new(42.28, -71.18, 42.42, -70.95), SuggestedNiches = ["Biopharma & Therapeutics", "Higher Education Tech", "Financial Advisory"] },
                            new() { Name = "Cambridge", Latitude = 42.3736, Longitude = -71.1097, BoundingBox = new(42.34, -71.15, 42.40, -71.07), SuggestedNiches = ["Biotech & Therapeutics", "AI Research Labs", "Venture Capital"] },
                            new() { Name = "Worcester", Latitude = 42.2626, Longitude = -71.8023, BoundingBox = new(42.20, -71.88, 42.32, -71.74), SuggestedNiches = ["Biomedical Engineering", "Advanced Robotics", "Higher Education"] }
                        ]
                    },
                    new()
                    {
                        Name = "Georgia",
                        Code = "GA",
                        Cities =
                        [
                            new() { Name = "Atlanta", Latitude = 33.7490, Longitude = -84.3880, BoundingBox = new(33.65, -84.50, 33.88, -84.28), SuggestedNiches = ["FinTech & Payment Processing", "Logistics & Supply Chain", "Film Production"] },
                            new() { Name = "Savannah", Latitude = 32.0809, Longitude = -81.0912, BoundingBox = new(32.00, -81.18, 32.15, -81.02), SuggestedNiches = ["Container Port Trade", "Aerospace Manufacturing", "Historic Tourism"] },
                            new() { Name = "Augusta", Latitude = 33.4735, Longitude = -82.0105, BoundingBox = new(33.40, -82.10, 33.54, -81.92), SuggestedNiches = ["Cybersecurity & Defense", "Medical Hub Services", "Golf & Hospitality"] }
                        ]
                    },
                    new()
                    {
                        Name = "Colorado",
                        Code = "CO",
                        Cities =
                        [
                            new() { Name = "Denver", Latitude = 39.7392, Longitude = -104.9903, BoundingBox = new(39.62, -105.10, 39.85, -104.85), SuggestedNiches = ["Aerospace & Defense", "Outdoor & Fitness Brands", "Clean Energy"] },
                            new() { Name = "Colorado Springs", Latitude = 38.8339, Longitude = -104.8214, BoundingBox = new(38.75, -104.90, 38.92, -104.72), SuggestedNiches = ["Aerospace & Defense Space", "Sports Organizations", "Cybersecurity"] },
                            new() { Name = "Boulder", Latitude = 40.0150, Longitude = -105.2705, BoundingBox = new(39.96, -105.34, 40.08, -105.20), SuggestedNiches = ["Quantum Computing & AI", "Natural Foods Brands", "Cleantech Labs"] }
                        ]
                    },
                    new()
                    {
                        Name = "Arizona",
                        Code = "AZ",
                        Cities =
                        [
                            new() { Name = "Phoenix", Latitude = 33.4484, Longitude = -112.0740, BoundingBox = new(33.30, -112.25, 33.60, -111.90), SuggestedNiches = ["Semiconductor Megafabs", "Real Estate & Construction", "Healthcare Systems"] },
                            new() { Name = "Tucson", Latitude = 32.2226, Longitude = -110.9747, BoundingBox = new(32.15, -111.05, 32.32, -110.88), SuggestedNiches = ["Optics & Photonics", "Aerospace Defense", "Mining Technology"] },
                            new() { Name = "Mesa", Latitude = 33.4152, Longitude = -111.8315, BoundingBox = new(33.35, -111.90, 33.48, -111.75), SuggestedNiches = ["Aviation & Aerospace", "Healthcare Services", "Education Technology"] },
                            new() { Name = "Scottsdale", Latitude = 33.4942, Longitude = -111.9261, BoundingBox = new(33.42, -111.98, 33.62, -111.85), SuggestedNiches = ["Healthcare Tech & Medical Spas", "Luxury Hospitality", "B2B Software"] }
                        ]
                    },
                    new()
                    {
                        Name = "Nevada",
                        Code = "NV",
                        Cities =
                        [
                            new() { Name = "Las Vegas", Latitude = 36.1699, Longitude = -115.1398, BoundingBox = new(36.05, -115.30, 36.28, -115.05), SuggestedNiches = ["Hospitality & Gaming", "Convention & Event Services", "Entertainment Tech"] },
                            new() { Name = "Reno", Latitude = 39.5296, Longitude = -119.8138, BoundingBox = new(39.45, -119.88, 39.60, -119.74), SuggestedNiches = ["Battery Gigafactories & Clean Tech", "Logistics Warehousing", "Tourism"] }
                        ]
                    },
                    new()
                    {
                        Name = "Pennsylvania",
                        Code = "PA",
                        Cities =
                        [
                            new() { Name = "Philadelphia", Latitude = 39.9526, Longitude = -75.1652, BoundingBox = new(39.88, -75.28, 40.05, -75.05), SuggestedNiches = ["Cell & Gene Therapy Biotech", "Financial Banking Services", "Higher Education"] },
                            new() { Name = "Pittsburgh", Latitude = 40.4406, Longitude = -79.9959, BoundingBox = new(40.38, -80.08, 40.50, -79.88), SuggestedNiches = ["Autonomous Robotics & AI", "Healthcare Networks", "Advanced Materials"] },
                            new() { Name = "Allentown", Latitude = 40.6084, Longitude = -75.4902, BoundingBox = new(40.55, -75.56, 40.66, -75.42), SuggestedNiches = ["Industrial Gases & Chemicals", "Logistics Hubs", "Manufacturing"] }
                        ]
                    },
                    new()
                    {
                        Name = "Ohio",
                        Code = "OH",
                        Cities =
                        [
                            new() { Name = "Columbus", Latitude = 39.9612, Longitude = -82.9988, BoundingBox = new(39.88, -83.12, 40.08, -82.88), SuggestedNiches = ["E-Commerce & Retail HQ", "Insurance & FinTech", "Data Centers"] },
                            new() { Name = "Cleveland", Latitude = 41.4993, Longitude = -81.6944, BoundingBox = new(41.42, -81.80, 41.58, -81.60), SuggestedNiches = ["World-Class Medical Care", "Advanced Manufacturing", "Polymers & Chemicals"] },
                            new() { Name = "Cincinnati", Latitude = 39.1031, Longitude = -84.5120, BoundingBox = new(39.04, -84.60, 39.18, -84.42), SuggestedNiches = ["Consumer Goods Innovation", "Aerospace Turbines", "Financial Services"] }
                        ]
                    },
                    new()
                    {
                        Name = "Michigan",
                        Code = "MI",
                        Cities =
                        [
                            new() { Name = "Detroit", Latitude = 42.3314, Longitude = -83.0458, BoundingBox = new(42.25, -83.18, 42.45, -82.95), SuggestedNiches = ["Automotive & EV Mobility", "FinTech & Mortgage Lending", "Robotics"] },
                            new() { Name = "Grand Rapids", Latitude = 42.9634, Longitude = -85.6681, BoundingBox = new(42.90, -85.74, 43.02, -85.60), SuggestedNiches = ["Medical Device Manufacturing", "Office Furniture Design", "Food & Beverage"] },
                            new() { Name = "Ann Arbor", Latitude = 42.2808, Longitude = -83.7430, BoundingBox = new(42.24, -83.80, 42.32, -83.68), SuggestedNiches = ["Autonomous Vehicle Software", "Life Sciences R&D", "Venture Capital"] }
                        ]
                    },
                    new()
                    {
                        Name = "North Carolina",
                        Code = "NC",
                        Cities =
                        [
                            new() { Name = "Charlotte", Latitude = 35.2271, Longitude = -80.8431, BoundingBox = new(35.15, -80.95, 35.32, -80.72), SuggestedNiches = ["Commercial Banking Capital", "Energy Consulting & Cleantech", "Motorsports Tech"] },
                            new() { Name = "Raleigh", Latitude = 35.7796, Longitude = -78.6382, BoundingBox = new(35.70, -78.75, 35.88, -78.55), SuggestedNiches = ["Research Triangle Biotech", "Enterprise Open-Source Software", "Agritech"] },
                            new() { Name = "Greensboro", Latitude = 36.0726, Longitude = -79.7920, BoundingBox = new(36.00, -79.88, 36.14, -79.70), SuggestedNiches = ["Aviation & Jet Maintenance", "Supply Chain Logistics", "Textile Tech"] }
                        ]
                    }
                ]
            },

            // 3. United Kingdom
            new()
            {
                Code = "GB",
                Name = "United Kingdom",
                FlagEmoji = "🇬🇧",
                States =
                [
                    new()
                    {
                        Name = "England",
                        Code = "ENG",
                        Cities =
                        [
                            new() { Name = "London", Latitude = 51.5074, Longitude = -0.1278, BoundingBox = new(51.40, -0.30, 51.62, 0.05), SuggestedNiches = ["FinTech & Wealth Management", "Boutique Law Practices", "Specialty Dental Clinics", "Luxury D2C Brands"] },
                            new() { Name = "Manchester", Latitude = 53.4808, Longitude = -2.2426, BoundingBox = new(53.42, -2.32, 53.54, -2.16), SuggestedNiches = ["Digital Marketing Agencies", "E-Commerce Warehousing", "Sports & Fitness Brands"] },
                            new() { Name = "Birmingham", Latitude = 52.4862, Longitude = -1.8904, BoundingBox = new(52.40, -2.00, 52.55, -1.78), SuggestedNiches = ["Jewellery & Precious Metals", "Automotive Engineering", "Healthcare Services"] },
                            new() { Name = "Leeds", Latitude = 53.8008, Longitude = -1.5491, BoundingBox = new(53.72, -1.65, 53.88, -1.45), SuggestedNiches = ["Legal Services", "Financial Technology", "HealthTech & Diagnostics"] },
                            new() { Name = "Liverpool", Latitude = 53.4084, Longitude = -2.9916, BoundingBox = new(53.35, -3.05, 53.48, -2.90), SuggestedNiches = ["Maritime Trade", "Life Sciences", "Creative Industries"] },
                            new() { Name = "Bristol", Latitude = 51.4545, Longitude = -2.5879, BoundingBox = new(51.40, -2.68, 51.52, -2.50), SuggestedNiches = ["Aerospace Engineering", "Creative & Media Hub", "Environmental Tech"] },
                            new() { Name = "Newcastle", Latitude = 54.9783, Longitude = -1.6178, BoundingBox = new(54.92, -1.72, 55.05, -1.52), SuggestedNiches = ["Digital Software", "Offshore Energy", "Healthcare Science"] },
                            new() { Name = "Sheffield", Latitude = 53.3811, Longitude = -1.4701, BoundingBox = new(53.32, -1.55, 53.45, -1.38), SuggestedNiches = ["Advanced Metallurgy & Forging", "Health & Medical Robotics", "Digital Studios"] },
                            new() { Name = "Nottingham", Latitude = 52.9548, Longitude = -1.1581, BoundingBox = new(52.90, -1.22, 53.02, -1.08), SuggestedNiches = ["Life Sciences & Pharma", "FinTech & Data Analytics", "Creative Digital"] },
                            new() { Name = "Leicester", Latitude = 52.6369, Longitude = -1.1398, BoundingBox = new(52.58, -1.20, 52.70, -1.06), SuggestedNiches = ["Space Technology R&D", "Textile & Garment Design", "Food Manufacturing"] },
                            new() { Name = "Cambridge", Latitude = 52.2053, Longitude = 0.1218, BoundingBox = new(52.16, 0.05, 52.26, 0.18), SuggestedNiches = ["Biotech & Genomics Clusters", "AI & Deeptech", "IP Law & Patent Firms"] },
                            new() { Name = "Oxford", Latitude = 51.7520, Longitude = -1.2577, BoundingBox = new(51.70, -1.32, 51.80, -1.18), SuggestedNiches = ["Vaccines & Therapeutics", "Quantum & Fusion Tech", "Publishing & EdTech"] },
                            new() { Name = "Southampton", Latitude = 50.9097, Longitude = -1.4044, BoundingBox = new(50.85, -1.48, 50.96, -1.32), SuggestedNiches = ["Deepwater Cruise & Cargo Ports", "Oceanography Marine Tech", "Logistics"] },
                            new() { Name = "Coventry", Latitude = 52.4068, Longitude = -1.5197, BoundingBox = new(52.36, -1.58, 52.46, -1.45), SuggestedNiches = ["Electric Vehicle Engineering", "Connected Mobility", "Higher Education"] }
                        ]
                    },
                    new()
                    {
                        Name = "Scotland",
                        Code = "SCT",
                        Cities =
                        [
                            new() { Name = "Edinburgh", Latitude = 55.9533, Longitude = -3.1883, BoundingBox = new(55.88, -3.30, 56.00, -3.10), SuggestedNiches = ["Financial & Asset Management", "Tourism & Heritage Lodging", "Tech Startups"] },
                            new() { Name = "Glasgow", Latitude = 55.8642, Longitude = -4.2518, BoundingBox = new(55.80, -4.38, 55.92, -4.15), SuggestedNiches = ["Engineering & Manufacturing", "Space Technology", "Higher Education"] },
                            new() { Name = "Aberdeen", Latitude = 57.1497, Longitude = -2.0943, BoundingBox = new(57.10, -2.18, 57.20, -2.02), SuggestedNiches = ["Offshore Wind & Renewables", "Subsea Engineering", "Marine Logistics"] },
                            new() { Name = "Dundee", Latitude = 56.4620, Longitude = -2.9707, BoundingBox = new(56.42, -3.05, 56.52, -2.88), SuggestedNiches = ["Video Game Development", "Biomedical R&D", "Creative Design Studios"] },
                            new() { Name = "Inverness", Latitude = 57.4778, Longitude = -4.2247, BoundingBox = new(57.42, -4.30, 57.52, -4.15), SuggestedNiches = ["Highlands Tourism & Lodging", "Renewable Hydro Energy", "Distillery Commerce"] }
                        ]
                    },
                    new()
                    {
                        Name = "Wales",
                        Code = "WLS",
                        Cities =
                        [
                            new() { Name = "Cardiff", Latitude = 51.4816, Longitude = -3.1791, BoundingBox = new(51.42, -3.28, 51.55, -3.10), SuggestedNiches = ["Media & Broadcasting", "Financial Services", "Medical Diagnostics"] },
                            new() { Name = "Swansea", Latitude = 51.6214, Longitude = -3.9436, BoundingBox = new(51.58, -4.02, 51.68, -3.88), SuggestedNiches = ["Marine Energy & Engineering", "MedTech", "Tourism & Coastal Hospitality"] },
                            new() { Name = "Newport", Latitude = 51.5842, Longitude = -2.9977, BoundingBox = new(51.52, -3.05, 51.64, -2.92), SuggestedNiches = ["Compound Semiconductor Cluster", "Data Centers", "Manufacturing"] }
                        ]
                    },
                    new()
                    {
                        Name = "Northern Ireland",
                        Code = "NIR",
                        Cities =
                        [
                            new() { Name = "Belfast", Latitude = 54.5973, Longitude = -5.9301, BoundingBox = new(54.52, -6.02, 54.68, -5.85), SuggestedNiches = ["Cybersecurity", "FinTech", "Film Production Studios"] },
                            new() { Name = "Derry", Latitude = 54.9966, Longitude = -7.3086, BoundingBox = new(54.95, -7.38, 55.05, -7.24), SuggestedNiches = ["Software Development", "Advanced Manufacturing", "Tourism & Culture"] },
                            new() { Name = "Lisburn", Latitude = 54.5167, Longitude = -6.0333, BoundingBox = new(54.48, -6.08, 54.56, -5.98), SuggestedNiches = ["Precision Engineering", "Commercial Retail Hubs", "Transport Fleets"] }
                        ]
                    }
                ]
            },

            // 4. United Arab Emirates
            new()
            {
                Code = "AE",
                Name = "United Arab Emirates",
                FlagEmoji = "🇦🇪",
                States =
                [
                    new()
                    {
                        Name = "Dubai",
                        Code = "DXB",
                        Cities =
                        [
                            new() { Name = "Dubai", Latitude = 25.2048, Longitude = 55.2708, BoundingBox = new(25.05, 55.10, 25.35, 55.45), SuggestedNiches = ["Luxury Real Estate Brokers", "Business Setup & Corporate Services", "High-End Cosmetic Clinics", "Tourism & Luxury Car Rentals"] },
                            new() { Name = "Deira", Latitude = 25.2697, Longitude = 55.3095, BoundingBox = new(25.24, 55.28, 25.30, 55.34), SuggestedNiches = ["Gold & Diamond Trading", "Wholesale Electronics", "Import Export Agencies"] },
                            new() { Name = "Business Bay", Latitude = 25.1857, Longitude = 55.2673, BoundingBox = new(25.16, 55.24, 25.21, 55.29), SuggestedNiches = ["Corporate Headquarters", "International Law Firms", "FinTech & Crypto Funds"] },
                            new() { Name = "Jumeirah", Latitude = 25.2000, Longitude = 55.2400, BoundingBox = new(25.14, 55.20, 25.24, 55.28), SuggestedNiches = ["Ultra-Luxury Medical Aesthetics", "Boutique Hospitality", "Fine Dining Restaurants"] },
                            new() { Name = "Dubai Marina", Latitude = 25.0805, Longitude = 55.1403, BoundingBox = new(25.05, 55.12, 25.12, 55.16), SuggestedNiches = ["Yacht Chartering", "High-Rise Commercial Real Estate", "Nightlife Hospitality"] }
                        ]
                    },
                    new()
                    {
                        Name = "Abu Dhabi",
                        Code = "AUH",
                        Cities =
                        [
                            new() { Name = "Abu Dhabi", Latitude = 24.4539, Longitude = 54.3773, BoundingBox = new(24.35, 54.28, 24.55, 54.50), SuggestedNiches = ["Oil & Gas Contracting", "Government Advisory", "Healthcare & Specialty Hospitals"] },
                            new() { Name = "Al Ain", Latitude = 24.2075, Longitude = 55.7447, BoundingBox = new(24.12, 55.65, 24.28, 55.85), SuggestedNiches = ["Agricultural Tech", "Educational Institutes", "Heritage Tourism"] },
                            new() { Name = "Al Dhafra", Latitude = 23.6500, Longitude = 53.7000, BoundingBox = new(23.55, 53.50, 23.80, 53.90), SuggestedNiches = ["Petrochemical Industry", "Solar Power Farms", "Logistics"] },
                            new() { Name = "Mussafah", Latitude = 24.3400, Longitude = 54.4900, BoundingBox = new(24.30, 54.44, 24.38, 54.54), SuggestedNiches = ["Heavy Industrial Manufacturing", "Automotive Workshops", "Building Materials"] }
                        ]
                    },
                    new()
                    {
                        Name = "Sharjah",
                        Code = "SHJ",
                        Cities =
                        [
                            new() { Name = "Sharjah", Latitude = 25.3463, Longitude = 55.4209, BoundingBox = new(25.28, 55.35, 25.42, 55.50), SuggestedNiches = ["Publishing & Education", "Industrial Manufacturing", "Wholesale Trading"] },
                            new() { Name = "Khor Fakkan", Latitude = 25.3375, Longitude = 56.3420, BoundingBox = new(25.30, 56.30, 25.38, 56.38), SuggestedNiches = ["Deep Sea Container Port", "Maritime Logistics", "Eco-Tourism & Resorts"] },
                            new() { Name = "Kalba", Latitude = 25.0333, Longitude = 56.3500, BoundingBox = new(25.00, 56.30, 25.08, 56.40), SuggestedNiches = ["Mangrove Eco-Tourism", "Marine Trading", "Agricultural Greenhouses"] }
                        ]
                    },
                    new()
                    {
                        Name = "Ajman",
                        Code = "AJM",
                        Cities =
                        [
                            new() { Name = "Ajman", Latitude = 25.4052, Longitude = 55.5136, BoundingBox = new(25.35, 55.45, 25.45, 55.58), SuggestedNiches = ["Ship Repair & Marine", "Textile Trading", "Real Estate Development"] }
                        ]
                    },
                    new()
                    {
                        Name = "Ras Al Khaimah",
                        Code = "RAK",
                        Cities =
                        [
                            new() { Name = "Ras Al Khaimah", Latitude = 25.7895, Longitude = 55.9432, BoundingBox = new(25.70, 55.88, 25.88, 56.02), SuggestedNiches = ["Ceramics & Building Materials", "Hospitality Resorts", "Pharmaceuticals"] },
                            new() { Name = "Al Hamra", Latitude = 25.6800, Longitude = 55.7800, BoundingBox = new(25.65, 55.75, 25.72, 55.82), SuggestedNiches = ["Luxury Golf Resorts", "Free Zone Tech Entities", "Marina Services"] }
                        ]
                    },
                    new()
                    {
                        Name = "Fujairah",
                        Code = "FUJ",
                        Cities =
                        [
                            new() { Name = "Fujairah", Latitude = 25.1288, Longitude = 56.3265, BoundingBox = new(25.08, 56.26, 25.18, 56.38), SuggestedNiches = ["Bunkering & Oil Storage", "Maritime Services", "Mining & Rock Quarries"] },
                            new() { Name = "Dibba Al-Fujairah", Latitude = 25.6196, Longitude = 56.2625, BoundingBox = new(25.56, 56.20, 25.68, 56.32), SuggestedNiches = ["Fisheries & Marine Export", "Resort Hospitality", "Port Logistics"] }
                        ]
                    },
                    new()
                    {
                        Name = "Umm Al Quwain",
                        Code = "UAQ",
                        Cities =
                        [
                            new() { Name = "Umm Al Quwain", Latitude = 25.5653, Longitude = 55.5533, BoundingBox = new(25.50, 55.50, 25.62, 55.62), SuggestedNiches = ["Free Trade Zone Enterprises", "Boat Building & Marine", "Tourism & Leisure"] }
                        ]
                    }
                ]
            },

            // 5. Canada
            new()
            {
                Code = "CA",
                Name = "Canada",
                FlagEmoji = "🇨🇦",
                States =
                [
                    new()
                    {
                        Name = "Ontario",
                        Code = "ON",
                        Cities =
                        [
                            new() { Name = "Toronto", Latitude = 43.6532, Longitude = -79.3832, BoundingBox = new(43.58, -79.55, 43.78, -79.25), SuggestedNiches = ["FinTech & Banking", "Immigration & Legal Firms", "Specialty Dental Practices"] },
                            new() { Name = "Ottawa", Latitude = 45.4215, Longitude = -75.6972, BoundingBox = new(45.32, -75.85, 45.48, -75.58), SuggestedNiches = ["GovTech Solutions", "Security & Defense Consulting"] },
                            new() { Name = "Mississauga", Latitude = 43.5890, Longitude = -79.6441, BoundingBox = new(43.50, -79.75, 43.68, -79.55), SuggestedNiches = ["Aerospace & Defense", "Pharma Distribution", "Freight Logistics"] },
                            new() { Name = "Hamilton", Latitude = 43.2557, Longitude = -79.8711, BoundingBox = new(43.18, -79.98, 43.32, -79.78), SuggestedNiches = ["Steel & Heavy Industry", "Health Sciences", "Advanced Manufacturing"] },
                            new() { Name = "Brampton", Latitude = 43.7315, Longitude = -79.7624, BoundingBox = new(43.65, -79.85, 43.82, -79.66), SuggestedNiches = ["Logistics & Warehousing Hubs", "Food & Beverage Processing", "Advanced Robotics"] },
                            new() { Name = "London", Latitude = 42.9849, Longitude = -81.2453, BoundingBox = new(42.90, -81.35, 43.05, -81.15), SuggestedNiches = ["Medical Devices & Biotech", "Digital Media", "Financial & Insurance Services"] },
                            new() { Name = "Markham", Latitude = 43.8561, Longitude = -79.3370, BoundingBox = new(43.80, -79.42, 43.92, -79.25), SuggestedNiches = ["High-Tech Semiconductor Hub", "Life Sciences", "Telecommunications"] },
                            new() { Name = "Kitchener", Latitude = 43.4516, Longitude = -80.4925, BoundingBox = new(43.40, -80.56, 43.52, -80.42), SuggestedNiches = ["Quantum Tech & AI Startups", "Advanced Manufacturing", "FinTech Systems"] },
                            new() { Name = "Windsor", Latitude = 42.3149, Longitude = -83.0364, BoundingBox = new(42.25, -83.10, 42.36, -82.95), SuggestedNiches = ["Automotive & Tooling", "Cross-Border Trade", "Automation Engineering"] }
                        ]
                    },
                    new()
                    {
                        Name = "Quebec",
                        Code = "QC",
                        Cities =
                        [
                            new() { Name = "Montreal", Latitude = 45.5017, Longitude = -73.5673, BoundingBox = new(45.42, -73.72, 45.60, -73.45), SuggestedNiches = ["AI & Deep Learning", "Video Game Studios", "Aerospace Manufacturing"] },
                            new() { Name = "Quebec City", Latitude = 46.8139, Longitude = -71.2080, BoundingBox = new(46.75, -71.32, 46.90, -71.12), SuggestedNiches = ["Optics & Photonics", "Public Sector Consulting", "Tourism & Culture"] },
                            new() { Name = "Laval", Latitude = 45.6066, Longitude = -73.7124, BoundingBox = new(45.54, -73.80, 45.68, -73.62), SuggestedNiches = ["Biopharmaceutical Hub", "Industrial Engineering", "Agri-Food Distribution"] },
                            new() { Name = "Gatineau", Latitude = 45.4765, Longitude = -75.7013, BoundingBox = new(45.42, -75.78, 45.54, -75.62), SuggestedNiches = ["GovTech Solutions", "Telecommunications", "Digital Media Studios"] }
                        ]
                    },
                    new()
                    {
                        Name = "British Columbia",
                        Code = "BC",
                        Cities =
                        [
                            new() { Name = "Vancouver", Latitude = 49.2827, Longitude = -123.1207, BoundingBox = new(49.20, -123.25, 49.32, -123.02), SuggestedNiches = ["CleanTech & Sustainability", "Film & VFX Studios", "Real Estate Developers"] },
                            new() { Name = "Victoria", Latitude = 48.4284, Longitude = -123.3656, BoundingBox = new(48.38, -123.45, 48.50, -123.30), SuggestedNiches = ["Marine Tech", "Tourism", "Public Sector"] },
                            new() { Name = "Surrey", Latitude = 49.1913, Longitude = -122.8490, BoundingBox = new(49.10, -122.92, 49.25, -122.75), SuggestedNiches = ["Clean Energy Tech", "Health Technology District", "Agri-Tech & Food"] },
                            new() { Name = "Burnaby", Latitude = 49.2488, Longitude = -122.9805, BoundingBox = new(49.20, -123.05, 49.30, -122.90), SuggestedNiches = ["Film & Digital VFX", "Wireless Tech & Robotics", "Biotechnology"] },
                            new() { Name = "Richmond", Latitude = 49.1666, Longitude = -123.1336, BoundingBox = new(49.12, -123.20, 49.22, -123.05), SuggestedNiches = ["Aviation Services & Cargo", "E-Commerce Trade", "High-Tech Manufacturing"] },
                            new() { Name = "Kelowna", Latitude = 49.8880, Longitude = -119.4960, BoundingBox = new(49.82, -119.56, 49.95, -119.40), SuggestedNiches = ["Wine & Agri-Tourism", "Aviation Engineering", "Digital Media"] }
                        ]
                    },
                    new()
                    {
                        Name = "Alberta",
                        Code = "AB",
                        Cities =
                        [
                            new() { Name = "Calgary", Latitude = 51.0447, Longitude = -114.0719, BoundingBox = new(50.92, -114.22, 51.15, -113.92), SuggestedNiches = ["Energy & Utilities", "Agribusiness", "Transportation & Logistics"] },
                            new() { Name = "Edmonton", Latitude = 53.5461, Longitude = -113.4938, BoundingBox = new(53.42, -113.65, 53.65, -113.35), SuggestedNiches = ["Petrochemicals", "Health Tech", "Environmental Services"] },
                            new() { Name = "Red Deer", Latitude = 52.2681, Longitude = -113.8112, BoundingBox = new(52.20, -113.88, 52.34, -113.72), SuggestedNiches = ["Petrochemical Services", "Agricultural Logistics", "Manufacturing"] },
                            new() { Name = "Lethbridge", Latitude = 49.6956, Longitude = -112.8451, BoundingBox = new(49.64, -112.92, 49.76, -112.76), SuggestedNiches = ["Agri-Food Processing Hub", "Higher Education Tech", "Commercial Trade"] }
                        ]
                    }
                ]
            },

            // 6. Saudi Arabia
            new()
            {
                Code = "SA",
                Name = "Saudi Arabia",
                FlagEmoji = "🇸🇦",
                States =
                [
                    new()
                    {
                        Name = "Riyadh Region",
                        Code = "RUH",
                        Cities =
                        [
                            new() { Name = "Riyadh", Latitude = 24.7136, Longitude = 46.6753, BoundingBox = new(24.55, 46.52, 24.88, 46.85), SuggestedNiches = ["Vision 2030 Contracting", "FinTech & Digital Payments", "Hospitality & Event Management", "Specialty Healthcare"] },
                            new() { Name = "Al Kharj", Latitude = 24.1554, Longitude = 47.3119, BoundingBox = new(24.08, 47.22, 24.24, 47.40), SuggestedNiches = ["Dairy & Poultry Mega-Farms", "Food Processing", "Industrial Manufacturing"] },
                            new() { Name = "Ad Diriyah", Latitude = 24.7337, Longitude = 46.5755, BoundingBox = new(24.68, 46.52, 24.78, 46.62), SuggestedNiches = ["Heritage Culture & Luxury Hotels", "Fine Dining & Cafes", "Creative Arts Hubs"] },
                            new() { Name = "Al Majma'ah", Latitude = 25.9000, Longitude = 45.3333, BoundingBox = new(25.84, 45.26, 25.96, 45.40), SuggestedNiches = ["Higher Education Academies", "Commercial Transport", "Agri Trading"] }
                        ]
                    },
                    new()
                    {
                        Name = "Makkah Region",
                        Code = "MKH",
                        Cities =
                        [
                            new() { Name = "Jeddah", Latitude = 21.5433, Longitude = 39.1728, BoundingBox = new(21.40, 39.05, 21.72, 39.30), SuggestedNiches = ["Port & Maritime Logistics", "Hospitality & Hotels", "Retail Megamalls"] },
                            new() { Name = "Mecca", Latitude = 21.3891, Longitude = 39.8579, BoundingBox = new(21.30, 39.75, 21.48, 39.95), SuggestedNiches = ["Pilgrimage Logistics", "Hotel Management", "Food & Beverage Distribution"] },
                            new() { Name = "Taif", Latitude = 21.4373, Longitude = 40.5127, BoundingBox = new(21.35, 40.40, 21.52, 40.60), SuggestedNiches = ["Agriculture & Rose Water", "Tourism Resorts", "Honey Trading"] },
                            new() { Name = "Rabigh", Latitude = 22.7986, Longitude = 39.0349, BoundingBox = new(22.72, 38.96, 22.88, 39.10), SuggestedNiches = ["Petrochemical Refining", "Plastics Industry", "King Abdullah Economic City Services"] }
                        ]
                    },
                    new()
                    {
                        Name = "Eastern Province",
                        Code = "EAS",
                        Cities =
                        [
                            new() { Name = "Dammam", Latitude = 26.4207, Longitude = 50.0888, BoundingBox = new(26.32, 49.98, 26.52, 50.18), SuggestedNiches = ["Oilfield Services", "Industrial Equipment", "Import & Export Logistics"] },
                            new() { Name = "Khobar", Latitude = 26.2172, Longitude = 50.1971, BoundingBox = new(26.12, 50.10, 26.30, 50.28), SuggestedNiches = ["Corporate Consulting", "IT Solutions", "Commercial Real Estate"] },
                            new() { Name = "Jubail", Latitude = 27.0046, Longitude = 49.6601, BoundingBox = new(26.92, 49.52, 27.12, 49.75), SuggestedNiches = ["Petrochemical Refining", "Industrial Construction", "Plastics"] },
                            new() { Name = "Dhahran", Latitude = 26.2361, Longitude = 50.0393, BoundingBox = new(26.18, 49.98, 26.30, 50.10), SuggestedNiches = ["Aramco Petroleum Tech", "Energy Research Centers", "Consulting Engineers"] },
                            new() { Name = "Al Ahsa", Latitude = 25.3835, Longitude = 49.5862, BoundingBox = new(25.30, 49.50, 25.48, 49.68), SuggestedNiches = ["Date Palm Mega-Export", "Oasis Heritage Tourism", "Agri Technology"] },
                            new() { Name = "Qatif", Latitude = 26.5654, Longitude = 50.0270, BoundingBox = new(26.50, 49.96, 26.62, 50.08), SuggestedNiches = ["Fisheries & Marine Trading", "Oilfield Contractors", "Commercial Retail"] },
                            new() { Name = "Hafar Al Batin", Latitude = 28.4328, Longitude = 45.9708, BoundingBox = new(28.36, 45.90, 28.50, 46.05), SuggestedNiches = ["Commercial Logistics", "Heavy Transport Fleets", "Livestock Markets"] }
                        ]
                    },
                    new()
                    {
                        Name = "Medina Region",
                        Code = "MED",
                        Cities =
                        [
                            new() { Name = "Medina", Latitude = 24.5247, Longitude = 39.5692, BoundingBox = new(24.42, 39.48, 24.62, 39.68), SuggestedNiches = ["Hospitality & Lodging", "Dates Farming & Export", "Transportation Services"] },
                            new() { Name = "Yanbu", Latitude = 24.0891, Longitude = 38.0637, BoundingBox = new(24.00, 37.95, 24.18, 38.15), SuggestedNiches = ["Industrial Port Hub", "Hydrocarbon Processing", "Desalination & Energy"] },
                            new() { Name = "Al Ula", Latitude = 26.6176, Longitude = 37.9238, BoundingBox = new(26.54, 37.85, 26.70, 38.00), SuggestedNiches = ["Ultra-Luxury Eco Resorts", "Archaeological Tourism", "Heritage Hospitality"] }
                        ]
                    },
                    new()
                    {
                        Name = "Asir Region",
                        Code = "ASR",
                        Cities =
                        [
                            new() { Name = "Abha", Latitude = 18.2164, Longitude = 42.5053, BoundingBox = new(18.15, 42.45, 18.28, 42.58), SuggestedNiches = ["Mountain Tourism & Resorts", "Regional Healthcare Centers", "Agri Trading"] },
                            new() { Name = "Khamis Mushait", Latitude = 18.3000, Longitude = 42.7333, BoundingBox = new(18.22, 42.65, 18.38, 42.82), SuggestedNiches = ["Commercial Wholesale Trading", "Military Logistics", "Apparel & Electronics"] }
                        ]
                    },
                    new()
                    {
                        Name = "Tabuk Region",
                        Code = "TBK",
                        Cities =
                        [
                            new() { Name = "Tabuk", Latitude = 28.3835, Longitude = 36.5662, BoundingBox = new(28.30, 36.48, 28.48, 36.65), SuggestedNiches = ["NEOM Gateway Logistics", "Modern Greenhouse Agriculture", "Renewable Energy"] }
                        ]
                    }
                ]
            },

            // 7. Australia
            new()
            {
                Code = "AU",
                Name = "Australia",
                FlagEmoji = "🇦🇺",
                States =
                [
                    new()
                    {
                        Name = "New South Wales",
                        Code = "NSW",
                        Cities =
                        [
                            new() { Name = "Sydney", Latitude = -33.8688, Longitude = 151.2093, BoundingBox = new(-33.95, 151.05, -33.75, 151.30), SuggestedNiches = ["FinTech & Wealth Management", "Enterprise SaaS", "Corporate Law Practices", "Specialty Medical Clinics"] },
                            new() { Name = "Newcastle", Latitude = -32.9283, Longitude = 151.7817, BoundingBox = new(-32.98, 151.70, -32.88, 151.85), SuggestedNiches = ["Clean Energy Innovation", "Port & Freight Shipping", "Health Research"] },
                            new() { Name = "Wollongong", Latitude = -34.4278, Longitude = 150.8931, BoundingBox = new(-34.48, 150.82, -34.38, 150.96), SuggestedNiches = ["Smart Metallurgy & Materials", "ICT Startups", "Coastal Tourism"] },
                            new() { Name = "Parramatta", Latitude = -33.8150, Longitude = 151.0011, BoundingBox = new(-33.85, 150.95, -33.78, 151.05), SuggestedNiches = ["Corporate Commercial Law", "Financial Banking Services", "Government Centers"] },
                            new() { Name = "Central Coast", Latitude = -33.3211, Longitude = 151.3917, BoundingBox = new(-33.40, 151.25, -33.20, 151.50), SuggestedNiches = ["Food Manufacturing", "Health & Aged Care", "Tourism"] }
                        ]
                    },
                    new()
                    {
                        Name = "Victoria",
                        Code = "VIC",
                        Cities =
                        [
                            new() { Name = "Melbourne", Latitude = -37.8136, Longitude = 144.9631, BoundingBox = new(-37.92, 144.85, -37.70, 145.10), SuggestedNiches = ["Biotech & Clinical Trials", "Creative Design Studios", "Boutique Hospitality & Cafes"] },
                            new() { Name = "Geelong", Latitude = -38.1499, Longitude = 144.3617, BoundingBox = new(-38.20, 144.30, -38.10, 144.42), SuggestedNiches = ["Carbon Fiber & Advanced Composites", "Health Informatics", "Tourism"] },
                            new() { Name = "Ballarat", Latitude = -37.5622, Longitude = 143.8503, BoundingBox = new(-37.62, 143.78, -37.50, 143.92), SuggestedNiches = ["Renewable Energy Tech", "Food Processing", "Heritage Tourism"] },
                            new() { Name = "Bendigo", Latitude = -36.7570, Longitude = 144.2794, BoundingBox = new(-36.82, 144.20, -36.70, 144.35), SuggestedNiches = ["Regional Banking & Finance", "Gold Mining Services", "Manufacturing"] }
                        ]
                    },
                    new()
                    {
                        Name = "Queensland",
                        Code = "QLD",
                        Cities =
                        [
                            new() { Name = "Brisbane", Latitude = -27.4698, Longitude = 153.0251, BoundingBox = new(-27.58, 152.92, -27.35, 153.15), SuggestedNiches = ["Mining & Renewable Energy", "Biomedical Research", "Tourism Operators"] },
                            new() { Name = "Gold Coast", Latitude = -28.0167, Longitude = 153.4000, BoundingBox = new(-28.10, 153.30, -27.90, 153.50), SuggestedNiches = ["Film & Television Studios", "Marine Manufacturing", "Sports Tech & Wellness"] },
                            new() { Name = "Sunshine Coast", Latitude = -26.6500, Longitude = 153.0667, BoundingBox = new(-26.75, 153.00, -26.55, 153.15), SuggestedNiches = ["CleanTech Solutions", "Boutique Hospitality", "Digital Nomads Tech"] },
                            new() { Name = "Townsville", Latitude = -19.2589, Longitude = 146.8169, BoundingBox = new(-19.32, 146.74, -19.20, 146.88), SuggestedNiches = ["Tropical Marine Science", "Critical Minerals Processing", "Defense Hub"] },
                            new() { Name = "Cairns", Latitude = -16.9186, Longitude = 145.7781, BoundingBox = new(-16.98, 145.72, -16.85, 145.82), SuggestedNiches = ["Reef Eco-Tourism", "Aviation Services", "Tropical Agriculture"] }
                        ]
                    },
                    new()
                    {
                        Name = "Western Australia",
                        Code = "WA",
                        Cities =
                        [
                            new() { Name = "Perth", Latitude = -31.9505, Longitude = 115.8605, BoundingBox = new(-32.05, 115.75, -31.85, 116.00), SuggestedNiches = ["Mining & Resources Engineering", "Hydrogen & Green Steel", "Agricultural Export"] },
                            new() { Name = "Fremantle", Latitude = -32.0569, Longitude = 115.7439, BoundingBox = new(-32.10, 115.70, -32.02, 115.78), SuggestedNiches = ["Deepwater Shipping & Maritime", "Hospitality & Breweries", "Arts & Design"] },
                            new() { Name = "Mandurah", Latitude = -32.5269, Longitude = 115.7214, BoundingBox = new(-32.58, 115.68, -32.48, 115.76), SuggestedNiches = ["Tourism & Marine Recreation", "Healthcare Clinics", "Retirement Living"] }
                        ]
                    },
                    new()
                    {
                        Name = "South Australia",
                        Code = "SA",
                        Cities =
                        [
                            new() { Name = "Adelaide", Latitude = -34.9285, Longitude = 138.6007, BoundingBox = new(-35.00, 138.50, -34.85, 138.70), SuggestedNiches = ["Defense & Naval Shipbuilding", "Space Industry R&D", "Wine Export Hubs"] }
                        ]
                    },
                    new()
                    {
                        Name = "Australian Capital Territory",
                        Code = "ACT",
                        Cities =
                        [
                            new() { Name = "Canberra", Latitude = -35.2809, Longitude = 149.1300, BoundingBox = new(-35.35, 149.05, -35.20, 149.20), SuggestedNiches = ["GovTech & Defense Contractors", "Space Systems R&D", "Public Policy Advisory"] }
                        ]
                    }
                ]
            },

            // 8. Germany
            new()
            {
                Code = "DE",
                Name = "Germany",
                FlagEmoji = "🇩🇪",
                States =
                [
                    new()
                    {
                        Name = "Bavaria",
                        Code = "BY",
                        Cities =
                        [
                            new() { Name = "Munich", Latitude = 48.1351, Longitude = 11.5820, BoundingBox = new(48.06, 11.45, 48.22, 11.70), SuggestedNiches = ["Automotive & Mobility Engineering", "Enterprise Software", "Biotech Startups"] },
                            new() { Name = "Nuremberg", Latitude = 49.4521, Longitude = 11.0767, BoundingBox = new(49.38, 11.00, 49.52, 11.16), SuggestedNiches = ["Industrial Automation", "Consumer Electronics", "Logistics & Transport"] },
                            new() { Name = "Augsburg", Latitude = 48.3705, Longitude = 10.8978, BoundingBox = new(48.32, 10.82, 48.42, 10.98), SuggestedNiches = ["Mechatronics & Robotics", "Aerospace Fiber Tech", "Environmental Tech"] },
                            new() { Name = "Regensburg", Latitude = 49.0134, Longitude = 12.1016, BoundingBox = new(48.96, 12.04, 49.08, 12.18), SuggestedNiches = ["Automotive Electronics", "Biopark Health Sciences", "Sensor Technologies"] },
                            new() { Name = "Wurzburg", Latitude = 49.7913, Longitude = 9.9534, BoundingBox = new(49.74, 9.88, 49.85, 10.02), SuggestedNiches = ["Biomedical R&D", "Print & Media Machinery", "Franconian Hospitality"] }
                        ]
                    },
                    new()
                    {
                        Name = "Berlin",
                        Code = "BE",
                        Cities =
                        [
                            new() { Name = "Berlin", Latitude = 52.5200, Longitude = 13.4050, BoundingBox = new(52.42, 13.25, 52.60, 13.55), SuggestedNiches = ["Tech Startups & FinTech", "Creative Studios & Media", "E-Commerce Platforms"] }
                        ]
                    },
                    new()
                    {
                        Name = "North Rhine-Westphalia",
                        Code = "NW",
                        Cities =
                        [
                            new() { Name = "Cologne", Latitude = 50.9375, Longitude = 6.9603, BoundingBox = new(50.88, 6.88, 51.02, 7.05), SuggestedNiches = ["Media & Television Broadcast", "Chemical Industries", "Trade Fair Hospitality"] },
                            new() { Name = "Dusseldorf", Latitude = 51.2277, Longitude = 6.7735, BoundingBox = new(51.18, 6.70, 51.30, 6.85), SuggestedNiches = ["Corporate Consulting", "Fashion & Retail Brands", "Telecommunications"] },
                            new() { Name = "Dortmund", Latitude = 51.5136, Longitude = 7.4653, BoundingBox = new(51.45, 7.38, 51.58, 7.55), SuggestedNiches = ["Logistics Software", "Robotics & Microtech", "E-Commerce Distribution"] },
                            new() { Name = "Essen", Latitude = 51.4556, Longitude = 7.0116, BoundingBox = new(51.40, 6.94, 51.52, 7.08), SuggestedNiches = ["Energy Trading & Cleantech", "Industrial Contracting", "Medical Centers"] },
                            new() { Name = "Duisburg", Latitude = 51.4344, Longitude = 6.7623, BoundingBox = new(51.38, 6.70, 51.50, 6.84), SuggestedNiches = ["World's Largest Inland Port", "Logistics & Multimodal Freight", "Green Steel"] },
                            new() { Name = "Bonn", Latitude = 50.7374, Longitude = 7.0982, BoundingBox = new(50.68, 7.02, 50.78, 7.18), SuggestedNiches = ["Telecommunications HQ", "United Nations Agencies", "Cybersecurity Research"] }
                        ]
                    },
                    new()
                    {
                        Name = "Baden-Wurttemberg",
                        Code = "BW",
                        Cities =
                        [
                            new() { Name = "Stuttgart", Latitude = 48.7758, Longitude = 9.1829, BoundingBox = new(48.70, 9.10, 48.85, 9.28), SuggestedNiches = ["Precision Auto Engineering", "Industrial Sensors", "Clean Energy Mobility"] },
                            new() { Name = "Karlsruhe", Latitude = 49.0069, Longitude = 8.4037, BoundingBox = new(48.95, 8.32, 49.08, 8.48), SuggestedNiches = ["IT Software Clusters", "Energy R&D", "Federal Judicial Services"] },
                            new() { Name = "Mannheim", Latitude = 49.4875, Longitude = 8.4660, BoundingBox = new(49.42, 8.40, 49.55, 8.54), SuggestedNiches = ["Medical Technology Innovation", "Precision Engineering", "Chemical Industry"] },
                            new() { Name = "Heidelberg", Latitude = 49.3988, Longitude = 8.6724, BoundingBox = new(49.35, 8.62, 49.45, 8.74), SuggestedNiches = ["Cancer Research & BioTech", "Software & Cloud Systems", "Scientific Publishing"] }
                        ]
                    },
                    new()
                    {
                        Name = "Hesse",
                        Code = "HE",
                        Cities =
                        [
                            new() { Name = "Frankfurt", Latitude = 50.1109, Longitude = 8.6821, BoundingBox = new(50.02, 8.55, 50.18, 8.78), SuggestedNiches = ["Central Banking & FinTech", "Commercial Real Estate", "Aviation Logistics Hub"] },
                            new() { Name = "Wiesbaden", Latitude = 50.0782, Longitude = 8.2398, BoundingBox = new(50.02, 8.18, 50.14, 8.30), SuggestedNiches = ["Consulting & Insurance HQ", "Medical Spas & Healthcare", "Film Production Studios"] },
                            new() { Name = "Darmstadt", Latitude = 49.8728, Longitude = 8.6512, BoundingBox = new(49.82, 8.58, 49.92, 8.72), SuggestedNiches = ["Space Operations (ESA)", "Merck Pharma & Chemicals", "Cybersecurity (ATHENE)"] }
                        ]
                    },
                    new()
                    {
                        Name = "Hamburg",
                        Code = "HH",
                        Cities =
                        [
                            new() { Name = "Hamburg", Latitude = 53.5511, Longitude = 9.9937, BoundingBox = new(53.45, 9.85, 53.65, 10.15), SuggestedNiches = ["Maritime Port & Logistics", "Renewable Wind Energy", "Civil Aviation (Airbus)"] }
                        ]
                    },
                    new()
                    {
                        Name = "Saxony",
                        Code = "SN",
                        Cities =
                        [
                            new() { Name = "Leipzig", Latitude = 51.3397, Longitude = 12.3731, BoundingBox = new(51.28, 12.28, 51.42, 12.45), SuggestedNiches = ["Air Cargo Logistics Hub", "Automotive Manufacturing (Porsche/BMW)", "Creative Arts"] },
                            new() { Name = "Dresden", Latitude = 51.0504, Longitude = 13.7373, BoundingBox = new(50.98, 13.65, 51.12, 13.82), SuggestedNiches = ["Silicon Saxony Semiconductors", "Optoelectronics", "Material Science"] }
                        ]
                    },
                    new()
                    {
                        Name = "Lower Saxony",
                        Code = "NI",
                        Cities =
                        [
                            new() { Name = "Hanover", Latitude = 52.3759, Longitude = 9.7320, BoundingBox = new(52.30, 9.65, 52.44, 9.82), SuggestedNiches = ["Global Trade Fair Infrastructure", "Automotive Tires & Parts", "Financial Insurance"] },
                            new() { Name = "Braunschweig", Latitude = 52.2689, Longitude = 10.5268, BoundingBox = new(52.22, 10.45, 52.32, 10.58), SuggestedNiches = ["Mobility Research & Railway", "Financial Services", "Biotech"] }
                        ]
                    }
                ]
            },

            // 9. Qatar
            new()
            {
                Code = "QA",
                Name = "Qatar",
                FlagEmoji = "🇶🇦",
                States =
                [
                    new()
                    {
                        Name = "Doha Municipality",
                        Code = "DOH",
                        Cities =
                        [
                            new() { Name = "Doha", Latitude = 25.2854, Longitude = 51.5310, BoundingBox = new(25.20, 51.45, 25.38, 51.60), SuggestedNiches = ["Sovereign & Energy FinTech", "Luxury Hospitality & Malls", "Engineering Consultants"] },
                            new() { Name = "West Bay", Latitude = 25.3200, Longitude = 51.5300, BoundingBox = new(25.30, 51.50, 25.35, 51.55), SuggestedNiches = ["Corporate Headquarters", "International Law Firms", "Embassies & Advisory"] },
                            new() { Name = "The Pearl", Latitude = 25.3700, Longitude = 51.5500, BoundingBox = new(25.35, 51.52, 25.40, 51.58), SuggestedNiches = ["Luxury Waterfront Real Estate", "Fine Dining Restaurants", "Yachting Services"] }
                        ]
                    },
                    new()
                    {
                        Name = "Al Rayyan Municipality",
                        Code = "RAY",
                        Cities =
                        [
                            new() { Name = "Al Rayyan", Latitude = 25.2919, Longitude = 51.4244, BoundingBox = new(25.24, 51.35, 25.34, 51.48), SuggestedNiches = ["Sports Infrastructure & Tech", "Healthcare Centers", "Commercial Distribution"] },
                            new() { Name = "Education City", Latitude = 25.3140, Longitude = 51.4410, BoundingBox = new(25.28, 51.40, 25.34, 51.46), SuggestedNiches = ["Higher Education Academies", "Research Labs", "Innovation Centers"] }
                        ]
                    },
                    new()
                    {
                        Name = "Al Wakrah Municipality",
                        Code = "WAK",
                        Cities =
                        [
                            new() { Name = "Al Wakrah", Latitude = 25.1768, Longitude = 51.6048, BoundingBox = new(25.12, 51.55, 25.22, 51.65), SuggestedNiches = ["Port Logistics & Shipping", "Heritage Souqs & Hospitality", "Commercial Real Estate"] },
                            new() { Name = "Mesaieed", Latitude = 24.9917, Longitude = 51.5489, BoundingBox = new(24.94, 51.50, 25.04, 51.60), SuggestedNiches = ["Petrochemical Refining", "Industrial Steel Plants", "Fertilizer Manufacturing"] }
                        ]
                    },
                    new()
                    {
                        Name = "Al Khor Municipality",
                        Code = "KHO",
                        Cities =
                        [
                            new() { Name = "Al Khor", Latitude = 25.6804, Longitude = 51.5078, BoundingBox = new(25.64, 51.45, 25.74, 51.55), SuggestedNiches = ["Fisheries & Marine Export", "Coastal Hospitality", "Residential Communities"] },
                            new() { Name = "Ras Laffan", Latitude = 25.9000, Longitude = 51.5333, BoundingBox = new(25.85, 51.48, 25.96, 51.58), SuggestedNiches = ["LNG Mega-Export Facilities", "Gas Field Support", "Industrial Marine Services"] }
                        ]
                    },
                    new()
                    {
                        Name = "Al Daayen Municipality",
                        Code = "DAA",
                        Cities =
                        [
                            new() { Name = "Lusail", Latitude = 25.4190, Longitude = 51.5042, BoundingBox = new(25.38, 51.46, 25.46, 51.54), SuggestedNiches = ["Smart City Real Estate", "Entertainment Tech", "FinTech Headquarters"] }
                        ]
                    }
                ]
            },

            // 10. Oman
            new()
            {
                Code = "OM",
                Name = "Oman",
                FlagEmoji = "🇴🇲",
                States =
                [
                    new()
                    {
                        Name = "Muscat Governorate",
                        Code = "MCT",
                        Cities =
                        [
                            new() { Name = "Muscat", Latitude = 23.5880, Longitude = 58.3829, BoundingBox = new(23.50, 58.30, 23.65, 58.55), SuggestedNiches = ["Corporate Banking & Finance", "Oil & Gas Contracting", "Diplomatic Services"] },
                            new() { Name = "Seeb", Latitude = 23.6703, Longitude = 58.1891, BoundingBox = new(23.62, 58.12, 23.72, 58.26), SuggestedNiches = ["Commercial Wholesale Trading", "Logistics & Transport", "Healthcare Clinics"] },
                            new() { Name = "Ruwi", Latitude = 23.5950, Longitude = 58.5420, BoundingBox = new(23.57, 58.52, 23.62, 58.56), SuggestedNiches = ["Commercial Financial District", "Gold & Jewelry Markets", "IT Services"] },
                            new() { Name = "Muttrah", Latitude = 23.6200, Longitude = 58.5600, BoundingBox = new(23.60, 58.54, 23.65, 58.58), SuggestedNiches = ["Cruise Tourism & Hospitality", "Traditional Souq Trading", "Maritime Services"] }
                        ]
                    },
                    new()
                    {
                        Name = "Dhofar Governorate",
                        Code = "DHO",
                        Cities =
                        [
                            new() { Name = "Salalah", Latitude = 17.0151, Longitude = 54.0924, BoundingBox = new(16.95, 54.00, 17.08, 54.18), SuggestedNiches = ["Free Zone Port Logistics", "Frankincense & Perfumes", "Khareef Season Tourism"] },
                            new() { Name = "Taqah", Latitude = 17.0397, Longitude = 54.3986, BoundingBox = new(17.00, 54.35, 17.08, 54.45), SuggestedNiches = ["Fisheries & Marine", "Heritage Tourism", "Agri Produce"] }
                        ]
                    },
                    new()
                    {
                        Name = "Al Batinah North",
                        Code = "BTN",
                        Cities =
                        [
                            new() { Name = "Sohar", Latitude = 24.3461, Longitude = 56.7075, BoundingBox = new(24.28, 56.62, 24.42, 56.78), SuggestedNiches = ["Deep Sea Industrial Port", "Aluminum & Metal Smelting", "Logistics Hubs"] },
                            new() { Name = "Shinas", Latitude = 24.7431, Longitude = 56.4678, BoundingBox = new(24.70, 56.42, 24.80, 56.52), SuggestedNiches = ["Cross-Border Freight Logistics", "Fisheries Trading", "Agriculture"] }
                        ]
                    },
                    new()
                    {
                        Name = "Al Dakhiliyah",
                        Code = "DAK",
                        Cities =
                        [
                            new() { Name = "Nizwa", Latitude = 22.9333, Longitude = 57.5333, BoundingBox = new(22.88, 57.48, 22.98, 57.58), SuggestedNiches = ["Heritage Cultural Tourism", "Silver Crafts & Pottery", "Agri Dates Processing"] }
                        ]
                    }
                ]
            },

            // 11. Kuwait
            new()
            {
                Code = "KW",
                Name = "Kuwait",
                FlagEmoji = "🇰🇼",
                States =
                [
                    new()
                    {
                        Name = "Al Asimah Governorate",
                        Code = "ASI",
                        Cities =
                        [
                            new() { Name = "Kuwait City", Latitude = 29.3759, Longitude = 47.9774, BoundingBox = new(29.30, 47.90, 29.42, 48.05), SuggestedNiches = ["Investment & Sovereign Funds", "Corporate Legal Practices", "Mega-Malls & Retail"] },
                            new() { Name = "Sharq", Latitude = 29.3850, Longitude = 47.9900, BoundingBox = new(29.36, 47.97, 29.41, 48.02), SuggestedNiches = ["Financial Banking Hub", "Consulting & Audit", "FinTech"] },
                            new() { Name = "Shuwaikh", Latitude = 29.3500, Longitude = 47.9300, BoundingBox = new(29.32, 47.90, 29.38, 47.96), SuggestedNiches = ["Industrial Area & Automotive", "Commercial Wholesale", "University Academies"] }
                        ]
                    },
                    new()
                    {
                        Name = "Hawalli Governorate",
                        Code = "HAW",
                        Cities =
                        [
                            new() { Name = "Hawalli", Latitude = 29.3328, Longitude = 48.0281, BoundingBox = new(29.30, 48.00, 29.36, 48.06), SuggestedNiches = ["IT & Computer Hardware Trade", "Private Clinics & Dentistry", "Electronics Retail"] },
                            new() { Name = "Salmiya", Latitude = 29.3344, Longitude = 48.0772, BoundingBox = new(29.30, 48.05, 29.37, 48.10), SuggestedNiches = ["Waterfront Hospitality & Dining", "Shopping Centers", "Aesthetic Clinics"] }
                        ]
                    },
                    new()
                    {
                        Name = "Farwaniya Governorate",
                        Code = "FAR",
                        Cities =
                        [
                            new() { Name = "Farwaniya", Latitude = 29.2783, Longitude = 47.9583, BoundingBox = new(29.24, 47.92, 29.32, 48.00), SuggestedNiches = ["Aviation Cargo & Logistics", "Wholesale Markets", "Commercial Transport"] },
                            new() { Name = "Al-Rai", Latitude = 29.3083, Longitude = 47.9333, BoundingBox = new(29.28, 47.90, 29.33, 47.96), SuggestedNiches = ["The Avenues Retail Ecosystem", "Automotive Dealerships", "Commercial Warehouses"] }
                        ]
                    },
                    new()
                    {
                        Name = "Ahmadi Governorate",
                        Code = "AHM",
                        Cities =
                        [
                            new() { Name = "Al Ahmadi", Latitude = 29.0769, Longitude = 48.0839, BoundingBox = new(29.02, 48.02, 29.12, 48.14), SuggestedNiches = ["KOC Oilfield Operations", "Petrochemical Engineering", "Refinery Services"] },
                            new() { Name = "Fahaheel", Latitude = 29.0833, Longitude = 48.1333, BoundingBox = new(29.05, 48.10, 29.12, 48.16), SuggestedNiches = ["Coastal Commercial Markets", "Industrial Marine Contracting", "Retail Plazas"] }
                        ]
                    }
                ]
            },

            // 12. Bahrain
            new()
            {
                Code = "BH",
                Name = "Bahrain",
                FlagEmoji = "🇧🇭",
                States =
                [
                    new()
                    {
                        Name = "Capital Governorate",
                        Code = "CAP",
                        Cities =
                        [
                            new() { Name = "Manama", Latitude = 26.2285, Longitude = 50.5860, BoundingBox = new(26.20, 50.52, 26.26, 50.62), SuggestedNiches = ["Offshore Financial Banking", "FinTech Regulatory Sandbox", "Corporate Consulting"] },
                            new() { Name = "Seef", Latitude = 26.2400, Longitude = 50.5350, BoundingBox = new(26.22, 50.51, 26.26, 50.56), SuggestedNiches = ["Corporate Headquarters", "Luxury Malls & Cinema", "FinTech Hubs"] },
                            new() { Name = "Juffair", Latitude = 26.2167, Longitude = 50.6000, BoundingBox = new(26.20, 50.58, 26.23, 50.62), SuggestedNiches = ["Expatriate Hospitality", "Fine Dining & Cafes", "Real Estate Rentals"] }
                        ]
                    },
                    new()
                    {
                        Name = "Muharraq Governorate",
                        Code = "MUH",
                        Cities =
                        [
                            new() { Name = "Muharraq", Latitude = 26.2572, Longitude = 50.6119, BoundingBox = new(26.23, 50.58, 26.28, 50.64), SuggestedNiches = ["Aviation Logistics & Hub", "Pearling Heritage Tourism", "Traditional Markets"] },
                            new() { Name = "Amwaj Islands", Latitude = 26.2900, Longitude = 50.6600, BoundingBox = new(26.27, 50.64, 26.31, 50.68), SuggestedNiches = ["Waterfront Luxury Real Estate", "Marina Yachting", "Boutique Hospitality"] }
                        ]
                    },
                    new()
                    {
                        Name = "Northern Governorate",
                        Code = "NOR",
                        Cities =
                        [
                            new() { Name = "Saar", Latitude = 26.1967, Longitude = 50.4850, BoundingBox = new(26.17, 50.46, 26.22, 50.51), SuggestedNiches = ["Expatriate Residential Communities", "Private Schools", "Specialty Clinics"] },
                            new() { Name = "Hamad Town", Latitude = 26.1167, Longitude = 50.5000, BoundingBox = new(26.08, 50.47, 26.15, 50.53), SuggestedNiches = ["Commercial Retail", "Healthcare Clinics", "Automotive Services"] }
                        ]
                    },
                    new()
                    {
                        Name = "Southern Governorate",
                        Code = "SOU",
                        Cities =
                        [
                            new() { Name = "Riffa", Latitude = 26.1300, Longitude = 50.5550, BoundingBox = new(26.08, 50.52, 26.16, 50.58), SuggestedNiches = ["Golf Resorts & Sports", "Healthcare Centers", "Luxury Living"] }
                        ]
                    }
                ]
            },

            // 13. Malaysia
            new()
            {
                Code = "MY",
                Name = "Malaysia",
                FlagEmoji = "🇲🇾",
                States =
                [
                    new()
                    {
                        Name = "Federal Territories",
                        Code = "KUL",
                        Cities =
                        [
                            new() { Name = "Kuala Lumpur", Latitude = 3.1390, Longitude = 101.6869, BoundingBox = new(3.05, 101.60, 3.22, 101.76), SuggestedNiches = ["Islamic Banking & FinTech", "Digital Media & Advertising", "Corporate Law Firms", "Specialty Medical Tourism"] },
                            new() { Name = "Putrajaya", Latitude = 2.9264, Longitude = 101.6964, BoundingBox = new(2.88, 101.65, 2.96, 101.73), SuggestedNiches = ["GovTech Solutions", "Public Sector Consulting", "Eco-Tourism & Conventions"] }
                        ]
                    },
                    new()
                    {
                        Name = "Selangor",
                        Code = "SGR",
                        Cities =
                        [
                            new() { Name = "Petaling Jaya", Latitude = 3.1073, Longitude = 101.6067, BoundingBox = new(3.05, 101.55, 3.15, 101.65), SuggestedNiches = ["Digital Agencies & Tech Hubs", "E-Commerce Startups", "Private Aesthetic Clinics"] },
                            new() { Name = "Shah Alam", Latitude = 3.0738, Longitude = 101.5183, BoundingBox = new(3.02, 101.46, 3.12, 101.56), SuggestedNiches = ["Industrial Automotive Manufacturing", "Chemical Plants", "Higher Education"] },
                            new() { Name = "Subang Jaya", Latitude = 3.0567, Longitude = 101.5851, BoundingBox = new(3.02, 101.55, 3.09, 101.62), SuggestedNiches = ["Commercial Retail Hubs", "Private Colleges", "Medical Diagnostics"] },
                            new() { Name = "Cyberjaya", Latitude = 2.9213, Longitude = 101.6559, BoundingBox = new(2.88, 101.62, 2.95, 101.68), SuggestedNiches = ["Global Data Centers", "Software Engineering Centers", "AI Labs"] }
                        ]
                    },
                    new()
                    {
                        Name = "Penang",
                        Code = "PNG",
                        Cities =
                        [
                            new() { Name = "George Town", Latitude = 5.4141, Longitude = 100.3288, BoundingBox = new(5.36, 100.28, 5.45, 100.35), SuggestedNiches = ["Medical Tourism Hospitals", "Heritage Tourism & Boutiques", "Culinary Cafes"] },
                            new() { Name = "Bayan Lepas", Latitude = 5.2975, Longitude = 100.2592, BoundingBox = new(5.25, 100.22, 5.34, 100.30), SuggestedNiches = ["Semiconductor Silicon Island", "Electronics Manufacturing", "Air Freight Hubs"] }
                        ]
                    },
                    new()
                    {
                        Name = "Johor",
                        Code = "JHR",
                        Cities =
                        [
                            new() { Name = "Johor Bahru", Latitude = 1.4927, Longitude = 103.7414, BoundingBox = new(1.44, 103.68, 1.55, 103.80), SuggestedNiches = ["Cross-Border Singapore Commerce", "Logistics Warehousing", "Health Tourism"] },
                            new() { Name = "Iskandar Puteri", Latitude = 1.4244, Longitude = 103.6428, BoundingBox = new(1.38, 103.60, 1.46, 103.68), SuggestedNiches = ["Smart City Tech", "Entertainment & Education Hubs", "Financial District"] }
                        ]
                    }
                ]
            },

            // 14. Singapore
            new()
            {
                Code = "SG",
                Name = "Singapore",
                FlagEmoji = "🇸🇬",
                States =
                [
                    new()
                    {
                        Name = "Central Region",
                        Code = "CEN",
                        Cities =
                        [
                            new() { Name = "Downtown Core", Latitude = 1.2879, Longitude = 103.8519, BoundingBox = new(1.27, 103.84, 1.30, 103.86), SuggestedNiches = ["Global FinTech & Private Banking", "International Law Practice", "Venture Capital"] },
                            new() { Name = "Orchard", Latitude = 1.3048, Longitude = 103.8318, BoundingBox = new(1.29, 103.82, 1.32, 103.84), SuggestedNiches = ["Luxury Flagship Retail", "High-End Medical Aesthetics", "Fine Dining Hospitality"] },
                            new() { Name = "Marina Bay", Latitude = 1.2830, Longitude = 103.8600, BoundingBox = new(1.27, 103.85, 1.29, 103.87), SuggestedNiches = ["MICE Global Conferences", "Integrated Resorts & Casino", "Financial Towers"] },
                            new() { Name = "Tanjong Pagar", Latitude = 1.2760, Longitude = 103.8440, BoundingBox = new(1.26, 103.83, 1.29, 103.85), SuggestedNiches = ["Tech Enterprise Offices", "Boutique Creative Agencies", "Specialty Coffee"] }
                        ]
                    },
                    new()
                    {
                        Name = "West Region",
                        Code = "WST",
                        Cities =
                        [
                            new() { Name = "Jurong East", Latitude = 1.3329, Longitude = 103.7436, BoundingBox = new(1.31, 103.72, 1.35, 103.76), SuggestedNiches = ["Regional Commercial Hub", "Health & Hospital Systems", "Commercial Retail"] },
                            new() { Name = "One-North", Latitude = 1.2990, Longitude = 103.7870, BoundingBox = new(1.28, 103.77, 1.31, 103.80), SuggestedNiches = ["Biomedical R&D (Biopolis)", "Media & Game Studios (Mediapolis)", "Tech Incubators"] }
                        ]
                    },
                    new()
                    {
                        Name = "East Region",
                        Code = "EST",
                        Cities =
                        [
                            new() { Name = "Changi", Latitude = 1.3550, Longitude = 103.9860, BoundingBox = new(1.33, 103.96, 1.38, 104.01), SuggestedNiches = ["Aviation Cargo & Maintenance", "Aerospace Engineering", "Logistics Megaparks"] }
                        ]
                    }
                ]
            },

            // 15. Ireland
            new()
            {
                Code = "IE",
                Name = "Ireland",
                FlagEmoji = "🇮🇪",
                States =
                [
                    new()
                    {
                        Name = "Leinster",
                        Code = "LPR",
                        Cities =
                        [
                            new() { Name = "Dublin", Latitude = 53.3498, Longitude = -6.2603, BoundingBox = new(53.28, -6.38, 53.42, -6.15), SuggestedNiches = ["Silicon Docks Tech EMEA HQs", "Aviation Finance & Leasing", "Corporate Law Chambers", "Specialty Healthcare"] },
                            new() { Name = "Dun Laoghaire", Latitude = 53.2944, Longitude = -6.1339, BoundingBox = new(53.27, -6.16, 53.31, -6.10), SuggestedNiches = ["Maritime Leisure & Marine Tech", "Creative Agencies", "Healthcare"] },
                            new() { Name = "Swords", Latitude = 53.4557, Longitude = -6.2197, BoundingBox = new(53.42, -6.25, 53.48, -6.18), SuggestedNiches = ["Airport Logistics Hubs", "Chemical & Pharma Distribution", "Commercial Retail"] }
                        ]
                    },
                    new()
                    {
                        Name = "Munster",
                        Code = "MPR",
                        Cities =
                        [
                            new() { Name = "Cork", Latitude = 51.8985, Longitude = -8.4756, BoundingBox = new(51.85, -8.55, 51.94, -8.40), SuggestedNiches = ["Biopharma & Therapeutics Megacluster", "Information Tech Hardware", "Food Sciences"] },
                            new() { Name = "Limerick", Latitude = 52.6638, Longitude = -8.6267, BoundingBox = new(52.62, -8.68, 52.70, -8.56), SuggestedNiches = ["Medical Device Engineering", "Digital Technology", "Higher Education"] },
                            new() { Name = "Waterford", Latitude = 52.2593, Longitude = -7.1101, BoundingBox = new(52.22, -7.16, 52.30, -7.05), SuggestedNiches = ["Precision Engineering", "Crystal & Artisanal Crafts", "Pharma Operations"] }
                        ]
                    },
                    new()
                    {
                        Name = "Connacht",
                        Code = "CPR",
                        Cities =
                        [
                            new() { Name = "Galway", Latitude = 53.2707, Longitude = -9.0568, BoundingBox = new(53.24, -9.12, 53.30, -8.98), SuggestedNiches = ["MedTech Innovation Global Hub", "Creative Film & Arts", "Boutique Hospitality"] },
                            new() { Name = "Sligo", Latitude = 54.2766, Longitude = -8.4761, BoundingBox = new(54.24, -8.52, 54.30, -8.42), SuggestedNiches = ["Precision Tooling", "Pharma Facilities", "Coastal Adventure Tourism"] }
                        ]
                    }
                ]
            },

            // 16. Netherlands
            new()
            {
                Code = "NL",
                Name = "Netherlands",
                FlagEmoji = "🇳🇱",
                States =
                [
                    new()
                    {
                        Name = "North Holland",
                        Code = "NH",
                        Cities =
                        [
                            new() { Name = "Amsterdam", Latitude = 52.3676, Longitude = 4.9041, BoundingBox = new(52.30, 4.80, 52.42, 5.00), SuggestedNiches = ["Global FinTech & Trade Platforms", "Creative Advertising Agencies", "Specialty Dentistry", "Boutique Hospitality"] },
                            new() { Name = "Haarlem", Latitude = 52.3874, Longitude = 4.6462, BoundingBox = new(52.35, 4.60, 52.42, 4.68), SuggestedNiches = ["Digital Media & Publishing", "Sustainable Food Tech", "Fine Dining"] },
                            new() { Name = "Amstelveen", Latitude = 52.3031, Longitude = 4.8714, BoundingBox = new(52.27, 4.83, 52.34, 4.92), SuggestedNiches = ["Corporate European Headquarters", "Accounting & Advisory", "IT Services"] }
                        ]
                    },
                    new()
                    {
                        Name = "South Holland",
                        Code = "ZH",
                        Cities =
                        [
                            new() { Name = "Rotterdam", Latitude = 51.9244, Longitude = 4.4777, BoundingBox = new(51.85, 4.38, 51.98, 4.58), SuggestedNiches = ["Maritime Clean Port Energy", "Cold Chain Logistics", "Architectural Studios"] },
                            new() { Name = "The Hague", Latitude = 52.0705, Longitude = 4.3007, BoundingBox = new(52.02, 4.22, 52.12, 4.38), SuggestedNiches = ["International Law & Arbitration", "GovTech & Peace Labs", "Cybersecurity (Hague Security Delta)"] },
                            new() { Name = "Leiden", Latitude = 52.1601, Longitude = 4.4970, BoundingBox = new(52.13, 4.45, 52.19, 4.54), SuggestedNiches = ["Bio Science Park Cluster", "Pharmaceutical Clinical Trials", "Space Science"] }
                        ]
                    },
                    new()
                    {
                        Name = "North Brabant",
                        Code = "NB",
                        Cities =
                        [
                            new() { Name = "Eindhoven", Latitude = 51.4416, Longitude = 5.4697, BoundingBox = new(51.38, 5.40, 51.50, 5.55), SuggestedNiches = ["Brainport DeepTech & Semiconductors", "Industrial Robotics & Mechatronics", "Design Academy Tech"] },
                            new() { Name = "Tilburg", Latitude = 51.5606, Longitude = 5.0919, BoundingBox = new(51.52, 5.03, 51.60, 5.15), SuggestedNiches = ["Logistics Distribution Hotspot", "Textile Heritage & Design", "Higher Education"] },
                            new() { Name = "Breda", Latitude = 51.5719, Longitude = 4.7683, BoundingBox = new(51.53, 4.71, 51.61, 4.82), SuggestedNiches = ["Video Game & Interactive Media", "Supply Chain Optimization", "Food Production"] }
                        ]
                    },
                    new()
                    {
                        Name = "Utrecht",
                        Code = "UT",
                        Cities =
                        [
                            new() { Name = "Utrecht", Latitude = 52.0907, Longitude = 5.1214, BoundingBox = new(52.04, 5.04, 52.14, 5.18), SuggestedNiches = ["HealthTech & Healthy Urban Living", "Railway & Infrastructure Tech", "Gaming Studios"] }
                        ]
                    }
                ]
            },

            // 17. Turkey
            new()
            {
                Code = "TR",
                Name = "Turkey",
                FlagEmoji = "🇹🇷",
                States =
                [
                    new()
                    {
                        Name = "Marmara Region",
                        Code = "MAR",
                        Cities =
                        [
                            new() { Name = "Istanbul", Latitude = 41.0082, Longitude = 28.9784, BoundingBox = new(40.90, 28.75, 41.15, 29.25), SuggestedNiches = ["Medical Aesthetics & Hair Transplants", "FinTech & E-Commerce Unicorns", "Textile & Haute Fashion", "Maritime Freight Trade"] },
                            new() { Name = "Bursa", Latitude = 40.1885, Longitude = 29.0610, BoundingBox = new(40.12, 28.98, 40.26, 29.18), SuggestedNiches = ["Automotive Assembly & Parts", "Textile & Silk Weaving", "Industrial Food Processing"] },
                            new() { Name = "Kocaeli", Latitude = 40.7654, Longitude = 29.9408, BoundingBox = new(40.70, 29.85, 40.82, 30.05), SuggestedNiches = ["Chemical & Petrochemical Industry", "Port & Freight Terminal", "Heavy Machinery"] }
                        ]
                    },
                    new()
                    {
                        Name = "Central Anatolia",
                        Code = "ICA",
                        Cities =
                        [
                            new() { Name = "Ankara", Latitude = 39.9334, Longitude = 32.8597, BoundingBox = new(39.85, 32.72, 40.02, 33.00), SuggestedNiches = ["Defense Aerospace & Electronics", "GovTech & Legal Advisory", "Biotechnology & Vaccines"] },
                            new() { Name = "Konya", Latitude = 37.8746, Longitude = 32.4932, BoundingBox = new(37.80, 32.42, 37.94, 32.58), SuggestedNiches = ["Agricultural Machinery", "Food & Flour Processing", "Automotive Casting"] }
                        ]
                    },
                    new()
                    {
                        Name = "Aegean Region",
                        Code = "EGE",
                        Cities =
                        [
                            new() { Name = "Izmir", Latitude = 38.4237, Longitude = 27.1428, BoundingBox = new(38.35, 27.05, 38.52, 27.25), SuggestedNiches = ["Renewable Wind Turbine Equipment", "Export Logistics & Food", "Biopharma Labs"] },
                            new() { Name = "Denizli", Latitude = 37.7765, Longitude = 29.0864, BoundingBox = new(37.72, 29.02, 37.84, 29.15), SuggestedNiches = ["Home Textiles & Towel Export", "Travertine Marble Mining", "Copper Wire Industry"] },
                            new() { Name = "Bodrum", Latitude = 37.0344, Longitude = 27.4305, BoundingBox = new(37.00, 27.38, 37.08, 27.48), SuggestedNiches = ["Luxury Yacht Building & Charter", "Boutique Resort Hospitality", "High-End Real Estate"] }
                        ]
                    },
                    new()
                    {
                        Name = "Mediterranean Region",
                        Code = "AKD",
                        Cities =
                        [
                            new() { Name = "Antalya", Latitude = 36.8969, Longitude = 30.7133, BoundingBox = new(36.82, 30.60, 36.98, 30.82), SuggestedNiches = ["Global Tourism & Luxury Resorts", "Agricultural Greenhouse Seedlings", "Yacht Construction"] },
                            new() { Name = "Adana", Latitude = 37.0000, Longitude = 35.3213, BoundingBox = new(36.94, 35.24, 37.06, 35.40), SuggestedNiches = ["Textile & Cotton Ginning", "Citrus & Food Export", "Heavy Industrial Chemicals"] }
                        ]
                    }
                ]
            },

            // 18. Switzerland
            new()
            {
                Code = "CH",
                Name = "Switzerland",
                FlagEmoji = "🇨🇭",
                States =
                [
                    new()
                    {
                        Name = "Zurich",
                        Code = "ZH",
                        Cities =
                        [
                            new() { Name = "Zurich", Latitude = 47.3769, Longitude = 8.5417, BoundingBox = new(47.32, 8.48, 47.43, 8.60), SuggestedNiches = ["Private Banking & Wealth Tech", "AI & Robotics Engineering", "Specialty Medical Diagnostics", "Boutique Hospitality"] },
                            new() { Name = "Winterthur", Latitude = 47.4984, Longitude = 8.7241, BoundingBox = new(47.46, 8.68, 47.54, 8.78), SuggestedNiches = ["Mechatronics & Machinery", "Insurance & Finance", "Biomedical Engineering"] }
                        ]
                    },
                    new()
                    {
                        Name = "Geneva",
                        Code = "GE",
                        Cities =
                        [
                            new() { Name = "Geneva", Latitude = 46.2044, Longitude = 6.1432, BoundingBox = new(46.16, 6.08, 46.25, 6.20), SuggestedNiches = ["Private Wealth Management", "Commodities Trading", "Haute Horlogerie Luxury Watches", "Diplomatic Services"] }
                        ]
                    },
                    new()
                    {
                        Name = "Basel-Stadt",
                        Code = "BS",
                        Cities =
                        [
                            new() { Name = "Basel", Latitude = 47.5596, Longitude = 7.5886, BoundingBox = new(47.52, 7.52, 47.60, 7.65), SuggestedNiches = ["Global Big Pharma HQs", "Agrochemicals & Biotech", "International Art Fair Commerce"] }
                        ]
                    },
                    new()
                    {
                        Name = "Vaud",
                        Code = "VD",
                        Cities =
                        [
                            new() { Name = "Lausanne", Latitude = 46.5197, Longitude = 6.6323, BoundingBox = new(46.48, 6.56, 46.56, 6.70), SuggestedNiches = ["Deeptech Innovations (EPFL)", "Sports Governance HQs", "Food Science Research"] },
                            new() { Name = "Montreux", Latitude = 46.4312, Longitude = 6.9107, BoundingBox = new(46.40, 6.88, 46.46, 6.95), SuggestedNiches = ["Hospitality Management Academies", "Festival & Event Commerce", "Luxury Aesthetics"] }
                        ]
                    },
                    new()
                    {
                        Name = "Zug",
                        Code = "ZG",
                        Cities =
                        [
                            new() { Name = "Zug", Latitude = 47.1662, Longitude = 8.5155, BoundingBox = new(47.13, 8.48, 47.20, 8.55), SuggestedNiches = ["Crypto Valley Blockchain Hub", "Global Commodity Brokerage", "Corporate Legal Structures"] }
                        ]
                    },
                    new()
                    {
                        Name = "Bern",
                        Code = "BE",
                        Cities =
                        [
                            new() { Name = "Bern", Latitude = 46.9480, Longitude = 7.4474, BoundingBox = new(46.90, 7.38, 46.98, 7.50), SuggestedNiches = ["Medical Tech & Implants", "GovTech Solutions", "Telecommunications HQs"] }
                        ]
                    }
                ]
            },

            // 19. France
            new()
            {
                Code = "FR",
                Name = "France",
                FlagEmoji = "🇫🇷",
                States =
                [
                    new()
                    {
                        Name = "Île-de-France",
                        Code = "IDF",
                        Cities =
                        [
                            new() { Name = "Paris", Latitude = 48.8566, Longitude = 2.3522, BoundingBox = new(48.78, 2.22, 48.92, 2.45), SuggestedNiches = ["Haute Couture & Luxury Goods", "FinTech & Private Equity", "Boutique Law Practices", "Fine Dining Gastronomy"] },
                            new() { Name = "Boulogne-Billancourt", Latitude = 48.8357, Longitude = 2.2402, BoundingBox = new(48.81, 2.22, 48.86, 2.26), SuggestedNiches = ["Automotive & Media HQ", "Digital Advertising", "Telecommunications"] },
                            new() { Name = "Saint-Denis", Latitude = 48.9362, Longitude = 2.3574, BoundingBox = new(48.91, 2.32, 48.96, 2.38), SuggestedNiches = ["Audiovisual Film Studios", "Sports Infrastructure", "IT Services"] }
                        ]
                    },
                    new()
                    {
                        Name = "Auvergne-Rhone-Alpes",
                        Code = "ARA",
                        Cities =
                        [
                            new() { Name = "Lyon", Latitude = 45.7640, Longitude = 4.8357, BoundingBox = new(45.70, 4.75, 45.82, 4.92), SuggestedNiches = ["Biopharma & Vaccines Hub", "Gastronomy & Culinary Commerce", "Chemical Engineering"] },
                            new() { Name = "Grenoble", Latitude = 45.1885, Longitude = 5.7245, BoundingBox = new(45.15, 5.68, 45.22, 5.78), SuggestedNiches = ["Microelectronics & Nano Clusters", "Clean Energy Labs", "Scientific Instruments"] }
                        ]
                    },
                    new()
                    {
                        Name = "Provence-Alpes-Cote d'Azur",
                        Code = "PAC",
                        Cities =
                        [
                            new() { Name = "Marseille", Latitude = 43.2965, Longitude = 5.3698, BoundingBox = new(43.22, 5.30, 43.38, 5.45), SuggestedNiches = ["Mediterranean Shipping Terminal", "Biomedical Research", "Creative Digital"] },
                            new() { Name = "Nice", Latitude = 43.7102, Longitude = 7.2620, BoundingBox = new(43.65, 7.18, 43.76, 7.32), SuggestedNiches = ["Sophia Antipolis Tech Valley", "Luxury Riviera Tourism", "Aroma & Perfume Science"] },
                            new() { Name = "Cannes", Latitude = 43.5528, Longitude = 7.0174, BoundingBox = new(43.52, 6.98, 43.58, 7.05), SuggestedNiches = ["Film & Entertainment Conferences", "Superyacht Brokerage", "High-End Hotels"] }
                        ]
                    },
                    new()
                    {
                        Name = "Occitanie",
                        Code = "OCC",
                        Cities =
                        [
                            new() { Name = "Toulouse", Latitude = 43.6047, Longitude = 1.4442, BoundingBox = new(43.54, 1.36, 43.66, 1.52), SuggestedNiches = ["Aerospace & Satellite (Airbus)", "Embedded IoT Systems", "Biotechnology"] },
                            new() { Name = "Montpellier", Latitude = 43.6108, Longitude = 3.8767, BoundingBox = new(43.56, 3.82, 43.65, 3.94), SuggestedNiches = ["HealthTech & AgTech Startups", "Video Game Development", "Renewables"] }
                        ]
                    },
                    new()
                    {
                        Name = "Nouvelle-Aquitaine",
                        Code = "NAQ",
                        Cities =
                        [
                            new() { Name = "Bordeaux", Latitude = 44.8378, Longitude = -0.5792, BoundingBox = new(44.78, -0.65, 44.90, -0.52), SuggestedNiches = ["Wine Trading & Agritech", "Optics & Laser Tech", "Aerospace Composites"] }
                        ]
                    },
                    new()
                    {
                        Name = "Hauts-de-France",
                        Code = "HDF",
                        Cities =
                        [
                            new() { Name = "Lille", Latitude = 50.6292, Longitude = 3.0573, BoundingBox = new(50.58, 2.98, 50.68, 3.12), SuggestedNiches = ["E-Commerce & Retail HQ", "Textile & Materials Innovation", "Logistics Hubs"] }
                        ]
                    }
                ]
            },

            // 20. Sweden
            new()
            {
                Code = "SE",
                Name = "Sweden",
                FlagEmoji = "🇸🇪",
                States =
                [
                    new()
                    {
                        Name = "Stockholm County",
                        Code = "AB",
                        Cities =
                        [
                            new() { Name = "Stockholm", Latitude = 59.3293, Longitude = 18.0686, BoundingBox = new(59.25, 17.92, 59.40, 18.22), SuggestedNiches = ["FinTech Unicorns & Gaming", "CleanTech Innovation", "B2B SaaS Systems", "Specialty Healthcare"] },
                            new() { Name = "Kista", Latitude = 59.4042, Longitude = 17.9472, BoundingBox = new(59.38, 17.92, 59.43, 17.98), SuggestedNiches = ["Telecommunications Silicon Valley", "5G/6G Microwave R&D", "Cloud Datacenters"] },
                            new() { Name = "Solna", Latitude = 59.3600, Longitude = 18.0000, BoundingBox = new(59.34, 17.96, 59.38, 18.04), SuggestedNiches = ["Karolinska Life Science Cluster", "Corporate HQs", "Medical Tech"] }
                        ]
                    },
                    new()
                    {
                        Name = "Vastra Gotaland",
                        Code = "O",
                        Cities =
                        [
                            new() { Name = "Gothenburg", Latitude = 57.7089, Longitude = 11.9746, BoundingBox = new(57.64, 11.85, 57.78, 12.08), SuggestedNiches = ["Automotive & EV Platforms (Volvo)", "Scandinavia's Largest Port", "Biomedical Systems"] },
                            new() { Name = "Boras", Latitude = 57.7210, Longitude = 12.9401, BoundingBox = new(57.68, 12.88, 57.76, 13.00), SuggestedNiches = ["Smart Textiles & Fashion Tech", "E-Commerce Logistics", "Automation"] }
                        ]
                    },
                    new()
                    {
                        Name = "Skane",
                        Code = "M",
                        Cities =
                        [
                            new() { Name = "Malmo", Latitude = 55.6050, Longitude = 13.0038, BoundingBox = new(55.54, 12.92, 55.65, 13.08), SuggestedNiches = ["Medicon Valley Cross-Border Tech", "Game Dev Clusters", "Sustainable Architecture"] },
                            new() { Name = "Lund", Latitude = 55.7047, Longitude = 13.1910, BoundingBox = new(55.67, 13.14, 55.74, 13.24), SuggestedNiches = ["MAX IV & ESS Synchrotron Science", "Wireless Technologies", "MedTech Devices"] }
                        ]
                    },
                    new()
                    {
                        Name = "Uppsala County",
                        Code = "C",
                        Cities =
                        [
                            new() { Name = "Uppsala", Latitude = 59.8586, Longitude = 17.6389, BoundingBox = new(59.82, 17.58, 59.90, 17.70), SuggestedNiches = ["Biopharmaceutical Manufacturing", "Genomics & Life Science", "Higher Education"] }
                        ]
                    }
                ]
            },

            // 21. Denmark
            new()
            {
                Code = "DK",
                Name = "Denmark",
                FlagEmoji = "🇩🇰",
                States =
                [
                    new()
                    {
                        Name = "Capital Region",
                        Code = "H",
                        Cities =
                        [
                            new() { Name = "Copenhagen", Latitude = 55.6761, Longitude = 12.5683, BoundingBox = new(55.62, 12.48, 55.74, 12.65), SuggestedNiches = ["Life Sciences & Pharma HQs", "Green Maritime Shipping", "Design & Architectural Agencies", "Specialty Dental Clinics"] },
                            new() { Name = "Frederiksberg", Latitude = 55.6780, Longitude = 12.5330, BoundingBox = new(55.66, 12.50, 55.70, 12.56), SuggestedNiches = ["Retail & High-End Gastronomy", "Business Consulting", "Medical Practices"] },
                            new() { Name = "Lyngby", Latitude = 55.7704, Longitude = 12.5038, BoundingBox = new(55.74, 12.46, 55.80, 12.54), SuggestedNiches = ["DTU Engineering Tech Incubators", "Biotech Software", "Chemical Tech"] }
                        ]
                    },
                    new()
                    {
                        Name = "Central Denmark",
                        Code = "M",
                        Cities =
                        [
                            new() { Name = "Aarhus", Latitude = 56.1629, Longitude = 10.2039, BoundingBox = new(56.10, 10.12, 56.22, 10.28), SuggestedNiches = ["Offshore Wind Power Hub", "Food & AgTech Science", "Digital Agency Ecosystem"] },
                            new() { Name = "Randers", Latitude = 56.4607, Longitude = 10.0364, BoundingBox = new(56.42, 9.98, 56.50, 10.10), SuggestedNiches = ["Industrial Engineering", "Clean Energy Equipment", "Agricultural Trade"] }
                        ]
                    },
                    new()
                    {
                        Name = "Region of Southern Denmark",
                        Code = "SD",
                        Cities =
                        [
                            new() { Name = "Odense", Latitude = 55.4038, Longitude = 10.4024, BoundingBox = new(55.35, 10.32, 55.45, 10.48), SuggestedNiches = ["Collaborative Robotics World Hub", "Health Informatics", "Horticulture Tech"] },
                            new() { Name = "Esbjerg", Latitude = 55.4703, Longitude = 8.4519, BoundingBox = new(55.42, 8.38, 55.52, 8.52), SuggestedNiches = ["Offshore Wind & Green Hydrogen", "Port Operations", "Marine Engineering"] }
                        ]
                    },
                    new()
                    {
                        Name = "North Denmark",
                        Code = "ND",
                        Cities =
                        [
                            new() { Name = "Aalborg", Latitude = 57.0488, Longitude = 9.9217, BoundingBox = new(57.00, 9.85, 57.10, 10.00), SuggestedNiches = ["Telecommunications & 5G R&D", "Renewable Energy Tech", "Cement & Building Materials"] }
                        ]
                    }
                ]
            },

            // 22. Norway
            new()
            {
                Code = "NO",
                Name = "Norway",
                FlagEmoji = "🇳🇴",
                States =
                [
                    new()
                    {
                        Name = "Eastern Norway",
                        Code = "OST",
                        Cities =
                        [
                            new() { Name = "Oslo", Latitude = 59.9139, Longitude = 10.7522, BoundingBox = new(59.85, 10.65, 59.98, 10.88), SuggestedNiches = ["Maritime Clean Energy Tech", "FinTech & Sovereign Wealth Services", "Boutique Legal Chambers", "HealthTech"] },
                            new() { Name = "Drammen", Latitude = 59.7441, Longitude = 10.2045, BoundingBox = new(59.70, 10.14, 59.78, 10.28), SuggestedNiches = ["Logistics & Auto Import Terminal", "Healthcare", "Engineering"] },
                            new() { Name = "Fredrikstad", Latitude = 59.2181, Longitude = 10.9298, BoundingBox = new(59.18, 10.86, 59.26, 11.00), SuggestedNiches = ["Sustainable Process Industry", "Recycling Tech", "Coastal Trade"] }
                        ]
                    },
                    new()
                    {
                        Name = "Western Norway",
                        Code = "VES",
                        Cities =
                        [
                            new() { Name = "Bergen", Latitude = 60.3913, Longitude = 5.3221, BoundingBox = new(60.32, 5.22, 60.46, 5.42), SuggestedNiches = ["Marine Aquaculture & Salmon Export", "Subsea Offshore Tech", "Media City Bergen Studios"] },
                            new() { Name = "Stavanger", Latitude = 58.9690, Longitude = 5.7331, BoundingBox = new(58.90, 5.65, 59.02, 5.82), SuggestedNiches = ["Energy Transition & Renewables", "Offshore Oil & Gas Systems", "Culinary Commerce"] },
                            new() { Name = "Sandnes", Latitude = 58.8524, Longitude = 5.7352, BoundingBox = new(58.80, 5.68, 58.90, 5.80), SuggestedNiches = ["Robotics & Automation", "Commercial Retail", "Industrial Contracting"] }
                        ]
                    },
                    new()
                    {
                        Name = "Trondelag",
                        Code = "TRD",
                        Cities =
                        [
                            new() { Name = "Trondheim", Latitude = 63.4305, Longitude = 10.3951, BoundingBox = new(63.38, 10.30, 63.48, 10.48), SuggestedNiches = ["Deeptech & AI (SINTEF/NTNU)", "Microelectronics", "Renewable Energy"] }
                        ]
                    },
                    new()
                    {
                        Name = "Northern Norway",
                        Code = "NOR",
                        Cities =
                        [
                            new() { Name = "Tromso", Latitude = 69.6492, Longitude = 18.9553, BoundingBox = new(69.60, 18.88, 69.70, 19.04), SuggestedNiches = ["Arctic Marine Biotechnology", "Satellite Ground Stations", "Aurora Eco-Tourism"] }
                        ]
                    }
                ]
            },

            // 23. New Zealand
            new()
            {
                Code = "NZ",
                Name = "New Zealand",
                FlagEmoji = "🇳🇿",
                States =
                [
                    new()
                    {
                        Name = "Auckland Region",
                        Code = "AUK",
                        Cities =
                        [
                            new() { Name = "Auckland", Latitude = -36.8485, Longitude = 174.7633, BoundingBox = new(-36.95, 174.65, -36.75, 174.90), SuggestedNiches = ["SaaS & Cloud Software", "Marine Yacht Construction", "Immigration & Legal Firms", "Specialty Medical Clinics"] },
                            new() { Name = "Manukau", Latitude = -36.9930, Longitude = 174.8780, BoundingBox = new(-37.03, 174.82, -36.95, 174.94), SuggestedNiches = ["Manufacturing & Food Processing", "Freight Logistics", "Commercial Retail"] },
                            new() { Name = "North Shore", Latitude = -36.8000, Longitude = 174.7600, BoundingBox = new(-36.85, 174.72, -36.75, 174.80), SuggestedNiches = ["Tech Startups", "Healthcare Clinics", "High-End Residential Services"] }
                        ]
                    },
                    new()
                    {
                        Name = "Wellington Region",
                        Code = "WGN",
                        Cities =
                        [
                            new() { Name = "Wellington", Latitude = -41.2865, Longitude = 174.7762, BoundingBox = new(-41.35, 174.70, -41.22, 174.85), SuggestedNiches = ["GovTech Solutions", "VFX & Digital Film (Weta)", "Gaming Studios & FinTech"] },
                            new() { Name = "Lower Hutt", Latitude = -41.2167, Longitude = 174.9167, BoundingBox = new(-41.25, 174.87, -41.17, 174.96), SuggestedNiches = ["Advanced Materials R&D", "Engineering", "Commercial Distribution"] }
                        ]
                    },
                    new()
                    {
                        Name = "Canterbury",
                        Code = "CAN",
                        Cities =
                        [
                            new() { Name = "Christchurch", Latitude = -43.5321, Longitude = 172.6362, BoundingBox = new(-43.60, 172.52, -43.45, 172.75), SuggestedNiches = ["Aerospace & Space Launch Tech", "Agritech & Food Innovation", "Electronics Manufacturing"] }
                        ]
                    },
                    new()
                    {
                        Name = "Waikato",
                        Code = "WKO",
                        Cities =
                        [
                            new() { Name = "Hamilton, NZ", Latitude = -37.7870, Longitude = 175.2793, BoundingBox = new(-37.84, 175.22, -37.72, 175.35), SuggestedNiches = ["Dairy Agritech Megacluster", "Bio-Agri Research", "Aviation Training"] }
                        ]
                    },
                    new()
                    {
                        Name = "Otago",
                        Code = "OTA",
                        Cities =
                        [
                            new() { Name = "Queenstown", Latitude = -45.0312, Longitude = 168.6626, BoundingBox = new(-45.06, 168.62, -45.00, 168.72), SuggestedNiches = ["Luxury Eco-Tourism & Ski Lodging", "Adventure Sports Gear", "Film Production Locations"] },
                            new() { Name = "Dunedin", Latitude = -45.8788, Longitude = 170.5028, BoundingBox = new(-45.92, 170.45, -45.82, 170.56), SuggestedNiches = ["Biomedical Health Sciences", "Video Game Development", "Higher Education"] }
                        ]
                    }
                ]
            },

            // 24. Spain
            new()
            {
                Code = "ES",
                Name = "Spain",
                FlagEmoji = "🇪🇸",
                States =
                [
                    new()
                    {
                        Name = "Community of Madrid",
                        Code = "MD",
                        Cities =
                        [
                            new() { Name = "Madrid", Latitude = 40.4168, Longitude = -3.7038, BoundingBox = new(40.32, -3.82, 40.52, -3.58), SuggestedNiches = ["Corporate Law & Advisory", "FinTech & Banking Platforms", "Commercial Real Estate", "Fine Gastronomy"] },
                            new() { Name = "Alcala de Henares", Latitude = 40.4819, Longitude = -3.3644, BoundingBox = new(40.45, -3.40, 40.52, -3.32), SuggestedNiches = ["Biopharmaceutical Manufacturing", "Logistics Centers", "Higher Education"] },
                            new() { Name = "Getafe", Latitude = 40.3083, Longitude = -3.7328, BoundingBox = new(40.27, -3.77, 40.34, -3.68), SuggestedNiches = ["Aerospace & Defense (Airbus)", "Heavy Metal Machining", "Transport Services"] }
                        ]
                    },
                    new()
                    {
                        Name = "Catalonia",
                        Code = "CT",
                        Cities =
                        [
                            new() { Name = "Barcelona", Latitude = 41.3879, Longitude = 2.1699, BoundingBox = new(41.32, 2.08, 41.46, 2.24), SuggestedNiches = ["Mobile & Tech Startups (22@)", "Biomedical & Oncology R&D", "Creative Design Studios", "Boutique Hospitality"] },
                            new() { Name = "Terrassa", Latitude = 41.5632, Longitude = 2.0089, BoundingBox = new(41.52, 1.96, 41.60, 2.06), SuggestedNiches = ["Textile & Optical Technology", "Audiovisual Production", "Industrial Tooling"] },
                            new() { Name = "Badalona", Latitude = 41.4500, Longitude = 2.2472, BoundingBox = new(41.42, 2.21, 41.48, 2.28), SuggestedNiches = ["Biomedical Research", "Chemical Industry", "Coastal Commerce"] }
                        ]
                    },
                    new()
                    {
                        Name = "Andalusia",
                        Code = "AN",
                        Cities =
                        [
                            new() { Name = "Seville", Latitude = 37.3891, Longitude = -5.9845, BoundingBox = new(37.32, -6.05, 37.45, -5.90), SuggestedNiches = ["Aeronautics & Space Industry", "Renewable Solar Thermal", "Agri-Food Export"] },
                            new() { Name = "Malaga", Latitude = 36.7213, Longitude = -4.4214, BoundingBox = new(36.68, -4.50, 36.78, -4.35), SuggestedNiches = ["Malaga TechPark Cybersecurity", "Digital Nomads Hub", "Tourism & Lodging"] },
                            new() { Name = "Marbella", Latitude = 36.5101, Longitude = -4.8824, BoundingBox = new(36.48, -4.94, 36.55, -4.82), SuggestedNiches = ["Ultra-Luxury Real Estate", "Cosmetic Aesthetic Clinics", "Yachting & Beach Clubs"] }
                        ]
                    },
                    new()
                    {
                        Name = "Valencian Community",
                        Code = "VC",
                        Cities =
                        [
                            new() { Name = "Valencia", Latitude = 39.4699, Longitude = -0.3763, BoundingBox = new(39.40, -0.45, 39.52, -0.30), SuggestedNiches = ["Container Port Logistics", "Agri Food & Citrus", "HealthTech & Telemedicine"] },
                            new() { Name = "Alicante", Latitude = 38.3452, Longitude = -0.4810, BoundingBox = new(38.30, -0.54, 38.40, -0.42), SuggestedNiches = ["EU Intellectual Property Law", "Digital Game Studios", "Tourism"] }
                        ]
                    },
                    new()
                    {
                        Name = "Basque Country",
                        Code = "PV",
                        Cities =
                        [
                            new() { Name = "Bilbao", Latitude = 43.2630, Longitude = -2.9350, BoundingBox = new(43.22, -2.98, 43.30, -2.88), SuggestedNiches = ["Advanced Metallurgy & Machine Tools", "Energy HQs (Iberdrola)", "Biotech"] }
                        ]
                    }
                ]
            },

            // 25. Italy
            new()
            {
                Code = "IT",
                Name = "Italy",
                FlagEmoji = "🇮🇹",
                States =
                [
                    new()
                    {
                        Name = "Lombardy",
                        Code = "LOM",
                        Cities =
                        [
                            new() { Name = "Milan", Latitude = 45.4642, Longitude = 9.1900, BoundingBox = new(45.38, 9.08, 45.54, 9.28), SuggestedNiches = ["Fashion & Luxury HQ", "Investment Banking & FinTech", "Design Agencies", "Private Specialty Clinics"] },
                            new() { Name = "Brescia", Latitude = 45.5416, Longitude = 10.2118, BoundingBox = new(45.50, 10.15, 45.58, 10.28), SuggestedNiches = ["Precision Metallurgy", "Industrial Automotive Components", "Machinery"] },
                            new() { Name = "Bergamo", Latitude = 45.6983, Longitude = 9.6773, BoundingBox = new(45.66, 9.62, 45.74, 9.74), SuggestedNiches = ["Kilometro Rosso Innovation Tech", "Electromechanical Components", "Pharma"] },
                            new() { Name = "Monza", Latitude = 45.5845, Longitude = 9.2744, BoundingBox = new(45.55, 9.23, 45.62, 9.32), SuggestedNiches = ["Motorsport & Automotive Tech", "Design Furniture", "Biomedical Systems"] }
                        ]
                    },
                    new()
                    {
                        Name = "Lazio",
                        Code = "LAZ",
                        Cities =
                        [
                            new() { Name = "Rome", Latitude = 41.9028, Longitude = 12.4964, BoundingBox = new(41.80, 12.35, 42.00, 12.60), SuggestedNiches = ["Corporate Law & State Advisory", "Aerospace & Defense HQ (Leonardo)", "Telecom Systems", "Heritage Hospitality"] }
                        ]
                    },
                    new()
                    {
                        Name = "Veneto",
                        Code = "VEN",
                        Cities =
                        [
                            new() { Name = "Venice", Latitude = 45.4408, Longitude = 12.3155, BoundingBox = new(45.40, 12.25, 45.48, 12.38), SuggestedNiches = ["Heritage Tourism & Hotels", "Artisanal Murano Glass", "Maritime Shipping"] },
                            new() { Name = "Verona", Latitude = 45.4384, Longitude = 10.9916, BoundingBox = new(45.38, 10.92, 45.48, 11.06), SuggestedNiches = ["Multimodal Freight Quadrante Europa", "Wine Export Hub", "Agri-Food"] },
                            new() { Name = "Padua", Latitude = 45.4064, Longitude = 11.8768, BoundingBox = new(45.36, 11.82, 45.46, 11.94), SuggestedNiches = ["Medical Sciences & Surgery Implants", "Industrial Logistics", "Biotech"] }
                        ]
                    },
                    new()
                    {
                        Name = "Emilia-Romagna",
                        Code = "EMR",
                        Cities =
                        [
                            new() { Name = "Bologna", Latitude = 44.4949, Longitude = 11.3426, BoundingBox = new(44.44, 11.26, 44.55, 11.42), SuggestedNiches = ["Motor Valley Supercars (Ferrari/Lamborghini)", "Packaging Machinery", "Health Science"] },
                            new() { Name = "Modena", Latitude = 44.6471, Longitude = 10.9252, BoundingBox = new(44.60, 10.86, 44.70, 10.98), SuggestedNiches = ["Luxury Automotive Engineering", "Traditional Balsamic Vinegar", "Biomedical Devices"] },
                            new() { Name = "Parma", Latitude = 44.8015, Longitude = 10.3279, BoundingBox = new(44.76, 10.26, 44.85, 10.38), SuggestedNiches = ["Food Valley Gastronomy & Dairy", "European Food Safety (EFSA)", "Pharma HQs"] }
                        ]
                    },
                    new()
                    {
                        Name = "Piedmont",
                        Code = "PMN",
                        Cities =
                        [
                            new() { Name = "Turin", Latitude = 45.0703, Longitude = 7.6869, BoundingBox = new(45.00, 7.58, 45.14, 7.78), SuggestedNiches = ["Automotive Design & EV Engineering", "Aerospace Space Modules", "Financial Technology"] }
                        ]
                    },
                    new()
                    {
                        Name = "Tuscany",
                        Code = "TOS",
                        Cities =
                        [
                            new() { Name = "Florence", Latitude = 43.7696, Longitude = 11.2558, BoundingBox = new(43.72, 11.18, 43.82, 11.32), SuggestedNiches = ["Luxury Leathergoods & Haute Fashion", "Biomedical Diagnostics", "Heritage Tourism"] },
                            new() { Name = "Pisa", Latitude = 43.7228, Longitude = 10.4017, BoundingBox = new(43.68, 10.35, 43.76, 10.46), SuggestedNiches = ["Information Technology & Robotics", "Higher Education Tech", "Biotech"] }
                        ]
                    }
                ]
            },

            // 26. Japan
            new()
            {
                Code = "JP",
                Name = "Japan",
                FlagEmoji = "🇯🇵",
                States =
                [
                    new()
                    {
                        Name = "Tokyo Metropolis",
                        Code = "TYO",
                        Cities =
                        [
                            new() { Name = "Tokyo", Latitude = 35.6762, Longitude = 139.6503, BoundingBox = new(35.55, 139.50, 35.80, 139.85), SuggestedNiches = ["Global FinTech & Trading", "Robotics & AI Engineering", "Enterprise Software", "Fine Gastronomy"] },
                            new() { Name = "Yokohama", Latitude = 35.4437, Longitude = 139.6380, BoundingBox = new(35.38, 139.55, 35.52, 139.70), SuggestedNiches = ["Automotive HQ & R&D", "Semiconductor Equipment", "Port Logistics"] },
                            new() { Name = "Kawasaki", Latitude = 35.5308, Longitude = 139.7029, BoundingBox = new(35.48, 139.65, 35.58, 139.75), SuggestedNiches = ["Heavy Chemical Industry", "Life Science Innovation (KING SKYFRONT)", "Electronics"] },
                            new() { Name = "Saitama", Latitude = 35.8617, Longitude = 139.6455, BoundingBox = new(35.80, 139.58, 35.92, 139.72), SuggestedNiches = ["Automotive & Precision Parts", "Pharmaceutical Labs", "Commercial Retail"] }
                        ]
                    },
                    new()
                    {
                        Name = "Kansai",
                        Code = "KN",
                        Cities =
                        [
                            new() { Name = "Osaka", Latitude = 34.6937, Longitude = 135.5023, BoundingBox = new(34.60, 135.40, 34.78, 135.60), SuggestedNiches = ["Pharmaceutical Giants HQs", "Industrial Electronics", "Culinary Commerce"] },
                            new() { Name = "Kyoto", Latitude = 35.0116, Longitude = 135.7681, BoundingBox = new(34.95, 135.70, 35.08, 135.85), SuggestedNiches = ["Video Game & Interactive Media (Nintendo)", "Electronic Ceramics (Kyocera)", "Heritage Craft"] },
                            new() { Name = "Kobe", Latitude = 34.6901, Longitude = 135.1955, BoundingBox = new(34.64, 135.12, 34.75, 135.28), SuggestedNiches = ["Biomedical Cluster (Kobe Port Island)", "Hydrogen Energy Carrier Tech", "Steel & Shipyard"] }
                        ]
                    },
                    new()
                    {
                        Name = "Chubu",
                        Code = "CB",
                        Cities =
                        [
                            new() { Name = "Nagoya", Latitude = 35.1815, Longitude = 136.9066, BoundingBox = new(35.10, 136.80, 35.25, 137.00), SuggestedNiches = ["Automotive Manufacturing Megacluster (Toyota)", "Aerospace Wings (Mitsubishi)", "Industrial Ceramics"] },
                            new() { Name = "Hamamatsu", Latitude = 34.7108, Longitude = 137.7261, BoundingBox = new(34.65, 137.66, 34.78, 137.80), SuggestedNiches = ["Musical Instruments (Yamaha/Roland)", "Motorcycles & Optoelectronics", "Precision Machinery"] }
                        ]
                    },
                    new()
                    {
                        Name = "Kyushu",
                        Code = "KY",
                        Cities =
                        [
                            new() { Name = "Fukuoka", Latitude = 33.5904, Longitude = 130.4017, BoundingBox = new(33.52, 130.30, 33.68, 130.50), SuggestedNiches = ["National Strategic Startup Zone", "Digital Software & Gaming", "Asian Cross-Border Commerce"] },
                            new() { Name = "Kumamoto", Latitude = 32.8031, Longitude = 130.7079, BoundingBox = new(32.74, 130.62, 32.86, 130.78), SuggestedNiches = ["Semiconductor Megafabs (TSMC/JASM)", "Precision Equipment", "Agritech"] }
                        ]
                    }
                ]
            },

            // 27. South Korea
            new()
            {
                Code = "KR",
                Name = "South Korea",
                FlagEmoji = "🇰🇷",
                States =
                [
                    new()
                    {
                        Name = "Seoul Capital Area",
                        Code = "SU",
                        Cities =
                        [
                            new() { Name = "Seoul", Latitude = 37.5665, Longitude = 126.9780, BoundingBox = new(37.45, 126.85, 37.68, 127.15), SuggestedNiches = ["FinTech & Venture Capital", "K-Beauty Aesthetic Clinics & Dermatology", "Entertainment Agencies (K-Pop)", "Corporate Law"] },
                            new() { Name = "Incheon", Latitude = 37.4563, Longitude = 126.7052, BoundingBox = new(37.38, 126.58, 37.54, 126.80), SuggestedNiches = ["Bio-Logics & Biomanufacturing (Songdo)", "International Aviation Hub", "Free Economic Zone"] },
                            new() { Name = "Suwon", Latitude = 37.2636, Longitude = 127.0286, BoundingBox = new(37.22, 126.96, 37.32, 127.08), SuggestedNiches = ["Semiconductors (Samsung Electronics HQ)", "Display Tech", "Digital Media"] },
                            new() { Name = "Seongnam", Latitude = 37.4200, Longitude = 127.1265, BoundingBox = new(37.36, 127.08, 37.46, 127.16), SuggestedNiches = ["Pangyo Techno Valley (Korean Silicon Valley)", "Gaming & Metaverse HQs", "AI & Cloud Platforms"] }
                        ]
                    },
                    new()
                    {
                        Name = "Yeongnam",
                        Code = "YN",
                        Cities =
                        [
                            new() { Name = "Busan", Latitude = 35.1796, Longitude = 129.0756, BoundingBox = new(35.08, 128.95, 35.28, 129.20), SuggestedNiches = ["World Transshipment Container Port", "Blockchain Special Zone", "Maritime Finance & Film"] },
                            new() { Name = "Daegu", Latitude = 35.8714, Longitude = 128.6014, BoundingBox = new(35.80, 128.50, 35.94, 128.70), SuggestedNiches = ["Robotics & Smart Mobility", "Medical City Daegu Healthcare", "High-Tech Textiles"] },
                            new() { Name = "Ulsan", Latitude = 35.5384, Longitude = 129.3114, BoundingBox = new(35.45, 129.20, 35.62, 129.42), SuggestedNiches = ["Automotive (Hyundai Megaplant)", "World's Largest Shipyard (HD Hyundai)", "Petrochemicals"] }
                        ]
                    },
                    new()
                    {
                        Name = "Hoseo",
                        Code = "HS",
                        Cities =
                        [
                            new() { Name = "Daejeon", Latitude = 36.3504, Longitude = 127.3845, BoundingBox = new(36.28, 127.30, 36.44, 127.46), SuggestedNiches = ["Daedeok Innopolis Science City", "Nuclear & Aerospace R&D", "KAIST Deeptech Incubators"] }
                        ]
                    },
                    new()
                    {
                        Name = "Honam",
                        Code = "HN",
                        Cities =
                        [
                            new() { Name = "Gwangju", Latitude = 35.1595, Longitude = 126.8526, BoundingBox = new(35.10, 126.78, 35.22, 126.94), SuggestedNiches = ["AI Data Center & Mobility Cluster", "Photonics Industry", "Cultural Content"] }
                        ]
                    }
                ]
            },

            // 28. Hong Kong
            new()
            {
                Code = "HK",
                Name = "Hong Kong",
                FlagEmoji = "🇭🇰",
                States =
                [
                    new()
                    {
                        Name = "Hong Kong Island",
                        Code = "HKI",
                        Cities =
                        [
                            new() { Name = "Central", Latitude = 22.2819, Longitude = 114.1581, BoundingBox = new(22.26, 114.14, 22.30, 114.18), SuggestedNiches = ["Global Investment Banking", "Private Equity & Hedge Funds", "International Commercial Chambers"] },
                            new() { Name = "Wan Chai", Latitude = 22.2760, Longitude = 114.1750, BoundingBox = new(22.26, 114.16, 22.29, 114.19), SuggestedNiches = ["Convention & Exhibition Services", "Commercial Law Practices", "Digital Agencies"] },
                            new() { Name = "Causeway Bay", Latitude = 22.2800, Longitude = 114.1850, BoundingBox = new(22.26, 114.17, 22.29, 114.20), SuggestedNiches = ["Flagship Retail Ecosystems", "Private Aesthetic Medicine", "Hospitality Dining"] }
                        ]
                    },
                    new()
                    {
                        Name = "Kowloon",
                        Code = "KLN",
                        Cities =
                        [
                            new() { Name = "Tsim Sha Tsui", Latitude = 22.2988, Longitude = 114.1722, BoundingBox = new(22.28, 114.16, 22.32, 114.19), SuggestedNiches = ["Luxury Tourism & Watches", "Cross-Border Trade Agencies", "Hospitality"] },
                            new() { Name = "Mong Kok", Latitude = 22.3193, Longitude = 114.1694, BoundingBox = new(22.30, 114.15, 22.33, 114.18), SuggestedNiches = ["Consumer Electronics Trade", "Telecom Retail", "Healthcare Clinics"] },
                            new() { Name = "Kwun Tong", Latitude = 22.3130, Longitude = 114.2250, BoundingBox = new(22.30, 114.21, 22.33, 114.24), SuggestedNiches = ["CBD2 Smart City Offices", "Design & Tech Studios", "E-Commerce Logistics"] }
                        ]
                    },
                    new()
                    {
                        Name = "New Territories",
                        Code = "NT",
                        Cities =
                        [
                            new() { Name = "Sha Tin", Latitude = 22.3771, Longitude = 114.1974, BoundingBox = new(22.35, 114.16, 22.42, 114.22), SuggestedNiches = ["Hong Kong Science Park (HKSTP)", "Biotech Research", "Higher Education"] },
                            new() { Name = "Tsuen Wan", Latitude = 22.3713, Longitude = 114.1141, BoundingBox = new(22.35, 114.09, 22.40, 114.14), SuggestedNiches = ["Smart Logistics & Cold Chain", "Precision Tooling", "Commercial Plazas"] }
                        ]
                    }
                ]
            },

            // 29. Estonia
            new()
            {
                Code = "EE",
                Name = "Estonia",
                FlagEmoji = "🇪🇪",
                States =
                [
                    new()
                    {
                        Name = "Harju County",
                        Code = "HAR",
                        Cities =
                        [
                            new() { Name = "Tallinn", Latitude = 59.4370, Longitude = 24.7535, BoundingBox = new(59.38, 24.62, 59.48, 24.88), SuggestedNiches = ["e-Residency & FinTech Unicorns", "Cybersecurity (NATO CCDCOE)", "B2B SaaS Hubs", "Specialty Dentistry"] },
                            new() { Name = "Maardu", Latitude = 59.4764, Longitude = 25.0064, BoundingBox = new(59.44, 24.96, 59.50, 25.05), SuggestedNiches = ["Deepwater Muuga Cargo Port", "Logistics & Multimodal Freight", "Chemical Storage"] },
                            new() { Name = "Viimsi", Latitude = 59.5000, Longitude = 24.8333, BoundingBox = new(59.46, 24.80, 59.54, 24.88), SuggestedNiches = ["High-End Coastal Real Estate", "Health Spas & Medical Centers", "Marine Tourism"] }
                        ]
                    },
                    new()
                    {
                        Name = "Tartu County",
                        Code = "TAR",
                        Cities =
                        [
                            new() { Name = "Tartu", Latitude = 58.3780, Longitude = 26.7290, BoundingBox = new(58.34, 26.65, 58.42, 26.78), SuggestedNiches = ["DeepTech & GenAI (Tartu University)", "Biomedicine & Genomics", "Space Tech R&D"] }
                        ]
                    },
                    new()
                    {
                        Name = "Parnu County",
                        Code = "PAR",
                        Cities =
                        [
                            new() { Name = "Parnu", Latitude = 58.3859, Longitude = 24.4971, BoundingBox = new(58.35, 24.44, 58.43, 24.56), SuggestedNiches = ["Summer Spa & Wellness Tourism", "Wood & Furniture Manufacturing", "Food Production"] }
                        ]
                    }
                ]
            },

            // 30. Poland
            new()
            {
                Code = "PL",
                Name = "Poland",
                FlagEmoji = "🇵🇱",
                States =
                [
                    new()
                    {
                        Name = "Masovian Voivodeship",
                        Code = "MZ",
                        Cities =
                        [
                            new() { Name = "Warsaw", Latitude = 52.2297, Longitude = 21.0122, BoundingBox = new(52.12, 20.85, 52.34, 21.18), SuggestedNiches = ["Central European Banking Capital", "Enterprise Software & Cloud HQs", "Corporate Litigation", "High-End Medical Aesthetic Clinics"] },
                            new() { Name = "Radom", Latitude = 51.4027, Longitude = 21.1471, BoundingBox = new(51.36, 21.08, 51.46, 21.22), SuggestedNiches = ["Precision Metalworking & Defense", "Aviation Logistics", "Leather & Footwear"] }
                        ]
                    },
                    new()
                    {
                        Name = "Lesser Poland",
                        Code = "MA",
                        Cities =
                        [
                            new() { Name = "Krakow", Latitude = 50.0647, Longitude = 19.9450, BoundingBox = new(49.98, 19.82, 50.12, 20.08), SuggestedNiches = ["Global Shared Services (BPO/IT)", "Video Game Studios (CD Projekt)", "Biotechnology & AI"] },
                            new() { Name = "Tarnow", Latitude = 50.0121, Longitude = 20.9858, BoundingBox = new(49.98, 20.92, 50.06, 21.04), SuggestedNiches = ["Chemical Nitrogen Fertilizers", "Electrical Machinery", "Agri-Food Processing"] }
                        ]
                    },
                    new()
                    {
                        Name = "Lower Silesia",
                        Code = "DS",
                        Cities =
                        [
                            new() { Name = "Wroclaw", Latitude = 51.1079, Longitude = 17.0385, BoundingBox = new(51.02, 16.90, 51.18, 17.15), SuggestedNiches = ["Silicon Lower Silesia Semiconductors", "E-Commerce Warehousing Hubs", "Automotive R&D"] },
                            new() { Name = "Legnica", Latitude = 51.2070, Longitude = 16.1550, BoundingBox = new(51.16, 16.10, 51.25, 16.22), SuggestedNiches = ["Copper Mining & Smelting (KGHM)", "Special Economic Zone", "Industrial Manufacturing"] }
                        ]
                    },
                    new()
                    {
                        Name = "Silesian Voivodeship",
                        Code = "SL",
                        Cities =
                        [
                            new() { Name = "Katowice", Latitude = 50.2649, Longitude = 19.0238, BoundingBox = new(50.20, 18.94, 50.32, 19.10), SuggestedNiches = ["GZM Metropolis Industrial Transformation", "Gaming & Esports Events", "IT Services"] },
                            new() { Name = "Gliwice", Latitude = 50.2945, Longitude = 18.6714, BoundingBox = new(50.25, 18.60, 50.35, 18.74), SuggestedNiches = ["Automotive Assembly (Stellantis)", "Software & Automation", "Logistics Hubs"] }
                        ]
                    },
                    new()
                    {
                        Name = "Greater Poland",
                        Code = "WP",
                        Cities =
                        [
                            new() { Name = "Poznan", Latitude = 52.4064, Longitude = 16.9252, BoundingBox = new(52.34, 16.82, 52.48, 17.02), SuggestedNiches = ["International Trade Fair Commerce", "Automotive & Commercial Vehicles", "Pharma HQs"] }
                        ]
                    },
                    new()
                    {
                        Name = "Pomeranian Voivodeship",
                        Code = "PM",
                        Cities =
                        [
                            new() { Name = "Gdansk", Latitude = 54.3520, Longitude = 18.6466, BoundingBox = new(54.28, 18.52, 54.42, 18.75), SuggestedNiches = ["Baltic Sea Deepwater Container Port", "Offshore Wind Engineering", "FinTech & IT Hubs"] },
                            new() { Name = "Gdynia", Latitude = 54.5189, Longitude = 18.5305, BoundingBox = new(54.46, 18.45, 54.56, 18.58), SuggestedNiches = ["Naval Shipyard & Defense", "Maritime Logistics", "Creative Film Arts"] }
                        ]
                    }
                ]
            },

            // 31. South Africa
            new()
            {
                Code = "ZA",
                Name = "South Africa",
                FlagEmoji = "🇿🇦",
                States =
                [
                    new()
                    {
                        Name = "Gauteng",
                        Code = "GP",
                        Cities =
                        [
                            new() { Name = "Johannesburg", Latitude = -26.2041, Longitude = 28.0473, BoundingBox = new(-26.30, 27.92, -26.10, 28.18), SuggestedNiches = ["African Financial Markets & JSE", "Mining & Resources Conglomerates", "Corporate Law Chambers", "Specialty Healthcare"] },
                            new() { Name = "Sandton", Latitude = -26.1076, Longitude = 28.0567, BoundingBox = new(-26.13, 28.02, -26.07, 28.08), SuggestedNiches = ["Africa's Richest Square Mile", "Investment Banking Towers", "Luxury Flagships"] },
                            new() { Name = "Pretoria", Latitude = -25.7479, Longitude = 28.2293, BoundingBox = new(-25.82, 28.12, -25.68, 28.32), SuggestedNiches = ["Government Administration & Diplomatic", "Automotive Manufacturing", "Research & CSIR"] },
                            new() { Name = "Centurion", Latitude = -25.8603, Longitude = 28.1895, BoundingBox = new(-25.90, 28.14, -25.80, 28.24), SuggestedNiches = ["Defense Aerospace Electronics", "Data Centers", "Commercial Real Estate"] }
                        ]
                    },
                    new()
                    {
                        Name = "Western Cape",
                        Code = "WC",
                        Cities =
                        [
                            new() { Name = "Cape Town", Latitude = -33.9249, Longitude = 18.4241, BoundingBox = new(-34.05, 18.30, -33.80, 18.60), SuggestedNiches = ["Silicon Cape Tech Startups", "Film & TV Production Studios", "Luxury Tourism & Vineyards"] },
                            new() { Name = "Stellenbosch", Latitude = -33.9321, Longitude = 18.8602, BoundingBox = new(-33.98, 18.80, -33.88, 18.92), SuggestedNiches = ["Agritech & Premium Wine Export", "FinTech Incubators", "Higher Education"] }
                        ]
                    },
                    new()
                    {
                        Name = "KwaZulu-Natal",
                        Code = "KZN",
                        Cities =
                        [
                            new() { Name = "Durban", Latitude = -29.8587, Longitude = 31.0218, BoundingBox = new(-29.95, 30.90, -29.75, 31.12), SuggestedNiches = ["Sub-Saharan Africa's Busiest Port", "Manufacturing & Textiles", "Sugar & Agri Refining"] },
                            new() { Name = "Umhlanga", Latitude = -29.7275, Longitude = 31.0850, BoundingBox = new(-29.75, 31.05, -29.70, 31.12), SuggestedNiches = ["Corporate Commercial HQs", "High-End Waterfront Hospitality", "Medical Spas"] }
                        ]
                    },
                    new()
                    {
                        Name = "Eastern Cape",
                        Code = "EC",
                        Cities =
                        [
                            new() { Name = "Gqeberha", Latitude = -33.9608, Longitude = 25.6022, BoundingBox = new(-34.02, 25.50, -33.90, 25.68), SuggestedNiches = ["Automotive Capital (VW/Isuzu)", "Deepwater Coega Port", "Wool & Mohair Export"] }
                        ]
                    }
                ]
            },

            // 32. India
            new()
            {
                Code = "IN",
                Name = "India",
                FlagEmoji = "🇮🇳",
                States =
                [
                    new()
                    {
                        Name = "Maharashtra",
                        Code = "MH",
                        Cities =
                        [
                            new() { Name = "Mumbai", Latitude = 19.0760, Longitude = 72.8777, BoundingBox = new(18.90, 72.75, 19.25, 73.05), SuggestedNiches = ["Financial Capital of India", "Bollywood Media & Film", "Commercial Real Estate", "Specialty Medical Hospitals"] },
                            new() { Name = "Pune", Latitude = 18.5204, Longitude = 73.8567, BoundingBox = new(18.42, 73.75, 18.62, 73.98), SuggestedNiches = ["Automotive & EV Hub (Tata/Bajaj)", "IT Software Parks (Hinjawadi)", "Biotech (Serum Institute)"] },
                            new() { Name = "Nagpur", Latitude = 21.1458, Longitude = 79.0882, BoundingBox = new(21.08, 79.00, 21.22, 79.18), SuggestedNiches = ["Multi-modal Logistics Hub (MIHAN)", "Defense Aerospace", "Citrus & Agro Commerce"] },
                            new() { Name = "Thane", Latitude = 19.2183, Longitude = 72.9781, BoundingBox = new(19.16, 72.92, 19.28, 73.04), SuggestedNiches = ["Information Technology Parks", "Chemical Manufacturing", "Commercial Retail"] },
                            new() { Name = "Nashik", Latitude = 19.9975, Longitude = 73.7898, BoundingBox = new(19.92, 73.72, 20.06, 73.86), SuggestedNiches = ["Wine Capital of India", "Defense Aircraft Manufacturing (HAL)", "Engineering Tools"] }
                        ]
                    },
                    new()
                    {
                        Name = "Karnataka",
                        Code = "KA",
                        Cities =
                        [
                            new() { Name = "Bengaluru", Latitude = 12.9716, Longitude = 77.5946, BoundingBox = new(12.85, 77.45, 13.10, 77.75), SuggestedNiches = ["Silicon Valley of India", "Aerospace & Defense R&D (ISRO)", "Deeptech & AI Startups"] },
                            new() { Name = "Mysuru", Latitude = 12.2958, Longitude = 76.6394, BoundingBox = new(12.24, 76.58, 12.36, 76.70), SuggestedNiches = ["IT Services Hub", "Silk & Sandalwood Heritage", "Heritage Tourism"] },
                            new() { Name = "Mangaluru", Latitude = 12.9141, Longitude = 74.8560, BoundingBox = new(12.85, 74.80, 12.98, 74.92), SuggestedNiches = ["Major Seaport & Petrochemicals", "Banking Capital", "Healthcare Institutions"] }
                        ]
                    },
                    new()
                    {
                        Name = "Delhi NCR",
                        Code = "DL",
                        Cities =
                        [
                            new() { Name = "New Delhi", Latitude = 28.6139, Longitude = 77.2090, BoundingBox = new(28.48, 77.05, 28.75, 77.35), SuggestedNiches = ["National Government Affairs", "Corporate Law Chambers", "Diplomatic Missions", "Medical Super-Specialties"] },
                            new() { Name = "Gurugram", Latitude = 28.4595, Longitude = 77.0266, BoundingBox = new(28.38, 76.95, 28.52, 77.10), SuggestedNiches = ["Global Tech EMEA/APAC HQs", "FinTech Unicorns", "Automotive HQs (Maruti Suzuki)"] },
                            new() { Name = "Noida", Latitude = 28.5355, Longitude = 77.3910, BoundingBox = new(28.46, 77.30, 28.62, 77.48), SuggestedNiches = ["Electronics & Mobile Manufacturing", "Data Centers", "Media & Film City"] }
                        ]
                    },
                    new()
                    {
                        Name = "Tamil Nadu",
                        Code = "TN",
                        Cities =
                        [
                            new() { Name = "Chennai", Latitude = 13.0827, Longitude = 80.2707, BoundingBox = new(12.95, 80.15, 13.20, 80.35), SuggestedNiches = ["Detroit of Asia (Automotive)", "SaaS Capital of India", "Healthcare & Medical Tourism"] },
                            new() { Name = "Coimbatore", Latitude = 11.0168, Longitude = 76.9558, BoundingBox = new(10.95, 76.88, 11.08, 77.05), SuggestedNiches = ["Textile & Cotton Machinery", "Pump & Motor Manufacturing", "Health Science"] },
                            new() { Name = "Madurai", Latitude = 9.9252, Longitude = 78.1198, BoundingBox = new(9.88, 78.06, 9.98, 78.18), SuggestedNiches = ["Temple Heritage Tourism", "Automotive Rubber", "Handloom Textiles"] }
                        ]
                    },
                    new()
                    {
                        Name = "Telangana",
                        Code = "TG",
                        Cities =
                        [
                            new() { Name = "Hyderabad, India", Latitude = 17.3850, Longitude = 78.4867, BoundingBox = new(17.28, 78.32, 17.50, 78.60), SuggestedNiches = ["Genome Valley Vaccine Capital", "Cyberabad IT & Cloud Giants", "Aerospace & Defense Precision"] },
                            new() { Name = "Warangal", Latitude = 17.9689, Longitude = 79.5941, BoundingBox = new(17.92, 79.52, 18.02, 79.66), SuggestedNiches = ["Textile Mega Park", "Higher Education (NIT)", "Agri Commodities"] }
                        ]
                    },
                    new()
                    {
                        Name = "Gujarat",
                        Code = "GJ",
                        Cities =
                        [
                            new() { Name = "Ahmedabad", Latitude = 23.0225, Longitude = 72.5714, BoundingBox = new(22.92, 72.45, 23.12, 72.68), SuggestedNiches = ["Textile Megamills", "Pharmaceutical Manufacturing", "FinTech (GIFT City)"] },
                            new() { Name = "Surat", Latitude = 21.1702, Longitude = 72.8311, BoundingBox = new(21.10, 72.74, 21.25, 72.90), SuggestedNiches = ["World Diamond Polishing Hub", "Synthetic Silk & Textiles", "Petrochemical Trade"] },
                            new() { Name = "Vadodara", Latitude = 22.3072, Longitude = 73.1812, BoundingBox = new(22.24, 73.10, 22.36, 73.25), SuggestedNiches = ["Heavy Electrical Equipment", "Chemical & Petrochemical", "Biotechnology"] },
                            new() { Name = "Rajkot", Latitude = 22.3039, Longitude = 70.8022, BoundingBox = new(22.24, 70.72, 22.36, 70.88), SuggestedNiches = ["Engineering Auto Components", "Diesel Engines & Castings", "Jewelry Trade"] }
                        ]
                    },
                    new()
                    {
                        Name = "West Bengal",
                        Code = "WB",
                        Cities =
                        [
                            new() { Name = "Kolkata", Latitude = 22.5726, Longitude = 88.3639, BoundingBox = new(22.45, 22.25, 22.68, 88.48), SuggestedNiches = ["Eastern India Commercial Capital", "Jute & Tea Export", "IT & FinTech (Salt Lake)"] },
                            new() { Name = "Siliguri", Latitude = 26.7271, Longitude = 88.3953, BoundingBox = new(26.68, 88.34, 26.78, 88.45), SuggestedNiches = ["Tea Auction & Export Hub", "Northeast Gateway Logistics", "Tourism"] }
                        ]
                    },
                    new()
                    {
                        Name = "Rajasthan",
                        Code = "RJ",
                        Cities =
                        [
                            new() { Name = "Jaipur", Latitude = 26.9124, Longitude = 75.7873, BoundingBox = new(26.82, 75.68, 27.02, 75.90), SuggestedNiches = ["Gemstone Cutting & Jewelry", "Heritage Luxury Tourism", "Handcrafted Textiles & Garments"] },
                            new() { Name = "Jodhpur", Latitude = 26.2389, Longitude = 73.0243, BoundingBox = new(26.18, 72.96, 26.30, 73.10), SuggestedNiches = ["Wooden Handicrafts & Furniture", "Solar Energy Megaparks", "Spices Trading"] }
                        ]
                    },
                    new()
                    {
                        Name = "Uttar Pradesh",
                        Code = "UP",
                        Cities =
                        [
                            new() { Name = "Lucknow", Latitude = 26.8467, Longitude = 80.9462, BoundingBox = new(26.76, 80.85, 26.94, 81.05), SuggestedNiches = ["Chikan Embroidery & Fashion", "Biomedical & Botanical R&D", "IT Services"] },
                            new() { Name = "Kanpur", Latitude = 26.4499, Longitude = 80.3319, BoundingBox = new(26.38, 80.24, 26.52, 80.42), SuggestedNiches = ["Leather Goods & Saddlery Export", "Defense Ordinance & Ordnance", "Textiles"] },
                            new() { Name = "Agra", Latitude = 27.1767, Longitude = 78.0081, BoundingBox = new(27.12, 77.94, 27.24, 78.08), SuggestedNiches = ["Heritage Tourism (Taj Mahal)", "Footwear Manufacturing", "Handicrafts"] }
                        ]
                    },
                    new()
                    {
                        Name = "Kerala",
                        Code = "KL",
                        Cities =
                        [
                            new() { Name = "Kochi", Latitude = 9.9312, Longitude = 76.2673, BoundingBox = new(9.86, 76.18, 10.02, 76.36), SuggestedNiches = ["International Transshipment Seaport", "Spices & Seafood Export", "IT (Infopark)"] },
                            new() { Name = "Thiruvananthapuram", Latitude = 8.5241, Longitude = 76.9366, BoundingBox = new(8.45, 76.86, 8.58, 77.02), SuggestedNiches = ["Technopark IT & Electronics", "Space Technology (VSSC)", "Ayurvedic Medical Tourism"] }
                        ]
                    }
                ]
            }

        

        

        ];
    }
}
