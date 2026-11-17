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
        ["San Diego"] = new(32.60, -117.28, 32.85, -117.05),
        ["San Jose"] = new(37.25, -122.00, 37.45, -121.75),
        ["New York City"] = new(40.58, -74.15, 40.85, -73.85),
        ["Houston"] = new(29.60, -95.55, 29.90, -95.20),
        ["Austin"] = new(30.15, -97.88, 30.40, -97.62),
        ["Dallas"] = new(32.65, -96.95, 32.90, -96.65),
        ["San Antonio"] = new(29.30, -98.65, 29.55, -98.35),
        ["Miami"] = new(25.68, -80.30, 25.88, -80.12),
        ["Orlando"] = new(28.40, -81.50, 28.65, -81.25),
        ["Tampa"] = new(27.85, -82.58, 28.08, -82.35),
        ["Chicago"] = new(41.70, -87.80, 42.02, -87.55),
        ["Seattle"] = new(47.50, -122.45, 47.72, -122.22),
        ["Boston"] = new(42.28, -71.18, 42.42, -70.95),
        ["Atlanta"] = new(33.65, -84.50, 33.88, -84.28),
        ["Denver"] = new(39.62, -105.10, 39.85, -104.85),
        ["Las Vegas"] = new(36.05, -115.30, 36.28, -115.05),

        ["London"] = new(51.40, -0.30, 51.62, 0.05),
        ["Manchester"] = new(53.42, -2.32, 53.54, -2.16),
        ["Birmingham"] = new(52.40, -2.00, 52.55, -1.78),
        ["Leeds"] = new(53.72, -1.65, 53.88, -1.45),
        ["Liverpool"] = new(53.35, -3.05, 53.48, -2.90),
        ["Bristol"] = new(51.40, -2.68, 51.52, -2.50),
        ["Newcastle"] = new(54.92, -1.72, 55.05, -1.52),
        ["Edinburgh"] = new(55.88, -3.30, 56.00, -3.10),
        ["Glasgow"] = new(55.80, -4.38, 55.92, -4.15),
        ["Cardiff"] = new(51.42, -3.28, 51.55, -3.10),
        ["Belfast"] = new(54.52, -6.02, 54.68, -5.85),

        ["Dubai"] = new(25.05, 55.10, 25.35, 55.45),
        ["Abu Dhabi"] = new(24.35, 54.28, 24.55, 54.50),
        ["Al Ain"] = new(24.12, 55.65, 24.28, 55.85),
        ["Sharjah"] = new(25.28, 55.35, 25.42, 55.50),
        ["Ajman"] = new(25.35, 55.45, 25.45, 55.58),
        ["Ras Al Khaimah"] = new(25.70, 55.88, 25.88, 56.02),

        ["Toronto"] = new(43.58, -79.55, 43.78, -79.25),
        ["Ottawa"] = new(45.32, -75.85, 45.48, -75.58),
        ["Mississauga"] = new(43.50, -79.75, 43.68, -79.55),
        ["Hamilton"] = new(43.18, -79.98, 43.32, -79.78),
        ["Montreal"] = new(45.42, -73.72, 45.60, -73.45),
        ["Vancouver"] = new(49.20, -123.25, 49.32, -123.02),
        ["Calgary"] = new(50.92, -114.22, 51.15, -113.92),
        ["Edmonton"] = new(53.42, -113.65, 53.65, -113.35),

        ["Riyadh"] = new(24.55, 46.52, 24.88, 46.85),
        ["Jeddah"] = new(21.40, 39.05, 21.72, 39.30),
        ["Mecca"] = new(21.30, 39.75, 21.48, 39.95),
        ["Medina"] = new(24.42, 39.48, 24.62, 39.68),
        ["Dammam"] = new(26.32, 49.98, 26.52, 50.18),
        ["Khobar"] = new(26.12, 50.10, 26.30, 50.28),
        ["Jubail"] = new(26.92, 49.52, 27.12, 49.75),

        ["Sydney"] = new(-33.95, 151.08, -33.78, 151.30),
        ["Melbourne"] = new(-37.90, 144.85, -37.72, 145.08),
        ["Brisbane"] = new(-27.58, 152.92, -27.35, 153.15),
        ["Gold Coast"] = new(-28.12, 153.30, -27.90, 153.50),
        ["Perth"] = new(-32.05, 115.75, -31.85, 115.98),

        ["Munich"] = new(48.05, 11.45, 48.22, 11.70),
        ["Berlin"] = new(52.42, 13.25, 52.62, 13.55),
        ["Frankfurt"] = new(50.02, 8.55, 50.18, 8.80),
        ["Hamburg"] = new(53.45, 9.85, 53.65, 10.15),
        ["Cologne"] = new(50.88, 6.85, 51.02, 7.05),
        ["Dusseldorf"] = new(51.18, 6.70, 51.30, 6.88),

        ["Doha"] = new(25.18, 51.42, 25.38, 51.62),
        ["Lusail"] = new(25.35, 51.42, 25.48, 51.56),
        ["Al Rayyan"] = new(25.22, 51.35, 25.36, 51.48),
        ["Al Wakrah"] = new(25.10, 51.52, 25.25, 51.66),

        ["Muscat"] = new(23.48, 58.25, 23.68, 58.55),
        ["Salalah"] = new(16.95, 54.00, 17.10, 54.20),
        ["Sohar"] = new(24.28, 56.65, 24.45, 56.82),

        ["Kuwait City"] = new(29.30, 47.90, 29.45, 48.05),
        ["Hawalli"] = new(29.28, 48.00, 29.38, 48.08),
        ["Salmiya"] = new(29.28, 48.02, 29.38, 48.12),
        ["Al Ahmadi"] = new(29.00, 48.00, 29.15, 48.15),

        ["Manama"] = new(26.18, 50.52, 26.28, 50.65),
        ["Riffa"] = new(26.08, 50.50, 26.18, 50.62),

        ["Kuala Lumpur"] = new(3.05, 101.60, 3.25, 101.78),
        ["Petaling Jaya"] = new(3.04, 101.55, 3.16, 101.66),
        ["George Town"] = new(5.35, 100.26, 5.48, 5.38),
        ["Johor Bahru"] = new(1.42, 103.65, 1.58, 103.82),

        ["Singapore"] = new(1.22, 103.60, 1.48, 104.05),

        ["Dublin"] = new(53.28, -6.40, 53.42, -6.15),
        ["Cork"] = new(51.84, -8.56, 51.96, -8.38),
        ["Galway"] = new(53.23, -9.14, 53.32, -8.98),

        ["Amsterdam"] = new(52.30, 4.80, 52.44, 5.02),
        ["Rotterdam"] = new(51.85, 4.38, 52.00, 4.58),
        ["The Hague"] = new(52.02, 4.22, 52.12, 4.38),
        ["Eindhoven"] = new(51.38, 5.40, 51.50, 5.55),

        ["Istanbul"] = new(40.85, 28.75, 41.20, 29.25),
        ["Ankara"] = new(39.82, 32.72, 40.05, 33.02),
        ["Izmir"] = new(38.32, 27.02, 38.52, 27.25),
        ["Bursa"] = new(40.12, 28.95, 40.26, 29.18),
        ["Antalya"] = new(36.80, 30.60, 36.98, 30.82),

        // Switzerland
        ["Zurich"] = new(47.30, 8.45, 47.45, 8.62),
        ["Winterthur"] = new(47.45, 8.68, 47.55, 8.78),
        ["Geneva"] = new(46.15, 6.08, 46.25, 6.22),
        ["Basel"] = new(47.50, 7.52, 47.60, 7.65),
        ["Bern"] = new(46.90, 7.38, 46.98, 7.52),
        ["Lausanne"] = new(46.48, 6.58, 46.56, 6.70),
        ["Zug"] = new(47.12, 8.46, 47.22, 8.58),

        // France
        ["Paris"] = new(48.78, 2.22, 48.92, 2.45),
        ["Lyon"] = new(45.70, 4.75, 45.82, 4.92),
        ["Marseille"] = new(43.22, 5.30, 43.38, 5.48),
        ["Nice"] = new(43.65, 7.18, 43.76, 7.32),
        ["Toulouse"] = new(43.54, 1.35, 43.68, 1.52),
        ["Bordeaux"] = new(44.78, -0.65, 44.90, -0.50),
        ["Lille"] = new(50.58, 2.98, 50.68, 3.14),

        // Nordic Countries (Sweden, Denmark, Norway)
        ["Stockholm"] = new(59.25, 17.92, 59.40, 18.22),
        ["Gothenburg"] = new(57.65, 11.88, 57.78, 12.08),
        ["Malmo"] = new(55.55, 12.92, 55.66, 13.10),
        ["Copenhagen"] = new(55.62, 12.48, 55.74, 12.65),
        ["Aarhus"] = new(56.10, 10.12, 56.22, 10.28),
        ["Odense"] = new(55.35, 10.32, 55.45, 10.48),
        ["Oslo"] = new(59.85, 10.65, 59.98, 10.85),
        ["Bergen"] = new(60.34, 5.25, 60.45, 5.40),
        ["Trondheim"] = new(63.38, 10.30, 63.48, 10.48),
        ["Stavanger"] = new(58.92, 5.65, 59.02, 5.80),

        // New Zealand
        ["Auckland"] = new(-36.95, 174.65, -36.75, 174.90),
        ["Wellington"] = new(-41.35, 174.70, -41.22, 174.85),
        ["Christchurch"] = new(-43.60, 172.52, -43.45, 172.72),
        ["Queenstown"] = new(-45.08, 168.60, -44.98, 168.75),

        // Southern & Central Europe (Spain, Italy, Poland, Estonia)
        ["Madrid"] = new(40.32, -3.82, 40.52, -3.58),
        ["Barcelona"] = new(41.30, 2.08, 41.48, 2.25),
        ["Valencia"] = new(39.42, -0.45, 39.52, -0.30),
        ["Seville"] = new(37.32, -6.05, 37.45, -5.90),
        ["Malaga"] = new(36.67, -4.50, 36.78, -4.35),
        ["Milan"] = new(45.38, 9.08, 45.54, 9.28),
        ["Rome"] = new(41.80, 12.38, 42.00, 12.60),
        ["Turin"] = new(45.00, 7.60, 45.14, 7.76),
        ["Bologna"] = new(44.45, 11.28, 44.55, 11.42),
        ["Florence"] = new(43.72, 11.18, 43.82, 11.32),
        ["Warsaw"] = new(52.15, 20.88, 52.32, 21.15),
        ["Krakow"] = new(50.00, 19.85, 50.12, 20.05),
        ["Wroclaw"] = new(51.05, 16.92, 51.18, 17.15),
        ["Tallinn"] = new(59.38, 24.62, 59.50, 24.88),
        ["Tartu"] = new(58.34, 26.65, 58.42, 26.78),

        // Asia-Pacific & Africa (Japan, South Korea, Hong Kong, South Africa, India)
        ["Tokyo"] = new(35.55, 139.50, 35.80, 139.85),
        ["Osaka"] = new(34.60, 135.40, 34.78, 135.60),
        ["Yokohama"] = new(35.35, 139.55, 35.52, 139.72),
        ["Nagoya"] = new(35.10, 136.80, 35.25, 137.02),
        ["Fukuoka"] = new(33.52, 130.32, 33.68, 130.48),
        ["Seoul"] = new(37.45, 126.82, 37.68, 127.15),
        ["Seongnam"] = new(37.35, 127.05, 37.48, 127.18),
        ["Busan"] = new(35.08, 128.98, 35.25, 129.18),
        ["Incheon"] = new(37.38, 126.60, 37.55, 126.78),
        ["Hong Kong"] = new(22.25, 114.12, 22.36, 114.22),
        ["Johannesburg"] = new(-26.30, 27.92, -26.10, 28.18),
        ["Cape Town"] = new(-34.02, 18.35, -33.85, 18.55),
        ["Durban"] = new(-29.95, 30.92, -29.78, 31.10),
        ["Bengaluru"] = new(12.85, 77.48, 13.12, 77.75),
        ["Mumbai"] = new(18.90, 72.75, 19.25, 73.00),
        ["Pune"] = new(18.42, 73.75, 18.62, 73.98),
        ["New Delhi"] = new(28.48, 77.05, 28.75, 77.35),
        ["Gurugram"] = new(28.38, 76.95, 28.52, 77.10),
        ["Noida"] = new(28.46, 77.30, 28.62, 77.48),
        ["Hyderabad"] = new(17.28, 78.32, 17.50, 78.60),
        ["Chennai"] = new(12.95, 80.15, 13.20, 80.35),
        ["Ahmedabad"] = new(22.92, 22.45, 23.12, 72.68)
    };

    public static readonly Dictionary<string, List<string>> CategoryPresets = new(StringComparer.OrdinalIgnoreCase)
    {
        // Universal Full City Discovery
        ["⭐ All Commercial Businesses & Enterprises"] = ["all_commercial", "clinic", "hospital", "dentist", "pharmacy", "restaurant", "hotel", "bank", "it", "lawyer", "solar", "tailor", "factory", "industrial", "contractor", "shop"],

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
        ["Security & Facility Services"] = ["security", "cleaning"],

        // Additional High-Demand Commercial Niches
        ["Solar & Renewable Energy"] = ["solar", "energy", "photovoltaic", "contractor"],
        ["Event Planners & Marriage Halls"] = ["events_venue", "banquet_hall", "party", "wedding"],
        ["Printing, Packaging & Signage"] = ["copyshop", "printer", "packaging", "signage"],
        ["Agricultural & Agro-Chemical"] = ["agrarian", "fertilizer", "seed", "farm"],
        ["Pet Care & Veterinary Clinics"] = ["veterinary", "pet", "pet_grooming"],
        ["Textile, Apparel & Fashion"] = ["tailor", "fabrics", "clothes", "boutique"]
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

        var isAllCommercial = amenities.Any(a => a.Equals("all_commercial", StringComparison.OrdinalIgnoreCase) || a.Equals("all", StringComparison.OrdinalIgnoreCase));

        if (bbox != null)
        {
            var bboxStr = $"{bbox.South:F4},{bbox.West:F4},{bbox.North:F4},{bbox.East:F4}";
            sb.AppendLine("(");

            if (isAllCommercial)
            {
                sb.AppendLine($"  node[\"amenity\"]({bboxStr});");
                sb.AppendLine($"  way[\"amenity\"]({bboxStr});");
                sb.AppendLine($"  node[\"shop\"]({bboxStr});");
                sb.AppendLine($"  way[\"shop\"]({bboxStr});");
                sb.AppendLine($"  node[\"office\"]({bboxStr});");
                sb.AppendLine($"  way[\"office\"]({bboxStr});");
                sb.AppendLine($"  node[\"craft\"]({bboxStr});");
                sb.AppendLine($"  way[\"craft\"]({bboxStr});");
                sb.AppendLine($"  node[\"healthcare\"]({bboxStr});");
                sb.AppendLine($"  way[\"healthcare\"]({bboxStr});");
                sb.AppendLine($"  node[\"industrial\"]({bboxStr});");
                sb.AppendLine($"  way[\"industrial\"]({bboxStr});");
                sb.AppendLine($"  node[\"tourism\"]({bboxStr});");
                sb.AppendLine($"  way[\"tourism\"]({bboxStr});");
            }
            else
            {
                foreach (var amenity in amenities)
                {
                    var clean = amenity.Trim().ToLowerInvariant();
                    sb.AppendLine($"  node[\"amenity\"=\"{clean}\"]({bboxStr});");
                    sb.AppendLine($"  way[\"amenity\"=\"{clean}\"]({bboxStr});");
                    sb.AppendLine($"  node[\"shop\"=\"{clean}\"]({bboxStr});");
                    sb.AppendLine($"  way[\"shop\"=\"{clean}\"]({bboxStr});");
                    sb.AppendLine($"  node[\"office\"=\"{clean}\"]({bboxStr});");
                    sb.AppendLine($"  way[\"office\"=\"{clean}\"]({bboxStr});");
                    sb.AppendLine($"  node[\"craft\"=\"{clean}\"]({bboxStr});");
                    sb.AppendLine($"  way[\"craft\"=\"{clean}\"]({bboxStr});");
                    sb.AppendLine($"  node[\"healthcare\"=\"{clean}\"]({bboxStr});");
                    sb.AppendLine($"  way[\"healthcare\"=\"{clean}\"]({bboxStr});");
                    sb.AppendLine($"  node[\"tourism\"=\"{clean}\"]({bboxStr});");
                    sb.AppendLine($"  way[\"tourism\"=\"{clean}\"]({bboxStr});");
                }
            }
            sb.AppendLine(");");
        }
        else
        {
            // Area-based query fallback
            var cityEscaped = query.City.Trim().Replace("\"", "\\\"");
            sb.AppendLine($"area[\"name\"=\"{cityEscaped}\"]->.searchArea;");
            sb.AppendLine("(");

            if (isAllCommercial)
            {
                sb.AppendLine("  node[\"amenity\"](area.searchArea);");
                sb.AppendLine("  way[\"amenity\"](area.searchArea);");
                sb.AppendLine("  node[\"shop\"](area.searchArea);");
                sb.AppendLine("  way[\"shop\"](area.searchArea);");
                sb.AppendLine("  node[\"office\"](area.searchArea);");
                sb.AppendLine("  way[\"office\"](area.searchArea);");
                sb.AppendLine("  node[\"craft\"](area.searchArea);");
                sb.AppendLine("  way[\"craft\"](area.searchArea);");
                sb.AppendLine("  node[\"healthcare\"](area.searchArea);");
                sb.AppendLine("  way[\"healthcare\"](area.searchArea);");
                sb.AppendLine("  node[\"tourism\"](area.searchArea);");
                sb.AppendLine("  way[\"tourism\"](area.searchArea);");
            }
            else
            {
                foreach (var amenity in amenities)
                {
                    var clean = amenity.Trim().ToLowerInvariant();
                    sb.AppendLine($"  node[\"amenity\"=\"{clean}\"](area.searchArea);");
                    sb.AppendLine($"  way[\"amenity\"=\"{clean}\"](area.searchArea);");
                    sb.AppendLine($"  node[\"shop\"=\"{clean}\"](area.searchArea);");
                    sb.AppendLine($"  way[\"shop\"=\"{clean}\"](area.searchArea);");
                    sb.AppendLine($"  node[\"office\"=\"{clean}\"](area.searchArea);");
                    sb.AppendLine($"  way[\"office\"=\"{clean}\"](area.searchArea);");
                    sb.AppendLine($"  node[\"craft\"=\"{clean}\"](area.searchArea);");
                    sb.AppendLine($"  way[\"craft\"=\"{clean}\"](area.searchArea);");
                    sb.AppendLine($"  node[\"healthcare\"=\"{clean}\"](area.searchArea);");
                    sb.AppendLine($"  way[\"healthcare\"=\"{clean}\"](area.searchArea);");
                    sb.AppendLine($"  node[\"tourism\"=\"{clean}\"](area.searchArea);");
                    sb.AppendLine($"  way[\"tourism\"=\"{clean}\"](area.searchArea);");
                }
            }
            sb.AppendLine(");");
        }

        sb.AppendLine($"out center {limit};");
        return sb.ToString();
    }
}
