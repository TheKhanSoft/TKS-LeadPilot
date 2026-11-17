# TKS LeadPilot — Changelog

All notable changes to the TKS LeadPilot project will be documented in this file.

---

## [Unreleased]

### [0.14.0] — Stage 14: Automated Multi-Platform Test Verification & Security Hardening — 2026-11-17
#### Added
- Comprehensive Security Hardening & Fuzzing Test Suite (`SecurityHardeningTests.cs`):
  - SSRF Security Validator: Enforces strict rejection across all loopback aliases (`127.0.0.1`, `127.1`, `127.0.1.1`, `localhost`, `sub.localhost`, `app.local`, `internal.service.lan`, `0.0.0.0`, `255.255.255.255`).
  - RFC 1918 Private Subnet Fuzzing: Exhaustive checks against `10.0.0.0/8`, `172.16.0.0/12`, and `192.168.0.0/16` addresses.
  - Cloud Metadata Shield: Verified blocking of AWS/GCP/Azure link-local metadata endpoints (`169.254.169.254`, `169.254.1.1`).
  - Protocol Boundary Enforcement: Rigorous rejection of non-HTTP schemes (`file:///`, `ftp://`, `gopher://`, `dict://`, `ldap://`).
  - Malformed Payload & Memory Safety: Verified `TechStackDetector` safely handles malicious/excessive HTML payloads (50KB+) without crash or memory leak.
- High-Availability OpenStreetMap Discovery Engine Resilience:
  - Verified Local Offline Cache (`OsmOfflineCache.cs`): Real, verified Pakistani business nodes for Islamabad, Lahore, Karachi, Rawalpindi, Faisalabad, Peshawar, and Multan covering Healthcare, Dining, Hospitality, Retail, and Education sectors.
  - Fail-Safe Circuit-Breaker: Live Overpass HTTP requests are capped at 8 seconds per mirror to prevent UI freezes. If public mirrors time out, rate limit, or throw 502/504 errors, the discovery engine automatically falls back to verified local OSM data seamlessly.
  - Provider Transparency: Discovery results now explicitly display whether records originated from live Overpass mirrors or the verified local fallback cache.
- Global Geographic Hierarchy & Proximity Services (`GeoLocationService.cs`):
  - Structured location taxonomy supporting Pakistan and international economic hubs (US, UK, UAE, Canada, Saudi Arabia, Germany, Australia).
  - Haversine proximity bounding-box calculations for geo-targeted local discovery.
  - Automated industry niche suggestions mapped to regional commercial hubs.
#### Fixed
- Fixed OpenStreetMap Discovery Engine hanging indefinitely when querying public Overpass mirrors (`overpass-api.de`, `lz4.overpass-api.de`) during upstream outages or rate limits.
- Fixed process file locking on `TKS.LeadPilot.UI.exe` during compilation.
- Fixed EF Core SQLite state filtering query in `BusinessRepositoryFilterTests.cs`.
- Rebuilt and synchronized both Debug binary (`desktop/src/TKS.LeadPilot.UI/bin/Debug/net10.0/TKS.LeadPilot.UI.exe`) and Release distribution (`desktop/dist/TKS.LeadPilot.UI.exe`).
#### Verified
- Automated test coverage expanded to **156 tests passing** (143 .NET Desktop, 7 Laravel API / 33 assertions, 6 Flutter Mobile).
- Zero test failures, zero regressions across all desktop, mobile, and backend suites.

### [0.13.0] — Stage 13: End-to-End Integration, Data Seeding & Cross-Platform Verification — 2026-11-17
#### Added
- Comprehensive Commercial Pakistani Business Dataset:
  - Pre-seeded 11 high-value Pakistani commercial business records across key metropolitan economic hubs (Lahore, Karachi, Islamabad, Rawalpindi, Faisalabad, Peshawar, Multan).
  - Pre-populated realistic technical website audits (`WebsiteAudits`) with concrete diagnostics (missing SSL/TLS, excessive TTFB latency, missing XML sitemaps, non-responsive desktop-only layouts).
  - Pre-populated explainable audit findings (`WebsiteAuditFindings`) with severity indicators and remediation recommendations.
  - Pre-calculated transparent lead scores (`LeadScores` & `LeadScoreComponents`) explainable down to exact point deductions.
  - Pre-configured actionable sales tasks (`SalesTasks`) with due dates and priority rankings (Urgent, High, Medium).
  - Pre-seeded contextual sales notes (`SalesNotes`) documenting phone interactions and client requirements.
  - Pre-drafted personalized cold outreach messages (`OutreachMessages`) in Message Studio pending approval queue.
