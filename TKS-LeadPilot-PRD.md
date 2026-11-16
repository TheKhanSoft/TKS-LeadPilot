# TKS LeadPilot --- Complete Master Development Prompt for Antigravity

**Project:** TKS LeadPilot\
**Owner:** TheKhanSoft (TKS)\
**Development approach:** Approval-driven, staged implementation\
**Primary platforms:** Windows Desktop + Android\
**Architecture:** Local-first hybrid, with optional central
synchronization\
**User interface:** Native Flutter applications only --- no web
dashboard\
**AI policy:** Gemini Developer API free tier only; zero unexpected
billing

------------------------------------------------------------------------

# 1. Your role and mission

Act as a senior software architect, product engineer, security engineer,
database designer, Flutter developer, Laravel developer, Python
automation engineer, QA engineer, and technical project manager.

Your task is to design and develop **TKS LeadPilot**, a professional
business-discovery, website-intelligence, website-auditing, AI-assisted
lead-generation, CRM and outreach platform for TheKhanSoft.

This is an internal business tool initially. Its purpose is to help
TheKhanSoft discover potential clients, assess their online presence,
identify website and digital-marketing opportunities, score leads,
prepare professional proposals/messages, manage sales activity, and
follow up with prospects.

Build it as a reliable, maintainable, modular product---not as a quick
prototype.

You must work incrementally. Do not generate the whole project in one
uncontrolled operation. First inspect the development environment and
repository, then prepare the architecture and implementation plan.
Divide implementation into clearly defined stages. At the end of each
stage, present completed work, test results, known limitations and the
next-stage plan. **Stop and wait for the user's explicit approval before
proceeding to the next stage.**

Do not claim that a feature is complete unless it is implemented, tested
and verified.

------------------------------------------------------------------------

# 2. Product vision

TKS LeadPilot should help TheKhanSoft perform the following workflow:

1.  Discover businesses using permitted public sources, search
    providers, directories, APIs and user-imported data.
2.  Collect and normalize business information while preserving source
    provenance.
3.  Identify whether a business has a website, social presence or other
    digital footprint.
4.  Inspect public websites safely and gather technical, performance,
    accessibility, SEO and content signals.
5.  Run website audits and store evidence-backed findings.
6.  Use Gemini to interpret audit results and public business
    information, identify opportunities and prepare recommendations.
7.  Calculate transparent lead scores using configurable rules.
8.  Organize prospects into lists, pipelines, campaigns and CRM records.
9.  Generate personalized outreach drafts and proposals.
10. Require human approval by default before messages are sent.
11. Track contact history, responses, follow-ups, opt-outs and sales
    outcomes.
12. Work locally when the central server is unavailable, then
    synchronize when connectivity returns.
13. Provide an Android companion app for mobile access and field/sales
    work.

The system must be designed so that optional providers and services can
be replaced without rewriting the whole application.

------------------------------------------------------------------------

# 3. Important architectural decision: no web dashboard

The product must **not require a browser-based user interface**.

The primary user interfaces are:

-   Flutter Windows desktop application.
-   Flutter Android mobile application.

Do not build a Laravel Blade, Livewire, React, Vue or other web
dashboard as the primary product interface.

A Laravel API server is permitted and recommended for optional
synchronization, shared data, user authentication and centralized
administration. An API server is not a web dashboard.

The desktop application must remain useful if the central API server is
offline or not configured. Local functions must not depend on the server
unnecessarily.

The Android application should support useful offline functions, but it
does not need to reproduce every desktop-intensive operation.

------------------------------------------------------------------------

# 4. Recommended technology stack

Use stable, mutually compatible releases available at implementation
time. Before installing packages, verify compatibility and document the
selected versions. Do not blindly upgrade dependencies or use deprecated
APIs.

## 4.1 Desktop and mobile clients

-   Flutter and Dart.
-   Windows desktop as the first target.
-   Android as the second target.
-   Shared Flutter codebase, with platform-specific adapters where
    necessary.
-   Material 3 design system, customized with professional TKS branding.
-   Riverpod or another well-maintained state-management solution;
    choose one and use it consistently.
-   GoRouter or an equivalent typed/navigation solution.
-   SQLite for local persistence.
-   A repository/service architecture; UI widgets must not contain
    business logic.
-   Secure storage for tokens and secrets.
-   Background task support appropriate to each platform.
-   Structured logging and crash/error reporting with privacy controls.

Do not use Flutter Web as a substitute for the desktop application. Do
not make a browser-based interface the main product.

## 4.2 Central backend and synchronization service

-   Laravel 13, or the latest compatible stable Laravel release if
    Laravel 13 is unavailable in the actual environment.
-   PHP version compatible with the selected Laravel release.
-   MySQL 8 or a compatible supported MySQL release.
-   Laravel Sanctum or another suitable API authentication mechanism.
-   Spatie Laravel Permission for roles and permissions if multi-user
    central access is enabled.
-   Laravel Queues and Scheduler for background jobs.
-   Redis is optional; the system must be able to start with a simpler
    queue/cache driver during development.
-   Versioned REST API with documented request/response contracts.
-   API rate limiting, validation, authorization and audit logging.

The backend is an optional central service. It must not be a mandatory
dependency for local desktop operation.

## 4.3 Website auditing and automation

-   Python for the audit/crawling engine.
-   Playwright for browser-based inspection where appropriate.
-   Lighthouse for performance, SEO, best-practice and accessibility
    signals.
-   axe-core or an equivalent accessibility testing engine where
    compatible.
-   Custom crawler for controlled page discovery and extraction.
-   PageSpeed Insights API as an optional supplementary provider.
-   A local desktop sidecar/service for audit jobs when practical.
-   Optional remote audit worker later, without making it mandatory.

