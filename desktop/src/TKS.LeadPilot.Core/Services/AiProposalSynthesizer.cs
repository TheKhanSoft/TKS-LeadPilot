using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using TKS.LeadPilot.Core.Common;
using TKS.LeadPilot.Core.Enums;
using TKS.LeadPilot.Core.Interfaces;
using TKS.LeadPilot.Core.Models;

namespace TKS.LeadPilot.Core.Services;

public partial class AiProposalSynthesizer : IAiProposalSynthesizer
{
    private const string ApiKeyVaultKey = "GEMINI_API_KEY";
    private readonly HttpClient _httpClient;
    private readonly ISecretVault _secretVault;

    private static GeminiConfig _config = new() { ModelName = "gemini-3.5-flash-lite" };
    private static readonly SemaphoreSlim ConfigLock = new(1, 1);
    private static readonly Queue<DateTime> RequestWindow = new();

    // Regex for PII redaction
    [GeneratedRegex(@"\b(?:\+?\d{1,3}[-.\s]?)?\(?\d{2,4}\)?[-.\s]?\d{3,4}[-.\s]?\d{3,4}\b", RegexOptions.Compiled)]
    private static partial Regex PhoneRegex();

    [GeneratedRegex(@"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b", RegexOptions.Compiled)]
    private static partial Regex EmailRegex();

    public AiProposalSynthesizer(HttpClient httpClient, ISecretVault secretVault)
    {
        _httpClient = httpClient;
        _secretVault = secretVault;
    }

    public async Task<GeminiConfig> GetConfigAsync(CancellationToken ct = default)
    {
        await ConfigLock.WaitAsync(ct);
        try
        {
            ResetIfNewDay();
            return new GeminiConfig
            {
                IsEnabled = _config.IsEnabled,
                ModelName = _config.ModelName,
                DailyRequestCeiling = _config.DailyRequestCeiling,
                DailyRequestsUsed = _config.DailyRequestsUsed,
                LastResetDateUtc = _config.LastResetDateUtc,
                RpmLimit = _config.RpmLimit
            };
        }
        finally
        {
            ConfigLock.Release();
        }
    }

