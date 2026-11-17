# TKS LeadPilot

> Professional Business-Discovery, Website-Intelligence, Website-Auditing, AI-Assisted Lead-Generation, CRM and Outreach Platform for TheKhanSoft.

---

## 🌟 Highlights

- **100% Native Client Applications:** Avalonia UI (C# / .NET 10) Windows Desktop Control Station & Flutter (Dart) Android Field Companion.
- **No Web Dashboard:** Zero browser-based dashboards; built strictly as high-performance native desktop and mobile applications.
- **Local-First Hybrid Architecture:** Fully functional offline against local SQLite databases (`%LOCALAPPDATA%\TKS.LeadPilot\tks_leadpilot.db`); optional bidirectional delta sync with a central Laravel 12 API.
- **Pre-Flight SSRF Guard & Native Website Auditor:** High-performance, in-process website intelligence engine inspecting tech stack/CMS, TLS/SSL certificates, security headers, crawlability, and TTFB latency with strict IP protection.
- **Cost-Guarded Google Places Enrichment:** Optional commercial enrichment with hard daily request ceilings, disabled-by-default switch, and 30-day SQLite caching to prevent duplicate spend ($0 on cache hits).
- **Zero-Unexpected-Billing AI Policy:** Google Gemini Developer API Free Tier only (`gemini-1.5-flash`); strict local spending ceiling of $0.00, hard rate limiters (12 RPM / 1,200 RPD), and 100% offline-first deterministic proposal fallbacks.
- **Explainable Lead Scoring & Service Matching:** Deterministic 0–100 opportunity score across 5 explainable components, paired with an automated rule engine matching findings to TheKhanSoft's service catalogue.
- **Complete CRM Sales Pipeline:** 14-stage interactive sales pipeline board, task management with due dates, and chronological activity notes/timeline.
- **DPAPI Credential Vault:** All sensitive API keys (Gemini, Google Places) are securely encrypted via Windows Data Protection API (DPAPI, `CurrentUser` scope) and never stored in plain text.

---

## 🚦 Development Status & Roadmap

| Stage | Description | Status |
|:---:|---|:---:|
| **Stage 0** | Environment and Repository Inspection | ✅ Completed |
| **Stage 1** | Product Specification & Architecture | ✅ Completed |
| **Stage 2** | UX Architecture & Design System | ✅ Completed |
| **Stage 3** | Local Desktop Foundation (Avalonia UI 12 + EF Core SQLite + DPAPI Vault) | ✅ Completed |
| **Stage 4** | Core Business Database, Search, Deduplication & CSV Import/Export | ✅ Completed |
| **Stage 5** | Free Discovery Pipeline (OpenStreetMap Overpass Engine & Rate Limiter) | ✅ Completed |
| **Stage 6** | Website Intelligence & Local Lightweight Auditor with SSRF Guard | ✅ Completed |
| **Stage 7** | Paid / Official Places Enrichment (Google Places API - Cost-Guarded & Cached) | ✅ Completed |
| **Stage 8** | Gemini AI Proposal Synthesizer (Free Tier) & Audit Comparison Engine | ✅ Completed |
| **Stage 9** | Transparent Lead Scoring (0–100), TKS Service Matcher & CRM Sales Pipeline | ✅ Completed |
| **Stage 10** | Outreach Message Studio & Multi-Channel Drafter (Email / WhatsApp with Human Approval) | ⏳ In Progress (Next MVP Stage) |
| **Stage 11** | Desktop Packaging, Polishing & End-to-End MVP Verification | ⏳ Upcoming (MVP Delivery) |
| **Stage 12** | Central Laravel 12 API & Sync Engine | ⏳ Post-MVP |
| **Stage 13** | Android Mobile Companion (Flutter) | ⏳ Post-MVP |
| **Stage 14** | Branded PDF Reporting & Direct Dispatch Gateways | ⏳ Post-MVP |
| **Stage 15** | Hardening, Packaging & Release | ⏳ Final Release |

---

## 📂 Repository Organization

```text
TKS-LeadPilot/
├── desktop/                  # Windows Desktop Client (Avalonia UI 12+ .NET 10)
│   ├── src/
│   │   ├── TKS.LeadPilot.Core/         # Domain models, enums, and service interfaces
│   │   ├── TKS.LeadPilot.Data/         # EF Core SQLite, repositories, DPAPI vault
│   │   └── TKS.LeadPilot.UI/           # Avalonia UI desktop views and viewmodels
│   ├── tests/
│   │   ├── TKS.LeadPilot.Core.Tests/   # Unit tests for core business logic & algorithms
│   │   └── TKS.LeadPilot.Data.Tests/   # Integration tests for database & repositories
│   └── TKS.LeadPilot.Desktop.sln       # Visual Studio / .NET solution
├── mobile/                   # Android Mobile Companion (Flutter 3.47+ / Dart 3.13+)
├── audit_engine/             # Local Website Audit & Crawler Sidecar (Python 3.13)
├── server/                   # Optional Central Sync & Administration API (Laravel 12)
├── docs/                     # Specifications, Architecture, and Compliance Docs
│   ├── PRODUCT_REQUIREMENTS.md
│   ├── ARCHITECTURE.md
│   ├── UX_AND_DESIGN_SYSTEM.md
│   ├── DATABASE_DESIGN.md
│   ├── AI_AND_COST_POLICY.md
│   ├── DISCOVERY_SOURCE_COMPLIANCE.md
│   ├── SYNC_DESIGN.md
│   ├── SECURITY_AND_PRIVACY.md
│   ├── TEST_PLAN.md
│   ├── MVP_SCOPE.md
│   ├── RISK_REGISTER.md
│   ├── CHANGELOG.md
│   └── DECISIONS/
│       └── ADR-001-ui-stack-selection.md
└── README.md
```

---

## 🛠️ Completed Architectural Capabilities (Stages 1–9)

### 1. Local Desktop Foundation & Security (`desktop/`)
- Built on **Avalonia UI 12.1.2** with MVVM architecture (`CommunityToolkit.Mvvm`).
- **Entity Framework Core SQLite** data layer with universal UUIDv4 primary keys and automatic database seeding.
- **Windows DPAPI Secret Vault (`DpapiSecretVault`):** Encrypts external API credentials using the Windows Data Protection API (`CurrentUser` scope). Credentials are never committed, logged, or exported.

### 2. Business Database, Deduplication & CSV Engine
- Multi-field search and filtering across business name, city, category, pipeline stage, score range, starred flag, and custom tags.
- **Intelligent Deduplication:** Multi-tier matching engine (`ExactWebsite` 100%, `ExactPhone` 95%, `ExactNameAndCity` 90%, `FuzzyNameAndCity`) with smart field merging without data loss.
- **RFC 4180 CSV / Excel Importer & Exporter:** Automatic column header synonym detection, pre-import validation, duplicate preview, and UTF-8 BOM export.

### 3. OpenStreetMap Overpass Discovery Engine
- Free, ethical lead discovery targeting metropolitan areas across Pakistan (Karachi, Lahore, Islamabad, Rawalpindi, Peshawar, Multan, Faisalabad) and commercial categories.
- Built-in rate limiter enforcing a minimum 2,000ms inter-query delay and failover across multiple public Overpass mirrors.

### 4. Website Intelligence & Lightweight Auditor
- **Pre-Flight SSRF Guard (`SsrfValidator`):** Blocks non-HTTP schemes, private RFC 1918 networks, loopback IP addresses, and cloud instance metadata endpoints (`169.254.169.254`).
- **Tech Stack & CMS Detection:** Identifies WordPress, WooCommerce, Shopify, Wix, Webflow, Laravel, React, Vue, Google Analytics, Meta Pixel, and web servers.
- **In-Process Audit Engine:** Inspects SSL/TLS certificates (issuer, validity days), HTTP-to-HTTPS redirects, security headers (HSTS, CSP, X-Frame-Options), SEO hygiene (title, meta description, mobile viewport, H1 tags, sitemap/robots.txt), and TTFB latency profiling.

### 5. Cost-Guarded Google Places API Enrichment
- Disabled by default with explicit user toggle and configurable daily request ceiling (default 50/day).
- **30-Day SQLite Caching:** Prevents duplicate queries for recently enriched businesses, guaranteeing $0 cost on cache hits.
- Fetches verified business status, Google star ratings, total review counts, formatted addresses, and operating hours.

### 6. Gemini AI Proposal Synthesis & Audit Comparison
- **Zero-Unexpected-Billing:** Configured strictly for Google AI Studio Free Tier (`gemini-1.5-flash`), with sliding-window rate limiters (12 RPM) and a hard daily quota ceiling (1,200 RPD).
- Pre-flight PII redaction (scrubbing phone numbers and emails) before prompt dispatch.
- Multi-tone synthesis (`Consultative`, `RoiFocused`, `Technical`) and **100% offline deterministic proposal generator** for instantaneous drafts with zero network dependency.
- **Historical Audit Comparison:** Side-by-side delta analysis between baseline and re-audits with tri-state finding classification (`Resolved`, `New`, `Persistent`).

### 7. Transparent Lead Scoring & CRM Sales Pipeline
- **Explainable 0–100 Opportunity Score:** Scored across 5 distinct components:
  1. *Website Presence & Condition* (Max 35 pts)
  2. *Performance & Latency* (Max 25 pts)
  3. *Security & Trust Hygiene* (Max 15 pts)
  4. *Search Visibility & SEO* (Max 10 pts)
  5. *Google Places Commercial Authority* (Max 15 pts)
- Manual score override capability with mandatory operator audit logging.
- **TKS Service Matcher:** Automatically pairs audit diagnostic signals to TheKhanSoft's core service catalogue (Web Modernization, E-Commerce, SEO, Zero-Trust Security) and generates personalized outreach pitch tokens.
- **CRM Sales Board:** Full management across 14 sales pipeline stages, interactive task scheduling (due dates, completion status), and chronological activity history notes.

---

## ⚙️ Configuration & Settings

All application settings are managed directly within the desktop UI via **System Settings & Vault**:

| Setting | Storage | Description | Default |
|---|---|---|---|
| **Gemini API Key** | Windows DPAPI Vault | Google AI Studio Developer API Key (Free Tier) | *Unset* |
| **Gemini AI Toggle** | SQLite / Vault | Enables or disables AI proposal generation | `Disabled` (opt-in) |
| **Gemini Model** | SQLite (`AppSetting`) | Target Gemini model identifier | `gemini-1.5-flash` |
| **Gemini Daily Limit** | SQLite / Memory | Hard ceiling to maintain $0.00 zero-cost policy | `1,200 RPD` (Max 1,500) |
| **Google Places API Key** | Windows DPAPI Vault | Google Cloud Places API key | *Unset* |
| **Google Places Toggle** | SQLite / Vault | Enables or disables commercial enrichment | `Disabled` (opt-in) |
| **Google Places Daily Ceiling** | SQLite (`AppSetting`) | Maximum daily paid API queries | `50 requests/day` |
| **Database Path** | Local Filesystem | SQLite single-file application store | `%LOCALAPPDATA%\TKS.LeadPilot\tks_leadpilot.db` |

---

## 🚀 Quick Start (Desktop Client)

### Prerequisites
- **Windows 10 / 11 (x64)**
- **[.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)**

### Build & Run
```powershell
# Clone the repository
git clone https://github.com/TheKhanSoft/TKS-LeadPilot.git
cd TKS-LeadPilot

# Restore dependencies and build the solution
dotnet build desktop/TKS.LeadPilot.Desktop.sln

# Launch the desktop station
dotnet run --project desktop/src/TKS.LeadPilot.UI
```

### Running Unit Tests
```powershell
# Execute the full test suite
dotnet test desktop/TKS.LeadPilot.Desktop.sln
```

---

## 📜 Documentation Index

- [Product Requirements Specification](docs/PRODUCT_REQUIREMENTS.md)
- [Target Architecture & Data Flows](docs/ARCHITECTURE.md)
- [UX Architecture & Design System](docs/UX_AND_DESIGN_SYSTEM.md)
- [Database Design & Schema](docs/DATABASE_DESIGN.md)
- [AI & Zero-Cost Billing Policy](docs/AI_AND_COST_POLICY.md)
- [Discovery Compliance & Ethics](docs/DISCOVERY_SOURCE_COMPLIANCE.md)
- [Delta Synchronization Protocol](docs/SYNC_DESIGN.md)
- [Security & SSRF Safeguards](docs/SECURITY_AND_PRIVACY.md)
- [Comprehensive Test Plan](docs/TEST_PLAN.md)
- [MVP Scope & Milestones](docs/MVP_SCOPE.md)
- [Project Changelog](docs/CHANGELOG.md)
- [Risk Register](docs/RISK_REGISTER.md)
- [Architecture Decision Record: UI Stack Selection](docs/DECISIONS/ADR-001-ui-stack-selection.md)

---

## 📄 License & Proprietary Rights

Proprietary software developed exclusively for **TheKhanSoft (TKS)**. All rights reserved.
