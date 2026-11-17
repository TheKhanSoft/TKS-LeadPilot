using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TKS.LeadPilot.Core.Models;

namespace TKS.LeadPilot.Core.Services;

public static class OverpassQueryBuilder
{
    public static readonly Dictionary<string, BoundingBox> CityBoundingBoxes = new(StringComparer.OrdinalIgnoreCase)
    {
        // Pakistan
        ["Karachi"] = new(24.75, 66.85, 25.10, 67.25),
        ["Lahore"] = new(31.40, 74.15, 31.65, 74.45),
        ["Islamabad"] = new(33.60, 72.95, 33.78, 73.18),
        ["Rawalpindi"] = new(33.50, 73.00, 33.65, 73.15),
        ["Faisalabad"] = new(31.35, 73.00, 31.50, 73.18),
        ["Peshawar"] = new(33.95, 71.45, 34.05, 71.60),
        ["Multan"] = new(30.12, 71.40, 30.25, 71.55),
        ["Gujranwala"] = new(32.12, 74.12, 32.25, 74.26),
        ["Sialkot"] = new(32.44, 74.46, 32.56, 74.58),
        ["Sargodha"] = new(32.02, 72.60, 32.15, 72.75),
        ["Bahawalpur"] = new(29.34, 71.62, 29.46, 71.76),
        ["Sheikhupura"] = new(31.65, 73.90, 31.78, 74.05),
        ["Gujrat"] = new(32.52, 74.01, 32.64, 74.15),
        ["Jhang"] = new(31.21, 72.25, 31.33, 72.39),
        ["Sahiwal"] = new(30.61, 73.04, 30.73, 73.18),
        ["Rahim Yar Khan"] = new(28.36, 70.24, 28.48, 70.37),
        ["Kasur"] = new(31.06, 74.39, 31.18, 74.52),
        ["Okara"] = new(30.75, 73.38, 30.87, 73.51),
        ["Dera Ghazi Khan"] = new(30.00, 70.57, 30.12, 70.71),
        ["Hyderabad"] = new(25.32, 68.28, 25.45, 68.42),
        ["Sukkur"] = new(27.65, 68.80, 27.76, 68.92),
        ["Larkana"] = new(27.50, 68.15, 27.62, 68.28),
        ["Nawabshah"] = new(26.19, 68.35, 26.31, 68.48),
        ["Mirpur Khas"] = new(25.47, 68.95, 25.59, 69.08),
        ["Jacobabad"] = new(28.22, 68.37, 28.34, 68.50),
        ["Abbottabad"] = new(34.12, 73.18, 34.22, 73.28),
        ["Mardan"] = new(34.14, 71.97, 34.26, 72.11),
        ["Swat"] = new(34.70, 72.30, 34.84, 72.43),
        ["Kohat"] = new(33.52, 71.37, 33.65, 71.51),
        ["Dera Ismail Khan"] = new(31.77, 70.83, 31.89, 70.97),
        ["Haripur"] = new(33.93, 72.86, 34.06, 73.00),
        ["Mansehra"] = new(34.27, 73.13, 34.39, 73.26),
        ["Quetta"] = new(30.12, 66.92, 30.24, 67.04),
        ["Gwadar"] = new(25.08, 62.28, 25.18, 62.38),
        ["Turbat"] = new(25.95, 62.98, 26.07, 63.12),
        ["Khuzdar"] = new(27.74, 66.55, 27.86, 66.68),
        ["Hub"] = new(24.84, 66.82, 24.96, 66.95),
        ["Gilgit"] = new(35.86, 74.25, 35.98, 74.38),
        ["Skardu"] = new(35.24, 75.56, 35.36, 75.70),
        ["Hunza"] = new(36.26, 74.58, 36.38, 74.72),
        ["Muzaffarabad"] = new(34.31, 73.41, 34.43, 73.54),
        ["Mirpur"] = new(33.09, 73.68, 33.21, 73.82),
        ["Rawalakot"] = new(33.80, 73.70, 33.92, 73.83),

        // International Hubs
        ["Los Angeles"] = new(33.90, -118.50, 34.20, -118.15),
        ["San Francisco"] = new(37.70, -122.52, 37.83, -122.35),
        ["New York City"] = new(40.58, -74.15, 40.85, -73.85),
        ["Houston"] = new(29.60, -95.55, 29.90, -95.20),
        ["Miami"] = new(25.68, -80.30, 25.88, -80.12),
        ["London"] = new(51.40, -0.30, 51.62, 0.05),
        ["Manchester"] = new(53.42, -2.32, 53.54, -2.16),
        ["Dubai"] = new(25.05, 55.10, 25.35, 55.45),
        ["Abu Dhabi"] = new(24.35, 54.28, 24.55, 54.50),
        ["Toronto"] = new(43.58, -79.55, 43.78, -79.25),
        ["Vancouver"] = new(49.20, -123.25, 49.32, -123.02),
        ["Riyadh"] = new(24.55, 46.52, 24.88, 46.85),
        ["Jeddah"] = new(21.40, 39.05, 21.72, 39.30),
        ["Sydney"] = new(-33.95, 151.08, -33.78, 151.30),
        ["Melbourne"] = new(-37.90, 144.85, -37.72, 145.08),
        ["Munich"] = new(48.05, 11.45, 48.22, 11.70),
        ["Berlin"] = new(52.42, 13.25, 52.62, 13.55)
    };

