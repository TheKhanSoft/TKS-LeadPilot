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
            StateProvince = "Punjab",
            City = "Lahore",
            Latitude = 31.5204,
            Longitude = 74.3587,
            RadiusKm = radiusKm,
            BoundingBox = CalculateProximityBoundingBox(31.5204, 74.3587, radiusKm),
            IsDetectedFromIp = false,
            Message = "Defaulting to Lahore, Pakistan"
        };

        if (_httpClient != null)
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(3)); // 3-second strict timeout

                // IP Geolocation probe (non-blocking, lightweight)
                var response = await _httpClient.GetStringAsync("https://ipapi.co/json/", cts.Token);
                using var doc = JsonDocument.Parse(response);
                var root = doc.RootElement;

                if (root.TryGetProperty("latitude", out var latProp) && latProp.TryGetDouble(out var lat) &&
                    root.TryGetProperty("longitude", out var lonProp) && lonProp.TryGetDouble(out var lon))
                {
                    result.Latitude = lat;
                    result.Longitude = lon;
                    result.RadiusKm = radiusKm;
                    result.BoundingBox = CalculateProximityBoundingBox(lat, lon, radiusKm);
                    result.IsDetectedFromIp = true;

                    if (root.TryGetProperty("city", out var cityProp)) result.City = cityProp.GetString() ?? result.City;
                    if (root.TryGetProperty("region", out var regProp)) result.StateProvince = regProp.GetString() ?? result.StateProvince;
                    if (root.TryGetProperty("country_name", out var countryProp)) result.Country = countryProp.GetString() ?? result.Country;

                    result.Message = $"Detected approximate location: {result.City}, {result.StateProvince}, {result.Country}";
                }
            }
            catch
            {
                // Fallback gracefully without throwing
                result.Message = "Could not resolve IP location; using regional anchor (Lahore, PK).";
            }
        }

        return result;
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
                            new() { Name = "Sialkot", Latitude = 32.4945, Longitude = 74.5229, BoundingBox = new(32.44, 74.46, 32.56, 74.58), SuggestedNiches = ["Surgical Instruments Exporters", "Sportswear & Leather Goods", "Sports Equipment Manufacturers", "Musical Instruments"] }
                        ]
                    },
                    new()
                    {
                        Name = "Sindh",
                        Code = "SD",
                        Cities =
                        [
                            new() { Name = "Karachi", Latitude = 24.8607, Longitude = 67.0011, BoundingBox = new(24.75, 66.85, 25.10, 67.25), SuggestedNiches = ["Textile & Apparel Exporters", "Private Diagnostic Clinics", "Corporate Law & Tax Firms", "Freight Forwarding & Logistics", "E-Commerce D2C Brands"] },
                            new() { Name = "Hyderabad", Latitude = 25.3960, Longitude = 68.3578, BoundingBox = new(25.32, 68.28, 25.45, 68.42), SuggestedNiches = ["Glass & Bangle Manufacturers", "Medical Centers & Pharmacies", "Auto Parts Wholesalers"] },
                            new() { Name = "Sukkur", Latitude = 27.7052, Longitude = 68.8574, BoundingBox = new(27.65, 68.80, 27.76, 68.92), SuggestedNiches = ["Agri Trading & Dates Exporters", "Wholesale Distribution", "River Transport"] }
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
                            new() { Name = "Abbottabad", Latitude = 34.1688, Longitude = 73.2215, BoundingBox = new(34.12, 73.18, 34.22, 73.28), SuggestedNiches = ["Tourism & Resort Hotels", "Private Medical Colleges", "Boarding Schools", "Real Estate"] }
                        ]
                    },
                    new()
                    {
                        Name = "Balochistan",
                        Code = "BA",
                        Cities =
                        [
                            new() { Name = "Quetta", Latitude = 30.1798, Longitude = 66.9750, BoundingBox = new(30.12, 66.92, 30.24, 67.04), SuggestedNiches = ["Mineral & Mining Traders", "Dry Fruit Distributors", "Transport & Warehousing"] },
                            new() { Name = "Gwadar", Latitude = 25.1264, Longitude = 62.3225, BoundingBox = new(25.08, 62.28, 25.18, 62.38), SuggestedNiches = ["Deep Sea Port Logistics", "Coastal Real Estate & Development", "Fisheries & Marine Export"] }
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
                            new() { Name = "San Diego", Latitude = 32.7157, Longitude = -117.1611, BoundingBox = new(32.60, -117.28, 32.85, -117.05), SuggestedNiches = ["Biotech & Life Sciences", "Craft Breweries", "Tourism & Lodging"] }
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
                            new() { Name = "Dallas", Latitude = 32.7767, Longitude = -96.7970, BoundingBox = new(32.65, -96.95, 32.90, -96.65), SuggestedNiches = ["Financial Services & Wealth Management", "Telecom & Data Solutions", "Corporate HQ Services"] }
                        ]
                    },
                    new()
                    {
                        Name = "Florida",
                        Code = "FL",
                        Cities =
                        [
                            new() { Name = "Miami", Latitude = 25.7617, Longitude = -80.1918, BoundingBox = new(25.68, -80.30, 25.88, -80.12), SuggestedNiches = ["Luxury Real Estate", "Aesthetic Clinics & Wellness", "Hospitality & Yachting"] }
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
                            new() { Name = "Birmingham", Latitude = 52.4862, Longitude = -1.8904, BoundingBox = new(52.40, -2.00, 52.55, -1.78), SuggestedNiches = ["Jewellery & Precious Metals", "Automotive Engineering", "Healthcare Services"] }
                        ]
                    },
                    new()
                    {
                        Name = "Scotland",
                        Code = "SCT",
                        Cities =
                        [
                            new() { Name = "Edinburgh", Latitude = 55.9533, Longitude = -3.1883, BoundingBox = new(55.88, -3.30, 56.00, -3.10), SuggestedNiches = ["Financial & Asset Management", "Tourism & Heritage Lodging", "Tech Startups"] }
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
                            new() { Name = "Abu Dhabi", Latitude = 24.4539, Longitude = 54.3773, BoundingBox = new(24.35, 54.28, 24.55, 54.50), SuggestedNiches = ["Oil & Gas Contracting", "Government Advisory", "Healthcare & Specialty Hospitals"] }
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
                            new() { Name = "Ottawa", Latitude = 45.4215, Longitude = -75.6972, BoundingBox = new(45.32, -75.85, 45.48, -75.58), SuggestedNiches = ["GovTech Solutions", "Security & Defense Consulting"] }
                        ]
                    },
                    new()
                    {
                        Name = "British Columbia",
                        Code = "BC",
                        Cities =
                        [
                            new() { Name = "Vancouver", Latitude = 49.2827, Longitude = -123.1207, BoundingBox = new(49.20, -123.25, 49.32, -123.02), SuggestedNiches = ["CleanTech & Sustainability", "Film & VFX Studios", "Real Estate Developers"] }
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
                            new() { Name = "Jeddah", Latitude = 21.5433, Longitude = 39.1728, BoundingBox = new(21.40, 39.05, 21.72, 39.30), SuggestedNiches = ["Port & Maritime Logistics", "Hospitality & Hotels", "Retail Megamalls"] }
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
                            new() { Name = "Sydney", Latitude = -33.8688, Longitude = 151.2093, BoundingBox = new(-33.95, 151.08, -33.78, 151.30), SuggestedNiches = ["Financial Services", "Coastal Tourism & Hospitality", "Legal & Accounting Practices"] }
                        ]
                    },
                    new()
                    {
                        Name = "Victoria",
                        Code = "VIC",
                        Cities =
                        [
                            new() { Name = "Melbourne", Latitude = -37.8136, Longitude = 144.9631, BoundingBox = new(-37.90, 144.85, -37.72, 145.08), SuggestedNiches = ["Coffee Roasters & Specialty Cafes", "Creative Design Agencies", "Biotech Startups"] }
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
                            new() { Name = "Munich", Latitude = 48.1351, Longitude = 11.5820, BoundingBox = new(48.05, 11.45, 48.22, 11.70), SuggestedNiches = ["Automotive Engineering", "Industrial IoT", "Private Banking & Wealth Management"] }
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
                    }
                ]
            }
        ];
    }
}