- Self-Healing SQLite Schema Migration & Dynamic Synchronizer:
  - Implemented automatic schema synchronization in `DatabaseInitializer.cs` that verifies and creates missing tables (`CREATE TABLE IF NOT EXISTS`) and adds missing columns (`EnsureColumnAsync`) to prevent SQLite schema mismatch errors across software versions.
  - Top-level unhandled exception logger in `Program.cs` capturing fatal startup crashes to `%LOCALAPPDATA%\TKS.LeadPilot\startup_error.log` for instant diagnostic visibility.
- End-to-End Integration Test Suite (`EndToEndIntegrationTests.cs`):
  - Scenario 1 (Offline Manual Entry): Validates local-first SQLite persistence and data fidelity across independent DbContext lifecycles.
  - Scenario 2 (Full Lead Lifecycle): Tests lead discovery -> technical audit inspection -> explainable lead scoring -> TKS service matching -> personalized outreach drafting -> CRM task assignment.
  - Scenario 3 (SSRF Security Guard): Rigorously validates rejection of dangerous/private IPs (`127.0.0.1`, `localhost`, `10.0.0.1`, `172.16.0.1`, `192.168.1.1`, `169.254.169.254`, `0.0.0.0`, `file://`, `ftp://`).
  - Scenario 4 (Sync Configuration & Delta Tracking): Validates device UUID generation and tracking of local mutations pending server push.
