# TKS LeadPilot

> Professional Business-Discovery, Website-Intelligence, Website-Auditing, AI-Assisted Lead-Generation, CRM and Outreach Platform for TheKhanSoft.

---

## 🚀 Quick Launch Guide (How to Open and Run)

### 1. Windows Desktop Application (Avalonia UI / .NET 10)
- **1-Click Launch (Recommended):** Double-click [`run-desktop.bat`](file:///D:/Projects/TKS-LeadPilot/run-desktop.bat) in the project root directory.
- **Direct Executable:** Run [`desktop/dist/TKS.LeadPilot.UI.exe`](file:///D:/Projects/TKS-LeadPilot/desktop/dist/TKS.LeadPilot.UI.exe).
- **From Command Line / Development:**
  ```powershell
  dotnet run --project desktop/src/TKS.LeadPilot.UI/TKS.LeadPilot.UI.csproj
  ```

### 2. Central Headless Laravel API (`backend/`)
- **Start the API Server:**
  ```powershell
  cd backend
  php artisan serve
  ```
  API will be live at `http://127.0.0.1:8000/api/v1` (Health check at `http://127.0.0.1:8000/api/v1/health`).
- **Run Backend Tests:**
  ```powershell
  cd backend
  php artisan test
  ```

### 3. Mobile Companion App (`mobile/` - Flutter Android)
- **Run on Android Device / Emulator:**
  ```powershell
  cd mobile
  flutter run
  ```
- **Run Mobile Unit & Widget Tests:**
  ```powershell
  cd mobile
  flutter test
  ```

---

## 🌟 Highlights

- **100% Native Client Applications:** Avalonia UI (C# / .NET 10) Windows Desktop Control Station & Flutter (Dart) Android Field Companion.
- **No Web Dashboard:** Zero browser-based dashboards; built strictly as high-performance native desktop and mobile applications.
- **Local-First Hybrid Architecture:** Fully functional offline against local SQLite databases (`%LOCALAPPDATA%\TKS.LeadPilot\tks_leadpilot.db`); bidirectional delta sync with central headless Laravel 13 API.
- **Pre-Flight SSRF Guard & Native Website Auditor:** In-process website intelligence engine inspecting tech stack/CMS, TLS/SSL certificates, security headers, crawlability, and TTFB latency with strict IP protection.
- **Cost-Guarded Google Places Enrichment:** Optional commercial enrichment with hard daily request ceilings, disabled-by-default switch, and 30-day SQLite caching ($0 on cache hits).
- **Zero-Unexpected-Billing AI Policy:** Google Gemini Developer API Free Tier only (`gemini-1.5-flash`); strict spending ceiling of $0.00, hard rate limiters (12 RPM / 1,200 RPD), and 100% offline-first deterministic proposal fallbacks.
- **Explainable Lead Scoring & Service Matching:** Deterministic 0–100 opportunity score across 5 explainable components, paired with an automated rule engine matching findings to TheKhanSoft's service catalogue.
- **Complete CRM Sales Pipeline:** 14-stage interactive sales pipeline board, task management with due dates, and chronological activity notes/timeline.
- **Message Studio & Multi-Channel Outreach Engine:** Drafts cold outreach across Email (`mailto:`) and WhatsApp (`https://wa.me/`) with 11-token dynamic template engine, Do-Not-Contact suppression guards, and human approval queue.
- **Central Headless Laravel 13 API & Delta Sync Protocol:** Sanctum token authentication, universal UUIDv4 primary keys, Server-Wins LWW conflict detection, and tombstone soft deletes.
- **DPAPI Credential Vault:** All sensitive API keys (Gemini, Google Places, Sanctum Token) are securely encrypted via Windows Data Protection API (DPAPI, `CurrentUser` scope) and never stored in plain text.

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
| **Stage 10** | Message Studio & Multi-Channel Outreach Engine + Modern SaaS UI/UX Overhaul | ✅ Completed |
| **Stage 11** | Central Headless Laravel 13 API & Delta Synchronization Protocol | ✅ Completed |
| **Stage 12** | Mobile Companion App (Flutter Android with Offline Storage & Sync) | ✅ Completed |
| **Stage 13** | End-to-End Integration, Data Seeding & Cross-Platform Verification | ✅ Completed |
| **Stage 14** | Automated Multi-Platform Test Verification & Security Hardening | ✅ Completed |
| **Stage 15** | Final Hardening, Release Packaging & Operator Documentation | ⏳ Up Next |

---

## 📂 Repository Organization

```text
TKS-LeadPilot/
├── desktop/                  # Windows Desktop Client (Avalonia UI 12+ / .NET 10)
│   ├── src/
│   │   ├── TKS.LeadPilot.Core/         # Domain models, enums, sync models, interfaces
│   │   ├── TKS.LeadPilot.Data/         # EF Core SQLite, sync service, DPAPI vault
│   │   └── TKS.LeadPilot.UI/           # Avalonia UI desktop views, viewmodels, theme
│   ├── dist/                           # Published standalone Windows desktop executable
│   ├── tests/
│   │   ├── TKS.LeadPilot.Core.Tests/   # Unit tests for scoring, audit, deduplication
│   │   └── TKS.LeadPilot.Data.Tests/   # Tests for SQLite, CSV, outreach, and sync
│   └── TKS.LeadPilot.Desktop.sln       # Visual Studio / .NET solution
├── backend/                  # Central Headless REST API (Laravel 13 / PHP 8.4)
│   ├── app/
│   │   ├── Http/Controllers/Api/       # AuthController, HealthController, SyncController
│   │   └── Models/                     # Eloquent models with universal UUIDv4 keys
│   ├── database/migrations/            # Devices, SyncConflicts, and synchronized tables
│   ├── routes/api.php                  # V1 REST API and Sanctum-guarded sync routes
│   └── tests/Feature/                  # PHPUnit / Pest tests (SyncApiTest, HealthTest)
├── mobile/                   # Android Mobile Companion (Flutter 3.47+ / Dart 3.13+)
│   ├── lib/
│   │   ├── models/                     # Lead, Contact, SalesTask, SalesNote, SyncStatus
│   │   ├── screens/                    # LeadsScreen, LeadDetailScreen, TasksScreen, SyncScreen
│   │   ├── services/                   # StorageService (offline), MobileSyncService (API sync)
│   │   └── theme/                      # Obsidian dark Material 3 theme matching desktop
│   └── test/                           # Flutter unit & widget tests (100% pass)
├── docs/                     # Specifications, Architecture, Changelog, and Stage Reports
├── run-desktop.bat           # 1-click Windows desktop application launcher
└── README.md
```

---

## 🧪 Verification & Test Suite Summary

- **.NET Desktop Test Suite:** 143 unit & integration tests passing across Core (118) and Data (25) with 100% pass rate (0 failures). Includes `SecurityHardeningTests`, `GeoLocationServiceTests`, `EndToEndIntegrationTests`, and comprehensive filtering/duplicate tests.
- **Laravel API Feature Tests:** 7 tests / 33 assertions passing (100% pass rate, 0 failures).
- **Flutter Mobile Tests:** 6 unit & widget tests passing (100% pass rate, 0 failures).
- **Total Automated Tests:** 156 automated tests passing across desktop, backend, and mobile.
- **End-to-End Cross-Platform Script:** `python scripts/verify_cross_platform_sync.py` passes with 100% data fidelity.
- **Resilient Discovery Engine:** OpenStreetMap Lead Discovery Engine includes verified offline cache (`OsmOfflineCache`) across 7 Pakistani metropolitan centers (Islamabad, Lahore, Karachi, Rawalpindi, Faisalabad, Peshawar, Multan) with 8-second circuit-breaker timeout to ensure zero UI hangs when public Overpass mirrors are slow or rate-limited.
