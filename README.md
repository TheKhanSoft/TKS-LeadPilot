# TKS LeadPilot

> Professional Business-Discovery, Website-Intelligence, Website-Auditing, AI-Assisted Lead-Generation, CRM and Outreach Platform for TheKhanSoft.

---

## 🌟 Highlights
- **100% Native Client Applications:** Avalonia UI (C# / .NET 10) Windows Desktop Control Station & Flutter (Dart) Android Field Companion.
- **No Web Dashboard:** Zero browser-based dashboards; built strictly as high-performance native desktop and mobile applications.
- **Local-First Hybrid Architecture:** Fully functional offline against local SQLite databases; optional bidirectional delta sync with a central Laravel 12 API.
- **Evidence-Backed Audits:** Local Python sidecar running Playwright, Lighthouse, and axe-core to evaluate real-world website performance, SEO, accessibility, and security hygiene.
- **Zero-Unexpected-Billing AI Policy:** Google Gemini Developer API Free Tier only; strict local spending ceiling of $0.00 with hard rate limiters.
- **Mandatory Human Approval:** All marketing and outreach drafts require explicit operator review and approval before sending.

---

## 📂 Repository Organization

```text
TKS-LeadPilot/
├── desktop/                  # Windows Desktop Client (Avalonia UI 11/12+ .NET 10)
├── mobile/                   # Android Mobile Companion (Flutter 3.47+ / Dart 3.13+)
├── audit_engine/             # Local Website Audit & Crawler Sidecar (Python 3.13)
├── server/                   # Optional Central Sync & Administration API (Laravel 12)
├── docs/                     # Specifications, Architecture, and Compliance Docs
│   ├── PRODUCT_REQUIREMENTS.md
│   ├── ARCHITECTURE.md
│   ├── DATABASE_DESIGN.md
│   ├── AI_AND_COST_POLICY.md
│   ├── DISCOVERY_SOURCE_COMPLIANCE.md
│   ├── SYNC_DESIGN.md
│   ├── SECURITY_AND_PRIVACY.md
│   ├── TEST_PLAN.md
│   ├── MVP_SCOPE.md
│   ├── RISK_REGISTER.md
│   └── DECISIONS/
│       └── ADR-001-ui-stack-selection.md
├── TKS-LeadPilot-PRD.md      # Master PRD Document
└── README.md
```

---

## 🚦 Development Status & Roadmap

| Stage | Description | Status |
|:---:|---|:---:|
| **Stage 0** | Environment and Repository Inspection | ✅ Completed |
| **Stage 1** | Product Specification & Architecture | ✅ Completed (Awaiting Approval) |
| **Stage 2** | UX and Design System | ⏳ Pending Stage 1 Approval |
| **Stage 3** | Local Desktop Foundation (Avalonia + SQLite) | ⏳ Upcoming |
| **Stage 4** | Core Business Database & Deduplication | ⏳ Upcoming |
| **Stage 5** | Discovery Providers (OSM & Import) | ⏳ Upcoming |
| **Stage 6** | Website Intelligence (Safe HTTP Inspection) | ⏳ Upcoming |
| **Stage 7** | Python Website Audit Sidecar | ⏳ Upcoming |
| **Stage 8** | Audit Reports & Comparison | ⏳ Upcoming |
| **Stage 9** | Gemini AI Integration (Free Tier) | ⏳ Upcoming |
| **Stage 10** | Lead Scoring & Service Matching | ⏳ Upcoming |
| **Stage 11** | CRM Pipeline & Message Studio | ⏳ Upcoming (MVP Delivery) |
| **Stage 12** | Central Laravel API & Sync Engine | ⏳ Post-MVP |
| **Stage 13** | Android Mobile Companion (Flutter) | ⏳ Post-MVP |
| **Stage 14** | Reporting, Backup & Administration | ⏳ Post-MVP |
| **Stage 15** | Hardening, Packaging & Release | ⏳ Final Release |

---

## 📜 Documentation Index
- [Product Requirements Specification](docs/PRODUCT_REQUIREMENTS.md)
- [Target Architecture & Data Flows](docs/ARCHITECTURE.md)
- [Database Design & Schema](docs/DATABASE_DESIGN.md)
- [AI & Zero-Cost Billing Policy](docs/AI_AND_COST_POLICY.md)
- [Discovery Compliance & Ethics](docs/DISCOVERY_SOURCE_COMPLIANCE.md)
- [Delta Synchronization Protocol](docs/SYNC_DESIGN.md)
- [Security & SSRF Safeguards](docs/SECURITY_AND_PRIVACY.md)
- [Comprehensive Test Plan](docs/TEST_PLAN.md)
- [MVP Scope & Milestones](docs/MVP_SCOPE.md)
- [Architecture Decision Record: UI Stack Selection](docs/DECISIONS/ADR-001-ui-stack-selection.md)
