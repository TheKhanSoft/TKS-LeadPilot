# -*- coding: utf-8 -*-
import os

from build_full_catalog import countries_data
from generate_more_catalog import COUNTRIES

all_countries = countries_data + COUNTRIES

# Let's add the remaining countries to reach all 32 countries:
REMAINING_COUNTRIES = [
    # 12. Spain
    {
        "Code": "ES", "Name": "Spain", "FlagEmoji": "🇪🇸",
        "States": [
            {
                "Name": "Community of Madrid", "Code": "MD",
                "Cities": [
                    ("Madrid", 40.4168, -3.7038, ["Fintech & Banking HQs", "Telecommunications (Telefonica)", "Corporate Litigation & Tax", "Luxury Hospitality & Dining", "Digital Media Studios"]),
                    ("Alcala de Henares", 40.4819, -3.3635, ["Biopharmaceutical Laboratories", "University Research", "Logistics Centers"]),
                    ("Mostoles", 40.3232, -3.8649, ["Energy Technology Labs", "Automotive Services", "Industrial Parks"]),
                    ("Getafe", 40.3083, -3.7328, ["Airbus Defense & Space Facility", "Aerospace Engineering", "Precision Metallurgy"])
                ]
            },
            {
                "Name": "Catalonia", "Code": "CT",
                "Cities": [
                    ("Barcelona", 41.3851, 2.1734, ["Mobile World Congress / Tech Ecosystem", "Biotech & Pharma (Grifols)", "Design & Creative Agencies", "Luxury Tourism & Architecture", "Port Freight Logistics"]),
                    ("Terrassa", 41.5632, 2.0089, ["Textile Tech & Industrial Engineering", "Audiovisual Production", "Optics"]),
                    ("Badalona", 41.4500, 2.2474, ["Biomedical Health Research", "Chemical Industry", "Coastal Commerce"]),
                    ("Girona", 41.9794, 2.8214, ["Michelin Star Gastronomy", "Cycling & Sports Tourism", "Food Processing"])
                ]
            },
            {
                "Name": "Andalusia", "Code": "AN",
                "Cities": [
                    ("Seville", 37.3891, -5.9845, ["Aerospace Assembly (Airbus A400M)", "Solar Thermal Energy", "Tourism & Historical Hospitality"]),
                    ("Malaga", 36.7213, -4.4214, ["Malaga TechPark (Digital Nomads)", "Cybersecurity (Google Center)", "Costa del Sol Tourism"]),
                    ("Granada", 37.1773, -3.5986, ["AI Computing Research", "Health Sciences TechPark", "Heritage Tourism"]),
                    ("Cordoba", 37.8882, -4.7794, ["Agri-Food & Olive Oil Export", "Jewelry & Silversmithing", "Logistics Hubs"])
                ]
            },
            {
                "Name": "Valencian Community", "Code": "VC",
                "Cities": [
                    ("Valencia", 39.4699, -0.3763, ["Port Terminal Logistics", "Ceramics & Tile Architecture", "Startups & Gaming", "Citrus Export"]),
                    ("Alicante", 38.3452, -0.4810, ["EU Intellectual Property Office (EUIPO)", "Digital District Tech", "Coastal Tourism"]),
                    ("Elche", 38.2669, -0.6983, ["Footwear & Leather Manufacturing", "Palm Agriculture", "Logistics"])
                ]
            },
            {
                "Name": "Basque Country", "Code": "PV",
                "Cities": [
                    ("Bilbao", 43.2630, -2.9350, ["Green Energy (Iberdrola)", "Advanced Metallurgy & Machine Tools", "Guggenheim Cultural Tourism", "Maritime Shipping"]),
                    ("San Sebastian", 43.3183, -1.9812, ["World-Class Gastronomy & Culinary Tech", "Nanoscience Labs", "Surfing Tourism"])
                ]
            }
        ]
    },

    # 13. Italy
    {
        "Code": "IT", "Name": "Italy", "FlagEmoji": "🇮🇹",
        "States": [
            {
                "Name": "Lombardy", "Code": "LOM",
                "Cities": [
                    ("Milan", 45.4642, 9.1900, ["Global High Fashion & Fashion Week", "Borsa Italiana & Investment Banking", "Corporate Law & Consulting", "Design & Salone del Mobile", "Private Hospitals & Clinics"]),
                    ("Brescia", 45.5416, 10.2118, ["Metals & Heavy Industrial Tooling", "Automotive Components", "Mechanical Engineering"]),
                    ("Bergamo", 45.6983, 9.6773, ["Electromechanics & Mechatronics", "Biomedical Equipment", "Airport Logistics"]),
                    ("Monza", 45.5845, 9.2744, ["Formula 1 Motorsports Engineering", "Precision Tooling", "Publishing"])
                ]
            },
            {
                "Name": "Lazio", "Code": "LAZ",
                "Cities": [
                    ("Rome", 41.9028, 12.4964, ["Government Relations & State Corporates (Eni/Enel)", "Cinecitta Film Studios", "Aerospace & Defense (Leonardo)", "Cultural Tourism & Hospitality", "Vatican Pilgrimage Services"]),
                    ("Latina", 41.4676, 12.9037, ["Pharmaceutical Manufacturing Cluster", "Agro-Food Logistics", "Plastics"])
                ]
            },
            {
                "Name": "Veneto", "Code": "VEN",
                "Cities": [
                    ("Venice", 45.4408, 12.3155, ["Maritime Tourism & Luxury Hotels", "Port of Marghera Chemical & Energy", "Murano Glass Artistry"]),
                    ("Verona", 45.4384, 10.9916, ["Vinitaly Wine Trade", "Logistics & Freight Terminal (Quadrante Europa)", "Marble Processing"]),
                    ("Padua", 45.4064, 11.8768, ["Biomedical Devices", "Wholesale Center (Interporto)", "Engineering"])
                ]
            },
            {
                "Name": "Emilia-Romagna", "Code": "EMR",
                "Cities": [
                    ("Bologna", 44.4949, 11.3426, ["Packaging Machine Valley", "Data Valley (CINECA Supercomputing)", "Food & Culinary Tourism"]),
                    ("Modena", 44.6471, 10.9252, ["Motor Valley Supercars (Ferrari/Maserati)", "Balsamic Vinegar Export", "Ceramic Tiles"]),
                    ("Parma", 44.8015, 10.3279, ["European Food Safety Authority (EFSA)", "Prosciutto & Parmigiano Export", "Pharmaceuticals"])
                ]
            },
            {
                "Name": "Piedmont", "Code": "PMN",
                "Cities": [
                    ("Turin", 45.0703, 7.6869, ["Automotive Design & EV Engineering (Stellantis)", "Aerospace Cluster", "ICT & Robotics", "Food & Wine (Barolo)"]),
                    ("Novara", 45.4469, 8.6214, ["Chemical Research", "Logistics Intermodal Rail", "Publishing"])
                ]
            },
            {
                "Name": "Tuscany", "Code": "TOS",
                "Cities": [
                    ("Florence", 43.7696, 11.2558, ["Luxury Leather Goods (Gucci/Ferragamo)", "Renaissance Art Tourism", "Pharmaceutical Formulations"]),
                    ("Pisa", 43.7228, 10.4017, ["Information Technology Labs (CNR)", "Biotechnology Research", "Tourism"]),
                    ("Prato", 43.8777, 11.1022, ["Fast Fashion Textile District", "Yarn Manufacturing", "Circular Fabric Recycling"])
                ]
            }
        ]
    },

    # 14. Netherlands
    {
        "Code": "NL", "Name": "Netherlands", "FlagEmoji": "🇳🇱",
        "States": [
            {
                "Name": "North Holland", "Code": "NH",
                "Cities": [
                    ("Amsterdam", 52.3676, 4.9041, ["Global Fintech (Adyen)", "AdTech & Creative Agencies", "Venture Studios & Startups", "Corporate Litigation Law", "Hospitality & Tourism"]),
                    ("Haarlem", 52.3874, 4.6462, ["CleanTech & Sustainable Tech", "Digital Media", "Healthcare Services"]),
                    ("Zaanstad", 52.4420, 4.8292, ["Cocoa & Food Processing (Cargill)", "Timber Industry", "Industrial Freight"])
                ]
            },
            {
                "Name": "South Holland", "Code": "ZH",
                "Cities": [
                    ("Rotterdam", 51.9244, 4.4777, ["Port of Rotterdam Maritime Logistics", "Hydrogen Transition Hub", "Petrochemical Refining", "Architectural Firms"]),
                    ("The Hague", 52.0705, 4.3007, ["International Court of Justice & Law", "Cybersecurity (The Hague Security Delta)", "Telecommunications (KPN)", "Energy Corporates (Shell Hub)"]),
                    ("Delft", 52.0116, 4.3571, ["TU Delft Quantum Computing", "Robotics Valley", "Water Management Engineering"]),
                    ("Leiden", 52.1601, 4.4970, ["Bio Science Park (Janssen Vaccines)", "Life Sciences Incubators", "Clinical Pharmacology"])
                ]
            },
            {
                "Name": "North Brabant", "Code": "NB",
                "Cities": [
                    ("Eindhoven", 51.4416, 5.4697, ["Brainport High-Tech Chip Equipment (ASML)", "Photonics & Semiconductors", "Philips HealthTech", "Industrial Design"]),
                    ("Tilburg", 51.5555, 5.0913, ["Logistics & Distribution Parks (Tesla Assembly)", "E-Commerce Fulfillment", "Chemical Industry"]),
                    ("Breda", 51.5719, 4.7683, ["Video Game Technology", "Food & Drink Logistics", "Commercial Services"])
                ]
            },
            {
                "Name": "Utrecht", "Code": "UT",
                "Cities": [
                    ("Utrecht", 52.0907, 5.1214, ["Health & Sustainability Startups", "Gaming Tech Incubator", "Financial Insurance (Rabobank/ASR)", "Railway Systems Hub (NS)"]),
                    ("Amersfoort", 52.1561, 5.3878, ["Civil Engineering & Infrastructure (Arcadis)", "Financial Services", "IT Consultancies"])
                ]
            }
        ]
    },

    # 15. Sweden
    {
        "Code": "SE", "Name": "Sweden", "FlagEmoji": "🇸🇪",
        "States": [
            {
                "Name": "Stockholm County", "Code": "AB",
                "Cities": [
                    ("Stockholm", 59.3293, 18.0686, ["Fintech Unicorns (Klarna/Spotify)", "Gaming Studios (Dice/King)", "MedTech & Karolinska Institute", "Clean Energy & Battery Tech", "Corporate Advisory & Private Equity"]),
                    ("Sodertalje", 59.1955, 17.6252, ["Scania Heavy Trucks HQs", "AstraZeneca Biomanufacturing", "Industrial Logistics"])
                ]
            },
            {
                "Name": "Vastra Gotaland County", "Code": "O",
                "Cities": [
                    ("Gothenburg", 57.7089, 11.9746, ["Automotive Mobility & EV (Volvo)", "Port of Gothenburg Maritime Freight", "Life Sciences (AstraZeneca Hub)", "Aero & Defense (Saab)"]),
                    ("Boras", 57.7210, 12.9401, ["Textile Fashion & Circular Tech", "E-Commerce Logistics", "Smart City Testing"])
                ]
            },
            {
                "Name": "Skane County", "Code": "M",
                "Cities": [
                    ("Malmo", 55.6050, 13.0038, ["Video Game Development", "Cleantech & Circular Economy", "FoodTech Innovations", "Cross-Oresund Commerce"]),
                    ("Lund", 55.7047, 13.1910, ["MAX IV & ESS Synchrotron Labs", "Wireless Telecom & Bluetooth", "Medical Devices"])
                ]
            },
            {
                "Name": "Uppsala County", "Code": "C",
                "Cities": [
                    ("Uppsala", 59.8586, 17.6389, ["Biotech & Life Sciences (Cytiva)", "Vaccine & Immunology Research", "EdTech"])
                ]
            }
        ]
    },

    # 16. Norway
    {
        "Code": "NO", "Name": "Norway", "FlagEmoji": "🇳🇴",
        "States": [
            {
                "Name": "Oslo", "Code": "03",
                "Cities": [
                    ("Oslo", 59.9139, 10.7522, ["Maritime Shipping & DNV Services", "Clean Energy & EV Infrastructure", "Fintech & Wealth Tech", "Cancer Biomedicine Cluster", "Commercial Law & M&A"])
                ]
            },
            {
                "Name": "Vestland", "Code": "46",
                "Cities": [
                    ("Bergen", 60.3913, 5.3221, ["Subsea Marine Technology", "Salmon Aquaculture & Fish Farming", "Fjord Tourism & Expeditions", "Ocean Renewable Energy"])
                ]
            },
            {
                "Name": "Rogaland", "Code": "11",
                "Cities": [
                    ("Stavanger", 58.9700, 5.7331, ["Equinor Energy HQs", "Offshore Wind Technologies", "Smart Cities Solutions", "Gastronomy"]),
                    ("Sandnes", 58.8524, 5.7352, ["Robotics & Automation", "Oilfield Equipment Services", "Industrial Manufacturing"])
                ]
            },
            {
                "Name": "Trondelag", "Code": "50",
                "Cities": [
                    ("Trondheim", 63.4305, 10.3951, ["NTNU Deep Tech & AI Labs", "SINTEF Energy Research", "Microelectronics Design"])
                ]
            }
        ]
    },

    # 17. Denmark
    {
        "Code": "DK", "Name": "Denmark", "FlagEmoji": "🇩🇰",
        "States": [
            {
                "Name": "Capital Region of Denmark", "Code": "84",
                "Cities": [
                    ("Copenhagen", 55.6761, 12.5683, ["Medicon Valley Pharma (Novo Nordisk)", "CleanTech & Offshore Wind (Orsted)", "Fintech & Sound Tech", "Gastronomy & High-End Tourism", "Architecture & Sustainable Cities"]),
                    ("Frederiksberg", 55.6791, 12.5348, ["EdTech & Higher Education", "Digital Design Agencies", "Healthcare Clinics"])
                ]
            },
            {
                "Name": "Central Denmark Region", "Code": "82",
                "Cities": [
                    ("Aarhus", 56.1629, 10.2039, ["Wind Power Engineering (Vestas)", "Agri-Food Innovation (Arla)", "Software & B2B SaaS", "Container Port Freight"]),
                    ("Randers", 56.4607, 10.0364, ["Agricultural Machinery", "Food Processing", "Clean Energy Tooling"])
                ]
            },
            {
                "Name": "Region of Southern Denmark", "Code": "83",
                "Cities": [
                    ("Odense", 55.4038, 10.4024, ["Universal Robots & Cobot Robotics Hub", "Drone & UAS Testing", "HealthTech"]),
                    ("Esbjerg", 55.4703, 8.4519, ["Offshore Wind Logistics Port", "Oil & Gas Decommissioning", "Fisheries"])
                ]
            },
            {
                "Name": "North Denmark Region", "Code": "81",
                "Cities": [
                    ("Aalborg", 57.0488, 9.9217, ["Wireless Communications & Telecom", "Smart Energy Systems", "Cement Industry"])
                ]
            }
        ]
    },

    # 18. New Zealand
    {
        "Code": "NZ", "Name": "New Zealand", "FlagEmoji": "🇳🇿",
        "States": [
            {
                "Name": "Auckland Region", "Code": "AUK",
                "Cities": [
                    ("Auckland", -36.8485, 174.7633, ["Fintech & SaaS Unicorns (Xero)", "Commercial Marine Yachting", "Biomedical & MedTech", "Digital Film & VFX", "Import/Export Port Logistics"]),
                    ("Manukau", -36.9930, 174.8770, ["Food Manufacturing & Export", "Intermodal Logistics", "Healthcare Centers"])
                ]
            },
            {
                "Name": "Wellington Region", "Code": "WGN",
                "Cities": [
                    ("Wellington", -41.2865, 174.7762, ["GovTech & Public Advisory", "Weta Digital VFX Studios", "Game Development", "Specialty Coffee Roasting"]),
                    ("Lower Hutt", -41.2092, 174.9081, ["Callaghan Innovation Tech", "Industrial Tooling", "Manufacturing"])
                ]
            },
            {
                "Name": "Canterbury", "Code": "CAN",
                "Cities": [
                    ("Christchurch", -43.5321, 172.6362, ["Aerospace & Rocket Research", "Agri-Tech & Dairy Processing", "Electronics Manufacturing", "Antarctic Gateway Operations"])
                ]
            },
            {
                "Name": "Waikato", "Code": "WKO",
                "Cities": [
                    ("Hamilton", -37.7870, 175.2793, ["Agricultural Fieldays & Equipment", "Dairy Science (Fonterra)", "Aviation Flight Training"])
                ]
            },
            {
                "Name": "Otago", "Code": "OTA",
                "Cities": [
                    ("Queenstown", -45.0312, 168.6626, ["Adventure Tourism & Heli-Skiing", "Luxury Boutique Lodges", "Film Location Production"]),
                    ("Dunedin", -45.8788, 170.5028, ["Otago Medical School Ecosystem", "Game Studios", "Specialty Biotechnology"])
                ]
            }
        ]
    },

    # 19. Ireland
    {
        "Code": "IE", "Name": "Ireland", "FlagEmoji": "🇮🇪",
        "States": [
            {
                "Name": "Leinster", "Code": "L",
                "Cities": [
                    ("Dublin", 53.3498, -6.2603, ["Silicon Docks Tech HQs (Google/Meta/Stripe)", "International Financial Services (IFSC)", "Corporate Litigation & Tax Chambers", "High-End Medical & Dental", "Aircraft Leasing Finance"]),
                    ("Swords", 53.4597, -6.2181, ["Aviation Logistics & Ryanair HQs", "Pharmaceutical Packing", "Distribution"]),
                    ("Dundalk", 54.0039, -6.4022, ["Biopharmaceutical Plants (WuXi)", "Cross-Border Logistics", "Engineering"])
                ]
            },
            {
                "Name": "Munster", "Code": "M",
                "Cities": [
                    ("Cork", 51.8985, -8.4756, ["Apple European HQs (Hollyhill)", "Biopharma Mega Cluster (Pfizer/Eli Lilly)", "Cybersecurity Operations", "Maritime Port Freight"]),
                    ("Limerick", 52.6638, -8.6267, ["Medical Device Manufacturing", "Analog Devices Semiconductors", "Aviation Services"]),
                    ("Waterford", 52.2593, -7.1101, ["Crystal Artisanal Manufacturing", "Pharmaceutical Plants (Teva)", "Healthcare"])
                ]
            },
            {
                "Name": "Connacht", "Code": "C",
                "Cities": [
                    ("Galway", 53.2707, -9.0568, ["Global MedTech Capital (Boston Scientific/Medtronic)", "Marine Research Institute", "Creative Digital Arts"]),
                    ("Sligo", 54.2766, -8.4761, ["Precision Tooling", "Pharmaceutical Laboratories (AbbVie)", "Digital Nomads Tech"])
                ]
            }
        ]
    },

    # 20. Japan
    {
        "Code": "JP", "Name": "Japan", "FlagEmoji": "🇯🇵",
        "States": [
            {
                "Name": "Tokyo Prefecture", "Code": "13",
                "Cities": [
                    ("Tokyo", 35.6762, 139.6503, ["Financial HQs & Tokyo Stock Exchange", "Robotics & AI Commercialization", "Consumer Electronics & Gaming Giants", "High-End Healthcare & Oncology", "Michelin Star Culinary Hospitality"]),
                    ("Hachioji", 35.6553, 139.3239, ["Precision Optics (Olympus)", "Semiconductor Assembly", "Industrial Robotics"])
                ]
            },
            {
                "Name": "Kanagawa Prefecture", "Code": "14",
                "Cities": [
                    ("Yokohama", 35.4437, 139.6380, ["Automotive R&D (Nissan HQs)", "Container Port Terminal", "Biomedical Research"]),
                    ("Kawasaki", 35.5308, 139.7029, ["Chemical Refining Complex", "Electronic Equipment", "R&D Science Parks"])
                ]
            },
            {
                "Name": "Osaka Prefecture", "Code": "27",
                "Cities": [
                    ("Osaka", 34.6937, 135.5023, ["Electronics Corporates (Panasonic)", "Pharmaceutical Giants (Takeda)", "Wholesale Trading Hub", "Food Innovation & Gastronomy"]),
                    ("Sakai", 34.5733, 135.4830, ["Bicycle Components (Shimano)", "Display Technology", "Petrochemicals"])
                ]
            },
            {
                "Name": "Aichi Prefecture", "Code": "23",
                "Cities": [
                    ("Nagoya", 35.1815, 136.9066, ["Toyota Automotive Monozukuri", "Aerospace Manufacturing (Mitsubishi)", "Industrial Ceramics (NGK)"])
                ]
            },
            {
                "Name": "Kyoto Prefecture", "Code": "26",
                "Cities": [
                    ("Kyoto", 35.0116, 135.7681, ["Semiconductors & Sensors (Kyocera/Rohm)", "Gaming Legends (Nintendo HQs)", "Traditional Silk & Ceramics", "Cultural Tourism"])
                ]
            },
            {
                "Name": "Fukuoka Prefecture", "Code": "40",
                "Cities": [
                    ("Fukuoka", 33.5904, 130.4017, ["Startup City Tech Hub", "Asian Gateway Logistics", "Robotics (Yaskawa)", "Healthcare"])
                ]
            }
        ]
    },

    # 21. South Korea
    {
        "Code": "KR", "Name": "South Korea", "FlagEmoji": "🇰🇷",
        "States": [
            {
                "Name": "Seoul Special City", "Code": "11",
                "Cities": [
                    ("Seoul", 37.5665, 126.9780, ["Gangnam Aesthetic & Plastic Surgery", "K-Pop & Entertainment Conglomerates", "Fintech & Web3 Crypto", "Corporate Chaebol HQs", "High-End Dental & Medical"]),
                    ("Gangnam", 37.4979, 127.0276, ["Tech Venture Capital", "Luxury Fashion Brands", "Premium Healthcare"]),
                    ("Yeouido", 37.5219, 126.9242, ["Investment Banking", "Securities & Exchanges", "National Media"])
                ]
            },
            {
                "Name": "Gyeonggi Province", "Code": "41",
                "Cities": [
                    ("Suwon", 37.2636, 127.0286, ["Samsung Electronics Digital City", "Semiconductor Fabs", "Industrial R&D"]),
                    ("Seongnam", 37.4200, 127.1265, ["Pangyo Techno Valley IT (Naver/Kakao)", "Gaming Studios (NCSOFT)", "AI Research"]),
                    ("Goyang", 37.6584, 126.8320, ["KINTEX Exhibition Hub", "Broadcast & Drama Studios", "Smart City Logistics"])
                ]
            },
            {
                "Name": "Incheon Metropolitan City", "Code": "28",
                "Cities": [
                    ("Incheon", 37.4563, 126.7052, ["Songdo Bio Cluster (Samsung Biologics/Celltrion)", "Incheon Air Cargo Port", "Free Economic Zone"])
                ]
            },
            {
                "Name": "Busan Metropolitan City", "Code": "26",
                "Cities": [
                    ("Busan", 35.1796, 129.0756, ["World 6th Largest Container Port", "Film Festival Studios", "Marine Logistics & Shipbuilding", "Ocean Tourism"])
                ]
            },
            {
                "Name": "Daejeon Metropolitan City", "Code": "30",
                "Cities": [
                    ("Daejeon", 36.3504, 127.3845, ["Daedeok Innopolis Science Valley", "KAIST Research Centers", "Aerospace & Nuclear Tech"])
                ]
            }
        ]
    },

    # 22. Singapore
    {
        "Code": "SG", "Name": "Singapore", "FlagEmoji": "🇸🇬",
        "States": [
            {
                "Name": "Central Region", "Code": "CR",
                "Cities": [
                    ("Singapore Central", 1.2838, 103.8591, ["Global Wealth Management & Family Offices", "Fintech & Blockchain Licensing", "Arbitration & International Law", "High-End Aesthetics & Mount Elizabeth Healthcare", "Commodity & Forex Trading"]),
                    ("Orchard", 1.3048, 103.8318, ["Luxury Retail Flagships", "Specialist Medical Suites", "Hospitality"]),
                    ("Marina Bay", 1.2830, 103.8607, ["Financial Towers", "Integrated Resort Hospitality", "Fintech Hubs"])
                ]
            },
            {
                "Name": "West Region", "Code": "WR",
                "Cities": [
                    ("Jurong East", 1.3329, 103.7436, ["Tuas Mega Port Terminal", "Biomedical Tuas Park", "Chemical Petrochemical Hub"]),
                    ("One-North", 1.2989, 103.7877, ["Biopolis Life Sciences", "Fusionopolis Tech Labs", "MediaCorp Studios"])
                ]
            },
            {
                "Name": "East Region", "Code": "ER",
                "Cities": [
                    ("Changi", 1.3644, 103.9915, ["Aviation & Air Freight Logistics", "Changi Business Park FinTech", "Maintenance"])
                ]
            }
        ]
    },

    # 23. Hong Kong
    {
        "Code": "HK", "Name": "Hong Kong", "FlagEmoji": "🇭🇰",
        "States": [
            {
                "Name": "Hong Kong Island", "Code": "HKI",
                "Cities": [
                    ("Central", 22.2819, 114.1581, ["Global Investment Banking (HKEX)", "Private Wealth Asset Management", "Barrister Chambers & Commercial Litigation", "Luxury Flagship Retail"]),
                    ("Wan Chai", 22.2770, 114.1722, ["Exhibition & Convention Center", "Corporate IT Consulting", "Specialist Clinics"]),
                    ("Quarry Bay", 22.2880, 114.2120, ["Taikoo Place Multinational HQs", "Advertising Agencies", "Fintech Labs"])
                ]
            },
            {
                "Name": "Kowloon", "Code": "KLN",
                "Cities": [
                    ("Tsim Sha Tsui", 22.2988, 114.1722, ["Luxury Tourism & Five Star Hotels", "Diamond & Jewelry Exchanges", "Retail Management"]),
                    ("Kowloon Bay", 22.3236, 114.2140, ["Fintech & Back-Office Support", "Enterprise Data Centers", "Logistics Operations"]),
                    ("Mong Kok", 22.3193, 114.1694, ["Consumer Electronics Retail", "Commercial Trading", "Healthcare Clinics"])
                ]
            },
            {
                "Name": "New Territories", "Code": "NT",
                "Cities": [
                    ("Sha Tin", 22.3817, 114.1877, ["Hong Kong Science & Technology Park", "InnoHK R&D Centers", "Biomedical Labs"]),
                    ("Tsuen Wan", 22.3713, 114.1141, ["Industrial Warehousing", "Cold Chain Logistics", "Textiles"])
                ]
            }
        ]
    },

    # 24. Malaysia
    {
        "Code": "MY", "Name": "Malaysia", "FlagEmoji": "🇲🇾",
        "States": [
            {
                "Name": "Kuala Lumpur", "Code": "KUL",
                "Cities": [
                    ("Kuala Lumpur", 3.1390, 101.6869, ["Islamic Finance & Sukuk Capital", "Petronas Oil & Gas Operations", "Medical Tourism & Dental Centers", "Digital Free Trade Zone (DFTZ)", "Fintech Platforms"]),
                    ("Bangsar", 3.1292, 101.6784, ["Tech Startups & Venture Studios", "Specialty Hospitality", "Boutique Creative Agencies"])
                ]
            },
            {
                "Name": "Selangor", "Code": "SGR",
                "Cities": [
                    ("Petaling Jaya", 3.1073, 101.6067, ["Multinational Commercial Offices", "Digital Media", "Healthcare Networks"]),
                    ("Shah Alam", 3.0738, 101.5183, ["Automotive Assembly (Proton)", "Industrial Manufacturing", "Logistics Parks"]),
                    ("Cyberjaya", 2.9213, 101.6559, ["Global Tech Data Centers", "Software Engineering", "AI Research Labs"]),
                    ("Klang", 3.0449, 101.4456, ["Port Klang Mega Container Port", "Maritime Freight", "Packaging"])
                ]
            },
            {
                "Name": "Penang", "Code": "PNG",
                "Cities": [
                    ("George Town", 5.4141, 100.3288, ["Silicon Island Semiconductors (Intel/Broadcom)", "Medical Device Assembly", "Heritage Gastronomy Tourism"]),
                    ("Bayan Lepas", 5.2954, 100.2658, ["Free Industrial Zone Electronics", "Precision Tooling", "Air Cargo Logistics"])
                ]
            },
            {
                "Name": "Johor", "Code": "JHR",
                "Cities": [
                    ("Johor Bahru", 1.4927, 103.7414, ["Cross-Border Singapore Commerce", "Data Center Mega Hubs", "Healthcare"]),
                    ("Iskandar Puteri", 1.4239, 103.6267, ["Smart City Tech Hub", "Medical City Clinics", "Education City"])
                ]
            }
        ]
    },

    # 25. Turkey
    {
        "Code": "TR", "Name": "Turkey", "FlagEmoji": "🇹🇷",
        "States": [
            {
                "Name": "Istanbul Province", "Code": "34",
                "Cities": [
                    ("Istanbul", 41.0082, 28.9784, ["Global Hair Transplant & Aesthetic Surgery", "Fintech & E-Commerce Unicorns (Trendyol)", "Bosphorus Cross-Continental Logistics", "Textile & Garment Manufacturing", "Luxury Hospitality & Fine Dining"]),
                    ("Kadikoy", 40.9927, 29.0277, ["Creative Tech Studios", "Specialist Healthcare Clinics", "Commercial Retail"]),
                    ("Sisli", 41.0602, 28.9877, ["Corporate Law & Financial HQs", "Private Hospitals", "Shopping Complexes"])
                ]
            },
            {
                "Name": "Ankara Province", "Code": "06",
                "Cities": [
                    ("Ankara", 39.9334, 32.8597, ["Defense Technology (Aselsan/Baykar)", "Government Advisory", "Biotechnology & Vaccines", "Software Engineering"]),
                    ("Cankaya", 39.9000, 32.8600, ["Diplomatic Missions", "Corporate Advisory", "High-End Healthcare"])
                ]
            },
            {
                "Name": "Izmir Province", "Code": "35",
                "Cities": [
                    ("Izmir", 38.4237, 27.1428, ["Aegean Maritime Export Port", "Wind Turbine Blade Manufacturing", "Organic Food & Olive Oil Export"]),
                    ("Bornova", 38.4682, 27.2185, ["Ege University Medical Tech", "Software Startups", "Packaging"])
                ]
            },
            {
                "Name": "Bursa Province", "Code": "16",
                "Cities": [
                    ("Bursa", 40.1885, 29.0610, ["Automotive Assembly (Fiat/Renault/TOGG)", "Textile Silk & Fabric Mills", "Industrial Machinery"])
                ]
            },
            {
                "Name": "Antalya Province", "Code": "07",
                "Cities": [
                    ("Antalya", 36.8969, 30.7133, ["International Resort Tourism & Hotels", "Dental Tourism & Aesthetics", "Yacht Building Freezone"])
                ]
            }
        ]
    },

    # 26. Poland
    {
        "Code": "PL", "Name": "Poland", "FlagEmoji": "🇵🇱",
        "States": [
            {
                "Name": "Masovian Voivodeship", "Code": "14",
                "Cities": [
                    ("Warsaw", 52.2297, 21.0122, ["Central Europe Financial Hub", "Enterprise Software & Cloud", "Corporate Law & Consulting", "Game Development (CD Projekt)", "Private Healthcare & Hospitals"]),
                    ("Radom", 51.4027, 21.1471, ["Precision Tooling & Armaments", "Metalwork", "Logistics"])
                ]
            },
            {
                "Name": "Lesser Poland Voivodeship", "Code": "12",
                "Cities": [
                    ("Krakow", 50.0647, 19.9450, ["Global Tech Innovation Hub", "AI & Big Data Centers", "Historical Tourism & Lodging", "Fintech Shared Services"])
                ]
            },
            {
                "Name": "Lower Silesian Voivodeship", "Code": "02",
                "Cities": [
                    ("Wroclaw", 51.1079, 17.0385, ["Software & Gaming Studios", "LG Battery Mega-Plant", "Semiconductor R&D", "E-Commerce Logistics"])
                ]
            },
            {
                "Name": "Silesian Voivodeship", "Code": "24",
                "Cities": [
                    ("Katowice", 50.2649, 19.0238, ["Esports & Gaming Production", "Clean Energy Transition", "Automotive Manufacturing"])
                ]
            },
            {
                "Name": "Greater Poland Voivodeship", "Code": "30",
                "Cities": [
                    ("Poznan", 52.4064, 16.9252, ["Trade Fair Conferences (MTP)", "Automotive Assembly (VW)", "Logistics & Warehousing"])
                ]
            },
            {
                "Name": "Pomeranian Voivodeship", "Code": "22",
                "Cities": [
                    ("Gdansk", 54.3520, 18.6466, ["Deepwater Container Terminal", "Shipbuilding & Marine Engineering", "BioTech Hub"])
                ]
            }
        ]
    },

    # 27. Estonia
    {
        "Code": "EE", "Name": "Estonia", "FlagEmoji": "🇪🇪",
        "States": [
            {
                "Name": "Harju County", "Code": "37",
                "Cities": [
                    ("Tallinn", 59.4370, 24.7535, ["e-Residency & Digital Governance", "Cybersecurity (NATO CCDCOE)", "Fintech Unicorns (Wise/Bolt)", "AI Startups & Venture Capital", "Baltic Maritime Freight"]),
                    ("Maardu", 59.4764, 25.0069, ["Port of Muuga Logistics", "Industrial Storage", "Chemical Processing"])
                ]
            },
            {
                "Name": "Tartu County", "Code": "78",
                "Cities": [
                    ("Tartu", 58.3780, 26.7290, ["Biotechnology & Genomics (Estonian Genome Center)", "Software Development Hub", "EdTech"])
                ]
            },
            {
                "Name": "Parnu County", "Code": "67",
                "Cities": [
                    ("Parnu", 58.3859, 24.4971, ["Summer Spa & Wellness Tourism", "Timber & Wood Products", "Electronics Manufacturing"])
                ]
            }
        ]
    },

    # 28. South Africa
    {
        "Code": "ZA", "Name": "South Africa", "FlagEmoji": "🇿🇦",
        "States": [
            {
                "Name": "Gauteng", "Code": "GP",
                "Cities": [
                    ("Johannesburg", -26.2041, 28.0473, ["JSE Stock Exchange & Banking HQs", "Gold & Platinum Mining Conglomerates", "Corporate Legal & Consulting", "Private Hospitals & Specialists"]),
                    ("Pretoria", -25.7479, 28.2293, ["Diplomatic Missions", "Automotive Manufacturing (BMW/Ford)", "Government IT Services"]),
                    ("Sandton", -26.1076, 28.0567, ["Financial Capital of Africa", "Luxury Retail & Real Estate", "Investment Firms"])
                ]
            },
            {
                "Name": "Western Cape", "Code": "WC",
                "Cities": [
                    ("Cape Town", -33.9249, 18.4241, ["Silicon Cape Tech Startups", "Film & TV Production Studios", "Wine Export Viticulture", "Luxury Tourism & Waterfront Dining"]),
                    ("Stellenbosch", -33.9321, 18.8602, ["Tech Incubators", "Premium Wine Estates", "Higher Education Research"])
                ]
            },
            {
                "Name": "KwaZulu-Natal", "Code": "KZN",
                "Cities": [
                    ("Durban", -29.8587, 31.0218, ["Port of Durban Container Shipping", "Chemical & Sugar Refining", "Automotive Assembly (Toyota)", "Coastal Tourism"]),
                    ("Pietermaritzburg", -29.6006, 30.3794, ["Aluminium Processing", "Timber Products", "Agri Transport"])
                ]
            }
        ]
    },

    # 29. Qatar
    {
        "Code": "QA", "Name": "Qatar", "FlagEmoji": "🇶🇦",
        "States": [
            {
                "Name": "Doha Municipality", "Code": "DA",
                "Cities": [
                    ("Doha", 25.2854, 51.5310, ["Sovereign Capital & QIA Investments", "Media & Broadcast (Al Jazeera)", "Sports & Event Management Infrastructure", "Luxury Hospitality & Private Healthcare", "Fintech Platforms"]),
                    ("West Bay", 25.3210, 51.5310, ["Diplomatic Embassies", "Tower Corporate HQs", "Asset Management"]),
                    ("The Pearl-Qatar", 25.3710, 51.5510, ["Luxury Marina Living", "High-End Dining", "Yacht Chartering"])
                ]
            },
            {
                "Name": "Al Rayyan Municipality", "Code": "RA",
                "Cities": [
                    ("Al Rayyan", 25.2919, 51.4244, ["Education City Universities", "Qatar Science & Technology Park", "HealthTech"]),
                    ("Education City", 25.3150, 51.4400, ["Research Laboratories", "Biomedical Studies", "EdTech"])
                ]
            },
            {
                "Name": "Al Wakrah Municipality", "Code": "WA",
                "Cities": [
                    ("Al Wakrah", 25.1768, 51.6048, ["Hamad Port Maritime Container Freight", "Coastal Logistics", "Fisheries"]),
                    ("Mesaieed", 24.9917, 51.5492, ["Petrochemical Complexes", "Steel Mills", "Industrial Refineries"])
                ]
            },
            {
                "Name": "Al Khor Municipality", "Code": "KH",
                "Cities": [
                    ("Al Khor", 25.6804, 51.5058, ["Ras Laffan LNG Port Operations", "Natural Gas Industrial City", "Marine Services"])
                ]
            }
        ]
    },

    # 30. Kuwait
    {
        "Code": "KW", "Name": "Kuwait", "FlagEmoji": "🇰🇼",
        "States": [
            {
                "Name": "Al Asimah (Capital)", "Code": "KU",
                "Cities": [
                    ("Kuwait City", 29.3759, 47.9774, ["Petroleum Finance & KPC Operations", "Sovereign Wealth (KIA)", "Corporate Law & Consulting", "Luxury Shopping & Hospitality", "High-End Aesthetics & Dental"]),
                    ("Shuwaikh", 29.3489, 47.9406, ["Commercial Port Logistics", "Automotive Showrooms", "Industrial Wholesale"]),
                    ("Sharq", 29.3850, 47.9900, ["Banking Financial Center", "Bourse Kuwait", "Corporate Towers"])
                ]
            },
            {
                "Name": "Hawalli Governorate", "Code": "HA",
                "Cities": [
                    ("Hawalli", 29.3328, 48.0289, ["Consumer Electronics Retail", "Commercial Services", "Private Clinics"]),
                    ("Salmiya", 29.3344, 48.0772, ["Coastal Dining & Hospitality", "Shopping Malls", "Medical Centers"])
                ]
            },
            {
                "Name": "Farwaniya Governorate", "Code": "FA",
                "Cities": [
                    ("Farwaniya", 29.2778, 47.9583, ["Commercial Wholesaling", "Air Freight Support (Kuwait Airport)", "Retail"]),
                    ("Al-Rai", 29.3000, 47.9300, ["Mega Shopping (The Avenues)", "Industrial Machinery", "Automotive"])
                ]
            },
            {
                "Name": "Ahmadi Governorate", "Code": "AH",
                "Cities": [
                    ("Ahmadi", 29.0769, 48.0839, ["Kuwait Oil Company HQs", "Petroleum Engineering", "Oilfield Equipment"]),
                    ("Fahaheel", 29.0833, 48.1333, ["Coastal Commerce", "Boutique Retail", "Fish & Food Markets"])
                ]
            }
        ]
    },

    # 31. Oman
    {
        "Code": "OM", "Name": "Oman", "FlagEmoji": "🇴🇲",
        "States": [
            {
                "Name": "Muscat Governorate", "Code": "MA",
                "Cities": [
                    ("Muscat", 23.5880, 58.3829, ["Maritime Logistics (Port Sultan Qaboos)", "Oil & Gas (PDO Operations)", "Sovereign Wealth & Private Banking", "Luxury Tourism & Heritage Lodging", "Private Healthcare & Dental"]),
                    ("Seeb", 23.6703, 58.1891, ["Muscat Airport Air Cargo", "Commercial Wholesale Hub", "Fisheries"]),
                    ("Bawshar", 23.5500, 58.4000, ["Private Medical Cities", "Commercial Malls", "Corporate Offices"])
                ]
            },
            {
                "Name": "Dhofar Governorate", "Code": "ZU",
                "Cities": [
                    ("Salalah", 17.0151, 54.0924, ["Port of Salalah Transshipment Hub", "Khareef Monsoon Eco-Tourism", "Frankincense & Agri Export", "Freezone Manufacturing"])
                ]
            },
            {
                "Name": "Al Batinah North", "Code": "BS",
                "Cities": [
                    ("Sohar", 24.3474, 56.7094, ["Sohar Port & Freezone Heavy Metals", "Aluminium Smelting", "Petrochemical Refineries"])
                ]
            },
            {
                "Name": "Ad Dakhiliyah", "Code": "DA",
                "Cities": [
                    ("Nizwa", 22.9333, 57.5333, ["Historical Fort Tourism", "Handicrafts & Silver Jewelry", "Agri Date Processing"])
                ]
            }
        ]
    },

    # 32. Bahrain
    {
        "Code": "BH", "Name": "Bahrain", "FlagEmoji": "🇧🇭",
        "States": [
            {
                "Name": "Capital Governorate", "Code": "01",
                "Cities": [
                    ("Manama", 26.2285, 50.5860, ["Fintech & Islamic Banking Capital", "Central Bank of Bahrain RegTech Sandbox", "Corporate Litigation & Advisory", "Luxury Hospitality & Dining", "Private Healthcare & Aesthetics"]),
                    ("Seef", 26.2411, 50.5367, ["Financial Towers", "Enterprise Tech Consulting", "Commercial Malls"]),
                    ("Diplomatic Area", 26.2370, 50.5920, ["Embassies & International Law", "Investment Banks", "Corporate HQs"])
                ]
            },
            {
                "Name": "Muharraq Governorate", "Code": "02",
                "Cities": [
                    ("Muharraq", 26.2572, 50.6119, ["Bahrain International Airport Logistics", "Pearling Trail UNESCO Tourism", "Traditional Craft"]),
                    ("Amwaj Islands", 26.2894, 50.6631, ["Waterfront Luxury Real Estate", "Yacht Clubs", "Resort Hospitality"])
                ]
            },
            {
                "Name": "Southern Governorate", "Code": "04",
                "Cities": [
                    ("Riffa", 26.1300, 50.5550, ["Bapco Petroleum Refining", "Aluminium Bahrain (Alba)", "Motorsports (Bahrain F1 Circuit)"])
                ]
            }
        ]
    }
]

total_countries = all_countries + REMAINING_COUNTRIES
print(f"Total compiled countries: {len(total_countries)}")

total_states = sum(len(c["States"]) for c in total_countries)
total_cities = sum(sum(len(s["Cities"]) for s in c["States"]) for c in total_countries)
print(f"Total states/provinces: {total_states}")
print(f"Total commercial cities: {total_cities}")
