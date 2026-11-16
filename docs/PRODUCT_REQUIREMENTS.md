# TKS LeadPilot — Product Requirements Document (PRD)

**Document Version:** 1.0.0  
**Project:** TKS LeadPilot  
**Owner:** TheKhanSoft (TKS)  
**Status:** Approved for Implementation (Stage 1 Specification)  
**Primary Platforms:** Windows Desktop (Avalonia C#) + Android Mobile (Flutter Dart)  
**Backend:** Optional Central Laravel 12 REST API + MySQL 8  
**Audit Automation:** Local Python 3.13 Sidecar (Playwright + Lighthouse + axe-core)  
**AI Engine:** Google Gemini Developer API (Free Tier Only — Zero-Billing Policy)  

---

## 1. Executive Summary & Purpose

TKS LeadPilot is an internal business-discovery, website-intelligence, website-auditing, AI-assisted lead-generation, CRM, and outreach platform engineered for TheKhanSoft.

TheKhanSoft delivers custom software development, modern web applications, e-commerce platforms, attendance & workforce solutions, enterprise digital transformation, and website optimization. TKS LeadPilot provides an automated, evidence-backed workflow to:
1. Discover potential business clients across geographic regions and industry verticals using compliant public directories and data feeds.
2. Verify their digital presence, website availability, technology stack, and contact points.
3. Perform non-invasive, evidence-backed technical audits (performance, SEO, accessibility, security hygiene, and conversion readiness).
4. Use Gemini Free Tier AI to translate complex audit findings into plain-English business impact narratives and tailored service proposals.
5. Compute transparent, explainable lead scores.
6. Manage sales pipelines, follow-ups, and personalized outreach drafts with mandatory human approval by default.
7. Operate local-first without requiring constant cloud connectivity, synchronizing seamlessly with an optional central Laravel API.

---

## 2. Core Operational Principles

1. **No Web Dashboard:** The system is exclusively accessed through the native Windows Desktop application (`desktop/`) and the native Android mobile application (`mobile/`). Central administration and sync occur via a headless REST API.
2. **Local-First Resilience:** The desktop application is 100% operational offline. All entities are stored in local SQLite databases using client-generated UUIDs. When internet connectivity is available, discovery, auditing, AI analysis, and sync become active.
3. **Evidence-Backed Audits:** AI is never permitted to invent, fabricate, or hallucinate audit metrics. Raw measurements are captured deterministically by the Python audit engine; AI is used solely for summarization, contextual translation, and proposal drafting.
4. **Mandatory Human-in-the-Loop:** Automated mass messaging is forbidden. Outreach drafts require explicit operator review and manual approval before transmission.
5. **Zero-Unexpected-Billing AI Policy:** Strict adherence to Google AI Studio Gemini Developer API Free Tier limits. Spending ceiling is enforced at $0.00. Automatic fallback to paid models or paid tiers is architecturally prevented.
6. **Data Privacy & Minimal Exposure:** Free-tier AI prompts never include private customer databases, credentials, or sensitive business secrets.

---

## 3. Platform Responsibilities: Desktop vs. Mobile

| Capability / Module | Windows Desktop (`desktop/`) | Android Mobile (`mobile/`) |
|---|:---:|:---:|
| **Local Persistence** | SQLite (Entity Framework Core) | SQLite (Drift) |
| **Offline Business & Lead Management** | Full CRUD, Tagging, Notes | Full CRUD, Tagging, Notes |
| **Pipeline & CRM Management** | Full Pipeline, Board & List Views | Stage Updates, Tasks, Follow-ups |
| **Business Discovery (OSM, Places, Import)** | Full (Heavy processing, bulk deduplication) | Read-only job history; trigger saved search |
| **CSV / XLSX Import & Export** | Full interactive column mapper & preview | Export selected summaries (vCard, CSV) |
| **Website Intelligence (HTTP Checks)** | Direct safe asynchronous inspection | View cached results |
| **Local Audit Engine (Python Sidecar)** | Directly launches & controls sidecar | View completed audit reports & scores |
| **Gemini AI Analysis & Draft Generation** | Direct API calls, prompt editor, preview | View AI summaries, request draft review |
| **Outreach Message Approval Queue** | Full template management, approval queue | Review & approve/reject queued drafts |
| **Direct Message Dispatch (Future compliant gateways)** | Available once configured | Available once configured |
| **Central Synchronization** | Full bidirectional delta sync | Full bidirectional delta sync |
| **System Settings & Integrations Manager** | Full configuration & secret store | Account connection & display settings |

---

## 4. Detailed Functional Modules

### Module 1: Authentication, Access Control & Security
- **Desktop Local Mode:** Master PIN / password protection stored using PBKDF2/Argon2 hashing. API keys encrypted using Windows Data Protection API (DPAPI).
- **Central Mode:** Laravel Sanctum token-based authentication. Granular roles: `SuperAdmin`, `Admin`, `Researcher`, `Auditor`, `SalesExecutive`, `Viewer`.
- **Session & Device Registry:** Devices assigned unique hardware/client UUIDs with token revocation capabilities.

### Module 2: Executive Dashboard & Analytics
- Live counters computed directly from database records: Discovered Businesses, Qualified Leads, Opportunities by Stage, Audits Completed/Pending, Average Lead Score, Drafts Pending Approval, Tasks Due/Overdue.
- Visual breakdown of businesses with vs. without websites.
- API quota consumption gauges (Gemini Free Tier daily/minute rate limits, OSM rate limit tracking).
- Synchronization status indicator (Offline, Synced, Changes Pending, Conflict Warning).

### Module 3: Business Discovery Engine
- **Search Criteria:** Country, State/Province, City, Locality, Category, Keywords, Name search, Minimum/Maximum review ratings (where permitted), Website filter (With / Without / Any), Deduplication policy.
- **Provider Adapters:**
  - *OpenStreetMap (OSM) / Overpass API:* Permitted queries, rate-limit conforming caching (max 1 req/sec on public endpoints), strict attribution preservation.
  - *Google Places API (New):* Disabled until user supplies key. Minimal field masks (`id`, `displayName`, `formattedAddress`, `websiteUri`, `nationalPhoneNumber`). Built-in hard budget caps.
  - *CSV / XLSX File Import:* Drag-and-drop import, dynamic header mapping, preview of first 50 rows, duplicate detection, import error log table.
  - *Manual Entry:* High-speed form entry for field contacts.
- **Job Tracking:** Background job persistence with start/end time, result count, error count, and raw payload isolation.

### Module 4: Business Database & Deduplication
- **Normalized Schema:** Business Profile, Physical Addresses, Phone Numbers, Emails, Websites, Social Links, Categories, Tags, Lead Status, Provenance records.
- **Deduplication Engine:** Multi-factor similarity scoring matching on:
  - Normalized clean business name (Levenshtein / Jaro-Winkler)
  - Normalized domain name
  - E.164 phone number
  - Physical address locality
- **Merge Studio:** Non-destructive matching. Conflicted fields presented side-by-side with preview; manual operator confirmation required. Full merge history preserved.

### Module 5: Website Intelligence (Lightweight Inspection)
- Safe HTTP/HTTPS crawler with SSRF protection (loopback, private subnets, link-local, cloud metadata blocked).
- Resolves DNS and inspects TLS certificates, redirect chains, HTTP status codes.
- Extracts meta title, meta description, Open Graph tags, canonical URL, language.
- Detects CMS / platforms (WordPress, Shopify, Wix, Squarespace, Webflow, custom).
- Discovers public contact URLs, visible email addresses, public phone numbers, and social media handles.
- Categorizes status: `ActiveWebsite`, `NoWebsite`, `ParkedDomain`, `UnderConstruction`, `ConnectionError`, `BlockedSSRF`.

### Module 6: Advanced Website Audit Engine (Python Sidecar)
- Standalone Python service executing in an isolated process.
- **Sub-Audits:**
  - *Performance:* Lighthouse CLI / Playwright DevTools Core Web Vitals (FCP, LCP, TBT, CLS), render-blocking CSS/JS, unoptimized image weight, compression (Gzip/Brotli).
  - *SEO:* Missing titles/descriptions, H1-H6 hierarchy integrity, broken internal links, robots.txt presence, sitemap.xml validation, structured data (JSON-LD) validation.
  - *Accessibility:* axe-core automated rule evaluation, contrast failures, missing alt attributes, unlabelled form inputs, keyboard navigation traps.
  - *Security & Technical Hygiene:* Strict TLS configuration, mixed content warnings, security headers (`Strict-Transport-Security`, `Content-Security-Policy`, `X-Content-Type-Options`, `X-Frame-Options`), directory listing checks. Non-invasive only.
  - *Conversion & Mobile UX:* Mobile viewport readiness, CTA presence, click-to-call phone linkage, contact form discoverability.
- **Output:** Structured JSON schema with normalized finding severity (`Critical`, `High`, `Medium`, `Low`, `Informational`), raw evidence, affected URL, suggested remediation, and estimated business impact.

### Module 7: AI Analysis Engine (Gemini Developer API Free Tier)
- Zero-cost architecture consuming Gemini 2.5 / 1.5 Flash Free Tier models via Google AI Studio API keys.
- **Capabilities:**
  1. Technical-to-Business Audit Translation: Translates complex Lighthouse/axe-core technical metrics into language business owners understand.
  2. Opportunity Scoring Insights: Explains why a prospect is prime for specific web development or digital solutions.
  3. Personalized Outreach Drafting: Generates high-converting, tailored email and WhatsApp outreach messages based on real website audit evidence.
  4. Proposal Outlines: Produces scope-of-work suggestions aligned with TKS service packages.
- **Cost Controls:**
  - Hard local rate-limiting counter (e.g., max 15 requests per minute, max 1,000 requests per day).
  - Spending ceiling fixed at $0.00.
  - Local caching: Identical audit summaries are cached locally by hash to prevent redundant API calls.
  - Emergency master kill-switch to shut down all outgoing AI traffic instantly.

### Module 8: Transparent Lead Scoring Engine
- Deterministic, explainable multi-component scoring formula (0–100 scale):
  - *Absence of Website:* +35 pts (Prime candidate for new website development)
  - *Critical Performance / Mobile Failure:* +25 pts (Candidate for modernization/redesign)
  - *Missing HTTPS / Security Hygiene Failures:* +15 pts (Security & maintenance candidate)
  - *Accessibility Violations:* +10 pts (Compliance risk fix candidate)
  - *SEO Deficits:* +10 pts (SEO optimization opportunity)
  - *High-Value Category Fit:* +5 pts
- Every score details its exact component contributions. Manual score overrides permitted with mandatory audit log note.

### Module 9: TKS Service Matching
- Configurable catalogue of TheKhanSoft service offerings:
  - Custom Web Application Development
  - Modern Business Websites (Redesign / Clean Build)
  - E-Commerce Platforms (Shopify, WooCommerce, Custom)
  - Workforce & Attendance Management Systems
  - Website Performance & Core Web Vitals Optimization
  - Technical SEO & Content Hygiene
  - Ongoing Website Maintenance & Security Retainers
- Rule-based logic links specific audit evidence to recommended service tiers with customizable pitch snippets.

### Module 10: CRM & Sales Pipeline
- **Pipeline Stages:** `Discovered` ➔ `ToReview` ➔ `Qualified` ➔ `Researching` ➔ `ContactDrafted` ➔ `AwaitingApproval` ➔ `Contacted` ➔ `Responded` ➔ `MeetingScheduled` ➔ `ProposalSent` ➔ `Negotiation` ➔ `Won` / `Lost` / `DoNotContact`.
- Lead profiles feature: Contact management, follow-up scheduling, activity history, rich-text notes, task reminders, lost reason tracking.

### Module 11: Message Studio & Outreach
- Multi-channel draft generator (Email, WhatsApp).
- Dynamic placeholder tokens: `{{business_name}}`, `{{contact_name}}`, `{{website_url}}`, `{{top_audit_issue}}`, `{{performance_score}}`, `{{suggested_service}}`.
- **Approval Queue:** All AI-generated or scheduled messages enter an approval queue. No outgoing message is ever transmitted automatically.
- **Compliance & Opt-Out:** Suppression list support, unsubscribed contact locking, GDPR/CAN-SPAM/Local privacy notice templates.

### Module 12: Reporting & Data Export
- Export business lists, audit findings, and outreach logs to CSV, XLSX, and formatted JSON.
- Foundation for print-ready PDF audit reports and proposal teasers (desktop).

### Module 13: Integration Manager
- Unified credential and health management for:
  - Google Gemini API (Free Tier key, usage counter, test ping)
  - Google Places API (Optional key, quota limiter, test ping)
  - Central Laravel Sync API (Endpoint URL, device token, sync health)
  - SMTP / Email gateway (Disabled by default)
  - WhatsApp Business Cloud API (Disabled by default)

### Module 14: Central Synchronization Engine
- Bidirectional delta synchronization between desktop/mobile clients and Laravel central API.
- UUIDv4 entity identifiers, client revision tracking, update vectors, and conflict detection (Last-Write-Wins with conflict archiving).
- Soft delete synchronization via tombstones.

### Module 15: Administration & Maintenance
- Database backup to single-file encrypted archive (`.tksbak`).
- Database vacuum, index optimization, log rotation, and data purge tools.
