# Architecture Decision Record (ADR) 001: UI Technology Stack Selection

**Status:** Approved  
**Date:** 2026-11-16  
**Deciders:** Project Owner (TheKhanSoft), Antigravity AI  
**Consulted:** Stage 0 Inspection & Framework Comparison Report  

---

## 1. Context & Problem Statement

TKS LeadPilot requires:
1. A rich, data-dense, professional Windows desktop control centre with high-performance DataGrids, advanced filtering, multi-pane layouts, and local-first SQLite persistence.
2. A fast, ergonomic Android companion mobile application for field sales, lead review, notes, task management, and offline updates.
3. No web dashboard (neither Laravel Blade/Livewire nor React/Vue) — native client experiences only.
4. Seamless integration with a local Python website crawler/audit sidecar, a Gemini Free-Tier AI provider, and an optional Laravel 12 synchronization API.

During Stage 0 inspection, it was discovered that Visual Studio Enterprise 2026 on the development machine did not have the C++ Desktop Development workload installed (~8 GB download required for Flutter Windows desktop runner), whereas .NET 10 SDK with the Managed Desktop workload (WPF/WinForms) was already installed and ready.

---

## 2. Decision

We have decided on a **hybrid multi-technology, dedicated-platform client architecture**:

1. **Windows Desktop Client (`desktop/`):** **Avalonia UI 11/12+ with C# and .NET 10**
   - Pattern: Model-View-ViewModel (MVVM) using `CommunityToolkit.Mvvm`.
   - Local Storage: Entity Framework Core (`Microsoft.EntityFrameworkCore.Sqlite`) with SQLite.
   - Styling: Semi.Avalonia / Fluent Material Theme for enterprise desktop aesthetics.
   - Zero additional heavy C++ workload installs required; takes immediate advantage of .NET 10 SDK.
   - Native desktop capabilities: robust DataGrid with column virtualization and multi-column sorting, splitters, tabbed docks, and direct process orchestration.

2. **Android Mobile Client (`mobile/`):** **Flutter 3.47+ / Dart 3.13+**
   - Platform: Native Android mobile companion app.
   - UI: Material 3 design system with mobile-first ergonomics.
   - Pattern: Repository & Clean Architecture using Riverpod (`flutter_riverpod`).
   - Local Storage: Drift (`drift` / `sqlite3_flutter_libs`) with SQLite.
   - Leverages Flutter's industry-leading mobile touch responsiveness, camera/share integrations, and fluid mobile UX.

3. **Audit Engine Sidecar (`audit_engine/`):** **Python 3.13 + FastAPI + Playwright + Lighthouse + axe-core**
   - Isolated out-of-process service communicating with the desktop client over local HTTP / JSON-RPC.

4. **Central Synchronization API (`server/`):** **Laravel 12 + PHP 8.4 + MySQL 8.4 + Sanctum**
   - Optional central synchronization and multi-device identity management.

5. **Folder Organization:** Dedicated, decoupled workspaces under `D:\Projects\TKS-LeadPilot`:
   - `desktop/` — Avalonia .NET solution
   - `mobile/` — Flutter Android project
   - `audit_engine/` — Python crawler & audit sidecar
   - `server/` — Laravel 12 API backend
   - `docs/` — Central documentation & specifications

---

## 3. Rationale & Alternatives Considered

| Option | Pros | Cons | Decision |
|---|---|---|---|
| **Flutter for Both** | Single codebase, 85-90% shared code | Required ~8 GB VS C++ workload install; Dart required for desktop; desktop DataGrid less mature than XAML | Rejected due to install overhead & desktop UI preferences |
| **WPF + Flutter** | WPF has 20-year mature DataGrid | WPF is Windows-only with older styling paradigms | Rejected in favor of modern Avalonia UI |
| **PyQt6 + Flutter** | Excellent desktop tables, native Python | Two distinct languages with no shared mobile path; Qt styling overhead | Rejected in favor of C# Avalonia |
| **Avalonia (Desktop) + Flutter (Mobile)** | **Best-of-both-worlds: Avalonia provides world-class desktop XAML data tables with 0 GB extra install; Flutter provides world-class Android mobile ergonomics; standard decoupled architecture** | Two distinct client codebases | **ACCEPTED** |

---

## 4. Consequences & Mitigations

- **Consequence:** Data contracts (models, JSON serialization schemas, SQLite schemas) must be kept strictly consistent between C# (Desktop) and Dart (Mobile).
- **Mitigation:**
  1. Define schema source-of-truth in `docs/DATABASE_DESIGN.md` and `docs/API_SPECIFICATION.md`.
  2. Use standard RFC 4122 v4 UUIDs for all primary keys across both platforms.
  3. Both SQLite databases use identical table definitions, column names, constraints, and UTC ISO 8601 timestamps.
  4. Both clients communicate with the identical Laravel sync endpoint contract (`/api/v1/sync/...`).

---

## 5. Compliance with Master Directives
- **Zero Web Dashboard:** Strictly enforced. No browser dashboard will be built.
- **Local-First:** Both Desktop and Mobile operate fully autonomously against local SQLite.
- **Cost Policy:** Maintained. Gemini Developer API Free Tier only.
