# TKS LeadPilot — Changelog

All notable changes to the TKS LeadPilot project will be documented in this file.

---

## [Unreleased]

### [0.7.0] — Stage 7: Paid / Official Places Enrichment (Google Places API - Cost-Guarded) — 2026-11-17
#### Added
- Google Places Enrichment Domain Models (`GooglePlacesConfig`, `GooglePlacesEnrichmentResult`).
- Google Places Enrichment Service (`IGooglePlacesEnrichmentService`, `GooglePlacesEnrichmentService`):
  - Hard daily request ceiling (default 50 requests/day, user-configurable up to 500) resetting at midnight UTC to prevent unexpected billing.
  - Mandatory disabled-by-default switch requiring explicit user activation before commercial queries are issued.
  - Windows DPAPI encrypted credential vault storage for the Google Places API key (`GOOGLE_PLACES_API_KEY`).
  - Strict 30-day SQLite caching check preventing duplicate API queries and accidental spend for recently enriched records ($0 cost on cache hits).
  - Robust Google Places Text Search JSON response parser extracting `place_id`, `name`, `formatted_address`, `rating`, `user_ratings_total`, geographic coordinates, and weekly operating hours.
  - Automated business record enrichment, address backfilling, and tag attribution (`Google Verified`, `Rated {X}★`).
- Desktop UI Settings & Cost Controls:
  - Added "Google Places API (Cost-Guarded Enrichment)" section in `SettingsView.axaml` and `SettingsViewModel.cs` with enable/disable toggle, DPAPI-secured API key input, and daily request limit slider.
- Business Database View UI Integration:
  - Added "✨ Enrich with Google Places" action button in `BusinessesView.axaml` Split Inspector.
  - Built Cost Guard confirmation modal dialog displaying live daily quota consumption and 30-day cache awareness before executing lookups.
  - Added Google Places verification scorecard in Inspector pane showing verified badge, star rating, review count, and business hours.
- 7 new unit tests in `GooglePlacesEnrichmentServiceTests.cs` validating disabled guards, missing API key blocks, 30-day cache hits, daily ceiling limits, JSON parsing, and business record persistence (total 67 passing tests, 0 failures).

### [0.6.0] — Stage 6: Website Intelligence & Local Lightweight Auditor — 2026-11-17
#### Added
- Pre-flight SSRF Guard (`ISsrfValidator`, `SsrfValidator`) strictly validating target URLs, disallowing non-HTTP schemes, internal/loopback IPs, and cloud metadata addresses (169.254.169.254).
- Technology Stack and CMS Detector (`TechStackDetector`):
  - Signature detection for WordPress, WooCommerce, Shopify, Wix, Webflow, Squarespace, Laravel, Next.js, React, and Vue.
  - Analytics and tag detectors for Google Analytics (GA4), Google Tag Manager, and Meta Pixel.
  - Server and infrastructure detection (Cloudflare CDN, Nginx, Apache, LiteSpeed, IIS).
- Comprehensive Local Website Audit Engine (`IWebsiteAuditEngine`, `WebsiteAuditEngine`):
  - HTTP to HTTPS redirection probe.
  - TLS/SSL certificate analyzer (certificate issuer, expiration dates, days remaining).
  - Security headers analyzer (Strict-Transport-Security, Content-Security-Policy, X-Frame-Options, X-Content-Type-Options).
  - SEO hygiene inspection (Title tag length, meta description, mobile viewport tag, H1 headings).
  - Crawlability verification (probe for robots.txt and sitemap.xml presence).
  - Lightweight performance profiling (Time to First Byte - TTFB, HTML page size).
  - Domain scoring algorithms (Performance, Security, SEO, and composite Overall Score).
  - Structured issue generation (`WebsiteAuditFinding`) with severity ratings, recommended fixes, and mapped TKS service offerings.
- Audit data repository (`IAuditRepository`, `AuditRepository`) persisting audit history and automatically updating linked business opportunity lead scores in SQLite.
- Desktop UI integration:
  - Added dedicated `AuditsView.axaml` and `AuditsViewModel.cs` with target lead picker, scorecards (Overall, Security, Performance, SEO), tech stack badges, and findings DataGrid.
  - Sidebar navigation entry for "🔬 Website Audits".
  - One-click Markdown audit report exporter.
