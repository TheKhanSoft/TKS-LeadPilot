# TKS LeadPilot — Changelog

All notable changes to the TKS LeadPilot project will be documented in this file.

---

## [Unreleased]

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