    public static readonly Dictionary<string, List<string>> CategoryPresets = new(StringComparer.OrdinalIgnoreCase)
    {
        // Core Sectors
        ["Healthcare & Dental"] = ["clinic", "dentist", "pharmacy", "hospital", "doctors"],
        ["Restaurants & Food"] = ["restaurant", "fast_food", "cafe"],
        ["Hotels & Lodging"] = ["hotel", "guest_house", "motel"],
        ["Retail & Shopping"] = ["supermarket", "clothes", "electronics", "department_store"],
        ["Automotive & Repair"] = ["car_repair", "car_rental", "fuel"],
        ["Education & Training"] = ["school", "college", "university"],

        // Expanded Commercial Niches
        ["Software & IT Agencies"] = ["it", "computer", "telecommunication", "office"],
        ["Legal & Corporate Law"] = ["lawyer", "legal", "notary", "office"],
        ["Finance & Accounting"] = ["bank", "accountant", "financial", "insurance"],
        ["Real Estate & Builders"] = ["estate_agent", "real_estate", "property_management"],
        ["Fitness Centers & Gyms"] = ["fitness_centre", "gym", "sports_centre"],
        ["Beauty Salons & Spas"] = ["hairdresser", "beauty", "spa"],
        ["Logistics & Warehousing"] = ["courier", "logistics", "storage_rental"],
        ["Manufacturing & Industrial"] = ["industrial", "factory", "warehouse"],
        ["Architecture & Construction"] = ["architect", "construction", "contractor"],
        ["Travel & Tourism Agencies"] = ["travel_agency", "tourist_guide"],
        ["Marketing & Media Studios"] = ["advertising_agency", "design", "photography"],
        ["Security & Facility Services"] = ["security", "cleaning"]
    };

    public static string BuildQuery(DiscoveryQuery query)
    {
        var timeout = Math.Max(15, Math.Min(60, query.TimeoutSeconds));
        var limit = Math.Max(10, Math.Min(500, query.Limit));

        var sb = new StringBuilder();
        sb.AppendLine($"[out:json][timeout:{timeout}];");

        // Use bounding box if provided or matched by city
        BoundingBox? bbox = query.BBox;
        if (bbox == null && !string.IsNullOrWhiteSpace(query.City) && CityBoundingBoxes.TryGetValue(query.City, out var cityBbox))
        {
            bbox = cityBbox;
        }

        var amenities = (query.TargetAmenities != null && query.TargetAmenities.Count > 0)
            ? query.TargetAmenities
            : ["clinic", "dentist", "pharmacy"];

        if (bbox != null)
        {
            // Bounding box query
            var bboxStr = $"{bbox.South:F4},{bbox.West:F4},{bbox.North:F4},{bbox.East:F4}";
            sb.AppendLine("(");
            foreach (var amenity in amenities)
            {
                var clean = amenity.Trim().ToLowerInvariant();
                sb.AppendLine($"  node[\"amenity\"=\"{clean}\"]({bboxStr});");
                sb.AppendLine($"  way[\"amenity\"=\"{clean}\"]({bboxStr});");
                sb.AppendLine($"  node[\"shop\"=\"{clean}\"]({bboxStr});");
                sb.AppendLine($"  way[\"shop\"=\"{clean}\"]({bboxStr});");
                sb.AppendLine($"  node[\"tourism\"=\"{clean}\"]({bboxStr});");
                sb.AppendLine($"  way[\"tourism\"=\"{clean}\"]({bboxStr});");
            }
            sb.AppendLine(");");
        }
        else
        {
            // Area-based query fallback
            var cityEscaped = query.City.Trim().Replace("\"", "\\\"");
            sb.AppendLine($"area[\"name\"=\"{cityEscaped}\"]->.searchArea;");
            sb.AppendLine("(");
            foreach (var amenity in amenities)
            {
                var clean = amenity.Trim().ToLowerInvariant();
                sb.AppendLine($"  node[\"amenity\"=\"{clean}\"](area.searchArea);");
                sb.AppendLine($"  way[\"amenity\"=\"{clean}\"](area.searchArea);");
                sb.AppendLine($"  node[\"shop\"=\"{clean}\"](area.searchArea);");
                sb.AppendLine($"  way[\"shop\"=\"{clean}\"](area.searchArea);");
                sb.AppendLine($"  node[\"tourism\"=\"{clean}\"](area.searchArea);");
                sb.AppendLine($"  way[\"tourism\"=\"{clean}\"](area.searchArea);");
            }
            sb.AppendLine(");");
        }

        sb.AppendLine($"out center {limit};");
        return sb.ToString();
    }
}
