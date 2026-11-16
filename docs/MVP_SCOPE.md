# TKS LeadPilot — MVP Scope vs. Future Releases

**Document Version:** 1.0.0  
**Status:** Approved Roadmap  

---

## 1. MVP Scope (Minimum Viable Product — Stages 2 to 11)

The MVP is engineered as a **complete, standalone Windows Desktop business discovery, intelligence, auditing, scoring, and CRM station**:

### In-Scope for MVP:
1. **Windows Desktop Shell (`desktop/`):**
   - Full Avalonia UI 11/12 application with responsive desktop navigation.
   - Local SQLite database managed via Entity Framework Core 10.
   - Master password/PIN local security with DPAPI encrypted secrets.
2. **Business Database & Management:**
   - Full CRUD for businesses, contacts, websites, notes, tasks, and tags.
   - CSV & XLSX spreadsheet import with header mapping, preview, and error isolation.
   - Deduplication detection with interactive side-by-side comparison and manual merge.
3. **Business Discovery:**
   - OpenStreetMap / Overpass API compliant provider with caching and rate-limiting.
   - Manual discovery job runner with background execution and results logging.
4. **Website Intelligence:**
   - Safe HTTP/HTTPS metadata crawler with built-in SSRF protection.
   - CMS detection, public contact discovery, and website availability classification.
5. **Python Website Audit Sidecar (`audit_engine/`):**
   - Standalone FastAPI sidecar on localhost.
   - Lighthouse performance & SEO checks.
   - axe-core accessibility compliance checks.
   - Security hygiene (HTTPS, headers, cookies) evaluation.
   - Evidence-backed finding storage.
6. **Gemini AI Integration (Free Tier Only):**
   - Google AI Studio Free Tier integration (`gemini-1.5-flash`).
   - Audit report translation into plain English business recommendations.
   - Hard rate-limiting (12 RPM / 1,200 RPD), $0.00 spending ceiling, and emergency kill switch.
7. **Explainable Lead Scoring & Service Matching:**
   - Transparent, multi-factor rule-based scoring (0-100) with full score component breakdown.
   - TKS service catalogue with automatic recommendation matching.
8. **CRM Pipeline & Message Studio:**
   - Visual Kanban pipeline and data-grid views across all lead stages.
   - Follow-up task scheduling and activity history.
   - Outreach message drafter (Email and WhatsApp) with personalized placeholders.
   - Mandatory human approval queue (no auto-send).

---

## 2. Post-MVP Releases

### Release 2.0 (Stages 12 & 13):
- **Central Laravel 12 API (`server/`):** Multi-user role-based access, device registration, and central MySQL store.
- **Bidirectional Delta Synchronization:** Idempotent delta synchronization between multiple desktops and the central server.
- **Android Mobile Companion (`mobile/`):** Flutter Android app for offline lead viewing, field notes, task updates, and sync.

### Release 3.0 (Stage 14 & Future):
- **PDF Export Engine:** Beautiful, branded, print-ready client audit reports and proposal teaser documents.
- **Direct Dispatch Gateways:** Compliant integration with official WhatsApp Cloud API and SMTP email gateways (requiring explicit operator configuration).
- **Google Places API Integration:** Production rollout of Google Places API adapter with strict SKU budgeting.
