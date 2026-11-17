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
        ["Abbottabad"] = new(34.12, 73.18, 34.22, 73.28),
        ["Quetta"] = new(30.12, 66.92, 30.24, 67.04),
        ["Gwadar"] = new(25.08, 62.28, 25.18, 62.38),

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