Do not run unrestricted browser crawling inside the Flutter UI process.
Use a separate process/service and communicate through a documented
local interface.

## 4.4 AI provider

Primary provider:

-   Google Gemini Developer API through Google AI Studio.
-   Use only a model that is actually available on the free API tier at
    implementation time.
-   Never assume the user's Google AI Pro consumer subscription includes
    API credits.
-   The Google AI Pro subscription and Gemini Developer API billing are
    separate.
-   Never enable paid API billing automatically.

AI must be accessed through a provider abstraction. The initial provider
is Gemini Free Tier. The architecture may allow future providers, but do
not implement or activate paid providers without explicit approval.

## 4.5 Optional future technologies

Do not add these without a concrete requirement:

-   Local LLM runtime.
-   Docker dependency for ordinary desktop usage.
-   Kubernetes or microservices.
-   Elasticsearch/OpenSearch.
-   Paid enrichment services.
-   Paid email/SMS/WhatsApp gateways.
-   Cloud-only database or storage.
-   A web-based administration dashboard.

Keep the first release lean and maintainable.

------------------------------------------------------------------------

# 5. Target architecture

Implement a **local-first hybrid architecture**.

## 5.1 Logical architecture

``` text
                 TKS LeadPilot
                       |
          +------------+------------+
          |                         |
   Flutter Windows             Flutter Android
     Desktop App                 Mobile App
          |                         |
   Local SQLite DB              Local SQLite DB
          |                         |
   Local Sync Engine             Sync Engine
          |                         |
   Local Python Audit              HTTPS API
       Service                       |
          |                   Laravel Central API
   Browser/Audit Tools                |
          |                       MySQL
          |                         |
          +------ Optional Sync ----+
```

External integrations are accessed through provider adapters:

``` text
Business Discovery
  |-- OpenStreetMap / permitted OSM data providers
  |-- Google Places API (optional, quota controlled)
  |-- Search engine APIs/providers (only when permitted)
  |-- Permitted business directories
  |-- CSV / Excel import
  |-- Manual entry

Website Intelligence
  |-- Python crawler
  |-- Playwright
  |-- Lighthouse
  |-- PageSpeed Insights API (optional)

AI
  |-- Gemini Developer API Free Tier
  |-- Deterministic local rules and scoring
```

## 5.2 Local-first behavior

The desktop app must:

-   Start and operate without a central server.
-   Store locally created records in SQLite.
-   Perform local filtering, searching, tagging and CRM operations.
-   Run local website audits through the Python sidecar.
-   Keep queued changes for later synchronization.
-   Clearly indicate local-only, pending-sync, synchronized and conflict
    states.
-   Never silently discard unsynchronized records.

Some functions necessarily require internet access: - External business
discovery APIs. - Fetching public websites. - Gemini API analysis. -
Sending email or WhatsApp messages. - Synchronizing with the central
API.

The application must explain when a feature requires connectivity.

## 5.3 Android behavior

The Android app should support:

-   Login to the central API when configured.
-   Offline viewing and editing of synchronized leads.
-   Local notes, tasks, reminders and follow-up records.
-   Contact and lead status updates.
-   Search, filters and saved lists.
-   Draft viewing and approval workflows.
-   Synchronization when connectivity returns.
-   Notifications where supported and permitted.

Do not attempt to run heavy Lighthouse or large-scale browser audits on
Android. Android can request an audit from a connected desktop or remote
worker when available.

------------------------------------------------------------------------

# 6. Main functional modules

Build the application as modular domains with clear boundaries.

## Module 1 --- Authentication, users and security

Desktop local mode: - Support a secure local owner/admin profile. -
Protect local access using an appropriate authentication mechanism. -
Store credentials securely; never store plain-text passwords. - Provide
a recovery strategy that does not expose secrets.

Central mode: - User accounts and authentication. - Roles and
permissions. - Device registration and session management. - Token
revocation. - Login history and audit events.

Suggested roles: - Owner / Super Admin - Administrator - Researcher -
Auditor - Sales Executive - Read-only user

Permissions must be granular and configurable. Use policies/gates on the
Laravel API. Do not rely on hiding buttons in Flutter as an
authorization mechanism.

## Module 2 --- Dashboard

Provide an at-a-glance overview:

-   Total businesses discovered.
-   New leads.
-   Leads by status.
-   Leads by source.
-   Businesses with websites.
-   Businesses without websites.
-   Audits completed and pending.
-   High-priority opportunities.
-   Average lead score.
-   Outreach drafts awaiting approval.
-   Follow-ups due and overdue.
-   Recent activities.
-   API quota and usage indicators.
-   Synchronization status.

Dashboard figures must be calculated from actual stored data. Do not
display fabricated sample statistics in production.

## Module 3 --- Business discovery

Allow the user to create discovery jobs using filters such as:

-   Country.
-   State/province/region.
-   City.
-   Neighborhood or area.
-   Business category.
-   Business keywords.
-   Business name.
-   Website availability.
-   Minimum/maximum rating where legally and technically available.
-   Source provider.
-   Result limit.
-   Discovery date.
-   Exclusion keywords.
-   Duplicate-handling policy.

Example categories: - Restaurants and cafes. - Hotels and guest
houses. - Schools and colleges. - Universities and training
institutes. - Clinics and hospitals. - Real estate agencies. -
Construction companies. - Retail shops. - Manufacturers. - Professional
services. - Travel agencies. - Local service providers. - Other
configurable business categories.

Discovery sources:

### A. OpenStreetMap

-   Use permitted OSM data sources and APIs.
-   Preserve required attribution.
-   Respect ODbL licensing and share-alike obligations where applicable.
-   Do not treat public Nominatim as a bulk data extraction service.
-   Follow the public Nominatim usage policy, including its request-rate
    limits and restrictions.
