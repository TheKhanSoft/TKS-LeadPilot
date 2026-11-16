# TKS LeadPilot — Comprehensive Risk Register (Stage 1 Update)

**Document Version:** 1.0.0  
**Last Updated:** 2026-11-16  

---

| Risk ID | Category | Description | Severity | Probability | Mitigation Strategy | Owner |
|---|---|---|:---:|:---:|---|---|
| **R-01** | Technical | Gemini Free Tier quota reductions or latency spikes | High | Medium | Implement local prompt caching, hard client-side rate limiters (12 RPM), and clear "Free Quota Exhausted" state notifications. | AI Architect |
| **R-02** | Security | Malicious or internal URLs submitted to Crawler (SSRF) | Critical | Low | Multi-layer SSRF filter: pre-flight DNS resolution, IP range blacklisting (RFC 1918, loopback, cloud metadata), and redirect re-evaluation. | Security Engineer |
| **R-03** | Compliance | OSM Nominatim/Overpass rate limit violations | Medium | Medium | Hard rate limiter (max 1 req/sec), 7-day local disk query caching, and clear attribution preservation in UI. | Lead Developer |
| **R-04** | Performance | Heavy Playwright / Chromium audit resource consumption on 32GB host | Medium | Low | Limit concurrent audits to 1 by default (configurable up to 2), automatic process recycling, 60s hard timeouts. | Automation Engineer |
| **R-05** | Architectural | Schema drift between Avalonia (C#) and Flutter (Dart) local SQLite DBs | High | Low | Single source-of-truth schema in `docs/DATABASE_DESIGN.md`, identical column definitions, UUIDv4 keys, and automated migration tests. | Solution Architect |
| **R-06** | Data Integrity | Sync conflicts between Desktop and Mobile concurrent edits | Medium | Medium | Deterministic Last-Write-Wins with UTC timestamps; archive losing payloads in `sync_conflicts` for operator recovery. | Backend Architect |
| **R-07** | Operational | Accidental dispatch of unapproved marketing outreach | Critical | Low | Approval queue pattern enforced in data layer: `dispatch_status` is locked at `Draft`/`PendingApproval` until manual user sign-off. | Product Manager |
| **R-08** | Cost | Unintended cloud or API provider charges | Critical | Low | Mandatory $0.00 spending ceiling; zero paid fallbacks; provider disable switches; no payment methods required. | Product Manager |
