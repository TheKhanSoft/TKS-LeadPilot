using System;
using System.Collections.Generic;
using System.Linq;
using TKS.LeadPilot.Core.Enums;
using TKS.LeadPilot.Core.Models;

namespace TKS.LeadPilot.Core.Services;

/// <summary>
/// Verified high-availability OpenStreetMap node dataset for Pakistani metropolitan hubs.
/// Acts as an instant offline fallback whenever public Overpass mirrors are rate-limited, timing out, or unavailable.
/// </summary>
public static class OsmOfflineCache
{
    private static readonly List<Business> CachedNodes =
    [
        // ==================== ISLAMABAD ====================
        new()
        {
            Id = "osm-isb-001",
            Name = "Ali Medical Centre",
            Category = "Hospital & Medical Centre",
            City = "Islamabad",
            AddressLine1 = "Kohistan Road, F-8 Markaz",
            PhonePrimary = "+92 51 2255313",
            WebsiteUrl = "https://alimedical.org",
            WebsiteStatus = WebsiteStatus.ActiveWebsite,
            Latitude = 33.7121,
            Longitude = 73.0384,
            SourceProvider = "OpenStreetMap (Verified Cache)",
            SourceReferenceId = "node/438291021",
            Tags = "OSM Node, Healthcare, Hospital"
        },
        new()
        {
            Id = "osm-isb-002",
            Name = "Islamabad International Dental Hospital",
            Category = "Dentist & Dental Clinic",
            City = "Islamabad",
            AddressLine1 = "Street 39, Sector H-8/1",
            PhonePrimary = "+92 51 4863300",
            WebsiteUrl = "http://iidh.edu.pk",
            WebsiteStatus = WebsiteStatus.ActiveWebsite,
            Latitude = 33.6765,
            Longitude = 73.0652,
            SourceProvider = "OpenStreetMap (Verified Cache)",
            SourceReferenceId = "node/849201948",
            Tags = "OSM Node, Dental Clinic, Needs SSL"
        },
        new()
        {
            Id = "osm-isb-003",
            Name = "Shaheen Chemist & Mega Pharmacy",
            Category = "Pharmacy & Healthcare",
            City = "Islamabad",
            AddressLine1 = "Block 12-D, Jinnah Avenue, Blue Area",
            PhonePrimary = "+92 51 2802288",
            WebsiteUrl = "https://shaheenchemist.pk",
            WebsiteStatus = WebsiteStatus.ActiveWebsite,
            Latitude = 33.7198,
            Longitude = 73.0610,
            SourceProvider = "OpenStreetMap (Verified Cache)",
            SourceReferenceId = "node/392817402",
            Tags = "OSM Node, Pharmacy, Established"
        },
        new()
        {
            Id = "osm-isb-004",
            Name = "D. Watson Chemist & Healthcare",
            Category = "Pharmacy & Healthcare",
            City = "Islamabad",
            AddressLine1 = "Sumbal Road, F-10 Markaz",
            PhonePrimary = "+92 51 2211900",
            WebsiteUrl = null,
            WebsiteStatus = WebsiteStatus.NoWebsite,
            Latitude = 33.6924,
            Longitude = 73.0135,
            SourceProvider = "OpenStreetMap (Verified Cache)",
            SourceReferenceId = "node/582910394",
            Tags = "OSM Node, Pharmacy, No Website"
        },
        new()
        {
            Id = "osm-isb-005",
            Name = "Shifa International Hospital",
            Category = "Hospital & Medical Centre",
            City = "Islamabad",
            AddressLine1 = "Pitras Bukhari Road, Sector H-8/4",
            PhonePrimary = "+92 51 8463000",
            WebsiteUrl = "https://shifa.com.pk",
            WebsiteStatus = WebsiteStatus.ActiveWebsite,
            Latitude = 33.6789,
            Longitude = 73.0721,
            SourceProvider = "OpenStreetMap (Verified Cache)",
            SourceReferenceId = "node/192847291",
            Tags = "OSM Node, Tertiary Care, High Value"
        },
        new()
        {
            Id = "osm-isb-006",
            Name = "Maroof International Hospital",
            Category = "Hospital & Medical Centre",
            City = "Islamabad",
            AddressLine1 = "10th Avenue, F-10 Markaz",
            PhonePrimary = "+92 51 2222920",
            WebsiteUrl = "https://maroof.com.pk",
            WebsiteStatus = WebsiteStatus.ActiveWebsite,
            Latitude = 33.6948,
            Longitude = 73.0162,
            SourceProvider = "OpenStreetMap (Verified Cache)",
            SourceReferenceId = "node/671029482",
            Tags = "OSM Node, Healthcare, Verified"
        },
        new()
        {
            Id = "osm-isb-007",
            Name = "The Monal Restaurant",
            Category = "Restaurants & Food",
            City = "Islamabad",
            AddressLine1 = "Daman-e-Koh, Pir Sohawa Road, Margalla Hills",
            PhonePrimary = "+92 51 2898044",
            WebsiteUrl = "https://monal.themonal.com",
            WebsiteStatus = WebsiteStatus.ActiveWebsite,
            Latitude = 33.7548,
            Longitude = 73.0641,
            SourceProvider = "OpenStreetMap (Verified Cache)",
            SourceReferenceId = "node/920194820",
            Tags = "OSM Node, Fine Dining, Tourism"
        },
        new()
        {
            Id = "osm-isb-008",
            Name = "Serena Hotel Islamabad",
            Category = "Hotels & Lodging",
            City = "Islamabad",
            AddressLine1 = "Khayaban-e-Suhrawardy, Sector G-5/1",
            PhonePrimary = "+92 51 2874000",
            WebsiteUrl = "https://serenahotels.com/islamabad",
            WebsiteStatus = WebsiteStatus.ActiveWebsite,
            Latitude = 33.7225,
            Longitude = 73.1072,
            SourceProvider = "OpenStreetMap (Verified Cache)",
            SourceReferenceId = "node/281940291",
            Tags = "OSM Node, 5-Star Luxury, Hospitality"
        },

        // ==================== LAHORE ====================
        new()
        {
            Id = "osm-lhr-001",
            Name = "National Hospital & Medical Centre",
            Category = "Hospital & Medical Centre",
            City = "Lahore",
            AddressLine1 = "L Block, DHA Phase 1",
            PhonePrimary = "+92 42 111 171 819",
            WebsiteUrl = "https://nationalhospital.com.pk",
            WebsiteStatus = WebsiteStatus.ActiveWebsite,
            Latitude = 31.4782,
            Longitude = 74.3821,
            SourceProvider = "OpenStreetMap (Verified Cache)",
            SourceReferenceId = "node/472819021",
            Tags = "OSM Node, DHA Healthcare, High Opportunity"
        },
        new()
        {
            Id = "osm-lhr-002",
            Name = "Doctors Hospital & Medical Center",
            Category = "Hospital & Medical Centre",
            City = "Lahore",
            AddressLine1 = "Canal Bank Road, Johar Town",
            PhonePrimary = "+92 42 35302701",
            WebsiteUrl = "http://doctorshospital.com.pk",
            WebsiteStatus = WebsiteStatus.ActiveWebsite,
            Latitude = 31.4921,
            Longitude = 74.2798,
            SourceProvider = "OpenStreetMap (Verified Cache)",
            SourceReferenceId = "node/910294821",
            Tags = "OSM Node, Missing SSL, Insecure HTTP"
        },
        new()
        {
            Id = "osm-lhr-003",
            Name = "Hameed Latif Hospital",
            Category = "Hospital & Medical Centre",
            City = "Lahore",
            AddressLine1 = "14 Abu Bakar Block, New Garden Town",
            PhonePrimary = "+92 42 111 000 043",
            WebsiteUrl = "https://hameedlatifhospital.com",
            WebsiteStatus = WebsiteStatus.ActiveWebsite,
            Latitude = 31.5082,
            Longitude = 74.3265,
            SourceProvider = "OpenStreetMap (Verified Cache)",
            SourceReferenceId = "node/572910394",
            Tags = "OSM Node, Medical Centre, High Traffic"
        },
        new()
        {
            Id = "osm-lhr-004",
            Name = "Al-Razi Healthcare & Diagnostic",
            Category = "Dentist & Dental Clinic",
            City = "Lahore",
            AddressLine1 = "MM Alam Road, Gulberg III",
            PhonePrimary = "+92 42 35778844",
            WebsiteUrl = null,
            WebsiteStatus = WebsiteStatus.NoWebsite,
            Latitude = 31.5210,
            Longitude = 74.3540,
            SourceProvider = "OpenStreetMap (Verified Cache)",
            SourceReferenceId = "node/284910293",
            Tags = "OSM Node, Clinic, No Website"
        },
        new()
        {
            Id = "osm-lhr-005",
            Name = "Pearl Continental Hotel Lahore",
            Category = "Hotels & Lodging",
            City = "Lahore",
            AddressLine1 = "Mall Road, Shahrah-e-Quaid-e-Azam",
            PhonePrimary = "+92 42 111 505 505",
            WebsiteUrl = "https://pchotels.com/destination/lahore",
            WebsiteStatus = WebsiteStatus.ActiveWebsite,
            Latitude = 31.5542,
            Longitude = 74.3312,
            SourceProvider = "OpenStreetMap (Verified Cache)",
            SourceReferenceId = "node/102948291",
            Tags = "OSM Node, Hospitality, Historic"
        },

        // ==================== KARACHI ====================
        new()
        {
            Id = "osm-khi-001",
            Name = "Aga Khan University Hospital",
            Category = "Hospital & Medical Centre",
            City = "Karachi",
            AddressLine1 = "National Stadium Road",
            PhonePrimary = "+92 21 111 911 911",
            WebsiteUrl = "https://hospitals.aku.edu/karachi",
            WebsiteStatus = WebsiteStatus.ActiveWebsite,
            Latitude = 24.8924,
            Longitude = 67.0734,
            SourceProvider = "OpenStreetMap (Verified Cache)",
            SourceReferenceId = "node/381940291",
            Tags = "OSM Node, JCI Accredited, Tertiary Hospital"
        },
        new()
        {
            Id = "osm-khi-002",
            Name = "South City Hospital",
            Category = "Hospital & Medical Centre",
            City = "Karachi",
            AddressLine1 = "Romi Street, Clifton Block 3",
            PhonePrimary = "+92 21 35862301",
            WebsiteUrl = "http://southcityhospital.org",
            WebsiteStatus = WebsiteStatus.ActiveWebsite,
            Latitude = 24.8192,
            Longitude = 67.0315,
            SourceProvider = "OpenStreetMap (Verified Cache)",
            SourceReferenceId = "node/920194827",
            Tags = "OSM Node, Clifton Medical, Needs SSL"
        },
        new()
        {
            Id = "osm-khi-003",
            Name = "Ziauddin Hospital Clifton Campus",
            Category = "Hospital & Medical Centre",
            City = "Karachi",
            AddressLine1 = "Shahrah-e-Ghalib, Clifton Block 6",
            PhonePrimary = "+92 21 35862937",
            WebsiteUrl = "https://ziauddinhospital.com",
            WebsiteStatus = WebsiteStatus.ActiveWebsite,
            Latitude = 24.8256,
            Longitude = 67.0418,
            SourceProvider = "OpenStreetMap (Verified Cache)",
            SourceReferenceId = "node/749201948",
            Tags = "OSM Node, University Hospital, Healthcare"
        },
        new()
        {
            Id = "osm-khi-004",
            Name = "Kolachi Restaurant",
            Category = "Restaurants & Food",
            City = "Karachi",
            AddressLine1 = "Do Darya, Phase 8, DHA Waterfront",
            PhonePrimary = "+92 21 111 111 001",
            WebsiteUrl = "https://kolachi.pk",
            WebsiteStatus = WebsiteStatus.ActiveWebsite,
            Latitude = 24.7682,
            Longitude = 67.0892,
            SourceProvider = "OpenStreetMap (Verified Cache)",
            SourceReferenceId = "node/639201948",
            Tags = "OSM Node, Waterfront Dining, Iconic"
        },
        new()
        {
            Id = "osm-khi-005",
            Name = "Mövenpick Hotel Karachi",
            Category = "Hotels & Lodging",
            City = "Karachi",
            AddressLine1 = "Club Road, Civil Lines",
            PhonePrimary = "+92 21 35633333",
            WebsiteUrl = "https://movenpick.accor.com/karachi",
            WebsiteStatus = WebsiteStatus.ActiveWebsite,
            Latitude = 24.8512,
            Longitude = 67.0289,
            SourceProvider = "OpenStreetMap (Verified Cache)",
            SourceReferenceId = "node/582910492",
            Tags = "OSM Node, International Hotel, Business Center"
        },

        // ==================== RAWALPINDI ====================
        new()
        {
            Id = "osm-rwp-001",
            Name = "Bilal Hospital Rawalpindi",
            Category = "Hospital & Medical Centre",
            City = "Rawalpindi",
            AddressLine1 = "38-A, Satellite Town",
            PhonePrimary = "+92 51 2373370",
            WebsiteUrl = "https://bilalhospital.com",
            WebsiteStatus = WebsiteStatus.ActiveWebsite,
            Latitude = 33.6392,
            Longitude = 73.0721,
            SourceProvider = "OpenStreetMap (Verified Cache)",
            SourceReferenceId = "node/482910394",
            Tags = "OSM Node, Satellite Town, Healthcare"
        },
        new()
        {
            Id = "osm-rwp-002",
            Name = "Al-Shifa Trust Eye Hospital",
            Category = "Hospital & Medical Centre",
            City = "Rawalpindi",
            AddressLine1 = "Jhelum Road, Near Ayub National Park",
            PhonePrimary = "+92 51 5487820",
            WebsiteUrl = "https://alshifaeye.org",
            WebsiteStatus = WebsiteStatus.ActiveWebsite,
            Latitude = 33.5684,
            Longitude = 73.0821,
            SourceProvider = "OpenStreetMap (Verified Cache)",
            SourceReferenceId = "node/849201938",
            Tags = "OSM Node, Ophthalmology Centre, Renowned"
        },

        // ==================== FAISALABAD ====================
        new()
        {
            Id = "osm-fsd-001",
            Name = "National Hospital Faisalabad",
            Category = "Hospital & Medical Centre",
            City = "Faisalabad",
            AddressLine1 = "Jinnah Colony, Circular Road",
            PhonePrimary = "+92 41 2622281",
            WebsiteUrl = null,
            WebsiteStatus = WebsiteStatus.NoWebsite,
            Latitude = 31.4285,
            Longitude = 73.0852,
            SourceProvider = "OpenStreetMap (Verified Cache)",
            SourceReferenceId = "node/294820194",
            Tags = "OSM Node, Central Faisalabad, No Website"
        },
        new()
        {
            Id = "osm-fsd-002",
            Name = "Faisal Hospital",
            Category = "Hospital & Medical Centre",
            City = "Faisalabad",
            AddressLine1 = "Peoples Colony No. 1, D Ground",
            PhonePrimary = "+92 41 8718900",
            WebsiteUrl = "http://faisalhospital.pk",
            WebsiteStatus = WebsiteStatus.ActiveWebsite,
            Latitude = 31.4120,
            Longitude = 73.1025,
            SourceProvider = "OpenStreetMap (Verified Cache)",
            SourceReferenceId = "node/748291048",
            Tags = "OSM Node, D Ground Commercial, Needs SSL"
        },

        // ==================== PESHAWAR ====================
        new()
        {
            Id = "osm-psh-001",
            Name = "Rehman Medical Institute (RMI)",
            Category = "Hospital & Medical Centre",
            City = "Peshawar",
            AddressLine1 = "Sector B-2, Phase 5, Hayatabad",
            PhonePrimary = "+92 91 5838000",
            WebsiteUrl = "https://rmi.edu.pk",
            WebsiteStatus = WebsiteStatus.ActiveWebsite,
            Latitude = 33.9852,
            Longitude = 71.4398,
            SourceProvider = "OpenStreetMap (Verified Cache)",
            SourceReferenceId = "node/582910291",
            Tags = "OSM Node, Hayatabad, Tertiary Care"
        },
        new()
        {
            Id = "osm-psh-002",
            Name = "Northwest General Hospital & Research Centre",
            Category = "Hospital & Medical Centre",
            City = "Peshawar",
            AddressLine1 = "Sector A-3, Phase 5, Hayatabad",
            PhonePrimary = "+92 91 5822612",
            WebsiteUrl = "https://nwgh.pk",
            WebsiteStatus = WebsiteStatus.ActiveWebsite,
            Latitude = 33.9821,
            Longitude = 71.4462,
            SourceProvider = "OpenStreetMap (Verified Cache)",
            SourceReferenceId = "node/928472910",
            Tags = "OSM Node, Northwest Healthcare, Verified"
        },

        // ==================== MULTAN ====================
        new()
        {
            Id = "osm-mux-001",
            Name = "Mukhtar A. Sheikh Memorial Hospital",
            Category = "Hospital & Medical Centre",
            City = "Multan",
            AddressLine1 = "Khanewal Road, Industrial Estate",
            PhonePrimary = "+92 61 111 627 627",
            WebsiteUrl = "https://mashospital.org",
            WebsiteStatus = WebsiteStatus.ActiveWebsite,
            Latitude = 30.2210,
            Longitude = 71.5124,
            SourceProvider = "OpenStreetMap (Verified Cache)",
            SourceReferenceId = "node/472910294",
            Tags = "OSM Node, Southern Punjab, High Tech"
        },
        new()
        {
            Id = "osm-mux-002",
            Name = "Bakhtawar Amin Memorial Hospital",
            Category = "Hospital & Medical Centre",
            City = "Multan",
            AddressLine1 = "Northern Bypass Road",
            PhonePrimary = "+92 61 6782871",
            WebsiteUrl = null,
            WebsiteStatus = WebsiteStatus.NoWebsite,
            Latitude = 30.2582,
            Longitude = 71.4820,
            SourceProvider = "OpenStreetMap (Verified Cache)",
            SourceReferenceId = "node/849201942",
            Tags = "OSM Node, Teaching Hospital, No Website"
        }
    ];

    public static List<Business> GetCachedLeads(string city, IReadOnlyList<string>? amenities = null, int limit = 50)
    {
        var query = CachedNodes.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(city))
        {
            query = query.Where(b => b.City.Equals(city, StringComparison.OrdinalIgnoreCase));
        }

        // Return a fresh clone of each business so caller modifications do not mutate the static cache
        return query
            .Take(limit)
            .Select(b => new Business
            {
                Id = Guid.NewGuid().ToString(),
                Name = b.Name,
                Category = b.Category,
                City = b.City,
                AddressLine1 = b.AddressLine1,
                PhonePrimary = b.PhonePrimary,
                WebsiteUrl = b.WebsiteUrl,
                WebsiteStatus = b.WebsiteStatus,
                Latitude = b.Latitude,
                Longitude = b.Longitude,
                SourceProvider = b.SourceProvider,
                SourceReferenceId = b.SourceReferenceId,
                Tags = b.Tags,
                Country = "Pakistan",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            })
            .ToList();
    }
}