-   Prefer permitted extracts or an appropriate provider for large-scale
    discovery.
-   Cache responsibly.

### B. Google Places API

-   Optional provider, disabled until the user configures an API key.
-   Use official Google Maps Platform APIs only.
-   Use field masks to request only necessary fields.
-   Track SKU, request count, free quota, estimated cost and provider
    errors.
-   Respect Google Maps Platform storage, attribution, display and
    retention restrictions.
-   Store Place IDs where permitted for later retrieval.
-   Do not copy restricted Google fields into permanent unrestricted CRM
    fields.
-   Never assume every field is free.
-   Provide a provider-level usage limit and emergency disable switch.

### C. Other directories and search providers

-   Implement adapters only for sources whose terms permit the intended
    access and use.
-   Do not bypass login restrictions, CAPTCHA, access controls or
    anti-bot measures.
-   Do not scrape sites merely because their pages are publicly
    viewable.
-   Respect robots directives, terms of service, rate limits and
    applicable laws.
-   Do not implement stealth scraping or proxy rotation to evade
    restrictions.

### D. CSV / Excel import

Support: - CSV import. - XLSX import. - Column mapping. - Preview before
import. - Validation and error reporting. - Duplicate detection. -
Import history. - Export of user-owned records. - Field-level
source/provenance tracking.

### E. Manual entry

Allow users to add businesses and contacts manually.

Each discovery job must store: - Job name. - Search criteria. - Source
providers. - Start/end timestamps. - Status. - Results count. - Error
count. - Provider usage. - Execution logs.

## Module 4 --- Business database and deduplication

A business record may have multiple sources, contacts, websites and
audit records.

Suggested business fields: - Business ID. - Name. - Legal/display name
if known. - Category and subcategory. - Description. - Country, region,
city and address. - Latitude/longitude where permitted. - Phone. -
Public business email. - Website URL. - Social profile URLs. - Public
business rating and review metadata only where permitted. - Source
provenance. - First discovered date. - Last verified date. - Lead
status. - Lead score. - Tags. - Internal notes.

Deduplication should use a configurable combination of: - Normalized
business name. - Domain. - Phone number. - Address. - Provider-specific
stable ID where permitted.

Do not automatically merge uncertain matches. Present a comparison and
allow the user to approve or reject a merge.

Preserve original source records and merge history.

## Module 5 --- Website intelligence

For each business, determine:

-   Website exists / no website / uncertain.
-   Canonical URL.
-   Redirect destination.
-   HTTPS status.
-   Domain and page title.
-   Meta description.
-   Main language.
-   Detected CMS or technology, where reasonably identifiable.
-   Public contact page.
-   Public email and phone displayed on the site.
-   Social links.
-   Sitemap availability.
-   Robots.txt availability.
-   Page count inspected.
-   Last checked timestamp.

Do not infer that a website is absent merely because one request failed.
Distinguish DNS failure, timeout, blocked request, TLS error, HTTP
error, no website and uncertain result.

## Module 6 --- Advanced website audit

Create a configurable audit engine with safe, evidence-based checks.

### Performance

-   Lighthouse performance score.
-   Core Web Vitals signals where available.
-   Page load indicators.
-   Image optimization opportunities.
-   Render-blocking resources.
-   JavaScript/CSS size indicators.
-   Caching and compression indicators.
-   Mobile performance.

### SEO

-   Page title.
-   Meta description.
-   Heading structure.
-   Canonical URL.
-   Indexability signals.
-   Sitemap.
-   Robots directives.
-   Image alt text.
-   Broken internal links.
-   Structured data presence and validity where practical.
-   Open Graph/social metadata.

### Accessibility

-   axe-core findings where available.
-   Missing accessible names.
-   Form label issues.
-   Contrast issues.
-   Heading and landmark issues.
-   Keyboard-related checks where testable.

### Security and technical hygiene

-   HTTPS/TLS status.
-   Mixed-content indicators.
-   Security headers.
-   Cookie flags where observable.
-   Exposed directory listing indicators.
-   Publicly exposed sensitive files only through safe, non-invasive
    checks.
-   Outdated software signals only when reliably identifiable.

Do not perform penetration testing, exploitation, password attacks,
vulnerability exploitation, destructive tests or intrusive scans. This
is a public website quality audit, not an offensive security scanner.

### Content and conversion

-   Contact information discoverability.
-   Clear calls to action.
-   Mobile layout indicators.
-   Navigation usability.
-   Service/product information.
-   Trust signals.
-   Broken forms or links where safely testable.
-   Business-specific opportunities.

### Audit output

Each finding must contain: - Finding title. - Category. - Severity. -
Evidence. - Affected URL. - Timestamp. - Detection method. -
Confidence. - Recommended fix. - Estimated effort. - Potential business
impact. - Whether AI-generated interpretation was used.

Keep raw audit measurements separate from AI interpretation. AI must
never fabricate metrics or claim that an untested issue was verified.

Provide: - Audit summary. - Category scores. - Prioritized findings. -
Evidence details. - Recommended improvements. - Re-audit comparison. -
Exportable report (PDF can be implemented in a later stage).

## Module 7 --- AI analysis using Gemini Free Tier

Build a provider abstraction with: - Provider interface. - Gemini
adapter. - Model configuration. - Prompt templates with versioning. -
Request/response logs with safe redaction. - Token/usage tracking when
available. - Retry and rate-limit handling. - Caching. - Quota
monitoring. - User-visible AI status.

