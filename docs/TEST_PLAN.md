# TKS LeadPilot — Comprehensive Test Plan

**Document Version:** 1.0.0  
**Status:** Approved Test Strategy  
**Coverage Target:** Unit, Integration, Security, and End-to-End Scenarios  

---

## 1. Testing Pyramid & Tooling

| Component | Framework / Runner | Target Coverage | Key Focus Areas |
|---|---|---|---|
| **Desktop (`desktop/`)** | `xUnit` + `FluentAssertions` + `Moq` | >80% Core Domain | Scoring formulas, deduplication, MVVM commands, SQLite EF migrations, Secret encryption |
| **Mobile (`mobile/`)** | `flutter_test` + `mocktail` | >80% Domain/State | Riverpod state transitions, Drift SQLite migrations, offline caching, UI widgets |
| **Audit Sidecar (`audit_engine/`)** | `pytest` + `pytest-asyncio` + `httpx` | >85% | SSRF IP block rules, redirect loops, HTML extractors, Playwright timeout handling |
| **Central API (`server/`)** | `PHPUnit` / `Pest` | >85% | Sanctum tokens, sync push/pull idempotency, conflict detection, rate limits |

---

## 2. Mandatory End-to-End Verification Scenarios

Before declaring any milestone or release ready, all 16 scenarios defined in the Master Prompt must pass:

1. **Scenario 1 — Offline Manual Entry:** Add a business and contact in Desktop with network disconnected. Verify data persists and remains queryable.
2. **Scenario 2 — CSV Import & Mapping:** Import a 50-row CSV file. Verify column auto-mapping, preview display, duplicate warning, and database insertion.
3. **Scenario 3 — Compliant Discovery Job:** Run an OSM Overpass discovery job for a specified city and category. Verify rate-limiting compliance (max 1 req/sec) and raw provenance logging.
4. **Scenario 4 — Deduplication & Merge:** Feed two records with 85% name match and identical phone. Verify candidate detection, side-by-side diff display, and non-destructive merge.
5. **Scenario 5 — Safe Website Check:** Probe a valid public URL. Verify metadata extraction (title, description, CMS, social links) and non-blocking background async execution.
6. **Scenario 6 — Local Website Audit:** Trigger an audit via Python sidecar. Verify performance, SEO, accessibility, and security findings are recorded with verifiable raw evidence.
7. **Scenario 7 — SSRF & Private Network Block:** Trigger an audit for `http://127.0.0.1`, `http://192.168.1.1`, and `http://169.254.169.254`. Verify immediate rejection with clear security error.
8. **Scenario 8 — Gemini Free-Tier Analysis:** Request an AI opportunity summary for an audited business. Verify generation succeeds using Free-Tier model without billing trigger.
9. **Scenario 9 — Transparent Lead Scoring:** Evaluate score calculation for a business missing a website vs. one with poor mobile performance. Verify exact component breakdown is explainable.
10. **Scenario 10 — TKS Service Recommendation:** Verify matching rules map poor mobile scores to "Website Redesign" and missing HTTPS to "Maintenance Retainer".
11. **Scenario 11 — Message Studio Draft & Approval:** Generate personalized outreach draft. Verify message is placed in `PendingApproval` queue; verify no automated dispatch occurs.
12. **Scenario 12 — Offline CRM Updates:** Update pipeline stage, add task and notes while offline. Verify all changes persist in local SQLite.
13. **Scenario 13 — Bidirectional Sync:** Reconnect network and trigger sync. Verify desktop mutations are received by central server without duplicates.
14. **Scenario 14 — Mobile Companion Review:** Open Android app, pull synchronized leads. Verify assigned lead, notes, and audit summaries render correctly.
15. **Scenario 15 — Sync Conflict Resolution:** Edit a lead title concurrently on Desktop and Mobile. Trigger sync on both. Verify Last-Write-Wins resolution and that superseded edit is archived in `sync_conflicts`.
16. **Scenario 16 — Zero-Billing Hard Limit:** Simulate exhaustion of daily free AI quota. Verify further AI requests are stopped and clearly communicated without fallback to paid services.
