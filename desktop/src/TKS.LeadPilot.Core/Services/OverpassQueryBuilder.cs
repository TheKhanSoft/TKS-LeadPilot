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
        ["Karachi"] = new(24.75, 66.85, 25.10, 67.25),
        ["Lahore"] = new(31.40, 74.15, 31.65, 74.45),
        ["Islamabad"] = new(33.60, 72.95, 33.78, 73.18),
        ["Rawalpindi"] = new(33.50, 73.00, 33.65, 73.15),
        ["Faisalabad"] = new(31.35, 73.00, 31.50, 73.18),
        ["Peshawar"] = new(33.95, 71.45, 34.05, 71.60),
        ["Multan"] = new(30.12, 71.40, 30.25, 71.55)
    };

    public static readonly Dictionary<string, List<string>> CategoryPresets = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Healthcare & Dental"] = ["clinic", "dentist", "pharmacy", "hospital", "doctors"],
        ["Restaurants & Food"] = ["restaurant", "fast_food", "cafe"],
        ["Hotels & Lodging"] = ["hotel", "guest_house", "motel"],
        ["Retail & Shopping"] = ["supermarket", "clothes", "electronics", "department_store"],
        ["Automotive & Repair"] = ["car_repair", "car_rental", "fuel"],
        ["Education & Training"] = ["school", "college", "university"]
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