AI use cases: 1. Summarize a business using permitted public
information. 2. Summarize website audit results. 3. Explain technical
issues in non-technical language. 4. Identify likely digital-service
opportunities. 5. Suggest suitable TKS services. 6. Generate
personalized outreach drafts. 7. Generate proposal outlines. 8.
Categorize leads. 9. Explain why a lead received its score. 10. Suggest
next sales actions.

### Zero-extra-billing policy --- mandatory

This is a critical product requirement.

-   Use Gemini Developer API Free Tier only.
-   Do not assume Google AI Pro includes API usage.
-   Do not enable paid billing.
-   Do not automatically fall back to a paid model/provider.
-   Do not automatically change to a paid Gemini model.
-   Maintain a configurable request/token quota below the provider's
    published free limits.
-   Add a local application-level spending ceiling of **zero**.
-   If free quota is exhausted, stop AI requests and queue them or
    notify the user.
-   Show quota exhaustion clearly.
-   Add an emergency AI disable switch.
-   Never expose API keys in logs, UI exports, source control or mobile
    builds.
-   Store keys using Windows secure storage or an appropriately
    protected local secret store.
-   On Android, do not embed a shared Gemini API key in the application
    package. Route AI calls through the user's secured desktop service
    or a properly secured backend. Explain that a centrally hosted API
    key requires server-side controls and that unpaid API privacy terms
    must be considered.

### Privacy

Treat unpaid API prompts as potentially accessible for service
improvement and human review under the provider's terms. Therefore: -
Send only the minimum public/business information needed. - Do not send
passwords, access tokens, private customer records or confidential
internal notes. - Do not send unnecessary personal contact details. -
Provide a prompt preview/redaction mechanism for sensitive workflows. -
Let the user disable AI entirely.

AI output is advisory. Lead scoring, compliance decisions, sending
messages and merging records must not be delegated blindly to AI.

## Module 8 --- Lead scoring

Implement a transparent, rule-based scoring engine.

Possible components: - Website absent. - Website has serious performance
issues. - Missing HTTPS. - SEO weaknesses. - Accessibility issues. -
Missing contact details. - Poor mobile experience. - Business category
fit. - Geographic fit. - Publicly observable digital maturity. - Data
completeness. - User-defined priority.

Example score bands: - 0--29: Low priority. - 30--49: Moderate. -
50--69: Good opportunity. - 70--84: High priority. - 85--100: Very high
priority.

These thresholds must be configurable.

For every score: - Store component values. - Store rule/version used. -
Explain score contributions. - Show evidence. - Allow manual override
with reason and audit trail. - Recalculate when source data or audit
results materially change.

Do not score a business negatively simply because data is unavailable.
Unknown is not the same as bad.

## Module 9 --- TKS service matching

Create a configurable catalogue of TheKhanSoft services, such as: -
Website design and development. - Business websites. - E-commerce
websites. - Web applications. - Custom software. - Attendance and
workforce solutions. - SEO and website optimization. - Maintenance and
support. - Digital transformation consulting. - Other services
configurable by the administrator.

For each lead, recommend possible services based on evidence and explain
why.

Do not hardcode pricing. Allow the owner to configure service
descriptions, starting prices, package details and proposal terms.

## Module 10 --- CRM and sales pipeline

Provide: - Lead stages. - Contact records. - Tasks. - Notes. -
Calls/meetings log. - Follow-up dates. - Reminders. - Tags. - Saved
filters. - Lead owner/assignee. - Sales pipeline. - Activity timeline. -
Lost/won reasons. - Next-action suggestions.

Suggested stages: - Discovered. - To Review. - Qualified. -
Researching. - Contact Drafted. - Awaiting Approval. - Contacted. -
Responded. - Meeting Scheduled. - Proposal Sent. - Negotiation. - Won. -
Lost. - Do Not Contact.

Make stages configurable through lookup/configuration records rather
than hard-coded enums where business changes are likely.

## Module 11 --- Message studio and outreach

Support message drafting for: - Email. - WhatsApp. - Other channels only
when a permitted integration is configured.

Features: - AI-assisted draft. - Manual editing. - Business-specific
personalization. - Template library. - Preview. - Approval queue. -
Message history. - Delivery status where provider supports it. - Reply
tracking where provider supports it. - Follow-up scheduling.

### Human approval by default

-   Never send a message automatically by default.
-   User must explicitly enable any automation.
-   Keep an approval queue.
-   Display recipient, channel, content and source of contact details
    before sending.
-   Provide a global emergency pause switch.
-   Respect suppression lists and opt-outs.
-   Prevent duplicate messages.
-   Maintain a full audit trail.

### Consent and legal compliance

-   A publicly listed phone number or email address is not automatically
    consent to receive marketing.
-   Require a documented lawful basis/consent state before promotional
    outreach.
-   Respect applicable Pakistani law and the laws of the recipient's
    country.
-   Respect WhatsApp Business Platform policies, opt-in requirements and
    approved template requirements where applicable.
-   Email must include appropriate sender identity and unsubscribe
    handling where required.
-   Track bounces, complaints, opt-outs and suppression status.
-   Never scrape personal contact information indiscriminately.
-   Do not support spam, unsolicited bulk messaging, evasion of provider
    restrictions or deceptive sender identity.

Do not implement WhatsApp Web automation or unofficial APIs that bypass
official platform restrictions.

Initially implement message drafting and approval workflows. Sending
integrations should remain disabled until the owner configures a
compliant provider.

## Module 12 --- Reports and exports

Provide: - Business list export. - Lead pipeline report. - Audit
report. - Lead score report. - Discovery job report. - Outreach activity
report. - Sales performance report. - Provider usage report. - CSV/XLSX
export. - PDF audit/proposal reports in a later stage.

Exports must respect permissions and data-source restrictions. Do not
export provider-restricted data.

