# TKS LeadPilot — Discovery Source Compliance & Data Ethics

**Document Version:** 1.0.0  
**Status:** Approved Policy  
**Effective Date:** 2026-11-16  

---

## 1. Compliance Directives

TKS LeadPilot is designed to discover and evaluate public business opportunities legally, ethically, and without evading platform controls.

### Core Rules:
1. **No Anti-Bot Bypass:** The system will never bypass CAPTCHAs, solve proof-of-work challenges, or defeat Cloudflare, Datadome, or similar anti-bot screens.
2. **No Stealth Scraping:** No headless browser or automated script may spoof identity, rotate unpermitted residential proxies, or access protected member directories.
3. **Respect for Robots Directives:** The crawler honors `robots.txt` disallow rules and rate limits.
4. **Transparent Identity:** Public crawl requests carry an honest User-Agent string:  
   `TKS-LeadPilot/1.0 (+https://thekhansoft.com/bot; contact@thekhansoft.com)`

---

## 2. Provider-Specific Policies

### 2.1 OpenStreetMap (OSM) / Overpass API
- **Permitted Use:** OpenStreetMap data is published under the Open Database License (ODbL).
- **Public Nominatim Constraints:**
  - Hard limit of **1 request per second**.
  - Must supply a valid User-Agent with contact email.
  - No bulk scraping of entire cities or countries via public Nominatim.
- **Implementation Strategy:**
  - For targeted discovery, use structured queries to official Overpass API mirrors with appropriate caching.
  - Caches must persist for at least 7 days to eliminate duplicate queries.
  - Retain required attribution: *"© OpenStreetMap contributors"*.

### 2.2 Google Places API (New)
- **Optional & User-Configured:** Disabled until operator explicitly provides a Google Cloud API key.
- **Field Masks:** Must use `X-Goog-FieldMask` to request solely needed fields (`places.id`, `places.displayName`, `places.formattedAddress`, `places.websiteUri`, `places.nationalPhoneNumber`).
- **Data Retention & Storage:**
  - In accordance with Google Maps Platform Terms of Service, Place IDs may be stored permanently.
  - Other Google-derived fields may not be permanently cached without refresh or stored to create an unrestricted substitute directory.
  - If Place details are updated from independent user verification or website inspection, the provenance must reflect the new source.

### 2.3 CSV / Excel Import
- Imported business records must track source provenance (e.g. `ImportBatchId`, `SourceFilename`, `ImportedAt`).
- Operators must confirm they have lawful right to import prospect databases into their CRM.
