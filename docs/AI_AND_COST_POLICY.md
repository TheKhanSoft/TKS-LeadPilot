# TKS LeadPilot — AI Integration & Zero-Cost Billing Policy

**Document Version:** 1.0.0  
**Status:** Mandatory Product Policy  
**Effective Date:** 2026-11-16  
**Enforcement:** Hard-coded architectural constraints in Desktop and Mobile clients  

---

## 1. Zero-Unexpected-Billing Policy

TKS LeadPilot enforces a strict **$0.00 spending ceiling**. Under no circumstance will the system initiate, fall back to, or trigger any paid AI operation or cloud billing.

### 1.1 Provider & Tier Mandate
1. The sole permitted AI provider is the **Google Gemini Developer API via Google AI Studio** using the **Free Tier**.
2. Never assume Google AI Pro consumer subscriptions include commercial API credits. Gemini consumer subscriptions and Google AI Studio Developer API billings are legally and technically separate accounts.
3. No credit card or billing account may be enabled on the Google Cloud project hosting the Gemini API key.
4. The system must verify that any configured key is operating strictly within the Free Tier.

---

## 2. Model Selection & Published Free Tier Limits

As of late 2024 / 2025–2026, the primary Free Tier eligible model for text analysis, code review, and summarization is:

| Model ID | Input / Output | Free Tier RPM | Free Tier RPD | Context Window |
|---|---|:---:|:---:|:---:|
| `gemini-1.5-flash` (or current `gemini-2.0-flash` free preview) | Text, Audio, Images, Video | 15 RPM | 1,500 RPD | 1M tokens |
| `gemini-1.5-pro` (if permitted under current free tier terms) | Text, Audio, Images, Video | 2 RPM | 50 RPD | 2M tokens |

> [!IMPORTANT]
> The default model is pinned to **`gemini-1.5-flash`** (or current stable free-tier flash model). The heavy `gemini-1.5-pro` model must not be called in automated discovery loops due to its strict 2 RPM / 50 RPD limit.

---

## 3. Mandatory Architectural Cost Controls

### 3.1 Local Client-Side Rate Limiter & Token Counter
Both desktop and mobile clients maintain an in-memory and persisted request ledger:
1. **Per-Minute Window:** Hard limit set to **12 RPM** (providing a safety buffer below the 15 RPM provider limit). Requests exceeding 12 RPM are queued locally.
2. **Per-Day Ledger:** Hard limit set to **1,200 RPD** (safety buffer below the 1,500 RPD limit). Once 1,200 requests have been issued in a 24-hour UTC window, all AI features enter a **`FreeQuotaExhausted`** paused state until the next UTC day.
3. **No Automatic Fallback:** If a request fails with HTTP 429 (Too Many Requests), the client will **not** attempt to switch to another paid model or paid provider. It will notify the user with a clear visual message and pause further requests.

### 3.2 Emergency Kill Switch
1. A prominent, globally accessible **"Disable AI"** toggle is present on the status bar and Settings screen of both Desktop and Mobile applications.
2. Activating this toggle immediately cancels all pending AI tasks in the local queue and rejects any subsequent requests before any network socket is opened.

### 3.3 Prompt Caching & Deduplication
1. Prompts for identical business profiles and audit reports are hashed (SHA-256).
2. If an audit report has not changed and an AI analysis already exists for that hash in `ai_analyses`, the local cached summary is returned without issuing an API call.

---

## 4. Privacy & Data Minimization

Under Google AI Studio Free Tier terms, submitted prompts may be processed and reviewed to improve Google products. Therefore, TKS LeadPilot enforces strict data sanitization:

1. **Redaction of Private Records:** Customer internal databases, employee records, proprietary business proposals, and confidential notes are **never** included in AI prompts.
2. **Minimal Public Footprint:** AI prompts include only publicly discoverable data (public business name, city, public website URL, and technical audit findings).
3. **Regex Secret Scrubber:** Before transmission, all prompt text passes through a sanitization filter that strips API keys, JWT tokens, email credentials, and private file paths.
4. **Advisory Role Only:** AI output is strictly advisory. Scoring calculations, database merges, and message sending are never executed autonomously by AI.