## Module 13 --- Integration manager

Provide a central configuration screen for: - Google Places API. -
Gemini API. - PageSpeed Insights API. - Permitted search providers. -
Email provider. - WhatsApp Business provider. - Optional central API. -
Other future integrations.

For every integration show: - Enabled/disabled. - Connection status. -
Last successful request. - Usage/quota. - Error details. - Configuration
completeness. - Test connection action. - Disable switch.

Never display complete secret values after saving. Provide safe key
replacement and deletion.

## Module 14 --- Synchronization manager

Design synchronization from the beginning, even if central sync is
delivered after local desktop features.

Requirements: - Stable UUIDs generated locally. - Created/updated
timestamps. - Version or revision tracking. - Idempotent sync
operations. - Retry with backoff. - Conflict detection. - Conflict
resolution UI for meaningful conflicts. - Tombstones/soft-delete
synchronization. - No silent overwrites. - No loss of offline-created
data. - Per-device sync cursor. - Last-sync time and pending changes
count.

Do not assume the desktop and mobile clocks are perfectly synchronized.
Use server timestamps where available and preserve local timestamps for
auditability.

## Module 15 --- Settings and administration

Include: - Application preferences. - Theme. - Language-ready
architecture. - Default country/region. - Discovery defaults. - Audit
configuration. - Lead scoring configuration. - Service catalogue. -
Outreach settings. - AI settings and privacy controls. - API quotas. -
Data retention. - Backup and restore. - Local database maintenance. -
Device and sync management. - Logs and diagnostics.

------------------------------------------------------------------------

# 7. Data model and persistence

Use SQLite for local desktop/mobile persistence. Use MySQL centrally
when the optional central backend is enabled.

Design normalized schemas with clear relationships, indexes and
migrations.

Suggested domain entities:

### Local and central core

-   users
-   roles
-   permissions
-   devices
-   application_settings
-   integration_settings
-   audit_logs

### Discovery

-   businesses
-   business_contacts
-   business_websites
-   business_social_profiles
-   business_categories
-   business_tags
-   business_source_records
-   discovery_jobs
-   discovery_job_sources
-   discovery_results
-   import_batches
-   import_errors
-   duplicate_candidates
-   merge_history

### Website intelligence

-   website_checks
-   website_pages
-   website_technologies
-   website_audits
-   website_audit_findings
-   website_audit_evidence
-   website_audit_recommendations
-   audit_job_logs

### AI

-   ai_tasks
-   ai_analyses
-   ai_prompt_templates
-   ai_prompt_versions
-   ai_usage_logs
-   ai_cache
-   ai_quota_state

### Lead scoring and services

-   lead_scores
-   lead_score_components
-   lead_score_rules
-   lead_score_rule_versions
-   service_catalogue
-   service_recommendations

### CRM and outreach

-   pipelines
-   pipeline_stages
-   sales_tasks
-   sales_activities
-   sales_notes
-   campaigns
-   campaign_recipients
-   outreach_messages
-   outreach_events
-   contact_consents
-   suppression_list
-   message_templates
-   message_approvals

### Synchronization

-   sync_devices
-   sync_operations
-   sync_cursors
-   sync_conflicts
-   sync_tombstones

Do not create every table blindly before validating the data model.
Produce an ERD and migration plan first. Use lookup tables for
changeable statuses and categories. Use idempotent seeders. Add indexes
for common filters and search operations.

Keep source-specific raw data separate from normalized business data.
Store provenance, provider, retrieval timestamp, permitted retention
status and source record identifiers.

------------------------------------------------------------------------

# 8. Desktop application experience

The Windows desktop app is the primary control centre.

Suggested navigation: - Overview - Discover Businesses - Business
Database - Website Intelligence - Website Audits - AI Analysis - Lead
Scoring - Service Matching - CRM / Pipeline - Message Studio -
Campaigns - Reports - Integrations - Synchronization - Settings

Desktop-specific requirements: - Efficient data tables with filtering,
sorting and pagination. - Multi-select and bulk operations with
confirmation. - Background job queue and progress. - Audit job
management. - Local database backup and restore. - Import/export. -
Detailed business profile. - Split views where useful. - Keyboard
shortcuts for common actions. - Responsive layout for different Windows
display sizes. - Clear offline/online and sync status.

The desktop app must not freeze while crawling, auditing, importing or
calling AI. Use asynchronous operations, isolates or separate processes
as appropriate.

------------------------------------------------------------------------

# 9. Android application experience

Android is a companion application, not a full replacement for desktop.

Suggested navigation: - Dashboard - Leads - Business Details - Tasks -
Follow-ups - Notes - Message Drafts - Notifications - Sync - Settings

Android requirements: - Offline cache of assigned/saved leads. - Fast
search and filters. - Add notes and activities. - Update lead stage. -
Create follow-up tasks. - View audit summaries and recommendations. -
Review message drafts. - Synchronize safely. - Secure local storage. -
Avoid excessive background battery use. - Request permissions only when
required.

Do not expose administrative API keys or Gemini API secrets in the
Android application.

------------------------------------------------------------------------

# 10. Security, privacy and safe crawling

Security must be part of the design, not a final patch.

## 10.1 SSRF protection

The crawler must block: - localhost and loopback addresses. - Private
IPv4 and IPv6 ranges. - Link-local addresses. - Cloud metadata
endpoints. - Internal hostnames and internal network destinations. -
Redirects to prohibited addresses.

Resolve DNS and revalidate destination IPs on redirects and connection
changes. Prevent DNS rebinding. Apply strict redirect, timeout,
response-size and page-count limits.

## 10.2 Crawler safety