- Cross-Platform Verification Script:
  - Created automated verification script [`scripts/verify_cross_platform_sync.py`](file:///D:/Projects/TKS-LeadPilot/scripts/verify_cross_platform_sync.py) validating SQLite database counts, binary freshness, and schema parity across Desktop, Backend, and Mobile companion.
#### Fixed
- Fixed startup crash where existing SQLite database files from earlier stages lacked newly added entity tables (`OutreachTemplates`, `AppSettings`, `SalesTasks`, `SalesNotes`, `WebsiteAudits`, `LeadScores`).
- Fixed Foreign Key constraint failure during commercial dataset seeding by resolving actual entity primary keys dynamically.
- Fixed EF Core entity tracking conflict in `BusinessRepository.UpdateAsync` and `UpdateBatchAsync` by safely checking change tracker state.
- Fixed unhandled `ArgumentException` in `SsrfValidator` when evaluating unspecified address `0.0.0.0`.
- Synchronized and verified both Debug executable (`desktop/src/TKS.LeadPilot.UI/bin/Debug/net10.0/TKS.LeadPilot.UI.exe`) and Release distribution (`desktop/dist/TKS.LeadPilot.UI.exe`).
#### Verified
- Automated test count expanded to 112 tests passing across all components (99 .NET, 7 Laravel with 33 assertions, 6 Flutter).

### [0.12.0] — Stage 12: Mobile Companion App (Flutter Android) & Desktop Launcher Polish — 2026-11-17
#### Added
- Mobile Companion Application (`mobile/` - Flutter 3.47+ / Dart 3.13+):
  - Created native Flutter Android mobile companion under package `com.thekhansoft.leadpilot`.
  - Luxury Material 3 Obsidian dark theme matching desktop aesthetic (`AppTheme`: `#080C14` background, `#0E1726` surface, `#2563EB` primary, `#06B6D4` accent).
  - Offline-first local storage service (`StorageService`) with persistent JSON/SharedPreferences caching for leads, contacts, tasks, and notes.
  - Universal UUIDv4 identity generator for all mobile-created leads and tasks without collisions.
  - Initial out-of-the-box sample leads and contacts for instant offline evaluation.
- Mobile Views & Field Workflows:
  - `LeadsScreen`: Real-time multi-field search, horizontal city filter chips (Lahore, Karachi, Islamabad, Peshawar), opportunity score badges, pull-to-refresh sync, and quick lead creation modal.
  - `LeadDetailScreen`: Full business profile, technical diagnostics overview, contact cards, activity notes logger, and 1-tap quick action bar:
    - 📞 One-tap Phone Dialing (`tel:{phone}`).
    - 💬 One-tap WhatsApp Cold Outreach (`https://wa.me/{clean_phone}`).
    - ✉️ One-tap Email Client Launch (`mailto:{email}`).
    - 🌐 In-app / external website browser launch.
  - `TasksScreen`: Sales follow-up to-do checklist with instant task toggle, due dates, and quick task creation.
  - `SyncScreen`: Central API endpoint URL configuration, device UUID display, live server health check indicator, and manual bidirectional sync trigger.
- Mobile Delta Sync Service (`MobileSyncService`):
  - Bi-directional sync with central Laravel API (`/api/v1/sync/push`, `/api/v1/sync/pull`).
  - Pull-to-refresh delta synchronization with cursor management.
- Desktop App Launcher & Distribution:
  - Created 1-click Windows launcher script [`run-desktop.bat`](file:///D:/Projects/TKS-LeadPilot/run-desktop.bat) in the project root.
  - Published standalone Release executable in [`desktop/dist/TKS.LeadPilot.UI.exe`](file:///D:/Projects/TKS-LeadPilot/desktop/dist/TKS.LeadPilot.UI.exe).
  - Updated `README.md` with comprehensive Quick Launch instructions for Desktop, Backend, and Mobile.
- Automated Tests:
  - 6 Flutter unit and widget tests passing (100% pass rate) in `widget_test.dart` and `storage_sync_test.dart`.
  - Static analysis passing with 0 warnings (`flutter analyze`).

### [0.11.0] — Stage 11: Central Headless Laravel API & Delta Synchronization Protocol — 2026-11-17
#### Added
- Central Headless Laravel 13 API Backend (`backend/`):
  - Strictly headless REST API architecture (zero Blade/Livewire/React web dashboards).
  - Laravel Sanctum authentication with Bearer tokens and per-device tokens.
  - Complete multi-device registration system (`Device` model tracking `device_id`, `device_name`, `platform`, `last_synced_at`).
  - System health monitor endpoint (`GET /api/v1/health`) returning server status, API version, and database connectivity.
- Unified Bidirectional Delta Synchronization Protocol (Module 14):
  - Universal UUIDv4 primary keys preserved across clients and server.
  - Monotonic revision versioning (`sync_version`) for conflict detection.
  - Soft-delete tombstone propagation across SQLite and central backend (`deleted_at` + `is_deleted` payload flag).
  - Batch idempotent push endpoint (`POST /api/v1/sync/push` and `/api/v1/dev-sync/push`):
    - Upserts client changes across 8 synced domains (Businesses, Contacts, Audits, Stages, Tasks, Notes, Outreach Templates, Outreach Messages).
    - Last-Write-Wins (LWW) conflict engine with automatic snapshot logging to `sync_conflicts` table whenever a stale client attempts to overwrite a higher server version.
  - Timestamp-based delta pull endpoint (`GET /api/v1/sync/pull` and `/api/v1/dev-sync/pull`):
    - Efficient cursor-based queries filtering by `since` ISO 8601 UTC timestamp.
    - Synchronizes newly added, updated, and soft-deleted entities.
  - Summary sync metrics endpoint (`GET /api/v1/sync/status`).
- Desktop Client Sync Integration (`TKS.LeadPilot.Data`):
  - Implemented `ISyncService` and `SyncService`:
    - Secure storage of API token via DPAPI secret vault (`SYNC_BEARER_TOKEN`).
    - Local sync cursor management in `AppSettings`.
    - Bidirectional synchronization (`PushLocalChangesAsync`, `PullRemoteChangesAsync`, `SynchronizeAsync`).
  - Added Central Laravel Synchronization section to `SettingsView.axaml` and `SettingsViewModel.cs` with endpoint URL configuration, Sanctum token input, live API status badge, and manual "Sync Now" trigger.
- Verification & Test Suites:
  - 7 automated feature tests in Laravel (`SyncApiTest.php` passing all 33 assertions: Health, Auth, Idempotent Push, Delta Pull with Tombstones, Server-Wins Conflict Detection).
  - 5 new automated unit tests in .NET (`SyncServiceTests.cs` verifying default configs, DPAPI vault token saving, online status checks, push payloads, and remote delta pull integration).
  - Full desktop test suite passing at **95/95 unit tests** (0 failures).

### [0.10.0] — Stage 10: Message Studio & Multi-Channel Outreach Engine + Premium UI/UX Polish — 2026-11-17
#### Added
- Outreach Domain Entities & Enums:
  - `OutreachMessage` entity (`Id`, `BusinessId`, `ContactId`, `TemplateId`, `Channel`, `RecipientAddress`, `Subject`, `RenderedBody`, `ApprovalStatus`, `DispatchedAtUtc`, `ExternalMessageId`, `ErrorMessage`).
  - `OutreachTemplate` entity (`Id`, `Name`, `Channel`, `Category`, `SubjectTemplate`, `BodyTemplate`, `CreatedAtUtc`, `UpdatedAtUtc`).
  - `OutreachChannel` (`Email`, `WhatsApp`) and `OutreachApprovalStatus` (`Draft`, `PendingApproval`, `Approved`, `Rejected`, `Dispatched`, `Failed`).
- Multi-Channel Outreach Service (`IOutreachService`, `OutreachService`):
  - Robust token substitution supporting 11 dynamic context variables: `{{business_name}}`, `{{contact_name}}`, `{{city}}`, `{{category}}`, `{{website_url}}`, `{{lead_score}}`, `{{suggested_service}}`, `{{top_audit_issue}}`, `{{ttfb_ms}}`, `{{ssl_status}}`, and `{{sender_name}}`.
  - Automatic fallback value resolution for missing or uninspected metrics without corrupting copy.
  - Strict Compliance & Do-Not-Contact guard (`InvalidOperationException` thrown if business lead is marked `DoNotContact` or tagged `Suppressed`).
  - Native sanitized OS dispatch handlers:
    - RFC 6068 URL-encoded `BuildMailtoUri` for default email client integration.
    - URL-encoded `BuildWhatsAppUri` (`https://wa.me/{clean_phone}?text={encoded}`) for direct WhatsApp messaging.
    - Direct zero-cost OS launching via `Process.Start` with protocol sanitization.
  - Full Draft Approval Queue lifecycle (`CreateDraftAsync`, `ApproveDraftAsync`, `RejectDraftAsync`, `DispatchApprovedAsync`, `GetOutboxQueueAsync`).
- Default Outbox & Discovery Templates Seeder (`DatabaseInitializer`):
  - Seeded 4 ready-to-use business outreach templates covering Technical Audits, Speed Optimization, Quick Diagnostics, and Missing Website offers across Email and WhatsApp channels.
- Modern & Premium UI/UX Polish:
  - Created dedicated `MessageStudioView.axaml` and `MessageStudioViewModel.cs` with dual-column layout:
    - Left pane: Recipient lead picker, contact selector, template catalog, multi-channel toggle pills, and dynamic token quick-insert badges.
    - Right pane: Live WYSIWYG rendered message preview, Human-in-the-Loop compliance approval notice, and direct dispatch buttons ("Approve & Launch Outlook/Thunderbird" / "Approve & Open WhatsApp Web").
    - Outbox Approval Queue tab with status pills and instant review workflow.
  - Elevated app-wide styling in `App.axaml`:
    - Premium button transitions and hover elevation.
    - Modern dark theme DataGrid styling with obsidian headers (`#080D16`), deep rows (`#0B111D`), and active row highlighting (`#1C2E4A`).
    - Rounded input controls and subtle slate border strokes.
  - Enhanced `DashboardView.axaml` with 2px accent gradient top borders on metric cards and quick action chips.
  - Enhanced `MainWindow.axaml` navigation bar with active route indicators, glowing brand badge, and zero-cost status indicator.
- 5 new unit tests in `OutreachServiceTests.cs` validating token substitution, recipient resolution, Do-Not-Contact suppression guards, approval transitions, and URL encoding for Mailto and WhatsApp links (total 90 passing tests, 0 failures).

### [0.9.0] — Stage 9: Transparent Lead Scoring & CRM Sales Pipeline — 2026-11-17
#### Added
- Transparent & Explainable Lead Scoring Engine (`ILeadScoringEngine`, `LeadScoringEngine`):
  - Deterministic 0–100 opportunity score calculated across 5 clear, explainable components:
    - *Website Presence & Condition* (Max 35 pts): +35 pts for absence of website, +25 pts for parked/inactive domain, +20 pts for critical site health issues.
    - *Performance & Server Latency* (Max 25 pts): +25 pts for TTFB > 1,500ms, +15 pts for TTFB > 800ms.
    - *Security & Trust Hygiene* (Max 15 pts): +15 pts for missing SSL or HTTP-to-HTTPS redirect failure, +8 pts for missing security headers.
    - *Search Visibility & SEO Hygiene* (Max 10 pts): +10 pts for missing crawler files (robots/sitemap) and meta tags.
    - *Google Places Commercial Authority* (Max 15 pts): +12 pts for unverified Google Maps presence, +8 pts for sub-4-star rating or low review volume.
  - Granular `LeadScoreComponent` entities detailing points awarded, max points, and human-readable explanation per component.
  - Manual score override capability with mandatory audit logging (`ApplyManualOverrideAsync`).
  - Uninspected metrics are never penalized.
- Evidence-Based TKS Service Matcher (`ITksServiceMatcher`, `TksServiceMatcher`):
  - Rule-based matching engine pairing business diagnostic signals to the 4 core TheKhanSoft service offerings.
  - Generates tailored outreach pitches with token substitution (`{{business_name}}`, `{{website_url}}`, `{{city}}`, `{{ttfb_ms}}`).
- CRM Sales Pipeline Service (`ICrmService`, `CrmService`):
  - Full board status queries across all 14 configured stages.
  - Stage transitions with automatic system audit notes.
  - Full sales task lifecycle management (create, complete, delete).
  - Activity notes and chronological timeline management.
- Desktop UI CRM & Pipeline View:
  - Created dedicated `PipelineView.axaml` and `PipelineViewModel.cs`.
  - Stage filtering ribbon with quick transition buttons.
  - Explainable scoring breakdown card showing points and explanations per component.
  - Evidence-based service recommendations card with match confidence percentages.
  - Interactive tasks to-do list with completion checkboxes.
  - Activity notes and timeline logger.
  - Manual Score Override modal dialog.
  - Linked navigation in sidebar via `MainViewModel` and `MainWindow.axaml`.
- 11 new unit tests across `LeadScoringEngineTests.cs`, `TksServiceMatcherTests.cs`, and `CrmServiceTests.cs` (total 85 passing tests, 0 failures).

### [0.8.0] — Stage 8: AI-Assisted Website Audit Report Synthesizer & Audit Comparison — 2026-11-17
#### Added
- Gemini AI Proposal Synthesis Domain Models (`AiTone`, `GeminiConfig`, `AuditProposalSynthesisRequest`, `AuditProposalSynthesisResult`, `AuditComparisonResult`).
- Zero-Cost Gemini Free Tier Proposal Synthesizer (`IAiProposalSynthesizer`, `AiProposalSynthesizer`):
  - Strict 12 RPM local sliding-window rate limit (under Google Free Tier's 15 RPM cap).
  - Hard 1,200 RPD daily quota ceiling (under Google Free Tier's 1,500 RPD cap) resetting automatically at 00:00 UTC.
  - Zero-cost architecture ($0.00 ceiling, emergency kill-switch, Windows DPAPI secret vault storage).
  - Automatic pre-flight PII redaction scrubbing customer phone numbers and emails before prompt delivery.
  - Multi-tone proposal generation (`Consultative`, `RoiFocused`, `Technical`).
  - Automatic mapping of audit defects to targeted TKS Service Packages (Web Modernization, Zero-Trust Security, Local SEO Dominance).
  - 100% Offline-First Deterministic Proposal Generator (`SynthesizeDeterministicProposal`) providing instant client proposals with zero network dependency.
- Audit History & Re-Audit Comparison Engine (`IAuditComparisonService`, `AuditComparisonService`):
  - Side-by-side delta analysis between baseline and re-audits (Overall, Performance, Security, and SEO score trends).
  - Tri-state finding classification (`Resolved`, `New`, `Persistent`).
  - Automated executive comparison markdown table generation.
- Desktop UI Enhancements:
  - Added "✨ AI Proposal Studio" modal in `AuditsView.axaml` and `AuditsViewModel.cs` with tone selector, real-time Gemini synthesis, offline template generation, markdown editor, and one-click export.
  - Added "⚖️ Compare Audits" modal tracking progress across historical scans with visual delta chips.
  - Added Gemini AI model selection, safety toggle, and daily request usage meters in `SettingsView.axaml` and `SettingsViewModel.cs`.
- 7 new unit tests across `AiProposalSynthesizerTests.cs` and `AuditComparisonServiceTests.cs` (total 74 passing tests, 0 failures).

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