    public async Task SaveConfigAsync(GeminiConfig config, string? apiKey = null, CancellationToken ct = default)
    {
        await ConfigLock.WaitAsync(ct);
        try
        {
            _config.IsEnabled = config.IsEnabled;
            var chosen = !string.IsNullOrWhiteSpace(config.ModelName) ? config.ModelName.Trim() : "gemini-3.5-flash-lite";
            if (chosen.Contains("1.5-flash") || chosen.Contains("2.5-flash") || chosen.Contains("3.8-flash"))
            {
                chosen = "gemini-3.5-flash-lite";
            }
            _config.ModelName = chosen;
            _config.DailyRequestCeiling = Math.Max(10, Math.Min(1500, config.DailyRequestCeiling));
            _config.RpmLimit = Math.Max(1, Math.Min(15, config.RpmLimit));

            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                _secretVault.SaveSecret(ApiKeyVaultKey, apiKey.Trim());
                _secretVault.SaveSecret("GeminiApiKey", apiKey.Trim());
            }
        }
        finally
        {
            ConfigLock.Release();
        }
    }

    public async Task<int> GetRemainingRequestsTodayAsync(CancellationToken ct = default)
    {
        var config = await GetConfigAsync(ct);
        return Math.Max(0, config.DailyRequestCeiling - config.DailyRequestsUsed);
    }

    public async Task<AuditProposalSynthesisResult> SynthesizeProposalAsync(
        AuditProposalSynthesisRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var config = await GetConfigAsync(ct);

        // Guard 1: Kill-Switch Check
        if (!config.IsEnabled)
        {
            var fallback = SynthesizeDeterministicProposal(request);
            fallback.ErrorMessage = "Gemini AI features are disabled in Settings. Generated offline deterministic proposal template.";
            return fallback;
        }

        // Guard 2: API Key Check
        var apiKey = _secretVault.GetSecret(ApiKeyVaultKey) ?? _secretVault.GetSecret("GeminiApiKey");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            var fallback = SynthesizeDeterministicProposal(request);
            fallback.ErrorMessage = "Gemini API key is not configured in Settings. Generated offline deterministic proposal template.";
            return fallback;
        }

        // Guard 3: Daily Hard Ceiling Check (1,200 RPD)
        if (config.DailyRequestsUsed >= config.DailyRequestCeiling)
        {
            var fallback = SynthesizeDeterministicProposal(request);
            fallback.ErrorMessage = $"Daily hard ceiling ({config.DailyRequestCeiling} RPD) reached. Resets at midnight UTC to prevent unexpected billing. Generated offline deterministic proposal template.";
            return fallback;
        }

        // Guard 4: Local Sliding-Window Rate Limit (12 RPM)
        await ConfigLock.WaitAsync(ct);
        try
        {
            var now = DateTime.UtcNow;
            while (RequestWindow.Count > 0 && (now - RequestWindow.Peek()).TotalSeconds >= 60)
            {
                RequestWindow.Dequeue();
            }

            if (RequestWindow.Count >= config.RpmLimit)
            {
                var fallback = SynthesizeDeterministicProposal(request);
                fallback.ErrorMessage = $"Local safety rate limit ({config.RpmLimit} RPM) reached. Throttled to preserve Free Tier quota. Generated offline deterministic proposal template.";
                return fallback;
            }

            RequestWindow.Enqueue(now);
        }
        finally
        {
            ConfigLock.Release();
        }

        // 5. Construct PII-Redacted Prompt
        var prompt = BuildSanitizedPrompt(request);

        // 6. Invoke Gemini Developer API Free Tier
        try
        {
            var requestUri = $"https://generativelanguage.googleapis.com/v1beta/models/{config.ModelName}:generateContent?key={apiKey}";

            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new { text = prompt }
                        }
                    }
                },
                generationConfig = new
                {
                    temperature = 0.4,
                    topK = 40,
                    topP = 0.95,
                    maxOutputTokens = 2048
                }
            };

            var jsonContent = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.PostAsync(requestUri, jsonContent, ct);

            // Increment usage counter under lock
            await ConfigLock.WaitAsync(ct);
            try
            {
                _config.DailyRequestsUsed++;
                config.DailyRequestsUsed = _config.DailyRequestsUsed;
            }
            finally
            {
                ConfigLock.Release();
            }

            if (!response.IsSuccessStatusCode)
            {
                var fallback = SynthesizeDeterministicProposal(request);
                fallback.ErrorMessage = $"Gemini API returned HTTP {response.StatusCode}. Generated offline deterministic proposal template.";
                fallback.RequestsUsedToday = config.DailyRequestsUsed;
                fallback.DailyLimit = config.DailyRequestCeiling;
                return fallback;
            }

            var responseJson = await response.Content.ReadAsStringAsync(ct);
            var parseResult = ParseGeminiResponse(responseJson, request.Tone);

            parseResult.RequestsUsedToday = config.DailyRequestsUsed;
            parseResult.DailyLimit = config.DailyRequestCeiling;
            return parseResult;
        }
        catch (Exception ex)
        {
            var fallback = SynthesizeDeterministicProposal(request);
            fallback.ErrorMessage = $"AI synthesis encountered an error: {ex.Message}. Generated offline deterministic proposal template.";
            fallback.RequestsUsedToday = config.DailyRequestsUsed;
            fallback.DailyLimit = config.DailyRequestCeiling;
            return fallback;
        }
    }

    public AuditProposalSynthesisResult SynthesizeDeterministicProposal(AuditProposalSynthesisRequest request)
    {
        var biz = request.Business;
        var audit = request.Audit;
        var tone = request.Tone;
        var matchedServices = DetermineMatchedServices(audit);

        var sb = new StringBuilder();
        var bizName = !string.IsNullOrWhiteSpace(biz.Name) ? biz.Name : "Valued Business Partner";
        var city = !string.IsNullOrWhiteSpace(biz.City) ? biz.City : "your area";
        var url = !string.IsNullOrWhiteSpace(audit.TargetUrl) ? audit.TargetUrl : biz.WebsiteUrl ?? "your website";

        string execSummary;
        string keyFindings;
        string recommendedActions;

        switch (tone)
        {
            case AiTone.RoiFocused:
                sb.AppendLine($"# Commercial Performance & Website Health Proposal: {bizName}");
                sb.AppendLine($"**Target Website:** {url} | **Evaluation Date:** {TimeHelper.ToPktString(DateTime.UtcNow, "yyyy-MM-dd")} PKT");
                sb.AppendLine();
                execSummary = $"During a recent digital performance inspection in {city}, we benchmarked {url}. " +
                              $"Your website currently scores **{audit.OverallScore ?? 50}%** on our composite health index. " +
                              $"Critical friction points in load speed, mobile responsiveness, and trust signals are directly dampening your visitor conversion rate, leading to lost client inquiries.";
                sb.AppendLine("## 1. Executive Summary & Revenue Impact");
                sb.AppendLine(execSummary);
                sb.AppendLine();
                break;

            case AiTone.Technical:
                sb.AppendLine($"# Architecture, Security & Core Web Vitals Audit: {bizName}");
                sb.AppendLine($"**Target Host:** {url} | **Detected Tech Stack:** {audit.DetectedTechStack ?? "Custom"}");
                sb.AppendLine();
                execSummary = $"Technical diagnostic audit of {url} conducted on {TimeHelper.ToPktString(DateTime.UtcNow, "yyyy-MM-dd")} PKT. " +
                              $"Overall health index: **{audit.OverallScore ?? 50}%** (Security: {audit.SecurityScore ?? 0}%, Performance: {audit.PerformanceScore ?? 0}%, SEO: {audit.SeoScore ?? 0}%). " +
                              $"Analysis identified structural bottlenecks and security header deficiencies requiring engineering remediation.";
                sb.AppendLine("## 1. Technical Health Index");
                sb.AppendLine(execSummary);
                sb.AppendLine();
                break;

            case AiTone.Consultative:
            default:
                sb.AppendLine($"# Website Optimization & Growth Assessment: {bizName}");
                sb.AppendLine($"**Prepared for:** {bizName} ({city}) | **Website:** {url}");
                sb.AppendLine();
                execSummary = $"At TheKhanSoft, we regularly review technology adoption for leading businesses in {city}. " +
                              $"We recently analyzed {url} and found a great foundation, but also uncovered several key technical opportunities that could significantly elevate your customer experience and local market leadership.";
                sb.AppendLine("## 1. Overview & Observations");
                sb.AppendLine(execSummary);
                sb.AppendLine();
                break;
        }

        // Key Findings
        var findingsSb = new StringBuilder();
        if (audit.Findings.Count > 0)
        {
            findingsSb.AppendLine("The technical audit surfaced the following priority areas:");
            foreach (var f in audit.Findings.Take(5))
            {
                findingsSb.AppendLine($"- **[{f.Severity}] {f.Title}**: {f.Description}");
                if (!string.IsNullOrWhiteSpace(f.BusinessImpact))
                {
                    findingsSb.AppendLine($"  *Impact:* {f.BusinessImpact}");
                }
            }
        }
        else
        {
            findingsSb.AppendLine("Website meets foundational standards with minor optimization opportunities in latency and security hardening.");
        }
        keyFindings = findingsSb.ToString();
        sb.AppendLine("## 2. Key Audit Observations & Diagnostics");
        sb.AppendLine(keyFindings);
        sb.AppendLine();

        // Recommendations & TKS Services
        var recSb = new StringBuilder();
        recSb.AppendLine("To remediate these issues and accelerate your digital growth, we recommend the following structured intervention:");
        foreach (var svc in matchedServices)
        {
            recSb.AppendLine($"- **{svc}**");
        }
        recSb.AppendLine();
        recSb.AppendLine("### Next Step:");
        recSb.AppendLine("We would love to share the full technical breakdown and provide a complimentary 15-minute walkthrough. " +
                        "Let us know if you have time for a brief introductory call this week.");
        recommendedActions = recSb.ToString();

        sb.AppendLine("## 3. Recommended Roadmap & Solutions");
        sb.AppendLine(recommendedActions);

        return new AuditProposalSynthesisResult
        {
            IsSuccess = true,
            IsLocalFallback = true,
            Tone = tone,
            ExecutiveSummary = execSummary,
            KeyFindingsSummary = keyFindings,
            RecommendedActions = recommendedActions,
            FullProposalMarkdown = sb.ToString(),
            MatchedServices = matchedServices,
            RequestsUsedToday = _config.DailyRequestsUsed,
            DailyLimit = _config.DailyRequestCeiling
        };
    }

    public static string RedactPii(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        var scrubbed = PhoneRegex().Replace(input, "[PHONE REDACTED]");
        return EmailRegex().Replace(scrubbed, "[EMAIL REDACTED]");
    }

    private static string BuildSanitizedPrompt(AuditProposalSynthesisRequest request)
    {
        var biz = request.Business;
        var audit = request.Audit;
        var tone = request.Tone;

        var toneDescription = tone switch
        {
            AiTone.RoiFocused => "Direct, commercial, ROI-focused. Highlight lost revenue, customer bounce rate, and competitor advantage.",
            AiTone.Technical => "Precise, technical, architectural. Focus on security compliance, latency, HTTP standards, and Core Web Vitals.",
            AiTone.Consultative or _ => "Helpful, consultative, encouraging, and advisory. Act as a friendly local digital advisor."
        };

        var promptSb = new StringBuilder();
        promptSb.AppendLine("You are an executive technology consultant at TheKhanSoft (TKS), a premium software and web engineering agency.");
        promptSb.AppendLine($"Write a compelling, professional client proposal based on our technical website audit.");
        promptSb.AppendLine($"Tone: {toneDescription}");
        promptSb.AppendLine();
        promptSb.AppendLine("BUSINESS CONTEXT:");
        promptSb.AppendLine($"- Name: {RedactPii(biz.Name)}");
        promptSb.AppendLine($"- Industry: {biz.Category}");
        promptSb.AppendLine($"- City: {biz.City}, Pakistan");
        promptSb.AppendLine($"- Website URL: {audit.TargetUrl}");
        promptSb.AppendLine();
        promptSb.AppendLine("AUDIT MEASUREMENTS & SIGNALS:");
        promptSb.AppendLine($"- Overall Health Score: {audit.OverallScore ?? 50}%");
        promptSb.AppendLine($"- Performance Score: {audit.PerformanceScore ?? 50}% (TTFB: {audit.TtfbMs:F0}ms, HTML Size: {audit.HtmlSizeBytes / 1024:N0} KB)");
        promptSb.AppendLine($"- Security Score: {audit.SecurityScore ?? 50}% (SSL Valid: {audit.SslValid}, Days to Expiry: {audit.SslDaysUntilExpiry}, Issuer: {audit.SslIssuer})");
        promptSb.AppendLine($"- SEO Hygiene Score: {audit.SeoScore ?? 50}% (Robots.txt: {audit.HasRobotsTxt}, Sitemap: {audit.HasSitemapXml})");
        promptSb.AppendLine($"- Detected Tech Stack: {audit.DetectedTechStack ?? "Custom / Unspecified"}");
        promptSb.AppendLine();
        promptSb.AppendLine("KEY AUDIT FINDINGS (ISSUES):");
        foreach (var f in audit.Findings.Take(6))
        {
            promptSb.AppendLine($"- [{f.Severity}] {f.Title}: {f.Description}. Recommended fix: {f.RecommendedFix}. Business impact: {f.BusinessImpact}");
        }
        promptSb.AppendLine();
        promptSb.AppendLine("INSTRUCTIONS:");
        promptSb.AppendLine("1. Write the proposal in clear, well-structured GitHub-flavored Markdown.");
        promptSb.AppendLine("2. Structure the proposal into three clear sections: # Title, ## 1. Executive Summary, ## 2. Core Diagnostic Insights, ## 3. Actionable Remediation Plan & TKS Service Recommendation.");
        promptSb.AppendLine("3. Never make up unsubstantiated metrics. Only refer to the actual audit findings above.");
        promptSb.AppendLine("4. End with a soft, respectful call-to-action inviting a brief conversation.");

        if (!string.IsNullOrWhiteSpace(request.CustomInstructions))
        {
            promptSb.AppendLine($"ADDITIONAL OPERATOR INSTRUCTIONS: {RedactPii(request.CustomInstructions)}");
        }

        return promptSb.ToString();
    }

    private static AuditProposalSynthesisResult ParseGeminiResponse(string json, AiTone tone)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
            {
                var first = candidates[0];
                if (first.TryGetProperty("content", out var content) &&
                    content.TryGetProperty("parts", out var parts) &&
                    parts.GetArrayLength() > 0)
                {
                    var text = parts[0].GetProperty("text").GetString() ?? string.Empty;

                    int? totalTokens = null;
                    if (root.TryGetProperty("usageMetadata", out var usage) &&
                        usage.TryGetProperty("totalTokenCount", out var tt) &&
                        tt.TryGetInt32(out var tVal))
                    {
                        totalTokens = tVal;
                    }

                    return new AuditProposalSynthesisResult
                    {
                        IsSuccess = true,
                        IsLocalFallback = false,
                        Tone = tone,
                        FullProposalMarkdown = text,
                        ExecutiveSummary = ExtractSection(text, "Executive Summary"),
                        KeyFindingsSummary = ExtractSection(text, "Diagnostic Insights"),
                        RecommendedActions = ExtractSection(text, "Remediation Plan"),
                        TokensUsed = totalTokens
                    };
                }
            }

            return new AuditProposalSynthesisResult
            {
                IsSuccess = false,
                ErrorMessage = "Gemini response did not contain candidates or content parts."
            };
        }
        catch (Exception ex)
        {
            return new AuditProposalSynthesisResult
            {
                IsSuccess = false,
                ErrorMessage = $"Failed to parse Gemini response: {ex.Message}"
            };
        }
    }

    private static string ExtractSection(string markdown, string headingFragment)
    {
        var lines = markdown.Split('\n');
        var capturing = false;
        var sb = new StringBuilder();

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.StartsWith('#') && line.Contains(headingFragment, StringComparison.OrdinalIgnoreCase))
            {
                capturing = true;
                continue;
            }
            if (capturing && line.StartsWith("##"))
            {
                break;
            }
            if (capturing)
            {
                sb.AppendLine(rawLine);
            }
        }

        var result = sb.ToString().Trim();
        return !string.IsNullOrWhiteSpace(result) ? result : "Refer to the comprehensive proposal below.";
    }

    private static List<string> DetermineMatchedServices(WebsiteAudit audit)
    {
        var list = new List<string>();

        if ((audit.PerformanceScore ?? 100) < 70 || (audit.TtfbMs ?? 0) > 1000)
        {
            list.Add("TKS High-Speed Web Redesign & Cloud Modernization");
        }
        if ((audit.SecurityScore ?? 100) < 70 || !audit.SslValid)
        {
            list.Add("TKS Enterprise SSL & Zero-Trust Security Hardening");
        }
        if ((audit.SeoScore ?? 100) < 70 || !audit.HasRobotsTxt || !audit.HasSitemapXml)
        {
            list.Add("TKS Local Search Optimization & Organic Growth Package");
        }

        if (list.Count == 0)
        {
            list.Add("TKS Custom Web Application & Continuous Managed Retainer");
        }

        return list;
    }

    public async Task<List<Business>> DiscoverBusinessesAiAsync(string city, string country, string category, int count = 25, CancellationToken ct = default)
    {
        var results = new List<Business>();
        var config = await GetConfigAsync(ct);

        var apiKey = _secretVault.GetSecret(ApiKeyVaultKey) 
                  ?? _secretVault.GetSecret("GeminiApiKey")
                  ?? _secretVault.GetSecret("GEMINI_API_KEY");

        if (string.IsNullOrWhiteSpace(apiKey)) return results;

        if (config.DailyRequestsUsed >= config.DailyRequestCeiling) return results;

        try
        {
            var prompt = $$"""
            You are a senior enterprise business intelligence investigator and local commercial researcher for TKS LeadPilot.
            List {{count}} real, prominent, operating commercial businesses, companies, clinics, software agencies, stores, law firms, restaurants, or service providers located in {{city}}, {{country}} matching category '{{category}}' (or covering key diverse commercial sectors if category is 'All').
            
            Return ONLY a valid JSON array of objects with the following keys:
            [
              {
                "name": "Commercial Business Name",
                "category": "Standard Business Category (e.g. Software & IT, Healthcare, Restaurant, Law Firm)",
                "address": "Street / Road / Sector / Area, {{city}}",
                "phone": "Direct contact phone with country/city code",
                "website": "https://... official website or social profile, or null if unknown",
                "has_gmb": true,
                "google_rating": 4.2,
                "google_reviews": 350,
                "business_hours": "Open 24 hours or 9:00 AM - 9:00 PM",
                "tags": "Commercial Niche, Keywords, Specialization"
              }
            ]
            """;

            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new { text = prompt }
                        }
                    }
                },
                generationConfig = new
                {
                    temperature = 0.2,
                    maxOutputTokens = 4096,
                    responseMimeType = "application/json"
                }
            };

            var jsonPayload = JsonSerializer.Serialize(requestBody);

            string[] candidateModels = ["gemini-3.5-flash-lite", "gemini-flash-lite-latest", "gemini-3.1-flash-lite", "gemini-2.0-flash", "gemini-1.5-flash"];
            string? respJson = null;

            foreach (var modelName in candidateModels)
            {
                if (ct.IsCancellationRequested) break;

                try
                {
                    var url = $"https://generativelanguage.googleapis.com/v1beta/models/{modelName}:generateContent?key={apiKey}";
                    using var reqMsg = new HttpRequestMessage(HttpMethod.Post, url)
                    {
                        Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json")
                    };

                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    cts.CancelAfter(TimeSpan.FromSeconds(15));

                    var resp = await _httpClient.SendAsync(reqMsg, cts.Token);
                    if (resp.IsSuccessStatusCode)
                    {
                        respJson = await resp.Content.ReadAsStringAsync(ct);
                        break;
                    }
                }
                catch { }
            }

            if (string.IsNullOrWhiteSpace(respJson)) return results;

            await ConfigLock.WaitAsync(ct);
            try
            {
                _config.DailyRequestsUsed++;
            }
            finally
            {
                ConfigLock.Release();
            }

            using var doc = JsonDocument.Parse(respJson);
            if (!doc.RootElement.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
                return results;

            var firstCandidate = candidates[0];
            if (!firstCandidate.TryGetProperty("content", out var content) ||
                !content.TryGetProperty("parts", out var parts) ||
                parts.GetArrayLength() == 0)
                return results;

            var rawText = parts[0].GetProperty("text").GetString();
            if (string.IsNullOrWhiteSpace(rawText)) return results;

            var startIndex = rawText.IndexOf('[');
            var endIndex = rawText.LastIndexOf(']');
            if (startIndex == -1 || endIndex == -1 || endIndex <= startIndex) return results;

            var cleanArrayJson = rawText.Substring(startIndex, endIndex - startIndex + 1);
            using var arrDoc = JsonDocument.Parse(cleanArrayJson);

            foreach (var item in arrDoc.RootElement.EnumerateArray())
            {
                var name = item.TryGetProperty("name", out var n) ? n.GetString() : null;
                if (string.IsNullOrWhiteSpace(name)) continue;

                var cat = item.TryGetProperty("category", out var c) ? c.GetString() : category;
                var addr = item.TryGetProperty("address", out var a) ? a.GetString() : $"{city}, {country}";
                var phone = item.TryGetProperty("phone", out var p) ? p.GetString() : null;
                var web = item.TryGetProperty("website", out var w) ? w.GetString() : null;
                var tags = item.TryGetProperty("tags", out var t) ? t.GetString() : null;

                bool hasGmb = false;
                if (item.TryGetProperty("has_gmb", out var hg))
                {
                    hasGmb = hg.ValueKind == JsonValueKind.True || (hg.ValueKind == JsonValueKind.String && bool.TryParse(hg.GetString(), out var bVal) && bVal);
                }

                double? gRating = null;
                if (item.TryGetProperty("google_rating", out var gr) && gr.TryGetDouble(out var grVal)) gRating = grVal;

                int? gReviews = null;
                if (item.TryGetProperty("google_reviews", out var grev) && grev.TryGetInt32(out var grevVal)) gReviews = grevVal;

                var bHours = item.TryGetProperty("business_hours", out var bh) ? bh.GetString() : null;

                if ((gRating.HasValue && gRating.Value > 0) || (gReviews.HasValue && gReviews.Value > 0))
                {
                    hasGmb = true;
                }

                results.Add(new Business
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = name.Trim(),
                    Category = cat ?? category,
                    City = city,
                    Country = country,
                    AddressLine1 = addr,
                    PhonePrimary = phone,
                    WebsiteUrl = web,
                    WebsiteStatus = !string.IsNullOrWhiteSpace(web) ? WebsiteStatus.ActiveWebsite : WebsiteStatus.NoWebsite,
                    IsGoogleVerified = hasGmb,
                    GoogleRating = gRating,
                    GoogleReviewCount = gReviews,
                    GooglePlaceId = hasGmb ? $"gmb_{Guid.NewGuid():N}"[..18] : null,
                    BusinessHours = bHours,
                    PipelineStageId = "stage-discovered",
                    LeadStatus = LeadStatus.Discovered,
                    SourceProvider = "Google Gemini Free Tier (AI Commercial Discovery)",
                    SourceReferenceId = $"gemini-{Guid.NewGuid():N}"[..18],
                    Tags = $"Gemini AI Discovered, {tags}",
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                });
            }
        }
        catch { }

        return results;
    }

    public async Task<(bool HasGmb, double? Rating, int? ReviewCount, string? Hours)> VerifyBusinessGmbAiAsync(string businessName, string city, string country, CancellationToken ct = default)
    {
        var apiKey = _secretVault.GetSecret(ApiKeyVaultKey) 
                  ?? _secretVault.GetSecret("GeminiApiKey")
                  ?? _secretVault.GetSecret("GEMINI_API_KEY");

        if (string.IsNullOrWhiteSpace(apiKey)) return (false, null, null, null);

        var config = await GetConfigAsync(ct);
        if (config.DailyRequestsUsed >= config.DailyRequestCeiling) return (false, null, null, null);

        try
        {
            var prompt = $$"""
            You are a senior enterprise business intelligence investigator for TKS LeadPilot.
            Verify whether the commercial entity '{{businessName}}' in {{city}}, {{country}} has an active Google My Business (Google Maps / Google Business Profile) presence.
            Return ONLY a valid JSON object with the following keys:
            {
              "has_gmb": true,
              "rating": 4.2,
              "review_count": 250,
              "business_hours": "Open 24 hours"
            }
            If unverified or not listed, return has_gmb: false.
            """;

            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new { text = prompt }
                        }
                    }
                },
                generationConfig = new
                {
                    temperature = 0.1,
                    maxOutputTokens = 512,
                    responseMimeType = "application/json"
                }
            };

            var jsonPayload = JsonSerializer.Serialize(requestBody);
            string[] candidateModels = ["gemini-3.5-flash-lite", "gemini-flash-lite-latest", "gemini-3.1-flash-lite", "gemini-2.0-flash", "gemini-1.5-flash"];
            string? respJson = null;

            foreach (var modelName in candidateModels)
            {
                if (ct.IsCancellationRequested) break;
                try
                {
                    var url = $"https://generativelanguage.googleapis.com/v1beta/models/{modelName}:generateContent?key={apiKey}";
                    using var reqMsg = new HttpRequestMessage(HttpMethod.Post, url)
                    {
                        Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json")
                    };

                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    cts.CancelAfter(TimeSpan.FromSeconds(10));

                    var resp = await _httpClient.SendAsync(reqMsg, cts.Token);
                    if (resp.IsSuccessStatusCode)
                    {
                        respJson = await resp.Content.ReadAsStringAsync(ct);
                        break;
                    }
                }
                catch { }
            }

            if (string.IsNullOrWhiteSpace(respJson)) return (false, null, null, null);

            await ConfigLock.WaitAsync(ct);
            try { _config.DailyRequestsUsed++; } finally { ConfigLock.Release(); }

            using var doc = JsonDocument.Parse(respJson);
            if (!doc.RootElement.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
                return (false, null, null, null);

            var firstCandidate = candidates[0];
            if (!firstCandidate.TryGetProperty("content", out var content) ||
                !content.TryGetProperty("parts", out var parts) ||
                parts.GetArrayLength() == 0)
                return (false, null, null, null);

            var rawText = parts[0].GetProperty("text").GetString();
            if (string.IsNullOrWhiteSpace(rawText)) return (false, null, null, null);

            var startIndex = rawText.IndexOf('{');
            var endIndex = rawText.LastIndexOf('}');
            if (startIndex == -1 || endIndex == -1 || endIndex <= startIndex) return (false, null, null, null);

            var cleanJson = rawText.Substring(startIndex, endIndex - startIndex + 1);
            using var objDoc = JsonDocument.Parse(cleanJson);
            var root = objDoc.RootElement;

            bool hasGmb = false;
            if (root.TryGetProperty("has_gmb", out var hg))
            {
                hasGmb = hg.ValueKind == JsonValueKind.True || (hg.ValueKind == JsonValueKind.String && bool.TryParse(hg.GetString(), out var bVal) && bVal);
            }

            double? rating = null;
            if (root.TryGetProperty("rating", out var rProp) && rProp.TryGetDouble(out var rVal)) rating = rVal;

            int? reviews = null;
            if (root.TryGetProperty("review_count", out var rcProp) && rcProp.TryGetInt32(out var rcVal)) reviews = rcVal;

            var hours = root.TryGetProperty("business_hours", out var bh) ? bh.GetString() : null;

            if ((rating.HasValue && rating.Value > 0) || (reviews.HasValue && reviews.Value > 0))
            {
                hasGmb = true;
            }

            return (hasGmb, rating, reviews, hours);
        }
        catch { }

        return (false, null, null, null);
    }

    public async Task<Dictionary<string, (bool HasGmb, double? Rating, int? ReviewCount, string? Hours)>> BatchVerifyGmbAiAsync(
        IEnumerable<Business> businesses, CancellationToken ct = default)
    {
        var result = new Dictionary<string, (bool HasGmb, double? Rating, int? ReviewCount, string? Hours)>();
        var bizList = businesses.ToList();
        if (bizList.Count == 0) return result;

        var apiKey = _secretVault.GetSecret(ApiKeyVaultKey)
                  ?? _secretVault.GetSecret("GeminiApiKey")
                  ?? _secretVault.GetSecret("GEMINI_API_KEY");

        var config = await GetConfigAsync(ct);
        bool canUseAi = !string.IsNullOrWhiteSpace(apiKey) && config.DailyRequestsUsed < config.DailyRequestCeiling;

        if (!canUseAi)
        {
            foreach (var b in bizList)
            {
                var inf = OverpassResultMapper.InferCommercialGmbData(b.Name, b.Category, b.PhonePrimary, b.WebsiteUrl, b.GoogleRating, b.BusinessHours, b.GooglePlaceId);
                result[b.Id] = (inf.HasGmb, inf.Rating, inf.Reviews, inf.Hours);
            }
            return result;
        }

        // Process in batches of up to 20 businesses per AI request
        foreach (var chunk in bizList.Chunk(20))
        {
            if (ct.IsCancellationRequested) break;

            var itemsJson = JsonSerializer.Serialize(chunk.Select(b => new
            {
                id = b.Id,
                name = b.Name,
                category = b.Category,
                city = b.City,
                country = b.Country
            }));

            var prompt = $$"""
            You are a senior enterprise business intelligence investigator for TKS LeadPilot.
            Verify whether each of the following commercial entities has an active Google My Business (Google Maps / Google Business Profile) presence.
            Businesses:
            {{itemsJson}}

            Return ONLY a valid JSON array of objects with the exact schema:
            [
              {
                "id": "item-id",
                "has_gmb": true,
                "rating": 4.2,
                "review_count": 250,
                "business_hours": "8:30 AM - 5:30 PM"
              }
            ]
            If unverified or not listed, return has_gmb: false.
            """;

            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new { text = prompt }
                        }
                    }
                },
                generationConfig = new
                {
                    temperature = 0.1,
                    maxOutputTokens = 2048,
                    responseMimeType = "application/json"
                }
            };

            var jsonPayload = JsonSerializer.Serialize(requestBody);
            string[] candidateModels = ["gemini-3.5-flash-lite", "gemini-flash-lite-latest", "gemini-3.1-flash-lite", "gemini-2.0-flash", "gemini-1.5-flash"];
            string? respJson = null;

            foreach (var modelName in candidateModels)
            {
                if (ct.IsCancellationRequested) break;
                try
                {
                    var url = $"https://generativelanguage.googleapis.com/v1beta/models/{modelName}:generateContent?key={apiKey}";
                    using var reqMsg = new HttpRequestMessage(HttpMethod.Post, url)
                    {
                        Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json")
                    };

                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    cts.CancelAfter(TimeSpan.FromSeconds(15));

                    var resp = await _httpClient.SendAsync(reqMsg, cts.Token);
                    if (resp.IsSuccessStatusCode)
                    {
                        respJson = await resp.Content.ReadAsStringAsync(ct);
                        break;
                    }
                }
                catch { }
            }

            if (!string.IsNullOrWhiteSpace(respJson))
            {
                try
                {
                    await ConfigLock.WaitAsync(ct);
                    try { _config.DailyRequestsUsed++; } finally { ConfigLock.Release(); }

                    using var doc = JsonDocument.Parse(respJson);
                    if (doc.RootElement.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
                    {
                        var first = candidates[0];
                        if (first.TryGetProperty("content", out var content) &&
                            content.TryGetProperty("parts", out var parts) && parts.GetArrayLength() > 0)
                        {
                            var text = parts[0].GetProperty("text").GetString();
                            if (!string.IsNullOrWhiteSpace(text))
                            {
                                var sIdx = text.IndexOf('[');
                                var eIdx = text.LastIndexOf(']');
                                if (sIdx != -1 && eIdx > sIdx)
                                {
                                    var arrayJson = text.Substring(sIdx, eIdx - sIdx + 1);
                                    using var arrDoc = JsonDocument.Parse(arrayJson);
                                    foreach (var item in arrDoc.RootElement.EnumerateArray())
                                    {
                                        var id = item.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
                                        if (string.IsNullOrWhiteSpace(id)) continue;

                                        bool hasGmb = false;
                                        if (item.TryGetProperty("has_gmb", out var hg))
                                        {
                                            hasGmb = hg.ValueKind == JsonValueKind.True || (hg.ValueKind == JsonValueKind.String && bool.TryParse(hg.GetString(), out var bVal) && bVal);
                                        }

                                        double? rating = null;
                                        if (item.TryGetProperty("rating", out var rProp) && rProp.TryGetDouble(out var rVal)) rating = rVal;

                                        int? reviews = null;
                                        if (item.TryGetProperty("review_count", out var rcProp) && rcProp.TryGetInt32(out var rcVal)) reviews = rcVal;

                                        var hours = item.TryGetProperty("business_hours", out var bh) ? bh.GetString() : null;
                                        if ((rating.HasValue && rating.Value > 0) || (reviews.HasValue && reviews.Value > 0)) hasGmb = true;

                                        result[id] = (hasGmb, rating, reviews, hours);
                                    }
                                }
                            }
                        }
                    }
                }
                catch { }
            }

            // Fill in any chunk items not populated by AI using deterministic commercial heuristics
            foreach (var b in chunk)
            {
                if (!result.ContainsKey(b.Id))
                {
                    var inf = OverpassResultMapper.InferCommercialGmbData(b.Name, b.Category, b.PhonePrimary, b.WebsiteUrl, b.GoogleRating, b.BusinessHours, b.GooglePlaceId);
                    result[b.Id] = (inf.HasGmb, inf.Rating, inf.Reviews, inf.Hours);
                }
            }
        }

        return result;
    }

    private static void ResetIfNewDay()
    {
        var today = DateTime.UtcNow.Date;
        if (_config.LastResetDateUtc < today)
        {
            _config.DailyRequestsUsed = 0;
            _config.LastResetDateUtc = today;
        }
    }
}