-   Use a clear user agent identifying the application.
-   Respect robots directives and site terms as applicable.
-   Apply per-domain rate limits.
-   Set concurrency limits.
-   Set maximum crawl depth and page count.
-   Limit downloaded content size.
-   Do not execute destructive actions.
-   Do not submit forms or make purchases.
-   Do not attempt authentication bypass.
-   Do not download or execute arbitrary files.
-   Sandbox browser processes.
-   Record audit scope and timestamps.

## 10.3 Secrets

-   Never commit API keys or credentials.
-   Provide `.env.example` with placeholders only.
-   Use secure storage on Windows and Android.
-   Keep server-side secrets on the server.
-   Redact secrets from logs and crash reports.
-   Rotate and revoke credentials safely.

## 10.4 Backups and data protection

-   Provide encrypted backup options where feasible.
-   Document restore procedures.
-   Warn before destructive actions.
-   Protect local database files.
-   Provide export/delete tools for user-managed data.
-   Keep an audit trail for sensitive operations.

------------------------------------------------------------------------

# 11. Cost-control and free-first policy

The initial product must minimize recurring costs.

Preferred: - Flutter and Dart. - Laravel. - MySQL. - SQLite. - Python. -
Playwright. - Lighthouse. - OSM data under its license. - CSV/XLSX
import. - Gemini API Free Tier, subject to current provider limits.

Optional/usage-based: - Google Places API. - PageSpeed Insights API
quotas. - Search APIs. - Email delivery providers. - WhatsApp Business
Platform. - Hosting and central database.

Mandatory cost controls: 1. Provider-level enable/disable. 2.
Configurable request caps. 3. Usage logs. 4. Quota warnings. 5. No
automatic paid fallback. 6. No automatic paid subscription activation.
7. Zero AI spending ceiling. 8. Clear indication when a provider is paid
or quota-limited. 9. Admin approval before enabling any potentially
billable integration. 10. Cost estimate before running large discovery
jobs.

Do not claim that a service is free without checking its current terms,
quotas and pricing. Record the date and source of pricing/limit
verification.

------------------------------------------------------------------------

# 12. Performance and resource constraints

Design for a modest development/server environment.

The user's existing Azure VM has approximately 4 GB RAM and limited disk
capacity. Do not assume it can host browser-heavy audit workers or local
LLMs efficiently.

-   Desktop audits should run locally on the Windows PC where possible.
-   Keep browser concurrency configurable and conservative.
-   Avoid running multiple heavy browser instances by default.
-   Keep central Laravel API lightweight.
-   Do not host a local LLM on the existing 4 GB VM.
-   Use pagination and efficient database queries.
-   Process large imports in background batches.
-   Avoid loading entire datasets into memory.
-   Add log rotation and retention settings.
-   Provide resource monitoring for audit workers.

------------------------------------------------------------------------

# 13. UX and visual design requirements

Create a polished, professional business application.

Design direction: - Modern SaaS-style desktop experience. - Clean and
uncluttered. - Professional TKS branding. - Consistent typography and
spacing. - Light and dark themes. - Clear status colors, but avoid
excessive decoration. - Good empty states and helpful error messages. -
Consistent forms and validation. - Accessible contrast and keyboard
navigation. - Data-dense desktop views without visual clutter. -
Mobile-first ergonomics on Android.

Do not build a generic template-looking app. Establish a reusable design
system before building many screens.

Create: - Color tokens. - Typography scale. - Spacing scale. - Component
library. - Form styles. - Table styles. - Dialog and drawer patterns. -
Status badges. - Loading and error states.

------------------------------------------------------------------------

# 14. Testing and quality standards

Testing is mandatory.

## Flutter

-   Unit tests for business logic.
-   Repository tests.
-   Widget tests for critical screens.
-   Integration tests for key workflows.
-   Offline/online transition tests.
-   SQLite migration tests.
-   Sync conflict tests.
-   Secure storage tests where practical.

## Laravel

-   Feature tests for API endpoints.
-   Authorization tests.
-   Validation tests.
-   Database tests.
-   Queue/job tests.
-   Rate-limit tests.
-   Sync idempotency tests.

## Python

-   Unit tests for crawler logic.
-   SSRF protection tests.
-   Redirect and DNS edge cases.
-   Timeout and size-limit tests.
-   Audit parser tests.
-   Playwright/Lighthouse integration tests.

## End-to-end scenarios

1.  Add a business manually while offline.
2.  Import a CSV file.
3.  Discover businesses through a configured permitted source.
4.  Deduplicate two records safely.
5.  Check a website.
6.  Run an audit.
7.  View evidence-backed findings.
8.  Generate a Gemini analysis within free-tier limits.
9.  Calculate and explain a lead score.
10. Draft a personalized message.
11. Approve a message without sending it.
12. Update CRM status offline.
13. Synchronize later without losing changes.
14. Open the same lead on Android.
15. Resolve a genuine sync conflict.
16. Verify that no paid AI request is made when free quota is exhausted.

Do not use real promotional contacts for testing. Use synthetic/test
data and safe test websites.

------------------------------------------------------------------------

# 15. Development stages and approval gates

Follow the stages below. The stage names and order are mandatory unless
the user approves a change.

## Stage 0 --- Environment and repository inspection

-   Inspect the current repository and development environment.
-   Identify existing code, dependencies and conventions.
-   Confirm available Flutter, Dart, PHP, Composer, Laravel, Python and
    database versions.
-   Check Windows desktop build requirements.
-   Check Android SDK/build requirements.
-   Identify missing dependencies.
-   Do not overwrite existing work.
-   Do not create large amounts of code yet.

Deliver: - Environment report. - Repository report. - Risks and
blockers. - Proposed folder structure. - Recommended dependency
versions.

Stop for approval.

