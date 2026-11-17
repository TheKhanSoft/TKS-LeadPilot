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
                            new() { Name = "Dera Ghazi Khan", Latitude = 30.0561, Longitude = 70.6403, BoundingBox = new(30.00, 70.57, 30.12, 70.71), SuggestedNiches = ["Cement Manufacturing", "Cotton Processing", "Mineral & Stone Crushing"] }
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
                            new() { Name = "Jacobabad", Latitude = 28.2810, Longitude = 68.4376, BoundingBox = new(28.22, 68.37, 28.34, 68.50), SuggestedNiches = ["Grain Storage & Distribution", "Cross-Provincial Logistics", "Agricultural Equipment"] }
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
                            new() { Name = "Mansehra", Latitude = 34.3302, Longitude = 73.1968, BoundingBox = new(34.27, 73.13, 34.39, 73.26), SuggestedNiches = ["Northern Tourism Gateway", "Pine Timber & Wood Mills", "Stone Crushing & Mining"] }
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
                            new() { Name = "Hub", Latitude = 24.9000, Longitude = 66.8833, BoundingBox = new(24.84, 66.82, 24.96, 66.95), SuggestedNiches = ["Automobile Assembly", "Chemical Manufacturing", "Power & Energy Plants"] }
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
                            new() { Name = "Hunza", Latitude = 36.3167, Longitude = 74.6500, BoundingBox = new(36.26, 74.58, 36.38, 74.72), SuggestedNiches = ["Luxury Boutique Hotels", "Organic Foods & Dry Fruits", "Cultural Heritage Tours"] }
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
                            new() { Name = "Rawalakot", Latitude = 33.8584, Longitude = 73.7604, BoundingBox = new(33.80, 73.70, 33.92, 73.83), SuggestedNiches = ["Hill Station Resorts", "Educational Institutions", "Agri Farming & Poultry"] }
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
                            new() { Name = "San Jose", Latitude = 37.3382, Longitude = -121.8863, BoundingBox = new(37.25, -122.00, 37.45, -121.75), SuggestedNiches = ["Semiconductors", "Hardware Engineering", "Cloud Infrastructure"] }
                        ]
                    },
                    new()
                    {
                        Name = "New York",
                        Code = "NY",
                        Cities =
                        [
                            new() { Name = "New York City", Latitude = 40.7128, Longitude = -74.0060, BoundingBox = new(40.58, -74.15, 40.85, -73.85), SuggestedNiches = ["Corporate Law & Litigation", "Investment Advisory & FinTech", "Commercial Real Estate", "Fine Dining & Bistros"] },
                            new() { Name = "Buffalo", Latitude = 42.8864, Longitude = -78.8784, BoundingBox = new(42.82, -78.93, 42.96, -78.78), SuggestedNiches = ["Advanced Manufacturing", "Healthcare Providers", "Educational Services"] }
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
                            new() { Name = "San Antonio", Latitude = 29.4241, Longitude = -98.4936, BoundingBox = new(29.30, -98.65, 29.55, -98.35), SuggestedNiches = ["Cybersecurity & Defense", "Healthcare Networks", "Hospitality & Heritage"] }
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
                            new() { Name = "Tampa", Latitude = 27.9506, Longitude = -82.4572, BoundingBox = new(27.85, -82.58, 28.08, -82.35), SuggestedNiches = ["Marine Shipping", "Financial Services", "Specialty Healthcare"] }
                        ]
                    },
                    new()
                    {
                        Name = "Illinois",
                        Code = "IL",
                        Cities =
                        [
                            new() { Name = "Chicago", Latitude = 41.8781, Longitude = -87.6298, BoundingBox = new(41.70, -87.80, 42.02, -87.55), SuggestedNiches = ["Logistics & Warehousing", "Commercial Law Practices", "Financial Trading & FinTech"] }
                        ]
                    },
                    new()
                    {
                        Name = "Washington",
                        Code = "WA",
                        Cities =
                        [
                            new() { Name = "Seattle", Latitude = 47.6062, Longitude = -122.3321, BoundingBox = new(47.50, -122.45, 47.72, -122.22), SuggestedNiches = ["Cloud Computing & Big Data", "E-Commerce Infrastructure", "Biotechnology"] }
                        ]
                    },
                    new()
                    {
                        Name = "Massachusetts",
                        Code = "MA",
                        Cities =
                        [
                            new() { Name = "Boston", Latitude = 42.3601, Longitude = -71.0589, BoundingBox = new(42.28, -71.18, 42.42, -70.95), SuggestedNiches = ["Biopharma & Therapeutics", "Higher Education Tech", "Financial Advisory"] }
                        ]
                    },
                    new()
                    {
                        Name = "Georgia",
                        Code = "GA",
                        Cities =
                        [
                            new() { Name = "Atlanta", Latitude = 33.7490, Longitude = -84.3880, BoundingBox = new(33.65, -84.50, 33.88, -84.28), SuggestedNiches = ["FinTech & Payment Processing", "Logistics & Supply Chain", "Film Production"] }
                        ]
                    },
                    new()
                    {
                        Name = "Colorado",
                        Code = "CO",
                        Cities =
                        [
                            new() { Name = "Denver", Latitude = 39.7392, Longitude = -104.9903, BoundingBox = new(39.62, -105.10, 39.85, -104.85), SuggestedNiches = ["Aerospace & Defense", "Outdoor & Fitness Brands", "Clean Energy"] }
                        ]
                    },
                    new()
                    {
                        Name = "Nevada",
                        Code = "NV",
                        Cities =
                        [
                            new() { Name = "Las Vegas", Latitude = 36.1699, Longitude = -115.1398, BoundingBox = new(36.05, -115.30, 36.28, -115.05), SuggestedNiches = ["Hospitality & Gaming", "Convention & Event Services", "Entertainment Tech"] }
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
                            new() { Name = "Newcastle", Latitude = 54.9783, Longitude = -1.6178, BoundingBox = new(54.92, -1.72, 55.05, -1.52), SuggestedNiches = ["Digital Software", "Offshore Energy", "Healthcare Science"] }
                        ]
                    },
                    new()
                    {
                        Name = "Scotland",
                        Code = "SCT",
                        Cities =
                        [
                            new() { Name = "Edinburgh", Latitude = 55.9533, Longitude = -3.1883, BoundingBox = new(55.88, -3.30, 56.00, -3.10), SuggestedNiches = ["Financial & Asset Management", "Tourism & Heritage Lodging", "Tech Startups"] },
                            new() { Name = "Glasgow", Latitude = 55.8642, Longitude = -4.2518, BoundingBox = new(55.80, -4.38, 55.92, -4.15), SuggestedNiches = ["Engineering & Manufacturing", "Space Technology", "Higher Education"] }
                        ]
                    },
                    new()
                    {
                        Name = "Wales",
                        Code = "WLS",
                        Cities =
                        [
                            new() { Name = "Cardiff", Latitude = 51.4816, Longitude = -3.1791, BoundingBox = new(51.42, -3.28, 51.55, -3.10), SuggestedNiches = ["Media & Broadcasting", "Financial Services", "Medical Diagnostics"] }
                        ]
                    },
                    new()
                    {
                        Name = "Northern Ireland",
                        Code = "NIR",
                        Cities =
                        [
                            new() { Name = "Belfast", Latitude = 54.5973, Longitude = -5.9301, BoundingBox = new(54.52, -6.02, 54.68, -5.85), SuggestedNiches = ["Cybersecurity", "FinTech", "Film Production Studios"] }
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
                            new() { Name = "Dubai", Latitude = 25.2048, Longitude = 55.2708, BoundingBox = new(25.05, 55.10, 25.35, 55.45), SuggestedNiches = ["Luxury Real Estate Brokers", "Business Setup & Corporate Services", "High-End Cosmetic Clinics", "Tourism & Luxury Car Rentals"] }
                        ]
                    },
                    new()
                    {
                        Name = "Abu Dhabi",
                        Code = "AUH",
                        Cities =
                        [
                            new() { Name = "Abu Dhabi", Latitude = 24.4539, Longitude = 54.3773, BoundingBox = new(24.35, 54.28, 24.55, 54.50), SuggestedNiches = ["Oil & Gas Contracting", "Government Advisory", "Healthcare & Specialty Hospitals"] },
                            new() { Name = "Al Ain", Latitude = 24.2075, Longitude = 55.7447, BoundingBox = new(24.12, 55.65, 24.28, 55.85), SuggestedNiches = ["Agricultural Tech", "Educational Institutes", "Heritage Tourism"] }
                        ]
                    },
                    new()
                    {
                        Name = "Sharjah",
                        Code = "SHJ",
                        Cities =
                        [
                            new() { Name = "Sharjah", Latitude = 25.3463, Longitude = 55.4209, BoundingBox = new(25.28, 55.35, 25.42, 55.50), SuggestedNiches = ["Publishing & Education", "Industrial Manufacturing", "Wholesale Trading"] }
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
                            new() { Name = "Ras Al Khaimah", Latitude = 25.7895, Longitude = 55.9432, BoundingBox = new(25.70, 55.88, 25.88, 56.02), SuggestedNiches = ["Ceramics & Building Materials", "Hospitality Resorts", "Pharmaceuticals"] }
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
                            new() { Name = "Hamilton", Latitude = 43.2557, Longitude = -79.8711, BoundingBox = new(43.18, -79.98, 43.32, -79.78), SuggestedNiches = ["Steel & Heavy Industry", "Health Sciences", "Advanced Manufacturing"] }
                        ]
                    },
                    new()
                    {
                        Name = "Quebec",
                        Code = "QC",
                        Cities =
                        [
                            new() { Name = "Montreal", Latitude = 45.5017, Longitude = -73.5673, BoundingBox = new(45.42, -73.72, 45.60, -73.45), SuggestedNiches = ["AI & Deep Learning", "Video Game Studios", "Aerospace Manufacturing"] },
                            new() { Name = "Quebec City", Latitude = 46.8139, Longitude = -71.2080, BoundingBox = new(46.75, -71.32, 46.90, -71.12), SuggestedNiches = ["Optics & Photonics", "Public Sector Consulting", "Tourism & Culture"] }
                        ]
                    },
                    new()
                    {
                        Name = "British Columbia",
                        Code = "BC",
                        Cities =
                        [
                            new() { Name = "Vancouver", Latitude = 49.2827, Longitude = -123.1207, BoundingBox = new(49.20, -123.25, 49.32, -123.02), SuggestedNiches = ["CleanTech & Sustainability", "Film & VFX Studios", "Real Estate Developers"] },
                            new() { Name = "Victoria", Latitude = 48.4284, Longitude = -123.3656, BoundingBox = new(48.38, -123.45, 48.50, -123.30), SuggestedNiches = ["Marine Tech", "Tourism", "Public Sector"] }
                        ]
                    },
                    new()
                    {
                        Name = "Alberta",
                        Code = "AB",
                        Cities =
                        [
                            new() { Name = "Calgary", Latitude = 51.0447, Longitude = -114.0719, BoundingBox = new(50.92, -114.22, 51.15, -113.92), SuggestedNiches = ["Energy & Utilities", "Agribusiness", "Transportation & Logistics"] },
                            new() { Name = "Edmonton", Latitude = 53.5461, Longitude = -113.4938, BoundingBox = new(53.42, -113.65, 53.65, -113.35), SuggestedNiches = ["Petrochemicals", "Health Tech", "Environmental Services"] }
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
                            new() { Name = "Riyadh", Latitude = 24.7136, Longitude = 46.6753, BoundingBox = new(24.55, 46.52, 24.88, 46.85), SuggestedNiches = ["Vision 2030 Contracting", "FinTech & Digital Payments", "Hospitality & Event Management", "Specialty Healthcare"] }
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
                            new() { Name = "Taif", Latitude = 21.4373, Longitude = 40.5127, BoundingBox = new(21.35, 40.40, 21.52, 40.60), SuggestedNiches = ["Agriculture & Rose Water", "Tourism Resorts", "Honey Trading"] }
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
                            new() { Name = "Jubail", Latitude = 27.0046, Longitude = 49.6601, BoundingBox = new(26.92, 49.52, 27.12, 49.75), SuggestedNiches = ["Petrochemical Refining", "Industrial Construction", "Plastics"] }
                        ]
                    },
                    new()
                    {
                        Name = "Medina Region",
                        Code = "MED",
                        Cities =
                        [
                            new() { Name = "Medina", Latitude = 24.5247, Longitude = 39.5692, BoundingBox = new(24.42, 39.48, 24.62, 39.68), SuggestedNiches = ["Hospitality & Lodging", "Dates Farming & Export", "Transportation Services"] }
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
                            new() { Name = "Sydney", Latitude = -33.8688, Longitude = 151.2093, BoundingBox = new(-33.95, 151.08, -33.78, 151.30), SuggestedNiches = ["Financial Services", "Coastal Tourism & Hospitality", "Legal & Accounting Practices"] },
                            new() { Name = "Newcastle", Latitude = -32.9283, Longitude = 151.7817, BoundingBox = new(-33.00, 151.68, -32.85, 151.88), SuggestedNiches = ["Port Facilities", "Mining Tech", "Clean Energy"] }
                        ]
                    },
                    new()
                    {
                        Name = "Victoria",
                        Code = "VIC",
                        Cities =
                        [
                            new() { Name = "Melbourne", Latitude = -37.8136, Longitude = 144.9631, BoundingBox = new(-37.90, 144.85, -37.72, 145.08), SuggestedNiches = ["Coffee Roasters & Specialty Cafes", "Creative Design Agencies", "Biotech Startups"] },
                            new() { Name = "Geelong", Latitude = -38.1499, Longitude = 144.3617, BoundingBox = new(-38.22, 144.28, -38.08, 144.45), SuggestedNiches = ["Advanced Manufacturing", "Health Tech", "Education"] }
                        ]
                    },
                    new()
                    {
                        Name = "Queensland",
                        Code = "QLD",
                        Cities =
                        [
                            new() { Name = "Brisbane", Latitude = -27.4698, Longitude = 153.0251, BoundingBox = new(-27.58, 152.92, -27.35, 153.15), SuggestedNiches = ["Mining & Resources", "AgTech", "Tourism Infrastructure"] },
                            new() { Name = "Gold Coast", Latitude = -28.0167, Longitude = 153.4000, BoundingBox = new(-28.12, 153.30, -27.90, 153.50), SuggestedNiches = ["Tourism & Hospitality", "Sports & Fitness", "Film Production"] }
                        ]
                    },
                    new()
                    {
                        Name = "Western Australia",
                        Code = "WA",
                        Cities =
                        [
                            new() { Name = "Perth", Latitude = -31.9505, Longitude = 115.8605, BoundingBox = new(-32.05, 115.75, -31.85, 115.98), SuggestedNiches = ["Mining & Exploration", "Renewable Energy", "Maritime Services"] }
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
                            new() { Name = "Munich", Latitude = 48.1351, Longitude = 11.5820, BoundingBox = new(48.05, 11.45, 48.22, 11.70), SuggestedNiches = ["Automotive Engineering", "Industrial IoT", "Private Banking & Wealth Management"] },
                            new() { Name = "Nuremberg", Latitude = 49.4521, Longitude = 11.0767, BoundingBox = new(49.38, 10.98, 49.52, 11.18), SuggestedNiches = ["Electronics", "Energy Tech", "Market Research"] }
                        ]
                    },
                    new()
                    {
                        Name = "Berlin",
                        Code = "BE",
                        Cities =
                        [
                            new() { Name = "Berlin", Latitude = 52.5200, Longitude = 13.4050, BoundingBox = new(52.42, 13.25, 52.62, 13.55), SuggestedNiches = ["E-Commerce Startups", "Creative Studios & Media", "Renewable Energy"] }
                        ]
                    },
                    new()
                    {
                        Name = "Hesse",
                        Code = "HE",
                        Cities =
                        [
                            new() { Name = "Frankfurt", Latitude = 50.1109, Longitude = 8.6821, BoundingBox = new(50.02, 8.55, 50.18, 8.80), SuggestedNiches = ["European Banking HQ", "FinTech Solutions", "Data Center Infrastructure"] }
                        ]
                    },
                    new()
                    {
                        Name = "Hamburg",
                        Code = "HH",
                        Cities =
                        [
                            new() { Name = "Hamburg", Latitude = 53.5511, Longitude = 9.9937, BoundingBox = new(53.45, 9.85, 53.65, 10.15), SuggestedNiches = ["Maritime Shipping", "Aviation Engineering", "Media & Advertising"] }
                        ]
                    },
                    new()
                    {
                        Name = "North Rhine-Westphalia",
                        Code = "NW",
                        Cities =
                        [
                            new() { Name = "Cologne", Latitude = 50.9375, Longitude = 6.9603, BoundingBox = new(50.88, 6.85, 51.02, 7.05), SuggestedNiches = ["Trade Fairs", "Digital Media", "Chemicals & Plastics"] },
                            new() { Name = "Dusseldorf", Latitude = 51.2277, Longitude = 6.7735, BoundingBox = new(51.18, 6.70, 51.30, 6.88), SuggestedNiches = ["Fashion & Retail", "Telecommunications", "Legal Advisory"] }
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
                        Name = "Doha",
                        Code = "DOH",
                        Cities =
                        [
                            new() { Name = "Doha", Latitude = 25.2854, Longitude = 51.5310, BoundingBox = new(25.18, 51.42, 25.38, 51.62), SuggestedNiches = ["FinTech & Islamic Banking", "Luxury Hospitality & Hotels", "Energy Contracting", "Corporate Law"] },
                            new() { Name = "Lusail", Latitude = 25.4206, Longitude = 51.4925, BoundingBox = new(25.35, 51.42, 25.48, 51.56), SuggestedNiches = ["Smart City Tech", "Entertainment & Sports", "Real Estate Investment"] }
                        ]
                    },
                    new()
                    {
                        Name = "Al Rayyan",
                        Code = "RAY",
                        Cities =
                        [
                            new() { Name = "Al Rayyan", Latitude = 25.2919, Longitude = 51.4244, BoundingBox = new(25.22, 51.35, 25.36, 51.48), SuggestedNiches = ["Education & Research Institutes", "Sports Medicine", "Commercial Retail"] }
                        ]
                    },
                    new()
                    {
                        Name = "Al Wakrah",
                        Code = "WAK",
                        Cities =
                        [
                            new() { Name = "Al Wakrah", Latitude = 25.1768, Longitude = 51.6048, BoundingBox = new(25.10, 51.52, 25.25, 51.66), SuggestedNiches = ["Port Facilities & Maritime", "Logistics Centers", "Fisheries Trading"] }
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
                            new() { Name = "Muscat", Latitude = 23.5880, Longitude = 58.3829, BoundingBox = new(23.48, 58.25, 23.68, 58.55), SuggestedNiches = ["Tourism & Heritage Lodging", "Oilfield & Energy Services", "Logistics & Cross-Border Trade"] },
                            new() { Name = "Seeb", Latitude = 23.6703, Longitude = 58.1891, BoundingBox = new(23.60, 58.10, 23.75, 58.28), SuggestedNiches = ["Wholesale Markets", "Fisheries", "Commercial Retail"] }
                        ]
                    },
                    new()
                    {
                        Name = "Dhofar Governorate",
                        Code = "DHO",
                        Cities =
                        [
                            new() { Name = "Salalah", Latitude = 17.0151, Longitude = 54.0924, BoundingBox = new(16.95, 54.00, 17.10, 54.20), SuggestedNiches = ["Free Zone Logistics", "Khareef Season Tourism", "Agriculture & Frankincense"] }
                        ]
                    },
                    new()
                    {
                        Name = "Al Batinah North",
                        Code = "BAT",
                        Cities =
                        [
                            new() { Name = "Sohar", Latitude = 24.3642, Longitude = 56.7468, BoundingBox = new(24.28, 56.65, 24.45, 56.82), SuggestedNiches = ["Port & Industrial Freezone", "Petrochemicals", "Steel & Metals"] }
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
                        Name = "Al Asimah (Capital)",
                        Code = "ASI",
                        Cities =
                        [
                            new() { Name = "Kuwait City", Latitude = 29.3759, Longitude = 47.9774, BoundingBox = new(29.30, 47.90, 29.45, 48.05), SuggestedNiches = ["Investment Banking & FinTech", "Corporate Law Firms", "Retail Megastores", "Aesthetic Healthcare"] }
                        ]
                    },
                    new()
                    {
                        Name = "Hawalli Governorate",
                        Code = "HAW",
                        Cities =
                        [
                            new() { Name = "Hawalli", Latitude = 29.3328, Longitude = 48.0281, BoundingBox = new(29.28, 48.00, 29.38, 48.08), SuggestedNiches = ["Consumer Electronics", "Commercial Shopping", "Private Clinics"] },
                            new() { Name = "Salmiya", Latitude = 29.3344, Longitude = 48.0758, BoundingBox = new(29.28, 48.02, 29.38, 48.12), SuggestedNiches = ["Seaside Hospitality", "Boutique Fashion", "Food & Beverage"] }
                        ]
                    },
                    new()
                    {
                        Name = "Al Ahmadi Governorate",
                        Code = "AHM",
                        Cities =
                        [
                            new() { Name = "Al Ahmadi", Latitude = 29.0769, Longitude = 48.0839, BoundingBox = new(29.00, 48.00, 29.15, 48.15), SuggestedNiches = ["Oil Refining & Petrochemicals", "Engineering Contracting", "Heavy Equipment"] }
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
                            new() { Name = "Manama", Latitude = 26.2285, Longitude = 50.5860, BoundingBox = new(26.18, 50.52, 26.28, 50.65), SuggestedNiches = ["Offshore Banking & FinTech", "Crypto & Blockchain Regulations", "Luxury Hospitality", "Corporate Advisory"] }
                        ]
                    },
                    new()
                    {
                        Name = "Muharraq Governorate",
                        Code = "MUH",
                        Cities =
                        [
                            new() { Name = "Muharraq", Latitude = 26.2572, Longitude = 50.6119, BoundingBox = new(26.22, 50.58, 26.30, 50.66), SuggestedNiches = ["Aviation Services", "Traditional Pearl & Heritage Trade", "Logistics"] }
                        ]
                    },
                    new()
                    {
                        Name = "Northern Governorate",
                        Code = "NOR",
                        Cities =
                        [
                            new() { Name = "Riffa", Latitude = 26.1300, Longitude = 50.5550, BoundingBox = new(26.08, 50.50, 26.18, 50.62), SuggestedNiches = ["Residential Real Estate", "Sports & Golf Facilities", "Private Education"] }
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
                        Name = "Federal Territory",
                        Code = "KL",
                        Cities =
                        [
                            new() { Name = "Kuala Lumpur", Latitude = 3.1390, Longitude = 101.6869, BoundingBox = new(3.05, 101.60, 3.25, 101.78), SuggestedNiches = ["FinTech & Islamic Finance", "Software Development Agencies", "Hospitality & Tourism", "E-Commerce D2C"] }
                        ]
                    },
                    new()
                    {
                        Name = "Selangor",
                        Code = "SGR",
                        Cities =
                        [
                            new() { Name = "Petaling Jaya", Latitude = 3.1073, Longitude = 101.6067, BoundingBox = new(3.04, 101.55, 3.16, 101.66), SuggestedNiches = ["Digital Marketing", "Tech Startups", "Consumer Goods"] },
                            new() { Name = "Shah Alam", Latitude = 3.0738, Longitude = 101.5183, BoundingBox = new(3.00, 101.45, 3.14, 101.58), SuggestedNiches = ["Automotive Assembly", "Industrial Manufacturing", "Logistics Hubs"] }
                        ]
                    },
                    new()
                    {
                        Name = "Penang",
                        Code = "PNG",
                        Cities =
                        [
                            new() { Name = "George Town", Latitude = 5.4141, Longitude = 100.3288, BoundingBox = new(5.35, 100.26, 5.48, 5.38), SuggestedNiches = ["Semiconductor Assembly", "Medical Tourism", "Heritage Hotels & Cafes"] }
                        ]
                    },
                    new()
                    {
                        Name = "Johor",
                        Code = "JHR",
                        Cities =
                        [
                            new() { Name = "Johor Bahru", Latitude = 1.4927, Longitude = 103.7414, BoundingBox = new(1.42, 103.65, 1.58, 103.82), SuggestedNiches = ["Cross-Border Singapore Services", "Petrochemicals", "Real Estate"] }
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
                        Name = "Singapore",
                        Code = "SGP",
                        Cities =
                        [
                            new() { Name = "Singapore", Latitude = 1.3521, Longitude = 103.8198, BoundingBox = new(1.22, 103.60, 1.48, 104.05), SuggestedNiches = ["Global FinTech & Wealth Management", "Regional Headquarters & SaaS", "Biomedical Sciences", "Maritime & Port Logistics"] },
                            new() { Name = "Jurong", Latitude = 1.3329, Longitude = 103.7436, BoundingBox = new(1.28, 103.68, 1.38, 103.78), SuggestedNiches = ["Advanced Manufacturing", "CleanTech Park", "Heavy Engineering"] }
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
                        Code = "L",
                        Cities =
                        [
                            new() { Name = "Dublin", Latitude = 53.3498, Longitude = -6.2603, BoundingBox = new(53.28, -6.40, 53.42, -6.15), SuggestedNiches = ["European Tech Headquarters (Silicon Docks)", "Aircraft Leasing & FinTech", "Pharma R&D"] }
                        ]
                    },
                    new()
                    {
                        Name = "Munster",
                        Code = "M",
                        Cities =
                        [
                            new() { Name = "Cork", Latitude = 51.8985, Longitude = -8.4756, BoundingBox = new(51.84, -8.56, 51.96, -8.38), SuggestedNiches = ["Biopharma & MedTech", "Information Technology", "Cybersecurity"] }
                        ]
                    },
                    new()
                    {
                        Name = "Connacht",
                        Code = "C",
                        Cities =
                        [
                            new() { Name = "Galway", Latitude = 53.2707, Longitude = -9.0568, BoundingBox = new(53.23, -9.14, 53.32, -8.98), SuggestedNiches = ["Medical Device Manufacturing", "Culture & Arts Tourism", "Software Development"] }
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
                            new() { Name = "Amsterdam", Latitude = 52.3676, Longitude = 4.9041, BoundingBox = new(52.30, 4.80, 52.44, 5.02), SuggestedNiches = ["Global FinTech & Payments", "Creative Agencies & Media", "Logistics & E-Commerce"] }
                        ]
                    },
                    new()
                    {
                        Name = "South Holland",
                        Code = "ZH",
                        Cities =
                        [
                            new() { Name = "Rotterdam", Latitude = 51.9244, Longitude = 4.4777, BoundingBox = new(51.85, 4.38, 52.00, 4.58), SuggestedNiches = ["Largest Port in Europe", "Clean Maritime Energy", "Logistics & Supply Chain"] },
                            new() { Name = "The Hague", Latitude = 52.0705, Longitude = 4.3007, BoundingBox = new(52.02, 4.22, 52.12, 4.38), SuggestedNiches = ["International Law & Arbitration", "Security & Intelligence", "Government Services"] }
                        ]
                    },
                    new()
                    {
                        Name = "North Brabant",
                        Code = "NB",
                        Cities =
                        [
                            new() { Name = "Eindhoven", Latitude = 51.4416, Longitude = 5.4697, BoundingBox = new(51.38, 5.40, 51.50, 5.55), SuggestedNiches = ["High-Tech Semiconductor Equipment", "Industrial Design", "Automotive R&D"] }
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
                            new() { Name = "Istanbul", Latitude = 41.0082, Longitude = 28.9784, BoundingBox = new(40.85, 28.75, 41.20, 29.25), SuggestedNiches = ["Health Tourism (Aesthetics & Dental)", "Textile & Garment Manufacturing", "E-Commerce Startups", "Logistics & Shipping"] },
                            new() { Name = "Bursa", Latitude = 40.1885, Longitude = 29.0610, BoundingBox = new(40.12, 28.95, 40.26, 29.18), SuggestedNiches = ["Automotive Manufacturing", "Silk & Textiles", "Food Processing"] }
                        ]
                    },
                    new()
                    {
                        Name = "Central Anatolia",
                        Code = "CEN",
                        Cities =
                        [
                            new() { Name = "Ankara", Latitude = 39.9334, Longitude = 32.8597, BoundingBox = new(39.82, 32.72, 40.05, 33.02), SuggestedNiches = ["Defense & Aerospace Industries", "Government IT Contractors", "Higher Education"] }
                        ]
                    },
                    new()
                    {
                        Name = "Aegean Region",
                        Code = "AEG",
                        Cities =
                        [
                            new() { Name = "Izmir", Latitude = 38.4237, Longitude = 27.1428, BoundingBox = new(38.32, 27.02, 38.52, 27.25), SuggestedNiches = ["Renewable Wind Energy", "Export Agriculture & Food", "Port Logistics"] }
                        ]
                    },
                    new()
                    {
                        Name = "Mediterranean Region",
                        Code = "MED",
                        Cities =
                        [
                            new() { Name = "Antalya", Latitude = 36.8969, Longitude = 30.7133, BoundingBox = new(36.80, 30.60, 36.98, 30.82), SuggestedNiches = ["Resort Hotels & Lodging", "Yachting & Coastal Tourism", "Fresh Citrus Export"] }
                        ]
                    }
                ]
            }
        ,

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
                            new() { Name = "Zurich", Latitude = 47.3769, Longitude = 8.5417, BoundingBox = new(47.30, 8.45, 47.45, 8.62), SuggestedNiches = ["Private Wealth Management & FinTech", "Boutique Medical & Aesthetic Clinics", "SaaS & AI Platforms", "Luxury E-Commerce & Retail"] },
                            new() { Name = "Winterthur", Latitude = 47.4984, Longitude = 8.7241, BoundingBox = new(47.45, 8.68, 47.55, 8.78), SuggestedNiches = ["Precision Engineering & Robotics", "Software & Cloud Services", "Higher Education Tech"] }
                        ]
                    },
                    new()
                    {
                        Name = "Geneva",
                        Code = "GE",
                        Cities =
                        [
                            new() { Name = "Geneva", Latitude = 46.2044, Longitude = 6.1432, BoundingBox = new(46.15, 6.08, 46.25, 6.22), SuggestedNiches = ["Private Banking & Asset Management", "International Corporate Law", "Luxury Horology & Jewelry", "Biotech & Diagnostics"] }
                        ]
                    },
                    new()
                    {
                        Name = "Basel-Stadt",
                        Code = "BS",
                        Cities =
                        [
                            new() { Name = "Basel", Latitude = 47.5596, Longitude = 7.5886, BoundingBox = new(47.50, 7.52, 47.60, 7.65), SuggestedNiches = ["Global Pharmaceutical HQs", "Life Sciences R&D", "Chemical Specialities", "Art & Design Galleries"] }
                        ]
                    },
                    new()
                    {
                        Name = "Bern",
                        Code = "BE",
                        Cities =
                        [
                            new() { Name = "Bern", Latitude = 46.9480, Longitude = 7.4474, BoundingBox = new(46.90, 7.38, 46.98, 7.52), SuggestedNiches = ["GovTech & Public IT", "Telecommunications", "Medical Technology & Devices"] },
                            new() { Name = "Thun", Latitude = 46.7580, Longitude = 7.6280, BoundingBox = new(46.72, 7.58, 46.80, 7.68), SuggestedNiches = ["Tourism & Alpine Hospitality", "Precision Machinery", "Digital Creative Agencies"] }
                        ]
                    },
                    new()
                    {
                        Name = "Vaud",
                        Code = "VD",
                        Cities =
                        [
                            new() { Name = "Lausanne", Latitude = 46.5197, Longitude = 6.6323, BoundingBox = new(46.48, 6.58, 46.56, 6.70), SuggestedNiches = ["Sports Tech & Global Federations", "Health Valley MedTech", "Digital Media Studios"] }
                        ]
                    },
                    new()
                    {
                        Name = "Zug",
                        Code = "ZG",
                        Cities =
                        [
                            new() { Name = "Zug", Latitude = 47.1662, Longitude = 8.5155, BoundingBox = new(47.12, 8.46, 47.22, 8.58), SuggestedNiches = ["Crypto Valley & Blockchain Hub", "Commodity Trading", "International Asset Holdings", "Corporate Consulting"] }
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
                            new() { Name = "Paris", Latitude = 48.8566, Longitude = 2.3522, BoundingBox = new(48.78, 2.22, 48.92, 2.45), SuggestedNiches = ["Luxury Fashion & Cosmetics E-Commerce", "B2B SaaS & Tech Startups (Station F)", "Corporate Law & Litigation", "High-End Gastronomy & Hospitality"] },
                            new() { Name = "Boulogne-Billancourt", Latitude = 48.8397, Longitude = 2.2399, BoundingBox = new(48.80, 2.20, 48.88, 2.28), SuggestedNiches = ["Digital Media & Advertising", "Telecom & Cloud Platforms", "Automotive Design"] }
                        ]
                    },
                    new()
                    {
                        Name = "Auvergne-Rhône-Alpes",
                        Code = "ARA",
                        Cities =
                        [
                            new() { Name = "Lyon", Latitude = 45.7640, Longitude = 4.8357, BoundingBox = new(45.70, 4.75, 45.82, 4.92), SuggestedNiches = ["Biopharma & Vaccines", "Cleantech & Smart Energy", "Fine Dining Gastronomy", "Digital Creative Studios"] },
                            new() { Name = "Grenoble", Latitude = 45.1885, Longitude = 5.7245, BoundingBox = new(45.14, 5.68, 45.24, 5.78), SuggestedNiches = ["Nanotech & Semiconductors", "Scientific Software", "Renewable Energy Research"] }
                        ]
                    },
                    new()
                    {
                        Name = "Provence-Alpes-Côte d'Azur",
                        Code = "PAC",
                        Cities =
                        [
                            new() { Name = "Marseille", Latitude = 43.2965, Longitude = 5.3698, BoundingBox = new(43.22, 5.30, 43.38, 5.48), SuggestedNiches = ["Mediterranean Maritime Logistics", "Subsea Telecom & Data Centers", "Healthcare Clinics"] },
                            new() { Name = "Nice", Latitude = 43.7102, Longitude = 7.2620, BoundingBox = new(43.65, 7.18, 43.76, 7.32), SuggestedNiches = ["Riviera Luxury Hospitality", "Sophia Antipolis Tech Hub", "Aesthetic Clinics & Wellness"] }
                        ]
                    },
                    new()
                    {
                        Name = "Occitanie",
                        Code = "OCC",
                        Cities =
                        [
                            new() { Name = "Toulouse", Latitude = 43.6047, Longitude = 1.4442, BoundingBox = new(43.54, 1.35, 43.68, 1.52), SuggestedNiches = ["Aerospace & Satellite Systems", "Embedded Software Engineering", "Biomedical R&D"] },
                            new() { Name = "Montpellier", Latitude = 43.6108, Longitude = 3.8767, BoundingBox = new(43.56, 3.82, 43.66, 3.94), SuggestedNiches = ["HealthTech & Diagnostics", "AgriTech & Viticulture", "Video Game Studios"] }
                        ]
                    },
                    new()
                    {
                        Name = "Nouvelle-Aquitaine",
                        Code = "NAQ",
                        Cities =
                        [
                            new() { Name = "Bordeaux", Latitude = 44.8378, Longitude = -0.5792, BoundingBox = new(44.78, -0.65, 44.90, -0.50), SuggestedNiches = ["Fine Wine E-Commerce & Export", "Green Tech & Photonics", "Digital Marketing Agencies"] }
                        ]
                    },
                    new()
                    {
                        Name = "Hauts-de-France",
                        Code = "HDF",
                        Cities =
                        [
                            new() { Name = "Lille", Latitude = 50.6292, Longitude = 3.0573, BoundingBox = new(50.58, 2.98, 50.68, 3.14), SuggestedNiches = ["E-Commerce & Retail Tech", "Logistics & Supply Chain", "Bio-Incubators"] }
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
                            new() { Name = "Stockholm", Latitude = 59.3293, Longitude = 18.0686, BoundingBox = new(59.25, 17.92, 59.40, 18.22), SuggestedNiches = ["Unicorn Tech & SaaS Startups", "FinTech & Open Banking", "E-Commerce Platforms & D2C", "GreenTech & Sustainable Living"] },
                            new() { Name = "Kista", Latitude = 59.4026, Longitude = 17.9443, BoundingBox = new(59.38, 17.90, 59.43, 17.98), SuggestedNiches = ["5G Telecom & Wireless Systems", "Semiconductor Design", "Cloud Infrastructure"] }
                        ]
                    },
                    new()
                    {
                        Name = "Västra Götaland",
                        Code = "O",
                        Cities =
                        [
                            new() { Name = "Gothenburg", Latitude = 57.7089, Longitude = 11.9746, BoundingBox = new(57.65, 11.88, 57.78, 12.08), SuggestedNiches = ["Automotive & EV Innovation", "Port & Maritime Logistics", "Life Sciences & MedTech"] }
                        ]
                    },
                    new()
                    {
                        Name = "Skåne",
                        Code = "M",
                        Cities =
                        [
                            new() { Name = "Malmö", Latitude = 55.6050, Longitude = 13.0038, BoundingBox = new(55.55, 12.92, 55.66, 13.10), SuggestedNiches = ["Game Development Studios", "Cross-Border Copenhagen Commerce", "Clean Energy Startups"] },
                            new() { Name = "Lund", Latitude = 55.7047, Longitude = 13.1910, BoundingBox = new(55.67, 13.15, 55.74, 13.24), SuggestedNiches = ["Biotechnology & Nanotech", "DeepTech Research", "Mobile Communications"] }
                        ]
                    },
                    new()
                    {
                        Name = "Uppsala County",
                        Code = "C",
                        Cities =
                        [
                            new() { Name = "Uppsala", Latitude = 59.8586, Longitude = 17.6389, BoundingBox = new(59.80, 17.56, 59.90, 17.70), SuggestedNiches = ["Biotech & Pharmaceuticals", "MedTech Devices", "Academic Software Labs"] }
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
                            new() { Name = "Copenhagen", Latitude = 55.6761, Longitude = 12.5683, BoundingBox = new(55.62, 12.48, 55.74, 12.65), SuggestedNiches = ["FinTech & Payment Solutions", "Medicon Valley Life Sciences", "Nordic Design & E-Commerce", "Renewable Wind Energy"] },
                            new() { Name = "Frederiksberg", Latitude = 55.6791, Longitude = 12.5348, BoundingBox = new(55.66, 12.50, 55.70, 12.56), SuggestedNiches = ["High-End Culinary & Cafes", "Boutique Creative Studios", "Digital Consulting"] }
                        ]
                    },
                    new()
                    {
                        Name = "Central Denmark",
                        Code = "M",
                        Cities =
                        [
                            new() { Name = "Aarhus", Latitude = 56.1629, Longitude = 10.2039, BoundingBox = new(56.10, 10.12, 56.22, 10.28), SuggestedNiches = ["Agri-Food Tech Innovation", "Software & IT Consultancies", "Container Port Logistics"] }
                        ]
                    },
                    new()
                    {
                        Name = "Southern Denmark",
                        Code = "S",
                        Cities =
                        [
                            new() { Name = "Odense", Latitude = 55.4038, Longitude = 10.4024, BoundingBox = new(55.35, 10.32, 55.45, 10.48), SuggestedNiches = ["Collaborative Robotics & Automation", "Drone Tech", "Health Innovation"] }
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
                        Name = "Oslo",
                        Code = "03",
                        Cities =
                        [
                            new() { Name = "Oslo", Latitude = 59.9139, Longitude = 10.7522, BoundingBox = new(59.85, 10.65, 59.98, 10.85), SuggestedNiches = ["Wealth & Energy Asset Management", "Maritime Tech & Shipping", "B2B SaaS & Enterprise Tech", "EV & Cleantech Infrastructure"] }
                        ]
                    },
                    new()
                    {
                        Name = "Vestland",
                        Code = "46",
                        Cities =
                        [
                            new() { Name = "Bergen", Latitude = 60.3913, Longitude = 5.3221, BoundingBox = new(60.34, 5.25, 60.45, 5.40), SuggestedNiches = ["Aquaculture & Salmon Export", "Subsea Offshore Engineering", "Fjord Tourism & Lodging"] }
                        ]
                    },
                    new()
                    {
                        Name = "Trøndelag",
                        Code = "50",
                        Cities =
                        [
                            new() { Name = "Trondheim", Latitude = 63.4305, Longitude = 10.3951, BoundingBox = new(63.38, 10.30, 63.48, 10.48), SuggestedNiches = ["Deeptech R&D & AI", "Semiconductors", "Renewable Hydro Energy"] }
                        ]
                    },
                    new()
                    {
                        Name = "Rogaland",
                        Code = "11",
                        Cities =
                        [
                            new() { Name = "Stavanger", Latitude = 58.9700, Longitude = 5.7331, BoundingBox = new(58.92, 5.65, 59.02, 5.80), SuggestedNiches = ["Offshore Energy & Gas", "Renewable Wind Support", "Engineering Contracting"] }
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
                            new() { Name = "Auckland", Latitude = -36.8485, Longitude = 174.7633, BoundingBox = new(-36.95, 174.65, -36.75, 174.90), SuggestedNiches = ["Cloud Software & SaaS", "FinTech & Payments", "Specialty Medical Practices", "Commercial Marine & Yachting"] }
                        ]
                    },
                    new()
                    {
                        Name = "Wellington Region",
                        Code = "WGN",
                        Cities =
                        [
                            new() { Name = "Wellington", Latitude = -41.2865, Longitude = 174.7762, BoundingBox = new(-41.35, 174.70, -41.22, 174.85), SuggestedNiches = ["GovTech Platforms", "Digital VFX & Film Production", "Boutique Roasteries & Cafes"] }
                        ]
                    },
                    new()
                    {
                        Name = "Canterbury",
                        Code = "CAN",
                        Cities =
                        [
                            new() { Name = "Christchurch", Latitude = -43.5321, Longitude = 172.6362, BoundingBox = new(-43.60, 172.52, -43.45, 172.72), SuggestedNiches = ["Aerospace & Satellite Launch", "High-Tech Manufacturing", "AgriTech Solutions"] }
                        ]
                    },
                    new()
                    {
                        Name = "Otago",
                        Code = "OTA",
                        Cities =
                        [
                            new() { Name = "Queenstown", Latitude = -45.0312, Longitude = 168.6626, BoundingBox = new(-45.08, 168.60, -44.98, 168.75), SuggestedNiches = ["Luxury Resort Hospitality", "Adventure Tourism Operators", "High-End Real Estate"] }
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
                            new() { Name = "Madrid", Latitude = 40.4168, Longitude = -3.7038, BoundingBox = new(40.32, -3.82, 40.52, -3.58), SuggestedNiches = ["FinTech & Open Banking", "Digital Nomad Platforms & SaaS", "Corporate Legal & Tax Advisory", "Hospitality & Gastronomy"] }
                        ]
                    },
                    new()
                    {
                        Name = "Catalonia",
                        Code = "CT",
                        Cities =
                        [
                            new() { Name = "Barcelona", Latitude = 41.3851, Longitude = 2.1734, BoundingBox = new(41.30, 2.08, 41.48, 2.25), SuggestedNiches = ["Mobile World & Tech Startups (22@)", "E-Commerce D2C Brands", "Biopharma & MedTech", "Creative Design Studios"] }
                        ]
                    },
                    new()
                    {
                        Name = "Valencian Community",
                        Code = "VC",
                        Cities =
                        [
                            new() { Name = "Valencia", Latitude = 39.4699, Longitude = -0.3763, BoundingBox = new(39.42, -0.45, 39.52, -0.30), SuggestedNiches = ["Port Logistics & Shipping", "Agri-Food Export", "Software & Game Studios"] }
                        ]
                    },
                    new()
                    {
                        Name = "Andalusia",
                        Code = "AN",
                        Cities =
                        [
                            new() { Name = "Seville", Latitude = 37.3891, Longitude = -5.9845, BoundingBox = new(37.32, -6.05, 37.45, -5.90), SuggestedNiches = ["Aerospace Components", "Renewable Solar Energy", "Cultural Tourism"] },
                            new() { Name = "Malaga", Latitude = 36.7213, Longitude = -4.4214, BoundingBox = new(36.67, -4.50, 36.78, -4.35), SuggestedNiches = ["Cybersecurity Hub", "International Tech Hub (Costa del Sol)", "Luxury Real Estate"] }
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
                            new() { Name = "Milan", Latitude = 45.4642, Longitude = 9.1900, BoundingBox = new(45.38, 9.08, 45.54, 9.28), SuggestedNiches = ["Global Fashion & Luxury E-Commerce", "Investment Banking & FinTech", "Industrial Design & Architecture", "Private Aesthetic Clinics"] },
                            new() { Name = "Brescia", Latitude = 45.5416, Longitude = 10.2118, BoundingBox = new(45.50, 10.15, 45.58, 10.28), SuggestedNiches = ["Precision Metalworking", "Automotive Parts", "Industrial Machinery"] }
                        ]
                    },
                    new()
                    {
                        Name = "Lazio",
                        Code = "LAZ",
                        Cities =
                        [
                            new() { Name = "Rome", Latitude = 41.9028, Longitude = 12.4964, BoundingBox = new(41.80, 12.38, 42.00, 12.60), SuggestedNiches = ["Public Sector & Defense Contracting", "Corporate Law Practices", "Heritage Tourism & Luxury Hotels"] }
                        ]
                    },
                    new()
                    {
                        Name = "Piedmont",
                        Code = "PIE",
                        Cities =
                        [
                            new() { Name = "Turin", Latitude = 45.0703, Longitude = 7.6869, BoundingBox = new(45.00, 7.60, 45.14, 7.76), SuggestedNiches = ["Automotive & EV Engineering", "Aerospace Systems", "Gourmet Food Processing"] }
                        ]
                    },
                    new()
                    {
                        Name = "Emilia-Romagna",
                        Code = "EMR",
                        Cities =
                        [
                            new() { Name = "Bologna", Latitude = 44.4949, Longitude = 11.3426, BoundingBox = new(44.45, 11.28, 44.55, 11.42), SuggestedNiches = ["Motor Valley (Supercars & Bikes)", "Packaging Automation", "Specialty Food & Wine"] }
                        ]
                    },
                    new()
                    {
                        Name = "Tuscany",
                        Code = "TOS",
                        Cities =
                        [
                            new() { Name = "Florence", Latitude = 43.7696, Longitude = 11.2558, BoundingBox = new(43.72, 11.18, 43.82, 11.32), SuggestedNiches = ["Luxury Leathercraft & Fashion", "Boutique Hospitality", "Artisan E-Commerce"] }
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
                        Code = "13",
                        Cities =
                        [
                            new() { Name = "Tokyo", Latitude = 35.6762, Longitude = 139.6503, BoundingBox = new(35.55, 139.50, 35.80, 139.85), SuggestedNiches = ["Global FinTech & Trading", "Robotics & AI Automation", "Digital Entertainment & Gaming", "B2B Enterprise Software"] }
                        ]
                    },
                    new()
                    {
                        Name = "Osaka Prefecture",
                        Code = "27",
                        Cities =
                        [
                            new() { Name = "Osaka", Latitude = 34.6937, Longitude = 135.5023, BoundingBox = new(34.60, 135.40, 34.78, 135.60), SuggestedNiches = ["Pharmaceuticals & Life Sciences", "Electronics & Battery Tech", "Wholesale Commercial Trade"] }
                        ]
                    },
                    new()
                    {
                        Name = "Kanagawa Prefecture",
                        Code = "14",
                        Cities =
                        [
                            new() { Name = "Yokohama", Latitude = 35.4437, Longitude = 139.6380, BoundingBox = new(35.35, 139.55, 35.52, 139.72), SuggestedNiches = ["Automotive Engineering", "Biotechnology R&D", "International Port Logistics"] }
                        ]
                    },
                    new()
                    {
                        Name = "Aichi Prefecture",
                        Code = "23",
                        Cities =
                        [
                            new() { Name = "Nagoya", Latitude = 35.1815, Longitude = 136.9066, BoundingBox = new(35.10, 136.80, 35.25, 137.02), SuggestedNiches = ["Automotive Heavy Industry", "Aerospace Manufacturing", "Robotics Hardware"] }
                        ]
                    },
                    new()
                    {
                        Name = "Fukuoka Prefecture",
                        Code = "40",
                        Cities =
                        [
                            new() { Name = "Fukuoka", Latitude = 33.5904, Longitude = 130.4017, BoundingBox = new(33.52, 130.32, 33.68, 130.48), SuggestedNiches = ["Startup City Japan Hub", "Asian Cross-Border Commerce", "HealthTech & Gaming"] }
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
                        Name = "Seoul Special City",
                        Code = "11",
                        Cities =
                        [
                            new() { Name = "Seoul", Latitude = 37.5665, Longitude = 126.9780, BoundingBox = new(37.45, 126.82, 37.68, 127.15), SuggestedNiches = ["K-Beauty & Cosmetic Clinics", "E-Commerce Superapps", "FinTech & Digital Banking", "Entertainment & K-Content Studios"] }
                        ]
                    },
                    new()
                    {
                        Name = "Gyeonggi-do",
                        Code = "41",
                        Cities =
                        [
                            new() { Name = "Seongnam", Latitude = 37.4200, Longitude = 127.1265, BoundingBox = new(37.35, 127.05, 37.48, 127.18), SuggestedNiches = ["Pangyo Techno Valley (SaaS & AI)", "Gaming Giants & Metaverse", "Cloud Systems"] },
                            new() { Name = "Suwon", Latitude = 37.2636, Longitude = 127.0286, BoundingBox = new(37.22, 126.96, 37.32, 127.08), SuggestedNiches = ["Semiconductor Research", "Electronics Engineering", "Display Technologies"] }
                        ]
                    },
                    new()
                    {
                        Name = "Busan Metropolitan City",
                        Code = "26",
                        Cities =
                        [
                            new() { Name = "Busan", Latitude = 35.1796, Longitude = 129.0756, BoundingBox = new(35.08, 128.98, 35.25, 129.18), SuggestedNiches = ["Maritime Logistics & Shipbuilding", "Blockchain Regulatory Free Zone", "Film & Tourism"] }
                        ]
                    },
                    new()
                    {
                        Name = "Incheon Metropolitan City",
                        Code = "28",
                        Cities =
                        [
                            new() { Name = "Incheon", Latitude = 37.4563, Longitude = 126.7052, BoundingBox = new(37.38, 126.60, 37.55, 126.78), SuggestedNiches = ["Songdo International Smart City", "Biologics Manufacturing (CDMO)", "Air Cargo Logistics"] }
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
                            new() { Name = "Central", Latitude = 22.2819, Longitude = 114.1581, BoundingBox = new(22.25, 114.12, 22.30, 114.20), SuggestedNiches = ["Global Investment Banking & Hedge Funds", "Corporate Law & Arbitration", "Family Office Services"] }
                        ]
                    },
                    new()
                    {
                        Name = "Kowloon",
                        Code = "KLN",
                        Cities =
                        [
                            new() { Name = "Kowloon", Latitude = 22.3193, Longitude = 114.1694, BoundingBox = new(22.28, 114.14, 22.36, 114.22), SuggestedNiches = ["Electronics Trading & Wholesale", "Textile & Sourcing Agencies", "Hospitality & Retail"] }
                        ]
                    },
                    new()
                    {
                        Name = "New Territories",
                        Code = "NT",
                        Cities =
                        [
                            new() { Name = "Sha Tin", Latitude = 22.3771, Longitude = 114.1974, BoundingBox = new(22.34, 114.15, 22.42, 114.24), SuggestedNiches = ["Hong Kong Science Park Tech Hub", "Biotech Research", "Robotics & Logistics"] }
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
                        Code = "37",
                        Cities =
                        [
                            new() { Name = "Tallinn", Latitude = 59.4370, Longitude = 24.7536, BoundingBox = new(59.38, 24.62, 59.50, 24.88), SuggestedNiches = ["e-Residency & Digital Incorporation", "Cybersecurity & Defense Tech", "Global B2B SaaS Startups", "FinTech & Digital Identity"] }
                        ]
                    },
                    new()
                    {
                        Name = "Tartu County",
                        Code = "78",
                        Cities =
                        [
                            new() { Name = "Tartu", Latitude = 58.3780, Longitude = 26.7290, BoundingBox = new(58.34, 26.65, 58.42, 26.78), SuggestedNiches = ["University Tech Spin-offs", "Bio-Engineering", "Software Development Labs"] }
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
                        Name = "Masovian",
                        Code = "MZ",
                        Cities =
                        [
                            new() { Name = "Warsaw", Latitude = 52.2297, Longitude = 21.0122, BoundingBox = new(52.15, 20.88, 52.32, 21.15), SuggestedNiches = ["Central European FinTech HQ", "IT Software Development Houses", "Commercial Real Estate", "E-Commerce Fulfillment"] }
                        ]
                    },
                    new()
                    {
                        Name = "Lesser Poland",
                        Code = "MA",
                        Cities =
                        [
                            new() { Name = "Krakow", Latitude = 50.0647, Longitude = 19.9450, BoundingBox = new(50.00, 19.85, 50.12, 20.05), SuggestedNiches = ["International Tech R&D Centers", "Gaming & Interactive Studios", "Specialty Medical Diagnostics"] }
                        ]
                    },
                    new()
                    {
                        Name = "Lower Silesian",
                        Code = "DS",
                        Cities =
                        [
                            new() { Name = "Wroclaw", Latitude = 51.1079, Longitude = 17.0385, BoundingBox = new(51.05, 16.92, 51.18, 17.15), SuggestedNiches = ["Silicon Lower Silesia Software", "AI & Cloud Engineering", "Electric Mobility"] }
                        ]
                    },
                    new()
                    {
                        Name = "Pomeranian",
                        Code = "PM",
                        Cities =
                        [
                            new() { Name = "Gdansk", Latitude = 54.3520, Longitude = 18.6466, BoundingBox = new(54.30, 18.52, 54.42, 18.75), SuggestedNiches = ["Baltic Maritime & Deep Sea Port", "Bio-Tech Parks", "Renewable Offshore Energy"] }
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
                        Code = "GT",
                        Cities =
                        [
                            new() { Name = "Johannesburg", Latitude = -26.2041, Longitude = 28.0473, BoundingBox = new(-26.30, 27.92, -26.10, 28.18), SuggestedNiches = ["FinTech & African Financial Hub", "Mining & Engineering Corporates", "Commercial Law Practices", "E-Commerce Retail Logistics"] },
                            new() { Name = "Pretoria", Latitude = -25.7479, Longitude = 28.2293, BoundingBox = new(-25.82, 28.15, -25.68, 28.32), SuggestedNiches = ["GovTech Consulting", "Automotive Manufacturing", "Research & Higher Education"] }
                        ]
                    },
                    new()
                    {
                        Name = "Western Cape",
                        Code = "WC",
                        Cities =
                        [
                            new() { Name = "Cape Town", Latitude = -33.9249, Longitude = 18.4241, BoundingBox = new(-34.02, 18.35, -33.85, 18.55), SuggestedNiches = ["Silicon Cape Tech Hub", "Digital Agencies & Web Studios", "Specialty Wine E-Commerce", "Coastal Tourism & Luxury Lodges"] }
                        ]
                    },
                    new()
                    {
                        Name = "KwaZulu-Natal",
                        Code = "NL",
                        Cities =
                        [
                            new() { Name = "Durban", Latitude = -29.8587, Longitude = 31.0218, BoundingBox = new(-29.95, 30.92, -29.78, 31.10), SuggestedNiches = ["Largest Container Port in Africa", "Sugar & Agri Processing", "Manufacturing & Textiles"] }
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
                        Name = "Karnataka",
                        Code = "KA",
                        Cities =
                        [
                            new() { Name = "Bengaluru", Latitude = 12.9716, Longitude = 77.5946, BoundingBox = new(12.85, 77.48, 13.12, 77.75), SuggestedNiches = ["Silicon Valley of India (SaaS, AI, Tech)", "Global Capability Centers (GCC)", "FinTech Unicorns", "HealthTech & Diagnostics"] },
                            new() { Name = "Mysuru", Latitude = 12.2958, Longitude = 76.6394, BoundingBox = new(12.25, 76.58, 12.35, 76.70), SuggestedNiches = ["IT Services & Outsourcing", "Silk & Handicrafts", "Education Tech"] }
                        ]
                    },
                    new()
                    {
                        Name = "Maharashtra",
                        Code = "MH",
                        Cities =
                        [
                            new() { Name = "Mumbai", Latitude = 19.0760, Longitude = 72.8777, BoundingBox = new(18.90, 72.75, 19.25, 73.00), SuggestedNiches = ["Financial Capital & Stock Exchanges", "Entertainment & Bollywood Media", "Corporate Legal & Tax Firms", "Commercial Real Estate"] },
                            new() { Name = "Pune", Latitude = 18.5204, Longitude = 73.8567, BoundingBox = new(18.42, 73.75, 18.62, 73.98), SuggestedNiches = ["Automotive Hub & EV", "Software Engineering Centers", "Biotech R&D"] }
                        ]
                    },
                    new()
                    {
                        Name = "Delhi NCR",
                        Code = "DL",
                        Cities =
                        [
                            new() { Name = "New Delhi", Latitude = 28.6139, Longitude = 77.2090, BoundingBox = new(28.48, 77.05, 28.75, 77.35), SuggestedNiches = ["Government & Policy Consulting", "E-Commerce Startups", "Healthcare Networks & Hospitals", "Corporate Advisory"] }
                        ]
                    },
                    new()
                    {
                        Name = "Haryana",
                        Code = "HR",
                        Cities =
                        [
                            new() { Name = "Gurugram", Latitude = 28.4595, Longitude = 77.0266, BoundingBox = new(28.38, 76.95, 28.52, 77.10), SuggestedNiches = ["Fortune 500 Corporate HQs", "B2B SaaS & Tech", "FinTech & Banking Centers"] }
                        ]
                    },
                    new()
                    {
                        Name = "Uttar Pradesh",
                        Code = "UP",
                        Cities =
                        [
                            new() { Name = "Noida", Latitude = 28.5355, Longitude = 77.3910, BoundingBox = new(28.46, 77.30, 28.62, 77.48), SuggestedNiches = ["Mobile Phone Manufacturing Hub", "Media & Broadcasting Studios", "IT Data Centers"] }
                        ]
                    },
                    new()
                    {
                        Name = "Telangana",
                        Code = "TG",
                        Cities =
                        [
                            new() { Name = "Hyderabad", Latitude = 17.3850, Longitude = 78.4867, BoundingBox = new(17.28, 78.32, 17.50, 78.60), SuggestedNiches = ["Genome Valley (Pharma & Biotech)", "HITEC City Software Campuses", "Defense & Aerospace"] }
                        ]
                    },
                    new()
                    {
                        Name = "Tamil Nadu",
                        Code = "TN",
                        Cities =
                        [
                            new() { Name = "Chennai", Latitude = 13.0827, Longitude = 80.2707, BoundingBox = new(12.95, 80.15, 13.20, 80.35), SuggestedNiches = ["Detroit of South Asia (Automotive)", "SaaS & Product Tech", "Medical Tourism & Healthcare"] }
                        ]
                    },
                    new()
                    {
                        Name = "Gujarat",
                        Code = "GJ",
                        Cities =
                        [
                            new() { Name = "Ahmedabad", Latitude = 23.0225, Longitude = 72.5714, BoundingBox = new(22.92, 22.45, 23.12, 72.68), SuggestedNiches = ["Textiles & Chemicals", "Pharmaceuticals", "GIFT City FinTech"] }
                        ]
                    }
                ]
            }

        ];
    }
}