- 24 new unit tests across `TKS.LeadPilot.Core.Tests` and `TKS.LeadPilot.Data.Tests` (total 60 passing tests, 0 failures).

### [0.5.0] — Stage 5: Free Discovery Pipeline (OpenStreetMap Overpass Engine) — 2026-11-17
#### Added
- OpenStreetMap Overpass discovery domain models (`DiscoveryQuery`, `DiscoveredLead`, `DiscoveryResult`, `BoundingBox`).
- `OverpassQueryBuilder` generating high-performance Overpass QL with preset metropolitan coordinates for Karachi, Lahore, Islamabad, Rawalpindi, Faisalabad, Peshawar, and Multan, plus business sector amenity presets.
- `OverpassResultMapper` converting raw OSM node and way elements into standardized `Business` models with automatic tag normalizations, address concatenation, coordinate capture, and website status detection.
- `OverpassDiscoveryService` (`IDiscoveryService`):
  - Strict compliance with Overpass Acceptable Use Policy (minimum 2,000ms inter-query delay rate limiter, identification User-Agent).
  - Multi-endpoint resilience with automatic failover across 3 global public Overpass mirror servers.
  - Automated deduplication evaluation against local SQLite database on discovery.
  - Batch import of discovered leads with configurable `Skip` or `Merge` strategies.
- Avalonia Desktop Discovery UI:
  - Top action bar "🛰️ Discover Leads (OSM)" button.
  - Discovery modal overlay with city selector, category preset picker, live scan progress, and preview DataGrid with checkbox lead selection.
- 9 new unit tests covering query generation, JSON mapping, rate limiting, and discovery deduplication (total 36 passing tests, 0 failures).

### [0.4.0] — Stage 4: Core Business Database, Search & Deduplication — 2026-11-16
#### Added
- Custom project brand icon and logo assets: `leadpilot-icon.ico` (multi-res 16px to 256px) and `leadpilot-logo.png` featuring supersonic navigation delta, radar sweep discovery rings, and growth beacon.
- Multi-field search and query engine with `BusinessFilterCriteria` supporting free text, city, category, pipeline stage, website presence, score range, starred flag, and custom tags.
- High-precision Deduplication Engine (`IDeduplicationService`, `DeduplicationService`):
  - Normalized website domain extraction (stripping protocol, www, paths, queries, ports).
  - Pakistan and international phone normalization (handling `+92`, `0092`, `03xx`, landlines).
  - Normalized name tokenization and noise-word removal.
  - Multi-tier duplicate matching (`ExactWebsite` 100%, `ExactPhone` 95%, `ExactNameAndCity` 90%, `FuzzyNameAndCity`).
  - Intelligent field-level business merging without data loss.
- High-performance RFC 4180 CSV / Excel Importer and Exporter (`ICsvImporterService`, `CsvImporterService`):
  - Smart column header auto-detection and synonym mapping.
  - Dry-run validation, duplicate detection, and preview table.
  - Configurable deduplication resolution (`Skip`, `Merge`, `Overwrite`).
  - Native Windows file picker integration via Avalonia `StorageProvider`.
  - UTF-8 BOM CSV export for Microsoft Excel compatibility.
- Desktop UI enhancements in `BusinessesView`:
  - Advanced Filter Ribbon with live count pill, city/category/website/stage dropdowns.
  - Split Inspector with contact person subresource management (Add/Delete contacts).
  - Tag management helpers on `Business` entity.
  - Add Business, Add Contact, and CSV Import modal dialogs.
- 21 new unit tests across `TKS.LeadPilot.Core.Tests` and `TKS.LeadPilot.Data.Tests` (total 27 passing tests, 0 failures).