## Stage 1 --- Product specification and architecture

-   Refine requirements into a product specification.
-   Define MVP and later releases.
-   Define desktop/mobile responsibilities.
-   Define local-first behavior.
-   Define optional central synchronization.
-   Produce architecture diagrams.
-   Define module boundaries and data flows.
-   Define provider abstraction contracts.
-   Define security boundaries.

Deliver: - Product requirements document. - Architecture document. -
Module map. - Data-flow diagrams. - Risk register. - MVP scope.

Stop for approval.

## Stage 2 --- UX and design system

-   Create desktop navigation and layout.
-   Create Android navigation and layout.
-   Define design tokens and reusable components.
-   Design dashboard, business list, business detail, audit report and
    CRM screens.
-   Define empty/loading/error/offline states.
-   Provide screen previews or implement initial UI shell.

Stop for approval.

## Stage 3 --- Local desktop foundation

-   Create Flutter Windows project structure.
-   Configure routing and state management.
-   Implement design system.
-   Configure SQLite and migrations.
-   Implement repositories and services.
-   Implement local settings and secure storage.
-   Implement logging and error handling.
-   Create the first functional navigation shell.

Acceptance: - Windows app builds and launches. - Local database
initializes. - App works without central server. - No secrets are
committed.

Stop for approval.

## Stage 4 --- Core business database

-   Business CRUD.
-   Contacts.
-   Websites.
-   Categories and tags.
-   Search, filtering and sorting.
-   Notes and activity history.
-   CSV/XLSX import and validation.
-   Duplicate candidate detection and manual merge.
-   Import/export.

Acceptance: - User can manage businesses fully offline. - Data persists
after restart. - Import errors are understandable. - No uncertain
automatic merges.

Stop for approval.

## Stage 5 --- Discovery providers

-   Provider abstraction.
-   Manual discovery.
-   OSM adapter using a permitted source.
-   Optional Google Places adapter.
-   Search/directory adapters only after terms review.
-   Discovery job queue.
-   Quota controls and usage logs.
-   Provider enable/disable settings.
-   Result normalization and provenance.

Acceptance: - A discovery job can be created and tracked. - Provider
failures do not crash the app. - Cost/usage controls work. - Restricted
source data is not stored improperly.

Stop for approval.

## Stage 6 --- Website intelligence

-   Website detection.
-   Safe HTTP inspection.
-   Redirect handling.
-   Metadata extraction.
-   Contact/social link extraction.
-   Website status classification.
-   Caching and recheck scheduling.
-   SSRF protections.

Acceptance: - Safe websites can be inspected. - Internal/private
destinations are blocked. - Uncertain failures are not misclassified as
missing websites.

Stop for approval.

## Stage 7 --- Python audit engine

-   Create isolated Python service/sidecar.
-   Define request/response contract.
-   Integrate Playwright.
-   Integrate Lighthouse.
-   Integrate accessibility checks.
-   Add SEO, performance and technical checks.
-   Add evidence storage.
-   Add limits, timeouts and sandboxing.
-   Integrate with desktop UI.

Acceptance: - User can launch an audit from desktop. - UI remains
responsive. - Audit findings contain evidence. - SSRF and resource-limit
tests pass.

Stop for approval.

## Stage 8 --- Audit reports and comparison

-   Audit history.
-   Category summaries.
-   Prioritized findings.
-   Recommendations.
-   Re-audit comparison.
-   Exportable report foundation.

Acceptance: - User can compare two audits. - AI is not required for raw
measurements. - Unsupported claims are not shown.

Stop for approval.

## Stage 9 --- Gemini AI integration

-   Verify current Gemini Developer API free-tier models, quotas and
    terms.
-   Implement provider abstraction and Gemini adapter.
-   Secure API key configuration.
-   Add prompt templates and versioning.
-   Add caching, retries and quota tracking.
-   Add redaction and privacy controls.
-   Enforce zero-spending behavior.
-   Add AI disable switch.

Acceptance: - AI analysis works with a free-tier eligible model. - No
paid fallback exists. - Exhausted quota stops requests. - Sensitive data
is not sent by default. - API key is not exposed in logs or client
builds.

Stop for approval.

## Stage 10 --- Lead scoring and service matching

-   Configurable scoring rules.
-   Explainable score components.
-   Rule versioning.
-   Manual override and history.
-   TKS service catalogue.
-   Evidence-based service recommendations.

Acceptance: - Score is reproducible and explainable. - Unknown data is
not treated as a negative finding. - User can adjust scoring
configuration.

Stop for approval.

## Stage 11 --- CRM and message studio

-   Pipeline and stages.
-   Tasks and reminders.
-   Notes and activities.
-   Message templates.
-   AI-assisted drafts.
-   Approval queue.
-   Consent and suppression records.
-   Global outreach pause.
-   Sending integrations remain disabled until explicitly configured and
    approved.

Acceptance: - User can manage the full lead lifecycle. - Drafts can be
reviewed and edited. - No message is sent without the required approval
and compliant integration.

Stop for approval.

## Stage 12 --- Central Laravel API and synchronization

-   Create Laravel API project.
-   Design MySQL schema.
-   Implement authentication and device registration.
-   Implement versioned endpoints.
-   Implement idempotent sync.
-   Implement conflict detection/resolution.
-   Implement sync status UI.
-   Add server-side authorization and audit logs.

Acceptance: - Desktop works when API is unavailable. - Offline changes
synchronize later. - Duplicate sync requests do not duplicate records. -
Conflicts are visible and recoverable.

Stop for approval.

## Stage 13 --- Android companion application