### [0.3.0] — Stage 3: Local Desktop Foundation — 2026-11-16
#### Added
- Visual Studio / .NET 10 Solution `desktop/TKS.LeadPilot.Desktop.sln`.
- Domain models and enums in `TKS.LeadPilot.Core` (`Business`, `BusinessContact`, `PipelineStage`, `WebsiteAudit`, `WebsiteAuditFinding`, `LeadScore`, `TksService`, `SalesTask`, `SalesNote`, `AppSetting`).
- Repository and service abstractions in `TKS.LeadPilot.Core.Interfaces` (`IBusinessRepository`, `IPipelineStageRepository`, `ISecretVault`, `IDatabaseInitializer`).
- SQLite EF Core data layer in `TKS.LeadPilot.Data` (`AppDbContext`, `BusinessRepository`, `PipelineStageRepository`, `DatabaseInitializer`).
- Automatic database migration and seeding for 14 standard pipeline stages, 4 TKS service offerings, and sample enterprise businesses.
- Avalonia UI 12.1.2 desktop application shell in `TKS.LeadPilot.UI` featuring:
  - Collapsible navigation sidebar (Dashboard, Businesses, Audits, Pipeline, Outreach, Settings).
  - Top status bar with offline-first status indicator and Gemini Free Tier quota meter.
  - Interactive Views and ViewModels: `MainViewModel`, `DashboardViewModel`, `BusinessesViewModel` with DataGrid + Split Inspector, `SettingsViewModel`.
  - Windows DPAPI credential vault (`DpapiSecretVault`) for local encrypted storage of API keys.
  - Dependency Injection container via `Microsoft.Extensions.DependencyInjection`.
- Comprehensive Unit Test Suites in `TKS.LeadPilot.Core.Tests` and `TKS.LeadPilot.Data.Tests` (6 tests passing, 0 failures).

### [0.2.0] — Stage 2: UX and Design System — 2026-11-16
#### Added
- Detailed UX Architecture & Design System specification (`docs/UX_AND_DESIGN_SYSTEM.md`).
- Brand design tokens (Primary Navy/Slate, TKS Royal Blue, Emerald Success, Amber Warning, Rose Danger).
- Standardized typography scale (Inter / Segoe UI Variable / Roboto) and 8-point spacing system.
- Desktop UX navigation shell blueprints (Avalonia UI collapsible sidebar + top status bar + split-pane inspector).
- Screen blueprints for Executive Dashboard, Business DataGrid, Website Audit Inspector, CRM Kanban, and Message Studio.
- Mobile UX ergonomics specifications for Flutter Android (bottom navigation, swipe-to-action cards, bottom sheets).
- System-wide UI state specifications for Empty, Loading, Error, Offline, and AI Quota Exhausted states.

### [0.1.0] — Stage 1: Product Specification & Architecture — 2026-11-16
#### Added
- Architecture Decision Record (`docs/DECISIONS/ADR-001-ui-stack-selection.md`) formalizing Avalonia C# Desktop + Flutter Android Mobile.
- Product Requirements Document (`docs/PRODUCT_REQUIREMENTS.md`) detailing all 15 functional domains.
- System Architecture Specification (`docs/ARCHITECTURE.md`) with Mermaid diagrams and sequence flows.
- Database Design & Universal UUIDv4 Schema Specification (`docs/DATABASE_DESIGN.md`).
- AI & Zero-Cost Billing Policy (`docs/AI_AND_COST_POLICY.md`) enforcing $0.00 spending ceiling.
- Discovery Compliance & Data Ethics (`docs/DISCOVERY_SOURCE_COMPLIANCE.md`) covering OSM Overpass and Google Places terms.
- Bidirectional Delta Synchronization Protocol (`docs/SYNC_DESIGN.md`).
- Security Architecture & SSRF Guard (`docs/SECURITY_AND_PRIVACY.md`).
- Comprehensive Test Plan (`docs/TEST_PLAN.md`) with 16 E2E scenarios.
- MVP Scope & Milestones (`docs/MVP_SCOPE.md`) and Risk Register (`docs/RISK_REGISTER.md`).
- Initialized Git repository, configured `.gitignore`, and verified Avalonia CLI templates.

### [0.0.1] — Stage 0: Environment & Repository Inspection — 2026-11-16
#### Added
- Complete environment inspection of Flutter, Dart, PHP, Composer, Python, Node, MySQL, and Visual Studio.
- Identification of prerequisites and resolution plan.