-   Configure Android app in shared Flutter codebase.
-   Implement authentication.
-   Implement local database/cache.
-   Implement assigned/saved leads.
-   Implement notes, tasks and follow-ups.
-   Implement audit summary viewing.
-   Implement sync and conflict handling.
-   Add notifications where appropriate.

Acceptance: - Android app builds and installs. - Offline lead operations
work. - Changes synchronize safely. - No API secrets are embedded.

Stop for approval.

## Stage 14 --- Reports, backup and administration

-   Reports.
-   CSV/XLSX exports.
-   PDF reports if approved.
-   Backup/restore.
-   Data retention.
-   Provider usage dashboard.
-   Diagnostics and logs.
-   Administrative settings.

Stop for approval.

## Stage 15 --- Hardening and release

-   Full regression testing.
-   Security review.
-   Dependency/license review.
-   Performance testing.
-   Database migration/backup tests.
-   Windows packaging and installer.
-   Android release build.
-   User documentation.
-   Admin guide.
-   Recovery and troubleshooting guide.
-   Release checklist.

Deliver: - Final architecture. - Feature completion matrix. - Test
results. - Known limitations. - Installation instructions. -
Backup/restore instructions. - Security notes. - Release artifacts and
version.

Stop and present the final production summary.

------------------------------------------------------------------------

# 16. Coding rules

Follow these rules throughout the project:

1.  Inspect before modifying.
2.  Do not overwrite existing files without understanding their purpose.
3.  Use modular architecture and clear naming.
4.  Keep UI, business logic, data access and provider integrations
    separate.
5.  Use dependency injection where appropriate.
6.  Avoid giant files and giant widgets.
7.  Avoid duplicated logic.
8.  Use typed models and explicit serialization.
9.  Validate all external input.
10. Handle exceptions and network failures.
11. Never silently swallow errors.
12. Use database migrations.
13. Use idempotent seeders.
14. Keep configuration out of source code.
15. Add tests alongside important features.
16. Run formatter, analyzer, static checks and tests after meaningful
    changes.
17. Document non-obvious architectural decisions.
18. Keep dependencies minimal.
19. Do not add packages without checking maintenance, license and
    compatibility.
20. Do not claim that a command succeeded unless its output confirms
    success.

For Laravel, use clean controllers, services/actions, policies, jobs and
repositories only where they add value. Do not create unnecessary
abstraction layers.

For Flutter, keep widgets focused and use repositories/services for data
and business operations.

For Python, use type hints, structured logging, explicit configuration
and testable modules.

------------------------------------------------------------------------

# 17. Required project documentation

Maintain these documents in the repository:

-   `README.md`
-   `docs/PRODUCT_REQUIREMENTS.md`
-   `docs/ARCHITECTURE.md`
-   `docs/DATABASE_DESIGN.md`
-   `docs/API_SPECIFICATION.md`
-   `docs/SECURITY_AND_PRIVACY.md`
-   `docs/DISCOVERY_SOURCE_COMPLIANCE.md`
-   `docs/AI_AND_COST_POLICY.md`
-   `docs/SYNC_DESIGN.md`
-   `docs/TEST_PLAN.md`
-   `docs/INSTALLATION_WINDOWS.md`
-   `docs/INSTALLATION_ANDROID.md`
-   `docs/BACKUP_AND_RESTORE.md`
-   `docs/CHANGELOG.md`
-   `docs/DECISIONS/` for architecture decision records.

Update documentation as implementation evolves. Do not leave documents
describing features that are not implemented without marking them as
planned.

------------------------------------------------------------------------

# 18. Definition of done

A stage is complete only when:

-   Its agreed requirements are implemented.
-   Relevant tests pass.
-   The application builds.
-   Errors and limitations are documented.
-   No secrets are exposed.
-   No known critical security issue remains unaddressed.
-   The user can verify the delivered functionality.
-   The stage summary is provided.
-   The next stage is proposed but not started until approval.

At every stage end, provide:

1.  What was completed.
2.  Files/modules created or modified.
3.  Tests and commands executed.
4.  Build status.
5.  Screenshots or demonstration where useful.
6.  Known limitations.
7.  Security/cost implications.
8.  Next stage plan.
9.  Explicit approval request.

Do not proceed simply because the user has not objected. Wait for
explicit approval.

------------------------------------------------------------------------

# 19. Initial MVP priority

Prioritize a useful desktop-first MVP:

1.  Windows desktop shell and local SQLite.
2.  Business database and CSV/XLSX import.
3.  Business discovery with at least one compliant source.
4.  Website detection and safe inspection.
5.  Local Python website audit.
6.  Gemini free-tier analysis.
7.  Explainable lead scoring.
8.  TKS service matching.
9.  CRM pipeline and follow-up tasks.
10. Message drafting and manual approval.
11. Optional central API and synchronization.
12. Android companion app.

Do not delay a useful desktop MVP by building all Android screens or
central infrastructure first. However, design IDs, repositories and data
structures so later synchronization is feasible.

------------------------------------------------------------------------

# 20. First instruction --- begin Stage 0 only

Start now with **Stage 0 --- Environment and Repository Inspection**.

Do not begin coding the full application yet.

First: 1. Inspect the current Antigravity workspace and repository. 2.
Determine whether this is a new project or an existing codebase. 3.
Inspect available development tools and versions. 4. Identify Windows
and Android build prerequisites. 5. Identify likely local Python sidecar
requirements. 6. Report any blockers or installation needs. 7. Propose
the repository/folder structure. 8. Confirm that the no-web-dashboard,
local-first hybrid architecture is understood. 9. Present the Stage 0
report.

**Stop after Stage 0 and wait for my explicit approval.**

The project must be developed collaboratively, one approved stage at a
time. Never skip approval gates, never silently change the architecture,
and never activate a paid service without my explicit permission.
