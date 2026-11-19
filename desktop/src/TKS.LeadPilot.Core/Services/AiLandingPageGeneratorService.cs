using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TKS.LeadPilot.Core.Interfaces;
using TKS.LeadPilot.Core.Models;

namespace TKS.LeadPilot.Core.Services;

public class AiLandingPageGeneratorService(
    HttpClient httpClient,
    ISecretVault secretVault) : ILandingPageGeneratorService
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly ISecretVault _secretVault = secretVault;
    private const string ApiKeyVaultKey = "GEMINI_API_KEY";
    private static readonly SemaphoreSlim ConfigLock = new(1, 1);
    private static int _dailyRequestsUsed = 0;

    public IReadOnlyList<LandingPageModuleOption> GetAvailableModules()
    {
        return
        [
            new LandingPageModuleOption
            {
                Id = "mod-hero",
                Name = "Hero Banner & Value Proposition",
                Description = "High-impact headline, subheadline, primary CTA button, rating badge, and background visual.",
                Category = "Conversion",
                IsSelected = true
            },
            new LandingPageModuleOption
            {
                Id = "mod-trust-bar",
                Name = "Trust Badges & Social Proof Bar",
                Description = "Prominent certification emblems, client logos, Google review rating (4.9/5), and press mentions.",
                Category = "Trust",
                IsSelected = true
            },
            new LandingPageModuleOption
            {
                Id = "mod-services",
                Name = "Core Solutions & Services Grid",
                Description = "Interactive cards detailing key services, features, deliverables, and commercial benefits.",
                Category = "Content",
                IsSelected = true
            },
            new LandingPageModuleOption
            {
                Id = "mod-before-after",
                Name = "Transformation & Case Studies",
                Description = "Before & after comparisons, measurable client metrics, and proven outcome benchmarks.",
                Category = "Proof",
                IsSelected = true
            },
            new LandingPageModuleOption
            {
                Id = "mod-team",
                Name = "Leadership & Professional Bio",
                Description = "Founder or lead specialist credentials, certifications, authority story, and headshot highlight.",
                Category = "Authority",
                IsSelected = true
            },
            new LandingPageModuleOption
            {
                Id = "mod-testimonials",
                Name = "Client Testimonials & Reviews",
                Description = "Authentic client reviews with 5-star ratings, author roles, avatars, and verified badges.",
                Category = "Proof",
                IsSelected = true
            },
            new LandingPageModuleOption
            {
                Id = "mod-pricing",
                Name = "Transparent Pricing & Packages",
                Description = "Multi-tier pricing matrix with featured 'Most Popular' plan, checklist, and direct booking CTA.",
                Category = "Conversion",
                IsSelected = true
            },
            new LandingPageModuleOption
            {
                Id = "mod-faq",
                Name = "Interactive FAQ Accordion",
                Description = "Expandable accordion resolving top client objections, pricing questions, and timelines.",
                Category = "Conversion",
                IsSelected = true
            },
            new LandingPageModuleOption
            {
                Id = "mod-lead-form",
                Name = "High-Converting Lead Capture Form",
                Description = "Frictionless inquiry form with instant validation, service dropdown, and confirmation modal.",
                Category = "Conversion",
                IsSelected = true
            },
            new LandingPageModuleOption
            {
                Id = "mod-whatsapp",
                Name = "Floating WhatsApp & Call Widget",
                Description = "Sticky bottom-corner instant messaging widget connecting directly to phone or WhatsApp.",
                Category = "Conversion",
                IsSelected = true
            },
            new LandingPageModuleOption
            {
                Id = "mod-calculator",
                Name = "Interactive ROI / Cost Estimator",
                Description = "Client-side interactive budget or savings slider estimating cost benefits and value.",
                Category = "Interactive",
                IsSelected = false
            },
            new LandingPageModuleOption
            {
                Id = "mod-seo",
                Name = "SEO Schema & Social Meta Tags",
                Description = "LocalBusiness JSON-LD schema, OpenGraph tags, and Twitter Cards for search engines.",
                Category = "SEO",
                IsSelected = true
            }
        ];
    }

    public async Task<LandingPageGenerationResult> GenerateLandingPageAsync(
        LandingPageGenerationRequest request,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        // 1. Check for Gemini API key
        var apiKey = _secretVault.GetSecret(ApiKeyVaultKey) ?? _secretVault.GetSecret("GeminiApiKey");

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            var noKeyFallback = GenerateDeterministicLandingPage(request);
            noKeyFallback.Provider = "TKS Local Neural Synthesizer (Offline Engine)";
            noKeyFallback.ErrorMessage = "No Gemini API Key found in Settings & Vault. Generated ultra-high-fidelity offline landing page and architecture.";
            noKeyFallback.ExecutionDuration = sw.Elapsed;
            return noKeyFallback;
        }

        // 2. Build Gemini Prompt for Bespoke Content Generation
        var prompt = BuildGeminiPrompt(request);

        // Production multi-model fallback chain ordered by current verified latency and quota availability
        string[] candidateModels = ["gemini-3.5-flash-lite", "gemini-flash-lite-latest", "gemini-3.1-flash-lite"];
        HttpResponseMessage? lastResponse = null;
        string lastErrorDetail = string.Empty;

        foreach (var modelName in candidateModels)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                var requestUri = $"https://generativelanguage.googleapis.com/v1beta/models/{modelName}:generateContent?key={apiKey}";

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
                        temperature = 0.25,
                        topK = 40,
                        topP = 0.95,
                        maxOutputTokens = 3500,
                        responseMimeType = "application/json"
                    }
                };

                var jsonContent = new StringContent(
                    JsonSerializer.Serialize(requestBody),
                    Encoding.UTF8,
                    "application/json");

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(25));

                var response = await _httpClient.PostAsync(requestUri, jsonContent, cts.Token);
                lastResponse = response;

                await ConfigLock.WaitAsync(ct);
                try
                {
                    _dailyRequestsUsed++;
                }
                finally
                {
                    ConfigLock.Release();
                }

                if (response.IsSuccessStatusCode)
                {
                    var responseJson = await response.Content.ReadAsStringAsync(ct);
                    var parsed = ParseGeminiResponse(responseJson, request);
                    parsed.Provider = $"Google Gemini Free Tier ({modelName})";
                    parsed.ExecutionDuration = sw.Elapsed;
                    parsed.RequestsUsedToday = _dailyRequestsUsed;
                    return parsed;
                }

                lastErrorDetail = await response.Content.ReadAsStringAsync(ct);

                // If 429 (Resource Exhausted / Quota Limit) or 404 (Model Not Found) or 503, immediately try next model in candidateModels chain
                continue;
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                lastErrorDetail = $"{modelName}: Request timed out after 25s.";
                continue;
            }
            catch (HttpRequestException ex)
            {
                // Network unreachable or DNS resolution failure - fail fast to offline neural synthesizer
                lastErrorDetail = $"{modelName}: Network connectivity error ({ex.Message}).";
                continue;
            }
            catch (Exception ex)
            {
                lastErrorDetail = $"{modelName}: {ex.Message}";
                continue;
            }
        }

        var fallback = GenerateDeterministicLandingPage(request);
        var statusStr = lastResponse != null ? lastResponse.StatusCode.ToString() : (sw.Elapsed.TotalSeconds >= 24 ? "Timeout" : "NetworkError");
        fallback.Provider = $"TKS Local Neural Synthesizer (Gemini HTTP {statusStr} Fallback)";
        fallback.ErrorMessage = !string.IsNullOrWhiteSpace(lastErrorDetail)
            ? $"Gemini returned HTTP {statusStr}: {lastErrorDetail}"
            : $"Gemini API returned status code {statusStr}. Instant offline blueprint deployed.";
        fallback.ExecutionDuration = sw.Elapsed;
        fallback.RequestsUsedToday = _dailyRequestsUsed;
        return fallback;
    }

    private static string BuildGeminiPrompt(LandingPageGenerationRequest request)
    {
        var modulesList = request.SelectedModules.Count > 0
            ? string.Join(", ", request.SelectedModules)
            : "Hero Banner, Trust Badges, Services Grid, Case Studies, Leadership Bio, Pricing Matrix, Testimonials, FAQ Accordion, Lead Capture Form, WhatsApp Float, SEO Schema";

        var sb = new StringBuilder();
        sb.AppendLine("You are TheKhanSoft's Principal Conversion Rate Optimization (CRO) Architect and Senior Lead Copywriter.");
        sb.AppendLine("Generate completely bespoke, unique, highly specific commercial landing page content and marketing architecture for the following client:");
        sb.AppendLine();
        sb.AppendLine($"- Business Name: {request.BusinessName}");
        sb.AppendLine($"- Industry / Niche: {request.Category}");
        sb.AppendLine($"- Location: {request.City}, {request.Country}");
        sb.AppendLine($"- Target Audience Persona: {request.TargetAudience}");
        sb.AppendLine($"- Page Archetype: {request.Archetype}");
        sb.AppendLine($"- Design Style & Color Scheme: {request.DesignStyle}");
        sb.AppendLine($"- Phone / Contact: {request.PhonePrimary ?? "+92 300 1234567"}");
        sb.AppendLine($"- Email: {request.EmailPrimary ?? "info@" + request.BusinessName.ToLowerInvariant().Replace(" ", "") + ".com"}");
        sb.AppendLine($"- Modules Selected: {modulesList}");

        if (!string.IsNullOrWhiteSpace(request.CustomPromptInstructions))
        {
            sb.AppendLine();
            sb.AppendLine($"SPECIAL OPERATOR PROMPT / INSTRUCTIONS:");
            sb.AppendLine($"\"\"\"{request.CustomPromptInstructions}\"\"\"");
        }

        sb.AppendLine();
        sb.AppendLine("CRITICAL INSTRUCTIONS FOR BESPOKE ARCHITECTURE & CONTENT:");
        sb.AppendLine("1. NEVER generate generic placeholders like 'Premium Delivery', 'Specialized Diagnostics & Audit', or fake corporate filler.");
        sb.AppendLine("2. The services must be 4 to 6 REAL, CONCRETE, bespoke commercial offerings tailored to this exact business and niche.");
        sb.AppendLine($"3. Testimonials must be 3 REALISTIC reviews featuring actual local customer names, neighborhood/title context in {request.City}, and specific authentic situations.");
        sb.AppendLine("4. FAQs must resolve 4 to 5 genuine customer objections regarding turnaround, pricing, scope, and process for this exact trade.");
        sb.AppendLine("5. Pricing must be 3 realistic commercial tiers with pricing amounts, period, and feature lists appropriate for this trade.");
        sb.AppendLine("6. Case Studies must feature 2-3 measurable before-and-after client transformations.");
        sb.AppendLine("7. Output MUST be strictly valid JSON conforming to the following structure:");
        sb.AppendLine();
        sb.AppendLine("{");
        sb.AppendLine("  \"headline\": \"Punchy, benefit-driven hero headline tailored to this business in this city\",");
        sb.AppendLine("  \"subheadline\": \"Clear, high-converting subheadline addressing visitor pain points\",");
        sb.AppendLine("  \"hero_badge\": \"Accreditation or ranking highlight e.g. #1 Rated Provider in {City}\",");
        sb.AppendLine("  \"call_to_action\": \"Primary action button text e.g. 'Book Priority Appointment' or 'Schedule Free Consultation'\",");
        sb.AppendLine("  \"secondary_cta\": \"Secondary action button text e.g. 'Direct WhatsApp Chat' or 'Call Now'\",");
        sb.AppendLine("  \"metrics\": [");
        sb.AppendLine("    { \"value\": \"15+ Yrs\", \"label\": \"Local Experience in {City}\" },");
        sb.AppendLine("    { \"value\": \"99.4%\", \"label\": \"Client Satisfaction Rate\" },");
        sb.AppendLine("    { \"value\": \"1,200+\", \"label\": \"Projects / Patients Served\" },");
        sb.AppendLine("    { \"value\": \"4.9 ★\", \"label\": \"Verified Review Score\" }");
        sb.AppendLine("  ],");
        sb.AppendLine("  \"trust_badges\": [\"Google Guaranteed\", \"ISO 9001 Certified\", \"Chamber Member\", \"Licensed Specialists\"],");
        sb.AppendLine("  \"services\": [");
        sb.AppendLine("    {");
        sb.AppendLine("      \"icon\": \"FontAwesome 6 class e.g. fa-solid fa-stethoscope or fa-solid fa-code\",");
        sb.AppendLine("      \"title\": \"Specific Service Title\",");
        sb.AppendLine("      \"description\": \"Detailed 2-sentence description of the deliverable and commercial value.\",");
        sb.AppendLine("      \"cta\": \"Service Action Link Text\"");
        sb.AppendLine("    }");
        sb.AppendLine("  ],");
        sb.AppendLine("  \"differentiators\": [");
        sb.AppendLine("    {");
        sb.AppendLine("      \"icon\": \"fa-solid fa-clock\",");
        sb.AppendLine("      \"title\": \"Unique Advantage Title\",");
        sb.AppendLine("      \"description\": \"Concrete explanation of why this client outperforms competitors.\"");
        sb.AppendLine("    }");
        sb.AppendLine("  ],");
        sb.AppendLine("  \"case_studies\": [");
        sb.AppendLine("    {");
        sb.AppendLine("      \"client_name\": \"Client or Project in {City}\",");
        sb.AppendLine("      \"challenge\": \"Problem or bottleneck faced\",");
        sb.AppendLine("      \"solution\": \"How this business resolved it\",");
        sb.AppendLine("      \"result_metric\": \"+340% Growth / 0-Min Wait / $42k Saved\",");
        sb.AppendLine("      \"result_description\": \"Detailed impact summary\"");
        sb.AppendLine("    }");
        sb.AppendLine("  ],");
        sb.AppendLine("  \"pricing_plans\": [");
        sb.AppendLine("    {");
        sb.AppendLine("      \"name\": \"Starter / Essential\",");
        sb.AppendLine("      \"price\": \"$89\",");
        sb.AppendLine("      \"period\": \"/visit\",");
        sb.AppendLine("      \"badge\": \"\",");
        sb.AppendLine("      \"description\": \"Ideal for straightforward needs\",");
        sb.AppendLine("      \"features\": [\"Feature 1\", \"Feature 2\", \"Feature 3\", \"Feature 4\"],");
        sb.AppendLine("      \"cta\": \"Select Plan\",");
        sb.AppendLine("      \"is_popular\": false");
        sb.AppendLine("    }");
        sb.AppendLine("  ],");
        sb.AppendLine("  \"leader_bio\": {");
        sb.AppendLine("    \"name\": \"Executive / Founder Full Name\",");
        sb.AppendLine("    \"role\": \"Professional Title & Experience\",");
        sb.AppendLine("    \"credentials\": \"Certifications, Degrees & Affiliations\",");
        sb.AppendLine("    \"bio\": \"Inspiring background story and commitment to quality in {City}.\",");
        sb.AppendLine("    \"quote\": \"Personal leadership philosophy\",");
        sb.AppendLine("    \"avatar_initials\": \"Two capital letters\"");
        sb.AppendLine("  },");
        sb.AppendLine("  \"calculator\": {");
        sb.AppendLine("    \"title\": \"Interactive ROI / Cost Estimator\",");
        sb.AppendLine("    \"subtitle\": \"Adjust the slider to preview your estimated value and returns.\",");
        sb.AppendLine("    \"slider_label\": \"Monthly Volume / Scope\",");
        sb.AppendLine("    \"unit\": \"$\",");
        sb.AppendLine("    \"min\": 1000,");
        sb.AppendLine("    \"max\": 50000,");
        sb.AppendLine("    \"default_value\": 10000,");
        sb.AppendLine("    \"multiplier\": 2.8,");
        sb.AppendLine("    \"result_label\": \"Estimated Annual Impact / Savings\",");
        sb.AppendLine("    \"result_suffix\": \" / year\"");
        sb.AppendLine("  },");
        sb.AppendLine("  \"testimonials\": [");
        sb.AppendLine("    {");
        sb.AppendLine("      \"quote\": \"Detailed, authentic customer quote describing a real experience and measurable result.\",");
        sb.AppendLine("      \"author\": \"Customer Full Name\",");
        sb.AppendLine("      \"role\": \"Customer Role / Organization / Neighborhood in {City}\",");
        sb.AppendLine("      \"initials\": \"Two capital letters\",");
        sb.AppendLine("      \"rating\": 5");
        sb.AppendLine("    }");
        sb.AppendLine("  ],");
        sb.AppendLine("  \"faqs\": [");
        sb.AppendLine("    {");
        sb.AppendLine("      \"question\": \"High-intent customer objection question?\",");
        sb.AppendLine("      \"answer\": \"Clear, reassuring, professional answer explaining process, timeline, or pricing policy.\"");
        sb.AppendLine("    }");
        sb.AppendLine("  ],");
        sb.AppendLine("  \"lead_form\": {");
        sb.AppendLine("    \"title\": \"Form Section Heading\",");
        sb.AppendLine("    \"subtitle\": \"Form Subheading explaining rapid follow-up\"");
        sb.AppendLine("  },");
        sb.AppendLine("  \"conversion_score\": 95,");
        sb.AppendLine("  \"architecture_blueprint_markdown\": \"# Strategic CRO Blueprint\\n## Executive Positioning...\",");
        sb.AppendLine("  \"developer_prompts\": \"Next.js 14 / Tailwind implementation prompt for frontend engineers...\",");
        sb.AppendLine("  \"ad_copy_prompts\": \"Google Search and Meta Ad copy variants...\",");
        sb.AppendLine("  \"midjourney_prompts\": [\"3-4 photorealistic commercial photography prompts\"]");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private LandingPageGenerationResult ParseGeminiResponse(string json, LandingPageGenerationRequest request)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
            {
                var fallback = GenerateDeterministicLandingPage(request);
                fallback.ErrorMessage = "Gemini response did not contain candidates. Used local generator.";
                return fallback;
            }

            var firstCandidate = candidates[0];
            if (!firstCandidate.TryGetProperty("content", out var content) ||
                !content.TryGetProperty("parts", out var parts) ||
                parts.GetArrayLength() == 0)
            {
                var fallback = GenerateDeterministicLandingPage(request);
                fallback.ErrorMessage = "Gemini content parts empty. Used local generator.";
                return fallback;
            }

            var rawText = parts[0].GetProperty("text").GetString() ?? string.Empty;

            // Clean json fences if present
            var cleanJson = rawText.Trim();
            if (cleanJson.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
            {
                cleanJson = cleanJson[7..];
            }
            else if (cleanJson.StartsWith("```"))
            {
                cleanJson = cleanJson[3..];
            }

            if (cleanJson.EndsWith("```"))
            {
                cleanJson = cleanJson[..^3];
            }
            cleanJson = cleanJson.Trim();

            var payload = ParseContentPayload(cleanJson, request);

            // Assemble into complete responsive HTML5 Tailwind engine
            var (primaryHex, accentHex, bgHex, cardBgHex, textHex, buttonClass) = GetThemeTokens(request.DesignStyle);
            var html = BuildBespokeProductionHtml(payload, request, primaryHex, accentHex, bgHex, cardBgHex, textHex, buttonClass);

            var blueprint = !string.IsNullOrWhiteSpace(payload.ArchitectureBlueprintMarkdown) && payload.ArchitectureBlueprintMarkdown.Length > 100
                ? payload.ArchitectureBlueprintMarkdown
                : BuildArchitectureBlueprint(request.BusinessName, request.Category, request.City, request.Country, request.Archetype, request.DesignStyle, request.SelectedModules, payload.Headline, payload.Subheadline, request.CustomPromptInstructions);

            return new LandingPageGenerationResult
            {
                IsSuccess = true,
                IsLocalFallback = false,
                BusinessName = request.BusinessName,
                Provider = "Google Gemini Free Tier",
                Headline = payload.Headline,
                Subheadline = payload.Subheadline,
                CallToActionText = payload.CallToAction,
                ConversionScore = payload.ConversionScore > 0 ? payload.ConversionScore : 95,
                EstimatedSectionsCount = request.SelectedModules.Count > 0 ? request.SelectedModules.Count : 8,
                GeneratedHtml = html,
                ArchitectureBlueprintMarkdown = blueprint,
                DeveloperPrompts = payload.DeveloperPrompts,
                AdCopyPrompts = payload.AdCopyPrompts,
                MidjourneyPrompts = payload.MidjourneyPrompts,
                RequestsUsedToday = _dailyRequestsUsed,
                ContentPayload = payload
            };
        }
        catch (Exception ex)
        {
            var fallback = GenerateDeterministicLandingPage(request);
            fallback.ErrorMessage = $"Failed to parse Gemini response: {ex.Message}. Local deterministic template deployed.";
            return fallback;
        }
    }

    private static BespokeContentPayload ParseContentPayload(string json, LandingPageGenerationRequest request)
    {
        var defaults = GetCategoryDefaults(request.Category, request.BusinessName, request.City, request.Country);

        try
        {
            using var doc = JsonDocument.Parse(json);
            var obj = doc.RootElement;

            var payload = new BespokeContentPayload
            {
                Headline = obj.TryGetProperty("headline", out var hl) && !string.IsNullOrWhiteSpace(hl.GetString()) ? hl.GetString()! : defaults.Headline,
                Subheadline = obj.TryGetProperty("subheadline", out var shl) && !string.IsNullOrWhiteSpace(shl.GetString()) ? shl.GetString()! : defaults.Subheadline,
                HeroBadge = obj.TryGetProperty("hero_badge", out var hb) && !string.IsNullOrWhiteSpace(hb.GetString()) ? hb.GetString()! : defaults.HeroBadge,
                CallToAction = obj.TryGetProperty("call_to_action", out var cta) && !string.IsNullOrWhiteSpace(cta.GetString()) ? cta.GetString()! : defaults.CallToAction,
                SecondaryCta = obj.TryGetProperty("secondary_cta", out var scta) && !string.IsNullOrWhiteSpace(scta.GetString()) ? scta.GetString()! : defaults.SecondaryCta,
                ConversionScore = obj.TryGetProperty("conversion_score", out var cs) && cs.TryGetInt32(out var csVal) ? csVal : 95,
                ArchitectureBlueprintMarkdown = obj.TryGetProperty("architecture_blueprint_markdown", out var ab) ? ab.GetString() ?? string.Empty : string.Empty,
                DeveloperPrompts = obj.TryGetProperty("developer_prompts", out var dp) ? dp.GetString() ?? string.Empty : string.Empty,
                AdCopyPrompts = obj.TryGetProperty("ad_copy_prompts", out var ac) ? ac.GetString() ?? string.Empty : string.Empty
            };

            // Metrics
            if (obj.TryGetProperty("metrics", out var metricsEl) && metricsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in metricsEl.EnumerateArray())
                {
                    var val = item.TryGetProperty("value", out var v) ? v.GetString() ?? string.Empty : string.Empty;
                    var lbl = item.TryGetProperty("label", out var l) ? l.GetString() ?? string.Empty : string.Empty;
                    if (!string.IsNullOrWhiteSpace(val) && !string.IsNullOrWhiteSpace(lbl))
                    {
                        payload.Metrics.Add(new BespokeMetricItem { Value = val, Label = lbl });
                    }
                }
            }
            if (payload.Metrics.Count == 0) payload.Metrics = defaults.Metrics;

            // Trust Badges
            if (obj.TryGetProperty("trust_badges", out var badgesEl) && badgesEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in badgesEl.EnumerateArray())
                {
                    var s = item.GetString();
                    if (!string.IsNullOrWhiteSpace(s)) payload.TrustBadges.Add(s);
                }
            }
            if (payload.TrustBadges.Count == 0) payload.TrustBadges = defaults.TrustBadges;

            // Services
            if (obj.TryGetProperty("services", out var servicesEl) && servicesEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in servicesEl.EnumerateArray())
                {
                    var title = item.TryGetProperty("title", out var t) ? t.GetString() ?? string.Empty : string.Empty;
                    var desc = item.TryGetProperty("description", out var d) ? d.GetString() ?? string.Empty : string.Empty;
                    var icon = item.TryGetProperty("icon", out var ic) ? ic.GetString() ?? "fa-solid fa-briefcase" : "fa-solid fa-briefcase";
                    var servCta = item.TryGetProperty("cta", out var sc) ? sc.GetString() ?? "Learn More" : "Learn More";

                    if (!string.IsNullOrWhiteSpace(title))
                    {
                        payload.Services.Add(new BespokeServiceItem { Title = title, Description = desc, Icon = icon, Cta = servCta });
                    }
                }
            }
            if (payload.Services.Count == 0) payload.Services = defaults.Services;

            // Differentiators
            if (obj.TryGetProperty("differentiators", out var diffEl) && diffEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in diffEl.EnumerateArray())
                {
                    var title = item.TryGetProperty("title", out var t) ? t.GetString() ?? string.Empty : string.Empty;
                    var desc = item.TryGetProperty("description", out var d) ? d.GetString() ?? string.Empty : string.Empty;
                    var icon = item.TryGetProperty("icon", out var ic) ? ic.GetString() ?? "fa-solid fa-check" : "fa-solid fa-check";

                    if (!string.IsNullOrWhiteSpace(title))
                    {
                        payload.Differentiators.Add(new BespokeDifferentiatorItem { Title = title, Description = desc, Icon = icon });
                    }
                }
            }
            if (payload.Differentiators.Count == 0) payload.Differentiators = defaults.Differentiators;

            // Case Studies / Before & After
            if (obj.TryGetProperty("case_studies", out var csArr) && csArr.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in csArr.EnumerateArray())
                {
                    var cName = item.TryGetProperty("client_name", out var cn) ? cn.GetString() ?? string.Empty : string.Empty;
                    var ch = item.TryGetProperty("challenge", out var chg) ? chg.GetString() ?? string.Empty : string.Empty;
                    var sol = item.TryGetProperty("solution", out var sln) ? sln.GetString() ?? string.Empty : string.Empty;
                    var rm = item.TryGetProperty("result_metric", out var rme) ? rme.GetString() ?? string.Empty : string.Empty;
                    var rd = item.TryGetProperty("result_description", out var rde) ? rde.GetString() ?? string.Empty : string.Empty;

                    if (!string.IsNullOrWhiteSpace(cName))
                    {
                        payload.CaseStudies.Add(new BespokeCaseStudy
                        {
                            ClientName = cName,
                            Challenge = ch,
                            Solution = sol,
                            ResultMetric = rm,
                            ResultDescription = rd
                        });
                    }
                }
            }
            if (payload.CaseStudies.Count == 0) payload.CaseStudies = defaults.CaseStudies;

            // Pricing Plans
            if (obj.TryGetProperty("pricing_plans", out var plansArr) && plansArr.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in plansArr.EnumerateArray())
                {
                    var pName = item.TryGetProperty("name", out var pn) ? pn.GetString() ?? string.Empty : string.Empty;
                    var pPrice = item.TryGetProperty("price", out var pp) ? pp.GetString() ?? "$99" : "$99";
                    var pPeriod = item.TryGetProperty("period", out var pr) ? pr.GetString() ?? "/month" : "/month";
                    var pBadge = item.TryGetProperty("badge", out var pb) ? pb.GetString() ?? string.Empty : string.Empty;
                    var pDesc = item.TryGetProperty("description", out var pd) ? pd.GetString() ?? string.Empty : string.Empty;
                    var pCta = item.TryGetProperty("cta", out var pc) ? pc.GetString() ?? "Get Started" : "Get Started";
                    var pPop = item.TryGetProperty("is_popular", out var pop) && pop.GetBoolean();

                    var feats = new List<string>();
                    if (item.TryGetProperty("features", out var fArr) && fArr.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var f in fArr.EnumerateArray())
                        {
                            var fStr = f.GetString();
                            if (!string.IsNullOrWhiteSpace(fStr)) feats.Add(fStr);
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(pName))
                    {
                        payload.PricingPlans.Add(new BespokePricingPlan
                        {
                            Name = pName,
                            Price = pPrice,
                            Period = pPeriod,
                            Badge = pBadge,
                            Description = pDesc,
                            Features = feats,
                            Cta = pCta,
                            IsPopular = pPop
                        });
                    }
                }
            }
            if (payload.PricingPlans.Count == 0) payload.PricingPlans = defaults.PricingPlans;

            // Leader Bio
            if (obj.TryGetProperty("leader_bio", out var lbEl) && lbEl.ValueKind == JsonValueKind.Object)
            {
                payload.LeaderBio = new BespokeLeaderBio
                {
                    Name = lbEl.TryGetProperty("name", out var lbn) ? lbn.GetString() ?? defaults.LeaderBio?.Name ?? string.Empty : defaults.LeaderBio?.Name ?? string.Empty,
                    Role = lbEl.TryGetProperty("role", out var lbr) ? lbr.GetString() ?? defaults.LeaderBio?.Role ?? string.Empty : defaults.LeaderBio?.Role ?? string.Empty,
                    Credentials = lbEl.TryGetProperty("credentials", out var lbc) ? lbc.GetString() ?? defaults.LeaderBio?.Credentials ?? string.Empty : defaults.LeaderBio?.Credentials ?? string.Empty,
                    Bio = lbEl.TryGetProperty("bio", out var lbb) ? lbb.GetString() ?? defaults.LeaderBio?.Bio ?? string.Empty : defaults.LeaderBio?.Bio ?? string.Empty,
                    Quote = lbEl.TryGetProperty("quote", out var lbq) ? lbq.GetString() ?? defaults.LeaderBio?.Quote ?? string.Empty : defaults.LeaderBio?.Quote ?? string.Empty,
                    AvatarInitials = lbEl.TryGetProperty("avatar_initials", out var lbi) ? lbi.GetString() ?? defaults.LeaderBio?.AvatarInitials ?? "MD" : defaults.LeaderBio?.AvatarInitials ?? "MD"
                };
            }
            else
            {
                payload.LeaderBio = defaults.LeaderBio;
            }

            // Calculator
            if (obj.TryGetProperty("calculator", out var calcEl) && calcEl.ValueKind == JsonValueKind.Object)
            {
                payload.Calculator = new BespokeCalculatorConfig
                {
                    Title = calcEl.TryGetProperty("title", out var ct) ? ct.GetString() ?? defaults.Calculator?.Title ?? "ROI Estimator" : defaults.Calculator?.Title ?? "ROI Estimator",
                    Subtitle = calcEl.TryGetProperty("subtitle", out var csb) ? csb.GetString() ?? defaults.Calculator?.Subtitle ?? string.Empty : defaults.Calculator?.Subtitle ?? string.Empty,
                    SliderLabel = calcEl.TryGetProperty("slider_label", out var csl) ? csl.GetString() ?? defaults.Calculator?.SliderLabel ?? "Volume" : defaults.Calculator?.SliderLabel ?? "Volume",
                    Unit = calcEl.TryGetProperty("unit", out var cu) ? cu.GetString() ?? "$" : "$",
                    Min = calcEl.TryGetProperty("min", out var cmin) && cmin.TryGetInt32(out var minVal) ? minVal : defaults.Calculator?.Min ?? 1000,
                    Max = calcEl.TryGetProperty("max", out var cmax) && cmax.TryGetInt32(out var maxVal) ? maxVal : defaults.Calculator?.Max ?? 50000,
                    DefaultValue = calcEl.TryGetProperty("default_value", out var cdv) && cdv.TryGetInt32(out var defVal) ? defVal : defaults.Calculator?.DefaultValue ?? 10000,
                    Multiplier = calcEl.TryGetProperty("multiplier", out var cmul) && cmul.TryGetDouble(out var mulVal) ? mulVal : defaults.Calculator?.Multiplier ?? 2.8,
                    ResultLabel = calcEl.TryGetProperty("result_label", out var crl) ? crl.GetString() ?? defaults.Calculator?.ResultLabel ?? "Projected Annual Return" : defaults.Calculator?.ResultLabel ?? "Projected Annual Return",
                    ResultSuffix = calcEl.TryGetProperty("result_suffix", out var crs) ? crs.GetString() ?? defaults.Calculator?.ResultSuffix ?? " / year" : defaults.Calculator?.ResultSuffix ?? " / year"
                };
            }
            else
            {
                payload.Calculator = defaults.Calculator;
            }

            // Testimonials
            if (obj.TryGetProperty("testimonials", out var testEl) && testEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in testEl.EnumerateArray())
                {
                    var quote = item.TryGetProperty("quote", out var q) ? q.GetString() ?? string.Empty : string.Empty;
                    var author = item.TryGetProperty("author", out var a) ? a.GetString() ?? string.Empty : string.Empty;
                    var role = item.TryGetProperty("role", out var r) ? r.GetString() ?? string.Empty : string.Empty;
                    var initials = item.TryGetProperty("initials", out var ini) ? ini.GetString() ?? "CL" : "CL";
                    var rating = item.TryGetProperty("rating", out var rt) && rt.TryGetInt32(out var rVal) ? rVal : 5;

                    if (!string.IsNullOrWhiteSpace(quote) && !string.IsNullOrWhiteSpace(author))
                    {
                        payload.Testimonials.Add(new BespokeTestimonialItem { Quote = quote, Author = author, Role = role, Initials = initials, Rating = rating });
                    }
                }
            }
            if (payload.Testimonials.Count == 0) payload.Testimonials = defaults.Testimonials;

            // FAQs
            if (obj.TryGetProperty("faqs", out var faqsEl) && faqsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in faqsEl.EnumerateArray())
                {
                    var question = item.TryGetProperty("question", out var q) ? q.GetString() ?? string.Empty : string.Empty;
                    var answer = item.TryGetProperty("answer", out var a) ? a.GetString() ?? string.Empty : string.Empty;

                    if (!string.IsNullOrWhiteSpace(question))
                    {
                        payload.Faqs.Add(new BespokeFaqItem { Question = question, Answer = answer });
                    }
                }
            }
            if (payload.Faqs.Count == 0) payload.Faqs = defaults.Faqs;

            // Lead Form
            if (obj.TryGetProperty("lead_form", out var lfEl))
            {
                payload.LeadFormTitle = lfEl.TryGetProperty("title", out var lft) ? lft.GetString() ?? defaults.LeadFormTitle : defaults.LeadFormTitle;
                payload.LeadFormSubtitle = lfEl.TryGetProperty("subtitle", out var lfs) ? lfs.GetString() ?? defaults.LeadFormSubtitle : defaults.LeadFormSubtitle;
            }
            else
            {
                payload.LeadFormTitle = defaults.LeadFormTitle;
                payload.LeadFormSubtitle = defaults.LeadFormSubtitle;
            }

            // Midjourney Prompts
            if (obj.TryGetProperty("midjourney_prompts", out var mjp) && mjp.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in mjp.EnumerateArray())
                {
                    var s = item.GetString();
                    if (!string.IsNullOrWhiteSpace(s)) payload.MidjourneyPrompts.Add(s);
                }
            }
            if (payload.MidjourneyPrompts.Count == 0) payload.MidjourneyPrompts = defaults.MidjourneyPrompts;

            return payload;
        }
        catch
        {
            return defaults;
        }
    }

    public LandingPageGenerationResult GenerateDeterministicLandingPage(LandingPageGenerationRequest request)
    {
        var bizName = !string.IsNullOrWhiteSpace(request.BusinessName) ? request.BusinessName.Trim() : "Enterprise Client";
        var category = !string.IsNullOrWhiteSpace(request.Category) ? request.Category.Trim() : "Commercial Business";
        var city = !string.IsNullOrWhiteSpace(request.City) ? request.City.Trim() : "Metropolitan Area";
        var country = !string.IsNullOrWhiteSpace(request.Country) ? request.Country.Trim() : "Global";

        var payload = GetCategoryDefaults(category, bizName, city, country);

        // Color theme styling
        var (primaryHex, accentHex, bgHex, cardBgHex, textHex, buttonClass) = GetThemeTokens(request.DesignStyle);

        var html = BuildBespokeProductionHtml(payload, request, primaryHex, accentHex, bgHex, cardBgHex, textHex, buttonClass);
        var blueprint = BuildArchitectureBlueprint(bizName, category, city, country, request.Archetype, request.DesignStyle, request.SelectedModules, payload.Headline, payload.Subheadline, request.CustomPromptInstructions);

        var phone = !string.IsNullOrWhiteSpace(request.PhonePrimary) ? request.PhonePrimary.Trim() : "+92 300 1234567";

        var midjourney = payload.MidjourneyPrompts.Count > 0 ? payload.MidjourneyPrompts : new List<string>
        {
            $"Commercial architectural photography, high-end {category} clinic/office interior in {city}, floor-to-ceiling glass, warm cinematic lighting, minimal modern furniture, 8k resolution, photorealistic, Hasselblad X2D 100C --ar 16:9 --style raw",
            $"Editorial portrait of lead {category} specialist director, friendly confident smile, tailored business suit, blurred luxury office background, soft studio lighting, sharp focus, 35mm lens --ar 4:5",
            $"Minimalist 3D isometric render of modern digital workflow solutions, glowing {primaryHex} neon accents, translucent frosted glass cards, clean dark background --ar 1:1"
        };

        var developerPrompts = !string.IsNullOrWhiteSpace(payload.DeveloperPrompts) ? payload.DeveloperPrompts :
                               $"Create a responsive Next.js 14 App Router landing page for {bizName} ({category} in {city}).\n" +
                               $"Use Tailwind CSS for styling, Lucide React icons, and Framer Motion for scroll reveal animations.\n" +
                               $"Include components for: HeroSection, TrustLogos, ServiceGrid, PricingTable, FaqAccordion, LeadCaptureForm, and FloatingWhatsApp.\n" +
                               $"Integrate Zod schema validation for the lead capture form with server action email dispatch via Resend.";

        var adCopy = !string.IsNullOrWhiteSpace(payload.AdCopyPrompts) ? payload.AdCopyPrompts :
                     $"### Google Search Ads:\n" +
                     $"**Headline 1:** Best {category} in {city} | {bizName}\n" +
                     $"**Headline 2:** Certified Specialists & Proven Results\n" +
                     $"**Headline 3:** Book Free Consultation Today\n" +
                     $"**Description:** Looking for premium {category} in {city}? {bizName} delivers guaranteed quality, modern technology & 5-star client satisfaction. Call {phone}!\n\n" +
                     $"### Meta (Facebook/Instagram) Ad Copy:\n" +
                     $"\"Struggling to find reliable, high-quality {category} services in {city}? At {bizName}, we combine international standards with personalized care. Join 1,200+ satisfied clients who trust us with their success. Tap below to claim your exclusive complimentary strategy session!\"";

        return new LandingPageGenerationResult
        {
            IsSuccess = true,
            IsLocalFallback = true,
            BusinessName = bizName,
            Headline = payload.Headline,
            Subheadline = payload.Subheadline,
            CallToActionText = payload.CallToAction,
            ConversionScore = 93,
            EstimatedSectionsCount = request.SelectedModules.Count > 0 ? request.SelectedModules.Count : 8,
            Provider = "TKS Local Neural Synthesizer (High-Fidelity Engine)",
            GeneratedHtml = html,
            ArchitectureBlueprintMarkdown = blueprint,
            MidjourneyPrompts = midjourney,
            DeveloperPrompts = developerPrompts,
            AdCopyPrompts = adCopy,
            RequestsUsedToday = _dailyRequestsUsed,
            ContentPayload = payload
        };
    }

    private static (string primaryHex, string accentHex, string bgHex, string cardBgHex, string textHex, string buttonClass) GetThemeTokens(LandingPageDesignStyle style)
    {
        return style switch
        {
            LandingPageDesignStyle.MidnightLuxuryGold =>
                ("#D97706", "#F59E0B", "#0A0E17", "#131B2A", "#F8FAFC", "bg-gradient-to-r from-amber-500 to-amber-600 hover:brightness-110 text-slate-950 font-bold shadow-lg shadow-amber-500/20"),
            LandingPageDesignStyle.TrustworthyEmerald =>
                ("#059669", "#10B981", "#061A14", "#0D2E24", "#F0FDF4", "bg-gradient-to-r from-emerald-500 to-teal-500 hover:from-emerald-600 hover:to-teal-600 text-slate-950 font-bold shadow-lg shadow-emerald-500/25"),
            LandingPageDesignStyle.BoldVibrantIndigo =>
                ("#4F46E5", "#6366F1", "#0C0A1D", "#161338", "#F5F3FF", "bg-gradient-to-r from-indigo-500 to-purple-600 hover:from-indigo-600 hover:to-purple-700 text-white font-bold shadow-lg shadow-indigo-500/25"),
            LandingPageDesignStyle.MinimalistMonochrome =>
                ("#E4E4E7", "#FAFAFA", "#09090B", "#18181B", "#FAFAFA", "bg-white hover:bg-slate-200 text-slate-950 font-bold shadow-lg"),
            _ => // ModernSlateCyan
                ("#0284C7", "#06B6D4", "#0B1322", "#131F33", "#F8FAFC", "bg-gradient-to-r from-cyan-500 to-blue-500 hover:from-cyan-600 hover:to-blue-600 text-slate-950 font-bold shadow-lg shadow-cyan-500/25")
        };
    }

    private static string BuildBespokeProductionHtml(
        BespokeContentPayload payload,
        LandingPageGenerationRequest request,
        string primaryHex,
        string accentHex,
        string bgHex,
        string cardBgHex,
        string textHex,
        string buttonClass)
    {
        var bizName = !string.IsNullOrWhiteSpace(request.BusinessName) ? request.BusinessName.Trim() : "Enterprise Client";
        var category = !string.IsNullOrWhiteSpace(request.Category) ? request.Category.Trim() : "Commercial Business";
        var city = !string.IsNullOrWhiteSpace(request.City) ? request.City.Trim() : "Metropolitan Area";
        var country = !string.IsNullOrWhiteSpace(request.Country) ? request.Country.Trim() : "Global";
        var phone = !string.IsNullOrWhiteSpace(request.PhonePrimary) ? request.PhonePrimary.Trim() : "+92 300 1234567";
        var email = !string.IsNullOrWhiteSpace(request.EmailPrimary) ? request.EmailPrimary.Trim() : $"contact@{bizName.ToLowerInvariant().Replace(" ", "")}.com";
        var modules = request.SelectedModules.Count > 0 ? request.SelectedModules : ["mod-hero", "mod-trust-bar", "mod-services", "mod-before-after", "mod-team", "mod-testimonials", "mod-pricing", "mod-faq", "mod-lead-form", "mod-whatsapp", "mod-seo"];

        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\" class=\"scroll-smooth\">");
        sb.AppendLine("<head>");
        sb.AppendLine("    <meta charset=\"UTF-8\">");
        sb.AppendLine("    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        sb.AppendLine($"    <title>{bizName} — Leading {category} in {city}</title>");
        sb.AppendLine($"    <meta name=\"description\" content=\"{payload.Subheadline}\">");
        sb.AppendLine("    <!-- Tailwind CSS CDN -->");
        sb.AppendLine("    <script src=\"https://cdn.tailwindcss.com\"></script>");

        // Google Fonts specific to design style and archetype
        sb.AppendLine("    <link rel=\"preconnect\" href=\"https://fonts.googleapis.com\">");
        sb.AppendLine("    <link rel=\"preconnect\" href=\"https://fonts.gstatic.com\" crossorigin>");

        if (request.DesignStyle == LandingPageDesignStyle.MidnightLuxuryGold || request.Archetype == LandingPageArchetype.LuxuryBrandShowcase)
        {
            sb.AppendLine("    <link href=\"https://fonts.googleapis.com/css2?family=Playfair+Display:ital,wght@0,400;0,600;0,700;0,900;1,400&family=Plus+Jakarta+Sans:wght@300;400;500;600;700&display=swap\" rel=\"stylesheet\">");
        }
        else if (request.DesignStyle == LandingPageDesignStyle.MinimalistMonochrome)
        {
            sb.AppendLine("    <link href=\"https://fonts.googleapis.com/css2?family=Space+Grotesk:wght@400;500;600;700&family=Inter:wght@300;400;500;600;700&display=swap\" rel=\"stylesheet\">");
        }
        else if (request.DesignStyle == LandingPageDesignStyle.BoldVibrantIndigo)
        {
            sb.AppendLine("    <link href=\"https://fonts.googleapis.com/css2?family=Syne:wght@500;700;800&family=Plus+Jakarta+Sans:wght@400;500;600;700&display=swap\" rel=\"stylesheet\">");
        }
        else
        {
            sb.AppendLine("    <link href=\"https://fonts.googleapis.com/css2?family=Plus+Jakarta+Sans:wght@400;500;600;700;800&family=Inter:wght@300;400;500;600;700&display=swap\" rel=\"stylesheet\">");
        }

        sb.AppendLine("    <!-- FontAwesome 6 Icons CDN -->");
        sb.AppendLine("    <link rel=\"stylesheet\" href=\"https://cdnjs.cloudflare.com/ajax/libs/font-awesome/6.5.1/css/all.min.css\">");
        sb.AppendLine("    <script>");
        sb.AppendLine("        tailwind.config = {");
        sb.AppendLine("            darkMode: 'class',");
        sb.AppendLine("            theme: {");
        sb.AppendLine("                extend: {");
        if (request.DesignStyle == LandingPageDesignStyle.MidnightLuxuryGold || request.Archetype == LandingPageArchetype.LuxuryBrandShowcase)
        {
            sb.AppendLine("                    fontFamily: { sans: ['Plus Jakarta Sans', 'sans-serif'], serif: ['Playfair Display', 'serif'] },");
        }
        else if (request.DesignStyle == LandingPageDesignStyle.MinimalistMonochrome)
        {
            sb.AppendLine("                    fontFamily: { sans: ['Space Grotesk', 'sans-serif'], mono: ['Space Grotesk', 'monospace'] },");
        }
        else
        {
            sb.AppendLine("                    fontFamily: { sans: ['Plus Jakarta Sans', 'Inter', 'sans-serif'] },");
        }
        sb.AppendLine($"                    colors: {{ brand: '{primaryHex}', brandAccent: '{accentHex}', brandDark: '{bgHex}', brandCard: '{cardBgHex}' }}");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine("    </script>");
        sb.AppendLine("    <style>");
        var primaryFont = (request.DesignStyle == LandingPageDesignStyle.MidnightLuxuryGold || request.Archetype == LandingPageArchetype.LuxuryBrandShowcase) ? "Plus Jakarta Sans" : (request.DesignStyle == LandingPageDesignStyle.MinimalistMonochrome ? "Space Grotesk" : "Plus Jakarta Sans");
        sb.AppendLine($"        body {{ background-color: {bgHex}; color: {textHex}; font-family: '{primaryFont}', sans-serif; }}");
        sb.AppendLine("        .glass-card { background: rgba(19, 31, 51, 0.65); backdrop-filter: blur(14px); border: 1px solid rgba(255, 255, 255, 0.08); }");
        sb.AppendLine("        .luxury-card { background: rgba(15, 20, 30, 0.75); backdrop-filter: blur(14px); border: 1px solid rgba(217, 119, 6, 0.25); }");
        sb.AppendLine("        .tab-active { border-color: " + accentHex + "; color: #ffffff; background: rgba(255,255,255,0.05); }");
        sb.AppendLine("    </style>");

        if (modules.Contains("mod-seo"))
        {
            sb.AppendLine("    <!-- SEO Schema.org LocalBusiness -->");
            sb.AppendLine("    <script type=\"application/ld+json\">");
            sb.AppendLine("    {");
            sb.AppendLine("      \"@context\": \"https://schema.org\",");
            sb.AppendLine("      \"@type\": \"LocalBusiness\",");
            sb.AppendLine($"      \"name\": \"{bizName}\",");
            sb.AppendLine($"      \"description\": \"{payload.Subheadline}\",");
            sb.AppendLine($"      \"telephone\": \"{phone}\",");
            sb.AppendLine($"      \"email\": \"{email}\",");
            sb.AppendLine("      \"address\": {");
            sb.AppendLine("        \"@type\": \"PostalAddress\",");
            sb.AppendLine($"        \"addressLocality\": \"{city}\",");
            sb.AppendLine($"        \"addressCountry\": \"{country}\"");
            sb.AppendLine("      },");
            sb.AppendLine("      \"aggregateRating\": {");
            sb.AppendLine("        \"@type\": \"AggregateRating\",");
            sb.AppendLine("        \"ratingValue\": \"4.9\",");
            sb.AppendLine("        \"reviewCount\": \"148\"");
            sb.AppendLine("      }");
            sb.AppendLine("    }");
            sb.AppendLine("    </script>");
        }

        sb.AppendLine("</head>");
        sb.AppendLine("<body class=\"antialiased min-h-screen flex flex-col justify-between selection:bg-cyan-500 selection:text-black\">");

        // 1. Archetype Urgent Top Announcement Bar (for LocalServiceBooking)
        if (request.Archetype == LandingPageArchetype.LocalServiceBooking)
        {
            sb.AppendLine($"    <div class=\"py-2 px-4 text-center text-xs font-semibold bg-emerald-500/15 border-b border-emerald-500/30 text-emerald-300 flex items-center justify-center space-x-2\">");
            sb.AppendLine($"        <span class=\"w-2 h-2 rounded-full bg-emerald-400 animate-pulse\"></span>");
            sb.AppendLine($"        <span>Same-Day Priority Slots Available Today in {city} • Hotline Active: <a href=\"tel:{phone}\" class=\"underline font-bold text-white\">{phone}</a></span>");
            sb.AppendLine($"    </div>");
        }

        // Top Navigation Bar
        sb.AppendLine("    <!-- Header Navigation -->");
        sb.AppendLine($"    <header class=\"sticky top-0 z-50 backdrop-blur-md bg-opacity-80 border-b border-slate-800/80\" style=\"background-color: {bgHex}D0;\">");
        sb.AppendLine("        <div class=\"max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 h-20 flex items-center justify-between\">");
        sb.AppendLine("            <div class=\"flex items-center space-x-3\">");
        sb.AppendLine($"                <div class=\"w-10 h-10 rounded-xl flex items-center justify-center font-bold text-lg\" style=\"background: linear-gradient(135deg, {primaryHex}, {accentHex}); color: #000;\">");
        sb.AppendLine($"                    {bizName[..Math.Min(2, bizName.Length)].ToUpperInvariant()}");
        sb.AppendLine("                </div>");
        sb.AppendLine($"                <span class=\"text-xl font-bold tracking-tight text-white\">{bizName}</span>");
        sb.AppendLine("            </div>");
        sb.AppendLine("            <nav class=\"hidden md:flex items-center space-x-8 text-sm font-medium text-slate-300\">");
        sb.AppendLine("                <a href=\"#services\" class=\"hover:text-white transition\">Services</a>");
        if (modules.Contains("mod-before-after")) sb.AppendLine("                <a href=\"#case-studies\" class=\"hover:text-white transition\">Case Studies</a>");
        if (modules.Contains("mod-pricing")) sb.AppendLine("                <a href=\"#pricing\" class=\"hover:text-white transition\">Pricing</a>");
        sb.AppendLine("                <a href=\"#testimonials\" class=\"hover:text-white transition\">Reviews</a>");
        sb.AppendLine("                <a href=\"#faq\" class=\"hover:text-white transition\">FAQ</a>");
        sb.AppendLine("            </nav>");
        sb.AppendLine("            <div class=\"flex items-center space-x-4\">");
        sb.AppendLine($"                <a href=\"#contact\" class=\"px-5 py-2.5 rounded-lg text-sm shadow-lg transition-all duration-200 transform hover:-translate-y-0.5 {buttonClass}\">{payload.CallToAction}</a>");
        sb.AppendLine("            </div>");
        sb.AppendLine("        </div>");
        sb.AppendLine("    </header>");

        sb.AppendLine("    <main class=\"flex-grow\">");

        // =========================================================================
        // BESPOKE ARCHETYPE HERO SECTION
        // =========================================================================
        if (modules.Contains("mod-hero"))
        {
            switch (request.Archetype)
            {
                case LandingPageArchetype.LocalServiceBooking:
                    // 2-COLUMN LOCAL CLINIC / URGENT SERVICE HERO
                    sb.AppendLine("        <!-- Local Service Booking 2-Column Hero -->");
                    sb.AppendLine("        <section class=\"relative pt-10 pb-16 md:pt-14 md:pb-20 overflow-hidden\">");
                    sb.AppendLine($"            <div class=\"absolute -top-40 left-1/2 transform -translate-x-1/2 w-[900px] h-[550px] rounded-full blur-3xl opacity-20 pointer-events-none\" style=\"background: radial-gradient(circle, {accentHex}, transparent 70%);\"></div>");
                    sb.AppendLine("            <div class=\"max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 relative z-10\">");
                    sb.AppendLine("                <div class=\"grid grid-cols-1 lg:grid-cols-12 gap-12 items-center\">");
                    sb.AppendLine("                    <div class=\"lg:col-span-7 text-left\">");
                    sb.AppendLine($"                        <div class=\"inline-flex items-center space-x-2 px-3.5 py-1.5 rounded-full border border-emerald-500/40 bg-emerald-950/40 text-xs font-semibold uppercase tracking-wider mb-6 text-emerald-400\">");
                    sb.AppendLine("                            <span class=\"w-2 h-2 rounded-full bg-emerald-400 animate-pulse\"></span>");
                    sb.AppendLine($"                            <span>{payload.HeroBadge}</span>");
                    sb.AppendLine("                        </div>");
                    sb.AppendLine($"                        <h1 class=\"text-4xl sm:text-5xl lg:text-6xl font-extrabold tracking-tight text-white leading-[1.1] mb-6\">{payload.Headline}</h1>");
                    sb.AppendLine($"                        <p class=\"text-base sm:text-lg text-slate-300 font-normal leading-relaxed mb-8\">{payload.Subheadline}</p>");
                    sb.AppendLine("                        <div class=\"space-y-3 mb-8\">");
                    sb.AppendLine($"                            <div class=\"flex items-center space-x-3 text-sm text-slate-200\"><i class=\"fa-solid fa-circle-check text-emerald-400\"></i><span>Zero waiting room queue with digital arrival check-in</span></div>");
                    sb.AppendLine($"                            <div class=\"flex items-center space-x-3 text-sm text-slate-200\"><i class=\"fa-solid fa-circle-check text-emerald-400\"></i><span>100% Board-certified attending specialists on-site</span></div>");
                    sb.AppendLine($"                            <div class=\"flex items-center space-x-3 text-sm text-slate-200\"><i class=\"fa-solid fa-circle-check text-emerald-400\"></i><span>Transparent upfront pricing &amp; direct insurance coordination</span></div>");
                    sb.AppendLine("                        </div>");
                    sb.AppendLine("                        <div class=\"flex flex-wrap items-center gap-4\">");
                    sb.AppendLine($"                            <a href=\"#contact\" class=\"px-7 py-3.5 rounded-xl text-base shadow-xl {buttonClass} transition transform hover:scale-105\"><i class=\"fa-solid fa-calendar-check mr-2\"></i>{payload.CallToAction}</a>");
                    sb.AppendLine($"                            <a href=\"tel:{phone}\" class=\"px-6 py-3.5 rounded-xl text-base font-semibold border border-slate-700 bg-slate-900/80 hover:bg-slate-800 text-white transition\"><i class=\"fa-solid fa-phone mr-2 text-emerald-400\"></i>{phone}</a>");
                    sb.AppendLine("                        </div>");
                    sb.AppendLine("                    </div>");

                    // Right Column: Interactive Same-Day Slot Card
                    sb.AppendLine("                    <div class=\"lg:col-span-5\">");
                    sb.AppendLine("                        <div class=\"p-7 rounded-3xl glass-card border border-emerald-500/30 shadow-2xl\">");
                    sb.AppendLine("                            <div class=\"flex items-center justify-between pb-4 border-b border-slate-800 mb-5\">");
                    sb.AppendLine("                                <div><h3 class=\"text-lg font-bold text-white\">Priority Appointment</h3><p class=\"text-xs text-slate-400\">Reserve instant queue slot</p></div>");
                    sb.AppendLine("                                <span class=\"px-2.5 py-1 rounded bg-emerald-500/20 text-emerald-400 text-xs font-bold\">🟢 Open Now</span>");
                    sb.AppendLine("                            </div>");
                    sb.AppendLine("                            <form onsubmit=\"handleFormSubmit(event)\" class=\"space-y-4\">");
                    sb.AppendLine("                                <div>");
                    sb.AppendLine("                                    <label class=\"block text-xs uppercase text-slate-400 font-bold mb-1.5\">Service Requested</label>");
                    sb.AppendLine("                                    <select class=\"w-full px-3.5 py-2.5 rounded-xl bg-slate-900 border border-slate-700 text-white text-xs\">");
                    foreach (var s in payload.Services) sb.AppendLine($"                                        <option value=\"{s.Title}\">{s.Title}</option>");
                    sb.AppendLine("                                    </select>");
                    sb.AppendLine("                                </div>");
                    sb.AppendLine("                                <div>");
                    sb.AppendLine("                                    <label class=\"block text-xs uppercase text-slate-400 font-bold mb-1.5\">Preferred Time Window</label>");
                    sb.AppendLine("                                    <div class=\"grid grid-cols-3 gap-2\">");
                    sb.AppendLine("                                        <button type=\"button\" onclick=\"selectSlot(this)\" class=\"slot-btn p-2 rounded-lg border border-slate-700 bg-slate-900/60 text-xs text-slate-300 hover:border-emerald-500 active-slot\">Morning<br><span class=\"text-[10px] text-slate-400\">8am - 12pm</span></button>");
                    sb.AppendLine("                                        <button type=\"button\" onclick=\"selectSlot(this)\" class=\"slot-btn p-2 rounded-lg border border-slate-700 bg-slate-900/60 text-xs text-slate-300 hover:border-emerald-500\">Afternoon<br><span class=\"text-[10px] text-slate-400\">12pm - 5pm</span></button>");
                    sb.AppendLine("                                        <button type=\"button\" onclick=\"selectSlot(this)\" class=\"slot-btn p-2 rounded-lg border border-slate-700 bg-slate-900/60 text-xs text-slate-300 hover:border-emerald-500\">Urgent Today<br><span class=\"text-[10px] text-emerald-400\">Next Available</span></button>");
                    sb.AppendLine("                                    </div>");
                    sb.AppendLine("                                </div>");
                    sb.AppendLine($"                                <div><label class=\"block text-xs uppercase text-slate-400 font-bold mb-1.5\">Patient Name</label><input type=\"text\" required placeholder=\"Full Name\" class=\"w-full px-3.5 py-2.5 rounded-xl bg-slate-900 border border-slate-700 text-white text-xs\"></div>");
                    sb.AppendLine($"                                <div><label class=\"block text-xs uppercase text-slate-400 font-bold mb-1.5\">Phone / WhatsApp</label><input type=\"tel\" required placeholder=\"{phone}\" class=\"w-full px-3.5 py-2.5 rounded-xl bg-slate-900 border border-slate-700 text-white text-xs\"></div>");
                    sb.AppendLine($"                                <button type=\"submit\" class=\"w-full py-3.5 rounded-xl font-bold text-sm bg-emerald-500 hover:bg-emerald-600 text-slate-950 transition transform hover:scale-[1.01] shadow-lg\"><i class=\"fa-solid fa-lock mr-2\"></i>Confirm Reservation Slot</button>");
                    sb.AppendLine("                                <p class=\"text-[11px] text-center text-slate-500\">Instant SMS / WhatsApp confirmation dispatched</p>");
                    sb.AppendLine("                            </form>");
                    sb.AppendLine("                        </div>");
                    sb.AppendLine("                    </div>");
                    sb.AppendLine("                </div>");

                    // Operational details strip
                    sb.AppendLine($"                <div class=\"mt-14 py-4 px-6 rounded-2xl bg-slate-950/60 border border-slate-800 flex flex-wrap items-center justify-between gap-4 text-xs text-slate-400\">");
                    sb.AppendLine($"                    <div class=\"flex items-center space-x-2\"><i class=\"fa-solid fa-location-dot text-emerald-400 text-base\"></i><span>{city}, {country}</span></div>");
                    sb.AppendLine($"                    <div class=\"flex items-center space-x-2\"><i class=\"fa-solid fa-clock text-cyan-400 text-base\"></i><span>Monday – Sunday: 8:00 AM – 9:00 PM</span></div>");
                    sb.AppendLine($"                    <div class=\"flex items-center space-x-2\"><i class=\"fa-solid fa-shield-heart text-rose-400 text-base\"></i><span>Emergency &amp; Same-Day Walk-Ins Accepted</span></div>");
                    sb.AppendLine("                </div>");
                    sb.AppendLine("            </div>");
                    sb.AppendLine("        </section>");
                    break;

                case LandingPageArchetype.ModernSaaSTech:
                    // MODERN SAAS TECH PRODUCT HERO WITH CODE / DASHBOARD UI PREVIEW
                    sb.AppendLine("        <!-- Modern SaaS & Tech Product Hero -->");
                    sb.AppendLine("        <section class=\"relative pt-12 pb-20 md:pt-16 md:pb-24 overflow-hidden\">");
                    sb.AppendLine($"            <div class=\"absolute -top-40 left-1/2 transform -translate-x-1/2 w-[1000px] h-[550px] rounded-full blur-3xl opacity-25 pointer-events-none\" style=\"background: radial-gradient(circle, {accentHex}, transparent 70%);\"></div>");
                    sb.AppendLine("            <div class=\"max-w-5xl mx-auto px-4 sm:px-6 lg:px-8 text-center relative z-10\">");
                    sb.AppendLine($"                <div class=\"inline-flex items-center space-x-2 px-4 py-1.5 rounded-full border border-cyan-500/40 bg-cyan-950/40 text-xs font-semibold uppercase tracking-wider mb-8 text-cyan-400\">");
                    sb.AppendLine("                    <span class=\"w-2 h-2 rounded-full bg-cyan-400 animate-pulse\"></span>");
                    sb.AppendLine($"                    <span>{payload.HeroBadge}</span>");
                    sb.AppendLine("                </div>");
                    sb.AppendLine($"                <h1 class=\"text-4xl sm:text-6xl md:text-7xl font-extrabold tracking-tight text-transparent bg-clip-text bg-gradient-to-r from-white via-slate-100 to-cyan-300 leading-[1.1] mb-6\">{payload.Headline}</h1>");
                    sb.AppendLine($"                <p class=\"max-w-3xl mx-auto text-lg sm:text-xl text-slate-300 leading-relaxed mb-10\">{payload.Subheadline}</p>");
                    sb.AppendLine("                <div class=\"flex flex-col sm:flex-row items-center justify-center gap-4 mb-14\">");
                    sb.AppendLine($"                    <a href=\"#contact\" class=\"w-full sm:w-auto px-8 py-4 rounded-xl text-base shadow-2xl {buttonClass} transition transform hover:scale-105\"><i class=\"fa-solid fa-bolt mr-2\"></i>{payload.CallToAction}</a>");
                    sb.AppendLine($"                    <a href=\"#case-studies\" class=\"w-full sm:w-auto px-8 py-4 rounded-xl text-base font-semibold border border-slate-700 bg-slate-900/80 hover:bg-slate-800 text-white transition\"><i class=\"fa-solid fa-laptop-code mr-2 text-cyan-400\"></i>View Live Architecture</a>");
                    sb.AppendLine("                </div>");

                    // Interactive Terminal / Dashboard Mockup
                    sb.AppendLine("                <!-- macOS Dashboard Mockup Window -->");
                    sb.AppendLine("                <div class=\"max-w-4xl mx-auto rounded-2xl glass-card border border-slate-700/80 shadow-2xl overflow-hidden text-left\">");
                    sb.AppendLine("                    <div class=\"px-4 py-3 bg-slate-950/80 border-b border-slate-800 flex items-center justify-between\">");
                    sb.AppendLine("                        <div class=\"flex items-center space-x-2\">");
                    sb.AppendLine("                            <span class=\"w-3 h-3 rounded-full bg-rose-500\"></span>");
                    sb.AppendLine("                            <span class=\"w-3 h-3 rounded-full bg-amber-500\"></span>");
                    sb.AppendLine("                            <span class=\"w-3 h-3 rounded-full bg-emerald-500\"></span>");
                    sb.AppendLine($"                            <span class=\"ml-3 text-xs text-slate-400 font-mono\">api.thekhansoft.io/{bizName.ToLowerInvariant().Replace(" ", "-")}/v2</span>");
                    sb.AppendLine("                        </div>");
                    sb.AppendLine("                        <div class=\"flex items-center space-x-3 text-xs text-slate-400 font-mono\">");
                    sb.AppendLine("                            <span class=\"text-emerald-400\">● 99.99% SLA</span>");
                    sb.AppendLine("                            <span>latency: 42ms</span>");
                    sb.AppendLine("                        </div>");
                    sb.AppendLine("                    </div>");
                    sb.AppendLine("                    <div class=\"p-6 bg-slate-950/90 font-mono text-xs text-slate-300 leading-relaxed overflow-x-auto\">");
                    sb.AppendLine($"                        <span class=\"text-cyan-400\">// Initializing high-throughput client infrastructure for {bizName} in {city}</span><br>");
                    sb.AppendLine($"                        <span class=\"text-purple-400\">const</span> cluster = <span class=\"text-purple-400\">new</span> EnterpriseCluster({{<br>");
                    sb.AppendLine($"                        &nbsp;&nbsp;region: <span class=\"text-emerald-300\">\"{city.ToLowerInvariant()}\"</span>,<br>");
                    sb.AppendLine($"                        &nbsp;&nbsp;highAvailability: <span class=\"text-amber-400\">true</span>,<br>");
                    sb.AppendLine($"                        &nbsp;&nbsp;autoScaleLimits: {{ min: <span class=\"text-amber-400\">3</span>, max: <span class=\"text-amber-400\">50</span> }},<br>");
                    sb.AppendLine($"                        &nbsp;&nbsp;security: [<span class=\"text-emerald-300\">\"SOC2\"</span>, <span class=\"text-emerald-300\">\"HIPAA\"</span>, <span class=\"text-emerald-300\">\"ISO27001\"</span>]<br>");
                    sb.AppendLine("                        });<br>");
                    sb.AppendLine($"                        <span class=\"text-slate-500\">✔ Cluster deployed in 3.4s • Zero downtime deployment enabled</span>");
                    sb.AppendLine("                    </div>");
                    sb.AppendLine("                </div>");
                    sb.AppendLine("            </div>");
                    sb.AppendLine("        </section>");
                    break;

                case LandingPageArchetype.LuxuryBrandShowcase:
                    // EDITORIAL LUXURY BRAND SHOWCASE HERO
                    sb.AppendLine("        <!-- Luxury Brand Showcase Hero -->");
                    sb.AppendLine("        <section class=\"relative pt-24 pb-28 md:pt-36 md:pb-40 overflow-hidden text-center\">");
                    sb.AppendLine($"            <div class=\"absolute -top-32 left-1/2 transform -translate-x-1/2 w-[800px] h-[400px] rounded-full blur-3xl opacity-15 pointer-events-none\" style=\"background: radial-gradient(circle, #D97706, transparent 70%);\"></div>");
                    sb.AppendLine("            <div class=\"max-w-4xl mx-auto px-4 sm:px-6 lg:px-8 relative z-10\">");
                    sb.AppendLine($"                <p class=\"text-xs font-semibold uppercase tracking-[0.25em] text-amber-400 mb-6\">Exclusive Bespoke Experience • {city}</p>");
                    sb.AppendLine($"                <h1 class=\"font-serif text-4xl sm:text-6xl md:text-7xl font-bold tracking-tight text-white leading-[1.15] mb-8\">{payload.Headline}</h1>");
                    sb.AppendLine($"                <div class=\"w-24 h-0.5 bg-gradient-to-r from-transparent via-amber-400 to-transparent mx-auto mb-8\"></div>");
                    sb.AppendLine($"                <p class=\"max-w-2xl mx-auto text-base sm:text-lg text-slate-300 font-light leading-relaxed mb-12\">{payload.Subheadline}</p>");
                    sb.AppendLine("                <div class=\"flex flex-col sm:flex-row items-center justify-center gap-5\">");
                    sb.AppendLine($"                    <a href=\"#contact\" class=\"px-9 py-4 rounded-full text-sm font-bold tracking-wider uppercase bg-gradient-to-r from-amber-500 to-amber-600 text-slate-950 shadow-2xl hover:brightness-110 transition\"><i class=\"fa-solid fa-crown mr-2\"></i>{payload.CallToAction}</a>");
                    sb.AppendLine($"                    <a href=\"#services\" class=\"px-8 py-4 rounded-full text-sm font-semibold tracking-wider uppercase border border-amber-500/40 text-amber-200 hover:bg-amber-500/10 transition\">Explore Collection</a>");
                    sb.AppendLine("                </div>");
                    sb.AppendLine("            </div>");
                    sb.AppendLine("        </section>");
                    break;

                case LandingPageArchetype.CorporateAgencyPortal:
                    // CORPORATE AGENCY PORTAL WITH B2B ROADMAP
                    sb.AppendLine("        <!-- Corporate Agency Portal Hero -->");
                    sb.AppendLine("        <section class=\"relative pt-20 pb-24 md:pt-28 md:pb-32 overflow-hidden\">");
                    sb.AppendLine("            <div class=\"max-w-7xl mx-auto px-4 sm:px-6 lg:px-8\">");
                    sb.AppendLine("                <div class=\"max-w-3xl text-left\">");
                    sb.AppendLine($"                    <span class=\"px-3.5 py-1.5 rounded-md bg-indigo-950/60 border border-indigo-500/40 text-xs font-bold text-indigo-300 uppercase tracking-wider mb-6 inline-block\">Enterprise Advisory • {city}</span>");
                    sb.AppendLine($"                    <h1 class=\"text-4xl sm:text-6xl font-extrabold text-white tracking-tight leading-[1.1] mb-6\">{payload.Headline}</h1>");
                    sb.AppendLine($"                    <p class=\"text-lg text-slate-300 mb-8\">{payload.Subheadline}</p>");
                    sb.AppendLine("                    <div class=\"flex flex-wrap gap-4\">");
                    sb.AppendLine($"                        <a href=\"#contact\" class=\"px-8 py-4 rounded-xl text-base font-bold shadow-xl {buttonClass} transition\"><i class=\"fa-solid fa-arrow-right mr-2\"></i>{payload.CallToAction}</a>");
                    sb.AppendLine($"                        <a href=\"tel:{phone}\" class=\"px-6 py-4 rounded-xl text-base font-semibold border border-slate-700 bg-slate-900 text-white hover:bg-slate-800 transition\">Direct Briefing: {phone}</a>");
                    sb.AppendLine("                    </div>");
                    sb.AppendLine("                </div>");
                    sb.AppendLine("            </div>");
                    sb.AppendLine("        </section>");
                    break;

                default:
                    // HIGH-CONVERTING 2-COLUMN SPLIT HERO (LeadGenerationFunnel & Ecommerce)
                    sb.AppendLine("        <!-- High-Converting 2-Column Split Hero -->");
                    sb.AppendLine("        <section class=\"relative pt-10 pb-16 md:pt-14 md:pb-20 overflow-hidden\">");
                    sb.AppendLine($"            <div class=\"absolute -top-40 left-1/2 transform -translate-x-1/2 w-[900px] h-[550px] rounded-full blur-3xl opacity-20 pointer-events-none\" style=\"background: radial-gradient(circle, {accentHex}, transparent 70%);\"></div>");
                    sb.AppendLine("            <div class=\"max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 relative z-10\">");
                    sb.AppendLine("                <div class=\"grid grid-cols-1 lg:grid-cols-12 gap-12 items-center\">");
                    sb.AppendLine("                    <div class=\"lg:col-span-7 text-left\">");
                    sb.AppendLine($"                        <div class=\"inline-flex items-center space-x-2 px-3.5 py-1.5 rounded-full border border-cyan-500/40 bg-cyan-950/40 text-xs font-semibold uppercase tracking-wider mb-6 text-cyan-400\">");
                    sb.AppendLine($"                            <span class=\"w-2 h-2 rounded-full animate-pulse\" style=\"background-color: {accentHex};\"></span>");
                    sb.AppendLine($"                            <span>{payload.HeroBadge}</span>");
                    sb.AppendLine("                        </div>");
                    sb.AppendLine($"                        <h1 class=\"text-4xl sm:text-5xl lg:text-6xl font-extrabold tracking-tight text-white leading-[1.1] mb-6\">{payload.Headline}</h1>");
                    sb.AppendLine($"                        <p class=\"text-base sm:text-lg text-slate-300 font-normal leading-relaxed mb-8\">{payload.Subheadline}</p>");
                    sb.AppendLine("                        <div class=\"space-y-3 mb-8\">");
                    sb.AppendLine($"                            <div class=\"flex items-center space-x-3 text-sm text-slate-200\"><i class=\"fa-solid fa-circle-check text-emerald-400\"></i><span>Verified industry track record serving {city} and regional clients</span></div>");
                    sb.AppendLine($"                            <div class=\"flex items-center space-x-3 text-sm text-slate-200\"><i class=\"fa-solid fa-circle-check text-emerald-400\"></i><span>Direct transparent pricing with zero unexpected overages or delays</span></div>");
                    sb.AppendLine($"                            <div class=\"flex items-center space-x-3 text-sm text-slate-200\"><i class=\"fa-solid fa-circle-check text-emerald-400\"></i><span>Dedicated priority support and continuous performance guarantee</span></div>");
                    sb.AppendLine("                        </div>");
                    sb.AppendLine("                        <div class=\"flex flex-wrap items-center gap-4\">");
                    sb.AppendLine($"                            <a href=\"#contact\" class=\"px-7 py-3.5 rounded-xl text-base shadow-xl {buttonClass} transition transform hover:scale-105\"><i class=\"fa-solid fa-calendar-check mr-2\"></i>{payload.CallToAction}</a>");
                    sb.AppendLine($"                            <a href=\"tel:{phone}\" class=\"px-6 py-3.5 rounded-xl text-base font-semibold border border-slate-700 bg-slate-900/80 hover:bg-slate-800 text-white transition\"><i class=\"fa-solid fa-phone mr-2 text-cyan-400\"></i>{phone}</a>");
                    sb.AppendLine("                        </div>");
                    sb.AppendLine("                    </div>");
                    sb.AppendLine("                    <div class=\"lg:col-span-5\">");
                    sb.AppendLine("                        <div class=\"p-7 rounded-3xl glass-card border border-cyan-500/30 shadow-2xl\">");
                    sb.AppendLine("                            <div class=\"flex items-center justify-between pb-4 border-b border-slate-800 mb-5\">");
                    sb.AppendLine($"                                <div><h3 class=\"text-lg font-bold text-white\">Instant Consultation</h3><p class=\"text-xs text-slate-400\">Request project proposal &amp; quote</p></div>");
                    sb.AppendLine("                                <span class=\"px-2.5 py-1 rounded bg-cyan-500/20 text-cyan-400 text-xs font-bold\">🟢 Priority Dispatch</span>");
                    sb.AppendLine("                            </div>");
                    sb.AppendLine("                            <form onsubmit=\"handleFormSubmit(event)\" class=\"space-y-4\">");
                    sb.AppendLine("                                <div>");
                    sb.AppendLine("                                    <label class=\"block text-xs uppercase text-slate-400 font-bold mb-1.5\">Service Requested</label>");
                    sb.AppendLine("                                    <select class=\"w-full px-3.5 py-2.5 rounded-xl bg-slate-900 border border-slate-700 text-white text-xs\">");
                    foreach (var s in payload.Services) sb.AppendLine($"                                        <option value=\"{s.Title}\">{s.Title}</option>");
                    sb.AppendLine("                                    </select>");
                    sb.AppendLine("                                </div>");
                    sb.AppendLine($"                                <div><label class=\"block text-xs uppercase text-slate-400 font-bold mb-1.5\">Client / Business Name</label><input type=\"text\" required placeholder=\"Full Name / Company\" class=\"w-full px-3.5 py-2.5 rounded-xl bg-slate-900 border border-slate-700 text-white text-xs\"></div>");
                    sb.AppendLine($"                                <div><label class=\"block text-xs uppercase text-slate-400 font-bold mb-1.5\">Direct Phone / WhatsApp</label><input type=\"tel\" required placeholder=\"{phone}\" class=\"w-full px-3.5 py-2.5 rounded-xl bg-slate-900 border border-slate-700 text-white text-xs\"></div>");
                    sb.AppendLine($"                                <button type=\"submit\" class=\"w-full py-3.5 rounded-xl font-bold text-sm {buttonClass} transition transform hover:scale-[1.01] shadow-lg\"><i class=\"fa-solid fa-paper-plane mr-2\"></i>Claim Free Consultation</button>");
                    sb.AppendLine($"                                <p class=\"text-[11px] text-center text-slate-500\">Direct response from {bizName} within 2 hours</p>");
                    sb.AppendLine("                            </form>");
                    sb.AppendLine("                        </div>");
                    sb.AppendLine("                    </div>");
                    sb.AppendLine("                </div>");
                    sb.AppendLine("            </div>");
                    sb.AppendLine("        </section>");
                    break;
            }

            // Key Metrics Grid
            if (payload.Metrics.Count > 0)
            {
                sb.AppendLine("        <!-- Trust Metrics Grid -->");
                sb.AppendLine("        <div class=\"max-w-6xl mx-auto px-4 sm:px-6 lg:px-8 mb-16\">");
                sb.AppendLine($"            <div class=\"grid grid-cols-2 sm:grid-cols-{Math.Min(4, payload.Metrics.Count)} gap-4\">");
                foreach (var metric in payload.Metrics)
                {
                    sb.AppendLine("                <div class=\"p-5 rounded-xl bg-slate-900/40 border border-slate-800 text-center\">");
                    sb.AppendLine($"                    <div class=\"text-3xl font-extrabold text-white mb-1\">{metric.Value}</div>");
                    sb.AppendLine($"                    <div class=\"text-xs text-slate-400 font-medium\">{metric.Label}</div>");
                    sb.AppendLine("                </div>");
                }
                sb.AppendLine("            </div>");
                sb.AppendLine("        </div>");
            }
        }

        // Trust Bar
        if (modules.Contains("mod-trust-bar") && payload.TrustBadges.Count > 0)
        {
            sb.AppendLine("        <!-- Trust & Logos Bar -->");
            sb.AppendLine("        <section class=\"py-8 border-y border-slate-800/60 bg-slate-950/40\">");
            sb.AppendLine("            <div class=\"max-w-7xl mx-auto px-4 text-center\">");
            sb.AppendLine($"                <p class=\"text-xs uppercase tracking-widest text-slate-500 font-semibold mb-6\">Accredited &amp; Trusted in {city} &amp; Surrounding Regions</p>");
            sb.AppendLine("                <div class=\"flex flex-wrap items-center justify-center gap-8 md:gap-16 opacity-80 hover:opacity-100 transition-all duration-300\">");
            foreach (var badge in payload.TrustBadges)
            {
                sb.AppendLine($"                    <div class=\"flex items-center space-x-2 text-slate-300 font-bold text-sm\"><i class=\"fa-solid fa-shield-halved text-lg text-emerald-400\"></i><span>{badge}</span></div>");
            }
            sb.AppendLine("                </div>");
            sb.AppendLine("            </div>");
            sb.AppendLine("        </section>");
        }

        // Corporate Agency 3-Step Methodology (Special for CorporateAgencyPortal)
        if (request.Archetype == LandingPageArchetype.CorporateAgencyPortal)
        {
            sb.AppendLine("        <!-- 3-Step Methodology Section -->");
            sb.AppendLine("        <section class=\"py-20 bg-slate-900/30 border-b border-slate-800/60\">");
            sb.AppendLine("            <div class=\"max-w-7xl mx-auto px-4 sm:px-6 lg:px-8\">");
            sb.AppendLine("                <div class=\"text-center max-w-2xl mx-auto mb-14\">");
            sb.AppendLine("                    <h2 class=\"text-xs font-bold uppercase tracking-widest text-indigo-400 mb-2\">Proven Framework</h2>");
            sb.AppendLine($"                    <p class=\"text-3xl font-extrabold text-white\">The {bizName} 3-Stage Scaling Engine</p>");
            sb.AppendLine("                </div>");
            sb.AppendLine("                <div class=\"grid grid-cols-1 md:grid-cols-3 gap-8\">");
            sb.AppendLine("                    <div class=\"p-6 rounded-2xl glass-card border-t-4 border-t-indigo-500\">");
            sb.AppendLine("                        <span class=\"text-3xl font-extrabold text-indigo-400\">01</span>");
            sb.AppendLine("                        <h4 class=\"text-lg font-bold text-white mt-3 mb-2\">Strategic Discovery &amp; Audit</h4>");
            sb.AppendLine($"                        <p class=\"text-sm text-slate-400 leading-relaxed\">Deep-dive assessment pinpointing revenue bottlenecks, competitive voids, and high-impact commercial opportunities in {city}.</p>");
            sb.AppendLine("                    </div>");
            sb.AppendLine("                    <div class=\"p-6 rounded-2xl glass-card border-t-4 border-t-cyan-500\">");
            sb.AppendLine("                        <span class=\"text-3xl font-extrabold text-cyan-400\">02</span>");
            sb.AppendLine("                        <h4 class=\"text-lg font-bold text-white mt-3 mb-2\">Precision Engineering &amp; Sprints</h4>");
            sb.AppendLine("                        <p class=\"text-sm text-slate-400 leading-relaxed\">Rapid bi-weekly deployment sprints executing custom infrastructure, lead generation, and operational automation.</p>");
            sb.AppendLine("                    </div>");
            sb.AppendLine("                    <div class=\"p-6 rounded-2xl glass-card border-t-4 border-t-emerald-500\">");
            sb.AppendLine("                        <span class=\"text-3xl font-extrabold text-emerald-400\">03</span>");
            sb.AppendLine("                        <h4 class=\"text-lg font-bold text-white mt-3 mb-2\">Market Dominance &amp; Scale</h4>");
            sb.AppendLine("                        <p class=\"text-sm text-slate-400 leading-relaxed\">Continuous SLA monitoring, conversion optimization, and dedicated retainer advisory to sustain market authority.</p>");
            sb.AppendLine("                    </div>");
            sb.AppendLine("                </div>");
            sb.AppendLine("            </div>");
            sb.AppendLine("        </section>");
        }

        // Services Grid
        if (modules.Contains("mod-services") && payload.Services.Count > 0)
        {
            sb.AppendLine("        <!-- Services Section -->");
            sb.AppendLine("        <section id=\"services\" class=\"py-24 relative\">");
            sb.AppendLine("            <div class=\"max-w-7xl mx-auto px-4 sm:px-6 lg:px-8\">");
            sb.AppendLine("                <div class=\"text-center max-w-3xl mx-auto mb-16\">");
            sb.AppendLine("                    <h2 class=\"text-xs font-bold uppercase tracking-widest text-cyan-400 mb-3\">Comprehensive Offerings</h2>");
            sb.AppendLine($"                    <p class=\"text-3xl sm:text-4xl font-extrabold text-white tracking-tight\">Engineered for Exceptional Results</p>");
            sb.AppendLine($"                    <p class=\"text-slate-400 mt-4 text-base\">Tailored commercial services for demanding enterprises in {city}.</p>");
            sb.AppendLine("                </div>");
            sb.AppendLine("                <div class=\"grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-8\">");

            foreach (var service in payload.Services)
            {
                sb.AppendLine($"                    <div class=\"p-8 rounded-2xl glass-card hover:border-cyan-500/50 transition duration-300 group flex flex-col justify-between\">");
                sb.AppendLine("                        <div>");
                sb.AppendLine($"                            <div class=\"w-14 h-14 rounded-xl flex items-center justify-center text-2xl mb-6 bg-cyan-500/10 text-cyan-400 group-hover:scale-110 transition\">");
                sb.AppendLine($"                                <i class=\"{service.Icon}\"></i>");
                sb.AppendLine("                            </div>");
                sb.AppendLine($"                            <h3 class=\"text-xl font-bold text-white mb-3\">{service.Title}</h3>");
                sb.AppendLine($"                            <p class=\"text-slate-400 text-sm leading-relaxed mb-6\">{service.Description}</p>");
                sb.AppendLine("                        </div>");
                sb.AppendLine($"                        <a href=\"#contact\" class=\"inline-flex items-center text-sm font-semibold text-cyan-400 hover:text-cyan-300\">{service.Cta} <i class=\"fa-solid fa-arrow-right ml-2 text-xs\"></i></a>");
                sb.AppendLine("                    </div>");
            }

            sb.AppendLine("                </div>");
            sb.AppendLine("            </div>");
            sb.AppendLine("        </section>");
        }

        // =========================================================================
        // CASE STUDIES & BEFORE-AFTER TRANSFORMATION (mod-before-after)
        // =========================================================================
        if (modules.Contains("mod-before-after") && payload.CaseStudies.Count > 0)
        {
            sb.AppendLine("        <!-- Case Studies & Transformation Section -->");
            sb.AppendLine("        <section id=\"case-studies\" class=\"py-24 bg-slate-900/40 border-y border-slate-800/80\">");
            sb.AppendLine("            <div class=\"max-w-7xl mx-auto px-4 sm:px-6 lg:px-8\">");
            sb.AppendLine("                <div class=\"text-center max-w-3xl mx-auto mb-16\">");
            sb.AppendLine("                    <span class=\"text-xs font-bold uppercase tracking-widest text-emerald-400 mb-2 block\">Case Studies &amp; Transformation</span>");
            sb.AppendLine($"                    <h2 class=\"text-3xl sm:text-4xl font-extrabold text-white tracking-tight\">Measurable Results Delivered Across {city}</h2>");
            sb.AppendLine("                    <p class=\"text-slate-400 mt-3 text-sm\">Real organizations, real roadblocks, and quantifiable outcome benchmarks achieved.</p>");
            sb.AppendLine("                </div>");
            sb.AppendLine("                <div class=\"grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-8\">");

            foreach (var cs in payload.CaseStudies)
            {
                sb.AppendLine("                    <div class=\"p-7 rounded-2xl glass-card flex flex-col justify-between hover:border-emerald-500/40 transition\">");
                sb.AppendLine("                        <div>");
                sb.AppendLine($"                            <span class=\"text-xs font-bold text-slate-400 uppercase tracking-wider block mb-1\">{cs.ClientName}</span>");
                sb.AppendLine($"                            <div class=\"text-3xl font-black text-emerald-400 my-4\">{cs.ResultMetric}</div>");
                sb.AppendLine($"                            <div class=\"mb-4 p-3 rounded-xl bg-slate-950/60 border border-slate-800/80 text-xs text-slate-400\">");
                sb.AppendLine($"                                <strong class=\"text-rose-400 block mb-1\"><i class=\"fa-solid fa-triangle-exclamation mr-1\"></i>Initial Challenge:</strong>{cs.Challenge}");
                sb.AppendLine("                            </div>");
                sb.AppendLine($"                            <div class=\"mb-4 p-3 rounded-xl bg-slate-950/60 border border-slate-800/80 text-xs text-slate-400\">");
                sb.AppendLine($"                                <strong class=\"text-cyan-400 block mb-1\"><i class=\"fa-solid fa-wand-magic-sparkles mr-1\"></i>Deployed Solution:</strong>{cs.Solution}");
                sb.AppendLine("                            </div>");
                sb.AppendLine("                        </div>");
                sb.AppendLine($"                        <p class=\"text-xs text-slate-300 font-medium italic pt-3 border-t border-slate-800\"><i class=\"fa-solid fa-circle-check text-emerald-400 mr-1.5\"></i>{cs.ResultDescription}</p>");
                sb.AppendLine("                    </div>");
            }

            sb.AppendLine("                </div>");
            sb.AppendLine("            </div>");
            sb.AppendLine("        </section>");
        }

        // =========================================================================
        // LEADERSHIP & PROFESSIONAL BIO (mod-team)
        // =========================================================================
        if (modules.Contains("mod-team") && payload.LeaderBio != null)
        {
            var bio = payload.LeaderBio;
            sb.AppendLine("        <!-- Leadership & Authority Bio Section -->");
            sb.AppendLine("        <section class=\"py-24 relative\">");
            sb.AppendLine("            <div class=\"max-w-5xl mx-auto px-4 sm:px-6 lg:px-8\">");
            sb.AppendLine("                <div class=\"p-8 sm:p-12 rounded-3xl glass-card border border-slate-700/80 shadow-2xl grid grid-cols-1 md:grid-cols-12 gap-8 items-center\">");
            sb.AppendLine("                    <div class=\"md:col-span-4 text-center md:text-left\">");
            sb.AppendLine($"                        <div class=\"w-32 h-32 mx-auto md:mx-0 rounded-2xl flex items-center justify-center font-black text-4xl shadow-2xl mb-4\" style=\"background: linear-gradient(135deg, {primaryHex}, {accentHex}); color: #000;\">");
            sb.AppendLine($"                            {bio.AvatarInitials}");
            sb.AppendLine("                        </div>");
            sb.AppendLine($"                        <h3 class=\"text-xl font-bold text-white\">{bio.Name}</h3>");
            sb.AppendLine($"                        <p class=\"text-xs font-semibold text-cyan-400 mt-1\">{bio.Role}</p>");
            sb.AppendLine($"                        <span class=\"mt-3 inline-block px-3 py-1 rounded-full bg-slate-900 border border-slate-700 text-[11px] text-slate-300 font-medium\">{bio.Credentials}</span>");
            sb.AppendLine("                    </div>");
            sb.AppendLine("                    <div class=\"md:col-span-8 space-y-4\">");
            sb.AppendLine($"                        <h4 class=\"text-2xl font-bold text-white tracking-tight\">Leadership &amp; Professional Authority</h4>");
            sb.AppendLine($"                        <p class=\"text-slate-300 text-sm leading-relaxed\">{bio.Bio}</p>");
            if (!string.IsNullOrWhiteSpace(bio.Quote))
            {
                sb.AppendLine($"                        <blockquote class=\"p-4 rounded-xl bg-slate-900/80 border-l-4 border-l-cyan-400 text-xs italic text-slate-300\">\"{bio.Quote}\"</blockquote>");
            }
            sb.AppendLine($"                        <div class=\"pt-2 flex flex-wrap gap-3 text-xs text-slate-400\">");
            sb.AppendLine($"                            <span class=\"flex items-center\"><i class=\"fa-solid fa-award text-amber-400 mr-1.5\"></i>Certified Specialist</span>");
            sb.AppendLine($"                            <span class=\"flex items-center\"><i class=\"fa-solid fa-user-shield text-emerald-400 mr-1.5\"></i>Direct Accountability</span>");
            sb.AppendLine($"                            <span class=\"flex items-center\"><i class=\"fa-solid fa-location-dot text-cyan-400 mr-1.5\"></i>Based in {city}</span>");
            sb.AppendLine("                        </div>");
            sb.AppendLine("                    </div>");
            sb.AppendLine("                </div>");
            sb.AppendLine("            </div>");
            sb.AppendLine("        </section>");
        }

        // =========================================================================
        // TRANSPARENT PRICING & PACKAGES MATRIX (mod-pricing)
        // =========================================================================
        if (modules.Contains("mod-pricing") && payload.PricingPlans.Count > 0)
        {
            sb.AppendLine("        <!-- Pricing & Packages Section -->");
            sb.AppendLine("        <section id=\"pricing\" class=\"py-24 bg-slate-950/50 border-t border-slate-800/80\">");
            sb.AppendLine("            <div class=\"max-w-7xl mx-auto px-4 sm:px-6 lg:px-8\">");
            sb.AppendLine("                <div class=\"text-center max-w-3xl mx-auto mb-16\">");
            sb.AppendLine("                    <span class=\"text-xs font-bold uppercase tracking-widest text-cyan-400 mb-2 block\">Pricing &amp; Packages</span>");
            sb.AppendLine($"                    <h2 class=\"text-3xl sm:text-4xl font-extrabold text-white tracking-tight\">Transparent Packages Designed for Every Stage</h2>");
            sb.AppendLine("                    <p class=\"text-slate-400 mt-3 text-sm\">Zero hidden retainers, no surprise overhead, and 100% formal scope agreements.</p>");
            sb.AppendLine("                </div>");
            sb.AppendLine($"                <div class=\"grid grid-cols-1 md:grid-cols-{Math.Min(3, payload.PricingPlans.Count)} gap-8 items-stretch\">");

            foreach (var plan in payload.PricingPlans)
            {
                var popularRing = plan.IsPopular ? "border-2 border-cyan-400 shadow-2xl relative" : "border border-slate-800";
                var badgeHtml = !string.IsNullOrWhiteSpace(plan.Badge)
                    ? $"<span class=\"absolute -top-3.5 left-1/2 transform -translate-x-1/2 px-3 py-1 rounded-full text-[10px] font-bold uppercase tracking-wider bg-cyan-400 text-slate-950 shadow-md\">{plan.Badge}</span>"
                    : "";

                sb.AppendLine($"                    <div class=\"p-8 rounded-3xl glass-card flex flex-col justify-between {popularRing}\">");
                sb.AppendLine($"                        {badgeHtml}");
                sb.AppendLine("                        <div>");
                sb.AppendLine($"                            <h3 class=\"text-xl font-bold text-white mb-2\">{plan.Name}</h3>");
                sb.AppendLine($"                            <p class=\"text-xs text-slate-400 mb-6\">{plan.Description}</p>");
                sb.AppendLine("                            <div class=\"flex items-baseline space-x-1 mb-6\">");
                sb.AppendLine($"                                <span class=\"text-4xl font-black text-white\">{plan.Price}</span>");
                sb.AppendLine($"                                <span class=\"text-xs text-slate-400\">{plan.Period}</span>");
                sb.AppendLine("                            </div>");
                sb.AppendLine("                            <ul class=\"space-y-3 mb-8 text-xs text-slate-300\">");
                foreach (var f in plan.Features)
                {
                    sb.AppendLine($"                                <li class=\"flex items-center space-x-2.5\"><i class=\"fa-solid fa-circle-check text-cyan-400\"></i><span>{f}</span></li>");
                }
                sb.AppendLine("                            </ul>");
                sb.AppendLine("                        </div>");
                sb.AppendLine($"                        <a href=\"#contact\" class=\"w-full py-3.5 rounded-xl text-center text-sm font-bold {buttonClass} block shadow-lg transition transform hover:scale-[1.02]\">{plan.Cta}</a>");
                sb.AppendLine("                    </div>");
            }

            sb.AppendLine("                </div>");
            sb.AppendLine("            </div>");
            sb.AppendLine("        </section>");
        }

        // =========================================================================
        // INTERACTIVE ROI / COST ESTIMATOR (mod-calculator)
        // =========================================================================
        if (modules.Contains("mod-calculator") && payload.Calculator != null)
        {
            var calc = payload.Calculator;
            var multStr = calc.Multiplier.ToString(CultureInfo.InvariantCulture);
            sb.AppendLine("        <!-- Interactive ROI / Cost Estimator -->");
            sb.AppendLine("        <section class=\"py-20 relative bg-gradient-to-b from-slate-900/40 to-slate-950/60 border-t border-slate-800/80\">");
            sb.AppendLine("            <div class=\"max-w-4xl mx-auto px-4 sm:px-6 lg:px-8\">");
            sb.AppendLine("                <div class=\"p-8 sm:p-12 rounded-3xl glass-card border border-cyan-500/30 shadow-2xl text-center\">");
            sb.AppendLine($"                    <span class=\"text-xs font-bold uppercase tracking-widest text-cyan-400 mb-2 block\">Interactive Financial Modeling</span>");
            sb.AppendLine($"                    <h2 class=\"text-3xl font-extrabold text-white mb-3\">{calc.Title}</h2>");
            sb.AppendLine($"                    <p class=\"text-slate-400 text-sm max-w-xl mx-auto mb-10\">{calc.Subtitle}</p>");
            sb.AppendLine("                    <div class=\"max-w-2xl mx-auto space-y-6 text-left mb-10\">");
            sb.AppendLine("                        <div class=\"flex justify-between items-center\">");
            sb.AppendLine($"                            <label class=\"text-xs font-bold uppercase text-slate-300\">{calc.SliderLabel}</label>");
            sb.AppendLine($"                            <span id=\"calcInputVal\" class=\"text-2xl font-black text-white\">{calc.Unit}{calc.DefaultValue:N0}</span>");
            sb.AppendLine("                        </div>");
            sb.AppendLine($"                        <input type=\"range\" min=\"{calc.Min}\" max=\"{calc.Max}\" value=\"{calc.DefaultValue}\" step=\"500\" class=\"w-full h-2.5 bg-slate-800 rounded-lg appearance-none cursor-pointer accent-cyan-400\" oninput=\"updateCalculator(this.value)\">");
            sb.AppendLine("                        <div class=\"flex justify-between text-[11px] text-slate-500 font-mono\">");
            sb.AppendLine($"                            <span>{calc.Unit}{calc.Min:N0}</span>");
            sb.AppendLine($"                            <span>{calc.Unit}{calc.Max:N0}+</span>");
            sb.AppendLine("                        </div>");
            sb.AppendLine("                    </div>");
            sb.AppendLine("                    <div class=\"p-6 rounded-2xl bg-slate-950/80 border border-slate-800 inline-block max-w-md w-full mb-8\">");
            sb.AppendLine($"                        <div class=\"text-xs uppercase tracking-wider text-slate-400 mb-2 font-bold\">{calc.ResultLabel}</div>");
            var defaultCalculated = Math.Round(calc.DefaultValue * calc.Multiplier);
            sb.AppendLine($"                        <div id=\"calcOutputVal\" class=\"text-4xl sm:text-5xl font-black text-cyan-400\">{calc.Unit}{defaultCalculated:N0}{calc.ResultSuffix}</div>");
            sb.AppendLine("                    </div>");
            sb.AppendLine("                    <div>");
            sb.AppendLine($"                        <a href=\"#contact\" class=\"px-8 py-4 rounded-xl text-base font-bold shadow-xl {buttonClass} transition transform hover:scale-105 inline-block\">Lock In These Projected Returns</a>");
            sb.AppendLine("                    </div>");
            sb.AppendLine("                </div>");
            sb.AppendLine("            </div>");
            sb.AppendLine("        </section>");
        }

        // Testimonials Section
        if (modules.Contains("mod-testimonials") && payload.Testimonials.Count > 0)
        {
            sb.AppendLine("        <!-- Testimonials Section -->");
            sb.AppendLine("        <section id=\"testimonials\" class=\"py-24 bg-slate-950/50 border-t border-slate-800/80\">");
            sb.AppendLine("            <div class=\"max-w-7xl mx-auto px-4 sm:px-6 lg:px-8\">");
            sb.AppendLine("                <div class=\"text-center max-w-2xl mx-auto mb-16\">");
            sb.AppendLine("                    <div class=\"text-amber-400 text-lg mb-2\"><i class=\"fa-solid fa-star\"></i><i class=\"fa-solid fa-star\"></i><i class=\"fa-solid fa-star\"></i><i class=\"fa-solid fa-star\"></i><i class=\"fa-solid fa-star\"></i></div>");
            sb.AppendLine($"                    <h2 class=\"text-3xl sm:text-4xl font-extrabold text-white tracking-tight\">Real Results from Real Clients in {city}</h2>");
            sb.AppendLine("                </div>");
            sb.AppendLine("                <div class=\"grid grid-cols-1 md:grid-cols-3 gap-8\">");

            foreach (var t in payload.Testimonials)
            {
                sb.AppendLine("                    <div class=\"p-6 rounded-2xl glass-card flex flex-col justify-between\">");
                sb.AppendLine("                        <div>");
                sb.AppendLine("                            <div class=\"flex items-center space-x-1 text-amber-400 text-xs mb-4\"><i class=\"fa-solid fa-star\"></i><i class=\"fa-solid fa-star\"></i><i class=\"fa-solid fa-star\"></i><i class=\"fa-solid fa-star\"></i><i class=\"fa-solid fa-star\"></i></div>");
                sb.AppendLine($"                            <p class=\"text-slate-300 text-sm italic mb-6 leading-relaxed\">\"{t.Quote}\"</p>");
                sb.AppendLine("                        </div>");
                sb.AppendLine("                        <div class=\"flex items-center space-x-3\">");
                sb.AppendLine($"                            <div class=\"w-10 h-10 rounded-full bg-slate-800 flex items-center justify-center font-bold text-white\">{t.Initials}</div>");
                sb.AppendLine($"                            <div><div class=\"text-sm font-bold text-white\">{t.Author}</div><div class=\"text-xs text-slate-400\">{t.Role}</div></div>");
                sb.AppendLine("                        </div>");
                sb.AppendLine("                    </div>");
            }

            sb.AppendLine("                </div>");
            sb.AppendLine("            </div>");
            sb.AppendLine("        </section>");
        }

        // FAQ Accordion
        if (modules.Contains("mod-faq") && payload.Faqs.Count > 0)
        {
            sb.AppendLine("        <!-- FAQ Accordion -->");
            sb.AppendLine("        <section id=\"faq\" class=\"py-20 max-w-4xl mx-auto px-4 sm:px-6\">");
            sb.AppendLine("            <div class=\"text-center mb-12\">");
            sb.AppendLine("                <h2 class=\"text-2xl sm:text-3xl font-extrabold text-white\">Frequently Asked Questions</h2>");
            sb.AppendLine("                <p class=\"text-slate-400 text-sm mt-2\">Got questions? We have clear answers.</p>");
            sb.AppendLine("            </div>");
            sb.AppendLine("            <div class=\"space-y-4\">");

            foreach (var faq in payload.Faqs)
            {
                sb.AppendLine("                <div class=\"border border-slate-800 rounded-xl bg-slate-900/60 p-5 cursor-pointer\" onclick=\"toggleFaq(this)\">");
                sb.AppendLine($"                    <div class=\"flex justify-between items-center font-semibold text-white text-base\"><span>{faq.Question}</span><i class=\"fa-solid fa-chevron-down text-xs transition transform\"></i></div>");
                sb.AppendLine($"                    <p class=\"text-slate-400 text-sm mt-3 hidden leading-relaxed\">{faq.Answer}</p>");
                sb.AppendLine("                </div>");
            }

            sb.AppendLine("            </div>");
            sb.AppendLine("        </section>");
        }

        // Lead Capture Form
        if (modules.Contains("mod-lead-form"))
        {
            sb.AppendLine("        <!-- Lead Capture Form -->");
            sb.AppendLine("        <section id=\"contact\" class=\"py-24 relative bg-gradient-to-b from-slate-900/80 to-slate-950 border-t border-slate-800/80\">");
            sb.AppendLine("            <div class=\"max-w-3xl mx-auto px-4 sm:px-6 lg:px-8\">");
            sb.AppendLine("                <div class=\"p-8 sm:p-12 rounded-3xl glass-card shadow-2xl border border-slate-700/80\">");
            sb.AppendLine("                    <div class=\"text-center mb-10\">");
            sb.AppendLine($"                        <h2 class=\"text-3xl font-extrabold text-white tracking-tight\">{payload.LeadFormTitle}</h2>");
            sb.AppendLine($"                        <p class=\"text-slate-400 text-sm mt-2\">{payload.LeadFormSubtitle}</p>");
            sb.AppendLine("                    </div>");
            sb.AppendLine("                    <form id=\"leadForm\" onsubmit=\"handleFormSubmit(event)\" class=\"space-y-5\">");
            sb.AppendLine("                        <div class=\"grid grid-cols-1 sm:grid-cols-2 gap-5\">");
            sb.AppendLine("                            <div>");
            sb.AppendLine("                                <label class=\"block text-xs font-semibold uppercase tracking-wider text-slate-300 mb-2\">Full Name *</label>");
            sb.AppendLine("                                <input type=\"text\" required placeholder=\"e.g. Kashif Khan\" class=\"w-full px-4 py-3 rounded-xl bg-slate-900/90 border border-slate-700 text-white placeholder-slate-500 text-sm focus:outline-none focus:border-cyan-400\">");
            sb.AppendLine("                            </div>");
            sb.AppendLine("                            <div>");
            sb.AppendLine("                                <label class=\"block text-xs font-semibold uppercase tracking-wider text-slate-300 mb-2\">Phone / WhatsApp *</label>");
            sb.AppendLine($"                                <input type=\"tel\" required placeholder=\"{phone}\" class=\"w-full px-4 py-3 rounded-xl bg-slate-900/90 border border-slate-700 text-white placeholder-slate-500 text-sm focus:outline-none focus:border-cyan-400\">");
            sb.AppendLine("                            </div>");
            sb.AppendLine("                        </div>");
            sb.AppendLine("                        <div>");
            sb.AppendLine("                            <label class=\"block text-xs font-semibold uppercase tracking-wider text-slate-300 mb-2\">Email Address</label>");
            sb.AppendLine("                            <input type=\"email\" placeholder=\"name@company.com\" class=\"w-full px-4 py-3 rounded-xl bg-slate-900/90 border border-slate-700 text-white placeholder-slate-500 text-sm focus:outline-none focus:border-cyan-400\">");
            sb.AppendLine("                        </div>");

            if (payload.Services.Count > 0)
            {
                sb.AppendLine("                        <div>");
                sb.AppendLine("                            <label class=\"block text-xs font-semibold uppercase tracking-wider text-slate-300 mb-2\">Interested Service</label>");
                sb.AppendLine("                            <select class=\"w-full px-4 py-3 rounded-xl bg-slate-900/90 border border-slate-700 text-white text-sm focus:outline-none focus:border-cyan-400\">");
                foreach (var s in payload.Services)
                {
                    sb.AppendLine($"                                <option value=\"{s.Title}\">{s.Title}</option>");
                }
                sb.AppendLine("                            </select>");
                sb.AppendLine("                        </div>");
            }

            sb.AppendLine("                        <div>");
            sb.AppendLine("                            <label class=\"block text-xs font-semibold uppercase tracking-wider text-slate-300 mb-2\">Project / Service Details</label>");
            sb.AppendLine($"                            <textarea rows=\"3\" placeholder=\"Tell us about your requirements in {city}...\" class=\"w-full px-4 py-3 rounded-xl bg-slate-900/90 border border-slate-700 text-white placeholder-slate-500 text-sm focus:outline-none focus:border-cyan-400\"></textarea>");
            sb.AppendLine("                        </div>");
            sb.AppendLine($"                        <button type=\"submit\" class=\"w-full py-4 rounded-xl text-base shadow-xl transition transform hover:scale-[1.01] {buttonClass}\">");
            sb.AppendLine($"                            <i class=\"fa-solid fa-paper-plane mr-2\"></i>Confirm &amp; Send Request");
            sb.AppendLine("                        </button>");
            sb.AppendLine("                        <p class=\"text-xs text-center text-slate-500 mt-3\"><i class=\"fa-solid fa-lock mr-1\"></i>100% Confidential • No Spam Guarantee</p>");
            sb.AppendLine("                    </form>");
            sb.AppendLine("                </div>");
            sb.AppendLine("            </div>");
            sb.AppendLine("        </section>");
        }

        sb.AppendLine("    </main>");

        // Footer
        sb.AppendLine("    <!-- Footer -->");
        sb.AppendLine("    <footer class=\"border-t border-slate-800 bg-slate-950 py-12 text-slate-400 text-sm\">");
        sb.AppendLine("        <div class=\"max-w-7xl mx-auto px-4 flex flex-col sm:flex-row justify-between items-center gap-6\">");
        sb.AppendLine($"            <div>&copy; {DateTime.UtcNow.Year} {bizName}. All Rights Reserved. Powered by TheKhanSoft LeadPilot Engine.</div>");
        sb.AppendLine("            <div class=\"flex space-x-6 text-slate-400 text-xs\">");
        sb.AppendLine("                <a href=\"#\" class=\"hover:text-white\">Privacy Policy</a>");
        sb.AppendLine("                <a href=\"#\" class=\"hover:text-white\">Terms of Service</a>");
        sb.AppendLine($"                <a href=\"tel:{phone}\" class=\"hover:text-white\"><i class=\"fa-solid fa-phone mr-1\"></i>{phone}</a>");
        sb.AppendLine("            </div>");
        sb.AppendLine("        </div>");
        sb.AppendLine("    </footer>");

        // WhatsApp Floating Button
        if (modules.Contains("mod-whatsapp"))
        {
            var cleanPhone = new string(phone.Where(char.IsDigit).ToArray());
            if (string.IsNullOrWhiteSpace(cleanPhone)) cleanPhone = "923001234567";
            sb.AppendLine("    <!-- Floating WhatsApp Widget -->");
            sb.AppendLine($"    <a href=\"https://wa.me/{cleanPhone}?text=Hello%20{Uri.EscapeDataString(bizName)},%20I%20would%20like%20to%20inquire%20about%20your%20services.\" target=\"_blank\" class=\"fixed bottom-6 right-6 z-50 flex items-center space-x-3 bg-emerald-500 hover:bg-emerald-600 text-white px-5 py-3.5 rounded-full shadow-2xl transition transform hover:scale-105 group\">");
            sb.AppendLine("        <i class=\"fa-brands fa-whatsapp text-2xl\"></i>");
            sb.AppendLine($"        <span class=\"font-bold text-sm hidden sm:inline\">Chat with {bizName}</span>");
            sb.AppendLine("    </a>");
        }

        // Scripts
        sb.AppendLine("    <script>");
        sb.AppendLine("        function toggleFaq(el) {");
        sb.AppendLine("            const p = el.querySelector('p');");
        sb.AppendLine("            const icon = el.querySelector('i');");
        sb.AppendLine("            if (p.classList.contains('hidden')) {");
        sb.AppendLine("                p.classList.remove('hidden');");
        sb.AppendLine("                icon.classList.add('rotate-180');");
        sb.AppendLine("            } else {");
        sb.AppendLine("                p.classList.add('hidden');");
        sb.AppendLine("                icon.classList.remove('rotate-180');");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine("        function selectSlot(btn) {");
        sb.AppendLine("            document.querySelectorAll('.slot-btn').forEach(b => b.classList.remove('border-emerald-500', 'bg-emerald-500/20'));");
        sb.AppendLine("            btn.classList.add('border-emerald-500', 'bg-emerald-500/20');");
        sb.AppendLine("        }");
        sb.AppendLine("        function updateCalculator(val) {");
        var multVal = payload.Calculator?.Multiplier ?? 2.8;
        var unitVal = payload.Calculator?.Unit ?? "$";
        var sfxVal = payload.Calculator?.ResultSuffix ?? " / year";
        sb.AppendLine($"            const mult = {multVal.ToString(CultureInfo.InvariantCulture)};");
        sb.AppendLine($"            const unit = '{unitVal}';");
        sb.AppendLine($"            const sfx = '{sfxVal}';");
        sb.AppendLine("            const num = Number(val);");
        sb.AppendLine("            document.getElementById('calcInputVal').innerText = unit + num.toLocaleString();");
        sb.AppendLine("            document.getElementById('calcOutputVal').innerText = unit + Math.round(num * mult).toLocaleString() + sfx;");
        sb.AppendLine("        }");
        sb.AppendLine("        function handleFormSubmit(e) {");
        sb.AppendLine("            e.preventDefault();");
        sb.AppendLine($"            alert('Thank you! Your request has been confirmed with {bizName}. An advisor will connect with you shortly.');");
        sb.AppendLine("            e.target.reset();");
        sb.AppendLine("        }");
        sb.AppendLine("    </script>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }

    private static (string eyebrow, string title, string subtitle) GetCategoryServicesContent(string category, string bizName, string city)
    {
        var catLower = category.ToLowerInvariant();
        if (catLower.Contains("health") || catLower.Contains("clinic") || catLower.Contains("medic") || catLower.Contains("doctor") || catLower.Contains("dental"))
            return ("Clinical Specialties", $"Comprehensive Healthcare Specialties & Diagnostics in {city}", $"Every treatment plan is directed by board-certified practitioners at {bizName} with zero waiting times.");
        if (catLower.Contains("soft") || catLower.Contains("it") || catLower.Contains("tech") || catLower.Contains("saas") || catLower.Contains("developer"))
            return ("Full-Stack Capabilities", $"Engineering Scalable Cloud, AI & Software Systems", $"Modern architectural standards, robust security frameworks, and rapid execution sprints designed for scale.");
        if (catLower.Contains("educat") || catLower.Contains("school") || catLower.Contains("attend") || catLower.Contains("academ"))
            return ("Campus Solutions", $"Automated Attendance & Cloud Campus ERP Infrastructure", $"Empowering educators, students, and parents with real-time biometric tracking and seamless communication.");
        if (catLower.Contains("real") || catLower.Contains("prop") || catLower.Contains("build") || catLower.Contains("construct"))
            return ("Portfolio & Developments", $"Prime Residential & Commercial Developments in {city}", $"Turnkey property developments, architectural excellence, and transparent investment opportunities.");
        if (catLower.Contains("law") || catLower.Contains("legal") || catLower.Contains("consult"))
            return ("Practice Areas", $"Specialized Advisory & Strategic Corporate Counsel in {city}", $"Uncompromising advocacy, regulatory compliance, and confidential partner-level representation.");
        if (catLower.Contains("solar") || catLower.Contains("energy"))
            return ("Clean Energy Solutions", $"Turnkey Solar Infrastructure & Net-Metering Systems", $"Tier-1 solar engineering, customized load calculations, and long-term utility bill elimination in {city}.");
        return ("Tailored Offerings", $"High-Impact {category} Capabilities in {city}", $"Engineered with precision, industry-leading methodology, and personalized care for every {bizName} client.");
    }

    private static string GetCategoryCaseStudiesHeading(string category, string city)
    {
        var catLower = category.ToLowerInvariant();
        if (catLower.Contains("health") || catLower.Contains("clinic") || catLower.Contains("dental"))
            return $"Proven Clinical Transformations & Patient Outcomes in {city}";
        if (catLower.Contains("soft") || catLower.Contains("it") || catLower.Contains("tech") || catLower.Contains("saas"))
            return $"Architecture Benchmarks & Deployed Infrastructure in {city}";
        if (catLower.Contains("educat") || catLower.Contains("school") || catLower.Contains("attend"))
            return $"Campus Transformations & Attendance Benchmarks in {city}";
        return $"Measurable Commercial Benchmarks Delivered Across {city}";
    }

    private static string GetCategoryPricingHeading(string category, string city)
    {
        var catLower = category.ToLowerInvariant();
        if (catLower.Contains("health") || catLower.Contains("clinic") || catLower.Contains("dental"))
            return "Transparent Clinical Care & Diagnostic Investment Schedules";
        if (catLower.Contains("soft") || catLower.Contains("it") || catLower.Contains("tech") || catLower.Contains("saas"))
            return "Predictable Development Sprints & Cloud Infrastructure Retainers";
        if (catLower.Contains("educat") || catLower.Contains("school") || catLower.Contains("attend"))
            return "Institutional School Licensing & Biometric Deployment Packages";
        return $"Transparent Packages Designed for Every Stage in {city}";
    }

    private static string GetCategoryReviewsHeading(string category, string city, string bizName = "Our Platform")
    {
        var catLower = category.ToLowerInvariant();
        if (catLower.Contains("health") || catLower.Contains("clinic") || catLower.Contains("dental"))
            return $"Verified Patient Experiences & Health Outcomes in {city}";
        if (catLower.Contains("soft") || catLower.Contains("it") || catLower.Contains("tech") || catLower.Contains("saas"))
            return $"What Engineering Leaders & CTOs Say About Our Systems";
        if (catLower.Contains("educat") || catLower.Contains("school") || catLower.Contains("attend"))
            return $"What Principals & Educators Say in {city}";
        return $"Real Results from Real Clients in {city}";
    }

    private static BespokeContentPayload GetCategoryDefaults(string category, string bizName, string city, string country)
    {
        var catLower = category.ToLowerInvariant();

        // 1. Healthcare & Dental
        if (catLower.Contains("health") || catLower.Contains("clinic") || catLower.Contains("doctor") || catLower.Contains("dental") || catLower.Contains("hospital") || catLower.Contains("medical"))
        {
            return new BespokeContentPayload
            {
                Headline = $"Premier Walk-In Healthcare & Urgent Medical Services in {city}",
                Subheadline = $"Don't wait hours in hospital emergency rooms. {bizName} provides same-day appointments, board-certified physicians, and comprehensive on-site diagnostics.",
                HeroBadge = $"#1 Rated Healthcare Provider in {city}",
                CallToAction = "Book Priority Appointment",
                SecondaryCta = "Call Direct Hotline",
                ConversionScore = 96,
                Metrics =
                [
                    new() { Value = "0 Min", Label = $"Wait with Priority Slot" },
                    new() { Value = "12,000+", Label = $"{city} Patients Served" },
                    new() { Value = "100%", Label = "Board-Certified MDs" },
                    new() { Value = "4.9 ★", Label = "Verified Patient Rating" }
                ],
                TrustBadges = ["HIPAA Compliant", "Board-Certified Specialists", "Urgent Care Accredited", $"{city} Medical Network"],
                Services =
                [
                    new() { Icon = "fa-solid fa-stethoscope", Title = "Urgent & Same-Day Walk-In Care", Description = "Immediate, compassionate treatment for acute illness, minor injuries, cuts, and sudden infections without hospital delays.", Cta = "Book Urgent Care" },
                    new() { Icon = "fa-solid fa-x-ray", Title = "Digital Onsite X-Ray & Lab Testing", Description = "Instant diagnostic clarity with state-of-the-art digital radiology and rapid turnaround point-of-care laboratory panels.", Cta = "Explore Diagnostics" },
                    new() { Icon = "fa-solid fa-child", Title = "Pediatric & Family Medicine", Description = "Gentle, experienced primary and wellness care designed specifically for infants, children, teenagers, and whole households.", Cta = "Schedule Family Care" },
                    new() { Icon = "fa-solid fa-heart-pulse", Title = "Preventative Health & Executive Physicals", Description = "Comprehensive cardiovascular evaluations, chronic illness monitoring, vaccinations, and proactive wellness screenings.", Cta = "Plan Health Checkup" }
                ],
                Differentiators =
                [
                    new() { Icon = "fa-solid fa-clock", Title = "Zero-Wait Priority Access", Description = "Reserve your consultation online to be seen within minutes of arrival by a licensed medical practitioner." },
                    new() { Icon = "fa-solid fa-user-doctor", Title = "100% Attending Physician Care", Description = "Every diagnosis and treatment plan is conducted by seasoned, board-certified doctors, not unsupervised trainees." },
                    new() { Icon = "fa-solid fa-file-invoice-dollar", Title = "Transparent, Predictable Billing", Description = "Upfront copay and self-pay pricing schedules with absolutely zero surprise bills or hidden facility fees." }
                ],
                CaseStudies =
                [
                    new() { ClientName = $"{city} Family Emergency", Challenge = "Severe pediatric ear infection on weekend evening when ER wait was 4 hours", Solution = "Same-day priority admission with onsite pediatric evaluation and direct antibiotic prescription", ResultMetric = "7 Min Intake", ResultDescription = "Complete relief provided with zero emergency room fees" },
                    new() { ClientName = $"{city} Sports Orthopedic Injury", Challenge = "Suspected wrist fracture requiring immediate radiological confirmation", Solution = "High-definition digital radiology with board-certified radiologist read in 15 minutes", ResultMetric = "35 Min Total", ResultDescription = "Splint applied and referral formalized without hospital hospitalization" }
                ],
                PricingPlans =
                [
                    new() { Name = "Urgent Consultation", Price = "$79", Period = "/visit", Badge = "", Description = "Complete clinical diagnosis & prescription", Features = ["Attending MD Examination", "Vitals & Oxygen Screening", "Digital Prescription Sent to Pharmacy", "Free 48-Hour Followup Message"], Cta = "Book Visit", IsPopular = false },
                    new() { Name = "Comprehensive Care + Labs", Price = "$149", Period = "/visit", Badge = "Most Popular", Description = "Full diagnostic battery with digital imaging", Features = ["All Urgent Consultation Features", "Onsite Digital X-Ray (Single Region)", "Rapid Point-of-Care Blood/Urine Panel", "Written Specialist Clinical Report"], Cta = "Reserve Priority Slot", IsPopular = true },
                    new() { Name = "Family Annual Wellness", Price = "$399", Period = "/year", Badge = "Best Value", Description = "Year-round priority healthcare for up to 4 members", Features = ["Unlimited Priority Queue Access", "4 Annual Comprehensive Wellness Physicals", "Discounted In-House Radiology & Labs", "24/7 Dedicated Physician WhatsApp Hotline"], Cta = "Enroll Family", IsPopular = false }
                ],
                LeaderBio = new BespokeLeaderBio
                {
                    Name = $"Dr. Alexander Miller, MD",
                    Role = "Chief Medical Director",
                    Credentials = "Board-Certified Emergency & Family Medicine • 18+ Yrs",
                    Bio = $"Dr. Miller founded {bizName} to bring compassionate, zero-wait clinical care directly to families across {city}. Having directed busy emergency rooms, he eliminated bureaucratic delays to focus entirely on patient healing.",
                    Quote = "Medicine is not just about prescriptions; it's about giving patients immediate reassurance and expert care when they feel most vulnerable.",
                    AvatarInitials = "AM"
                },
                Calculator = new BespokeCalculatorConfig
                {
                    Title = "Hospital ER vs. Urgent Care Savings Estimator",
                    Subtitle = "Compare typical hospital emergency room bills with our transparent upfront outpatient pricing.",
                    SliderLabel = "Estimated Number of Annual Outpatient Visits",
                    Unit = "",
                    Min = 1,
                    Max = 12,
                    DefaultValue = 3,
                    Multiplier = 450,
                    ResultLabel = "Estimated Out-of-Pocket Savings",
                    ResultSuffix = " saved vs. Hospital ER"
                },
                Testimonials =
                [
                    new() { Quote = $"When my child developed an intense fever on Sunday evening, {bizName} in {city} took us into an exam room in under 10 minutes. The doctor was compassionate, gentle, and thorough.", Author = "Sarah Jenkins", Role = $"Parent & {city} Resident", Initials = "SJ", Rating = 5 },
                    new() { Quote = $"Injured my wrist during recreation. Got digital imaging, diagnostic review, and a supportive brace in 35 minutes flat. Far superior and more cost-effective than the hospital ER.", Author = "David Miller", Role = $"{city} Local", Initials = "DM", Rating = 5 },
                    new() { Quote = $"The cleanest clinic and most respectful medical staff I've encountered in {city}. Booking via WhatsApp was effortless, and the medication instructions were crystal clear.", Author = "Dr. Asma Tariq", Role = $"Healthcare Professional, {city}", Initials = "AT", Rating = 5 }
                ],
                Faqs =
                [
                    new() { Question = $"Do I need an advance appointment for walk-in medical services at {bizName}?", Answer = "Walk-ins are welcomed 7 days a week! However, booking your arrival slot online guarantees you are seated in an exam room without waiting." },
                    new() { Question = $"Which insurance and payment options are accepted?", Answer = "We accept all major commercial insurance plans, regional health networks, and offer transparent, discounted self-pay rates for uninsured patients." },
                    new() { Question = "What types of conditions can be treated at your clinic?", Answer = "We treat non-life-threatening illnesses and injuries including lacerations, sprains, minor fractures, respiratory infections, allergies, burns, and pediatric ailments." },
                    new() { Question = "Are onsite laboratory panels and digital X-rays open on weekends?", Answer = "Yes, our CLIA-certified testing facility and digital imaging suite operate throughout all business hours including evenings and weekends." }
                ],
                LeadFormTitle = "Schedule Priority Medical Consultation",
                LeadFormSubtitle = $"Fill out the form below. Our {city} care coordinator will confirm your preferred arrival time within 15 minutes."
            };
        }

        // 2. Software & IT Agencies
        if (catLower.Contains("software") || catLower.Contains("it") || catLower.Contains("tech") || catLower.Contains("saas") || catLower.Contains("developer") || catLower.Contains("computer"))
        {
            return new BespokeContentPayload
            {
                Headline = $"Accelerate Enterprise Growth with Elite Software & Cloud Solutions in {city}",
                Subheadline = $"From high-velocity SaaS engineering to mission-critical cloud modernization, {bizName} delivers production-ready systems that scale seamlessly and outperform expectations.",
                HeroBadge = $"Award-Winning Software Engineering Firm in {city}",
                CallToAction = "Schedule Technical Briefing",
                SecondaryCta = "Explore Past Architecture",
                ConversionScore = 95,
                Metrics =
                [
                    new() { Value = "150+", Label = "Custom Platforms Shipped" },
                    new() { Value = "99.99%", Label = "High-Availability SLA" },
                    new() { Value = "3.4x", Label = "Average Client ROI" },
                    new() { Value = "100%", Label = "IP & Codebase Ownership" }
                ],
                TrustBadges = ["AWS Certified Partner", "ISO 27001 Security", "Google Cloud Partner", "Agile Certified Delivery"],
                Services =
                [
                    new() { Icon = "fa-solid fa-code", Title = "Custom SaaS & Web Application Development", Description = "Bespoke cloud-native web architectures engineered with Next.js, .NET Core, and PostgreSQL for maximum performance and security.", Cta = "Build Your SaaS" },
                    new() { Icon = "fa-solid fa-mobile-screen", Title = "Cross-Platform Mobile App Engineering", Description = "Fluid, responsive iOS and Android applications utilizing Flutter and React Native with native device capability integration.", Cta = "Build Mobile App" },
                    new() { Icon = "fa-solid fa-cloud-arrow-up", Title = "DevOps, Kubernetes & Cloud Architecture", Description = "Automated CI/CD deployment pipelines, zero-downtime infrastructure orchestration, and automated cost optimization on AWS and Azure.", Cta = "Optimize Cloud" },
                    new() { Icon = "fa-solid fa-brain", Title = "AI Integration & Intelligent Automation", Description = "Embed cutting-edge LLMs, document understanding, and predictive analytics pipelines directly into your operational workflows.", Cta = "Deploy AI Systems" }
                ],
                Differentiators =
                [
                    new() { Icon = "fa-solid fa-gauge-high", Title = "Rapid Bi-Weekly Sprint Deliverables", Description = "Watch your working product evolve every 14 days with tangible functional demos and continuous stakeholder alignment." },
                    new() { Icon = "fa-solid fa-shield-halved", Title = "100% Unencumbered Source Code Ownership", Description = "All intellectual property, repositories, Docker images, and documentation belong strictly to your company from day one." },
                    new() { Icon = "fa-solid fa-headset", Title = "Post-Launch SLA & Dedicated Engineering Support", Description = "Guaranteed 15-minute response times for critical incidents, proactive security patching, and ongoing feature velocity." }
                ],
                CaseStudies =
                [
                    new() { ClientName = "FinTech Global Payments", Challenge = "Monolithic backend experiencing latency spikes and transaction timeout failures during peak volume", Solution = "Migrated to event-driven microservices architecture utilizing Kafka and high-throughput .NET 8 APIs", ResultMetric = "99.99% Uptime", ResultDescription = "Latency reduced by 74% and handled $140M in transaction volume without an error" },
                    new() { ClientName = "Logistics Telematics Mobile Platform", Challenge = "Field technicians struggling with offline data synchronization and manual paper reporting", Solution = "Engineered cross-platform mobile app with SQLite offline caching and background sync to cloud database", ResultMetric = "4.2x Throughput", ResultDescription = "Eliminated 100% of paper logs and boosted driver delivery capacity by 35%" }
                ],
                PricingPlans =
                [
                    new() { Name = "MVP Acceleration Sprint", Price = "$4,900", Period = "/sprint", Badge = "", Description = "Rapid 3-week proof-of-concept release", Features = ["UI/UX Interactive Figma Prototype", "Production Full-Stack Architecture", "Core Database & Authentication", "CI/CD Deployment to Cloud Vercel/AWS"], Cta = "Book Discovery", IsPopular = false },
                    new() { Name = "Dedicated Product Team", Price = "$9,500", Period = "/month", Badge = "Recommended", Description = "Autonomous cross-functional development squad", Features = ["Senior Full-Stack Engineers & Architect", "Bi-Weekly Production Sprint Releases", "Direct Slack/Teams Channel Integration", "Guaranteed 99.9% Uptime SLA Guarantee"], Cta = "Hire Squad", IsPopular = true },
                    new() { Name = "Enterprise Cloud Modernization", Price = "Custom", Period = "/engagement", Badge = "", Description = "Complete legacy system overhaul & migration", Features = ["Enterprise Architecture Governance", "Zero-Downtime Database Migration", "Kubernetes Multi-Region Orchestration", "24/7/365 On-Call SRE Incident Response"], Cta = "Request Briefing", IsPopular = false }
                ],
                LeaderBio = new BespokeLeaderBio
                {
                    Name = $"{bizName} Engineering Leadership",
                    Role = "Principal Solutions Architect",
                    Credentials = "AWS Certified Solutions Architect Professional • 15+ Yrs",
                    Bio = $"Our engineering leadership team has architected mission-critical platforms handling millions of transactions. We bridge commercial strategy with uncompromising software engineering quality.",
                    Quote = "Great code isn't measured by lines written, but by the commercial value it creates and how reliably it runs in production.",
                    AvatarInitials = "CTO"
                },
                Calculator = new BespokeCalculatorConfig
                {
                    Title = "Engineering Velocity & ROI Estimator",
                    Subtitle = "Calculate potential annual development cost savings compared to hiring full-time in-house engineering squads.",
                    SliderLabel = "Monthly Engineering Budget / Team Size",
                    Unit = "$",
                    Min = 3000,
                    Max = 40000,
                    DefaultValue = 10000,
                    Multiplier = 2.4,
                    ResultLabel = "Estimated Value & Operational Savings",
                    ResultSuffix = " / year"
                },
                Testimonials =
                [
                    new() { Quote = $"{bizName} transformed our legacy monolithic software into a modern microservices architecture ahead of schedule. The engineering discipline and communication in {city} were exceptional.", Author = "Alexander Wright", Role = "CTO, VentureScale", Initials = "AW", Rating = 5 },
                    new() { Quote = $"Our mobile app launch exceeded 50,000 active users in the first quarter with zero stability issues. {bizName}'s technical leadership and clean code standards made this possible.", Author = "Elena Rostova", Role = "VP of Product, FinTech Pulse", Initials = "ER", Rating = 5 },
                    new() { Quote = $"Outstanding partners. They didn't just build our specifications; they challenged our assumptions and saved us thousands in unnecessary cloud infrastructure overhead.", Author = "Marcus Vance", Role = "Founder & CEO, CloudAutomate", Initials = "MV", Rating = 5 }
                ],
                Faqs =
                [
                    new() { Question = "How does your team handle project discovery and scope definition?", Answer = "We conduct a thorough 1-week technical discovery phase to define system architecture, data models, user flows, and a fixed milestone-based roadmap." },
                    new() { Question = "Who owns the intellectual property and code repository during development?", Answer = "You retain 100% ownership of all source code, architectural designs, algorithms, and deployment configurations from the moment they are committed." },
                    new() { Question = "What is the typical timeline for an MVP or phase-one release?", Answer = "Most core MVP systems are designed, engineered, tested, and deployed to production staging within 6 to 12 weeks." },
                    new() { Question = "Do you provide ongoing SLA maintenance and infrastructure monitoring?", Answer = "Yes, we provide tiered SLA support agreements including 24/7 uptime monitoring, vulnerability audits, and proactive dependency upgrades." }
                ],
                LeadFormTitle = "Request Technical Consultation & Architecture Review",
                LeadFormSubtitle = "Share your project vision below. Our Principal Solutions Architect will respond with actionable technical insights within 2 business hours."
            };
        }

        // 2b. Education, Attendance & EdTech
        if (catLower.Contains("educat") || catLower.Contains("school") || catLower.Contains("attend") || catLower.Contains("academ") || catLower.Contains("train") || catLower.Contains("college") || catLower.Contains("student"))
        {
            return new BespokeContentPayload
            {
                Headline = $"Smart Biometric Attendance & Cloud School Management in {city}",
                Subheadline = $"Eliminate manual paper registers, notify parents instantly via automated WhatsApp/SMS alerts upon student entry, and streamline school tuition fees with {bizName}.",
                HeroBadge = $"#1 Rated School Automation Partner in {city}",
                CallToAction = "Schedule Live Campus Demo",
                SecondaryCta = "Calculate Campus ROI",
                ConversionScore = 96,
                Metrics =
                [
                    new() { Value = "99.8%", Label = "Attendance Tracking Accuracy" },
                    new() { Value = "0 Mins", Label = "Manual Register Paperwork" },
                    new() { Value = "45+", Label = $"Institutions Modernized in {city}" },
                    new() { Value = "18,000+", Label = "Daily Student Logins" }
                ],
                TrustBadges = ["Biometric Hardware Certified", "ISO 27001 Data Security", "Parent WhatsApp Gateway", $"{city} Educational Alliance"],
                Services =
                [
                    new() { Icon = "fa-solid fa-fingerprint", Title = "Biometric & RFID Attendance Tracking", Description = "Instant student arrival logging using high-speed facial recognition and RFID smart cards with automated parent notifications.", Cta = "Explore Biometrics" },
                    new() { Icon = "fa-solid fa-graduation-cap", Title = "All-in-One Cloud School ERP", Description = "Streamlined fee voucher collection, online examination gradebooks, teacher timetables, and mobile campus portal.", Cta = "View ERP Modules" },
                    new() { Icon = "fa-solid fa-bell", Title = "Instant Parent Alert SMS & WhatsApp", Description = "Automated real-time broadcast system for unexcused absences, examination dates, emergency campus closures, and fee reminders.", Cta = "Test Alert Gateway" }
                ],
                Differentiators =
                [
                    new() { Icon = "fa-solid fa-shield-halved", Title = "Bank-Grade Student Data Privacy", Description = "Encrypted biometric storage conforming strictly to international student privacy standards and local data governance laws." },
                    new() { Icon = "fa-solid fa-wifi", Title = "Offline-First Sync Capability", Description = "Biometric terminals operate reliably during power or internet interruptions, auto-syncing the moment connection restores." },
                    new() { Icon = "fa-solid fa-chalkboard-user", Title = "Turnkey Teacher & Staff Training", Description = "Our local field team conducts hands-on on-campus training for your administrative staff and teachers." }
                ],
                CaseStudies =
                [
                    new() { ClientName = $"{city} Model High School (1,400 Students)", Challenge = "Morning roll calls consumed 25 minutes of instructional time per class with frequent unauthorized departures", Solution = "Installed 4 biometric entrance terminals paired with automated SMS parent dispatch", ResultMetric = "0 Min Roll Call", ResultDescription = "Student punctuality improved 42% and unverified absences dropped to near zero" }
                ],
                PricingPlans =
                [
                    new() { Name = "Starter Campus (Up to 500)", Price = "$49", Period = "/month", Badge = "", Description = "Complete attendance & basic student records", Features = ["2x Biometric Terminal Integration", "Automated Daily SMS / WhatsApp Alerts", "Digital Fee Voucher Tracking", "Basic Teacher & Student Portals"], Cta = "Start Pilot", IsPopular = false },
                    new() { Name = "Standard Academy (Up to 1,500)", Price = "$99", Period = "/month", Badge = "Most Popular", Description = "Full-featured cloud school ERP suite", Features = ["4x High-Speed Facial/RFID Terminals", "Full Examination & Report Card Engine", "Parent Mobile App with Fee Payments", "Dedicated Local Field Support Specialist"], Cta = "Deploy Standard", IsPopular = true },
                    new() { Name = "Multi-Campus Enterprise", Price = "$199", Period = "/month", Badge = "", Description = "Centralized governance across campus networks", Features = ["Unlimited Campus Terminals", "Centralized Super-Admin Dashboard", "Custom Ministry Reporting Export", "24/7 Priority Emergency Hotline"], Cta = "Contact Sales", IsPopular = false }
                ],
                LeaderBio = new BespokeLeaderBio
                {
                    Name = $"Director of Educational Technology",
                    Role = "Chief Academic Solutions Architect",
                    Credentials = "EdTech Systems Specialist • 14+ Yrs",
                    Bio = $"Leading the modernization of regional educational institutions in {city}. Passionate about empowering teachers and ensuring transparent parent-school partnerships.",
                    Quote = "When schools digitize attendance and communication, teachers get back their time to do what matters most: inspire students.",
                    AvatarInitials = "ED"
                },
                Calculator = new BespokeCalculatorConfig
                {
                    Title = "Campus Administrative Time & Fee Recovery Estimator",
                    Subtitle = "Adjust the slider to your student body size to preview estimated weekly hours saved and fee collection recovery.",
                    SliderLabel = "Total Enrolled Students",
                    Unit = "Students: ",
                    Min = 200,
                    Max = 3000,
                    DefaultValue = 800,
                    Multiplier = 1.75,
                    ResultLabel = "Estimated Hours of Administrative Paperwork Saved Monthly",
                    ResultSuffix = " hrs/mo"
                },
                Testimonials =
                [
                    new() { Quote = $"{bizName} transformed our morning routine. Parents receive an SMS within 3 seconds of their child tapping in. Attendance disputes have vanished completely.", Author = "Prof. Tariq Mahmood", Role = $"Principal, {city} Cambridge School", Initials = "TM", Rating = 5 },
                    new() { Quote = $"The fee collection module alone paid for the system in 2 months. Parents can pay directly online and our accounts office has zero paperwork backlogs.", Author = "Mrs. Farzana Khan", Role = "Administrator, City Grammar Campus", Initials = "FK", Rating = 5 },
                    new() { Quote = $"Super reliable hardware. Even during power cuts, the backup batteries keep attendance running smoothly. Highly recommended for any serious school.", Author = "Engr. Asadullah", Role = "Campus Operations Director", Initials = "EA", Rating = 5 }
                ],
                Faqs =
                [
                    new() { Question = "What happens if our school internet connection goes down during morning entry?", Answer = "Our biometric terminals operate with full onboard offline memory. Students tap in normally, and all records sync to the cloud the instant internet returns." },
                    new() { Question = "Are parents charged any extra fees to receive WhatsApp or SMS notifications?", Answer = "No, all parent notifications are included within your institutional monthly plan with zero per-message charges." },
                    new() { Question = "How quickly can the system and hardware be deployed on our campus?", Answer = $"Our local team in {city} completes hardware mounting, network configuration, and student enrollment within 3 business days." }
                ],
                LeadFormTitle = "Schedule a Free On-Campus ERP Demonstration",
                LeadFormSubtitle = $"Enter your school details below. Our {city} academic specialist will visit your campus for a live demonstration."
            };
        }

        // 3. Restaurants & Food
        if (catLower.Contains("restaurant") || catLower.Contains("food") || catLower.Contains("cafe") || catLower.Contains("dining") || catLower.Contains("bakery"))
        {
            return new BespokeContentPayload
            {
                Headline = $"Experience Unrivaled Culinary Artistry & Hospitality in {city}",
                Subheadline = $"Fresh locally-sourced seasonal ingredients, master-crafted recipes, and an intimate dining ambiance crafted by {bizName} for memorable gatherings.",
                HeroBadge = $"Top-Rated Dining Destination in {city}",
                CallToAction = "Reserve Your Table",
                SecondaryCta = "View Seasonal Menu",
                ConversionScore = 94,
                Metrics =
                [
                    new() { Value = "100%", Label = "Farm-Fresh Local Ingredients" },
                    new() { Value = "4.9 ★", Label = "Customer Review Average" },
                    new() { Value = "25k+", Label = "Guests Delighted Annually" },
                    new() { Value = "Top 5", Label = $"Culinary Venue in {city}" }
                ],
                TrustBadges = ["Master Chef Excellence", "Locally Sourced Organic", "Food Safety Certified", "Chamber of Commerce"],
                Services =
                [
                    new() { Icon = "fa-solid fa-utensils", Title = "Artisanal Dine-In & Chef's Table", Description = "An unforgettable multi-course sensory journey highlighting seasonal gastronomy, paired wines, and exceptional service.", Cta = "Book Dine-In" },
                    new() { Icon = "fa-solid fa-glass-cheers", Title = "Private Events & Executive Banquets", Description = "Exclusive dining spaces designed for corporate dinners, wedding celebrations, and milestone private parties.", Cta = "Plan Private Event" },
                    new() { Icon = "fa-solid fa-truck-fast", Title = "Express Gourmet Catering & Delivery", Description = "Restaurant-quality culinary platters and hot banquet delivery prepared fresh for corporate offices and private homes.", Cta = "Order Catering" }
                ],
                Differentiators =
                [
                    new() { Icon = "fa-solid fa-leaf", Title = "Farm-to-Fork Philosophy", Description = $"Every dish is prepared using fresh produce and organic meats delivered daily from trusted regional farmers near {city}." },
                    new() { Icon = "fa-solid fa-award", Title = "Award-Winning Master Chefs", Description = "Our kitchen brigade brings international culinary credentials and deep reverence for traditional flavor craftsmanship." },
                    new() { Icon = "fa-solid fa-heart", Title = "Warm, Attentive Hospitality", Description = "From table greeting to dessert presentation, our front-of-house team ensures you feel genuinely celebrated." }
                ],
                CaseStudies =
                [
                    new() { ClientName = "Annual Corporate Gala (120 Guests)", Challenge = "Strict multi-course dietary requirements across executive leadership", Solution = "Bespoke 5-course seasonal tasting menu paired with sommelier-selected reserve wines", ResultMetric = "100% Satisfaction", ResultDescription = "Delivered seamless simultaneous table service with zero delays" }
                ],
                PricingPlans =
                [
                    new() { Name = "Seasonal Tasting Menu", Price = "$65", Period = "/guest", Badge = "", Description = "4-course curated dining experience", Features = ["Chef's Amuse-Bouche", "Choice of Artisan Appetizer", "Prime Seasonal Entree", "Signature House Dessert"], Cta = "Reserve Seats", IsPopular = false },
                    new() { Name = "Sommelier Signature Experience", Price = "$110", Period = "/guest", Badge = "Most Popular", Description = "6-course pairing journey", Features = ["All Tasting Menu Courses", "Exclusive Vintage Wine Pairings", "Chef's Table Kitchen Tour", "Priority Window Seating Guarantee"], Cta = "Book Chef's Table", IsPopular = true },
                    new() { Name = "Private Banquet Buyout", Price = "Custom", Period = "/event", Badge = "", Description = "Exclusive venue access for up to 100 guests", Features = ["Full Exclusive Dining Room Access", "Custom Monogrammed Menu Cards", "Dedicated Mixologist & Full Bar", "Audio/Visual Presentation Setup"], Cta = "Inquire Dates", IsPopular = false }
                ],
                LeaderBio = new BespokeLeaderBio
                {
                    Name = "Executive Chef Matteo Rossi",
                    Role = "Culinary Director & Founder",
                    Credentials = "Michelin-Trained Gastronomy Specialist • 20+ Yrs",
                    Bio = $"Chef Matteo founded {bizName} in {city} to bring unpretentious culinary excellence and vibrant regional flavors directly to discerning diners.",
                    Quote = "True hospitality is about transforming dinner into an unforgettable story that friends share around the table.",
                    AvatarInitials = "MR"
                },
                Testimonials =
                [
                    new() { Quote = $"Hands down the most memorable dining experience in {city}. The ribeye and seasonal sides were cooked to absolute perfection. Impeccable wine pairings.", Author = "Sophia Martinez", Role = $"{city} Food Enthusiast", Initials = "SM", Rating = 5 },
                    new() { Quote = $"Hosted our annual executive corporate dinner at {bizName}. The private dining suite, customized menu cards, and discreet service blew our clients away.", Author = "James Bennett", Role = "Managing Director, Global Media", Initials = "JB", Rating = 5 },
                    new() { Quote = $"Warm atmosphere, breathtaking presentation, and flavors that linger in the best possible way. This is now our go-to celebration spot in {city}.", Author = "Chloe Dupont", Role = $"{city} Resident", Initials = "CD", Rating = 5 }
                ],
                Faqs =
                [
                    new() { Question = $"How far in advance should I reserve a table at {bizName}?", Answer = "We recommend reserving weekend tables at least 3-5 days in advance, although weekday walk-ins are seated whenever capacity permits." },
                    new() { Question = "Do you accommodate specialized dietary restrictions or food allergies?", Answer = "Absolutely! Our culinary team happily tailors dishes for gluten-free, vegetarian, vegan, and dairy-free guests upon advance notice." },
                    new() { Question = "What is the guest capacity for private banquets and celebrations?", Answer = "Our private dining room accommodates intimate parties of 12 up to full venue buyouts of 120 seated guests." }
                ],
                LeadFormTitle = "Reserve Your Table or Event",
                LeadFormSubtitle = "Submit your reservation request below. Our concierge team will reach out with instant confirmation."
            };
        }

        // 4. Solar & Renewable Energy
        if (catLower.Contains("solar") || catLower.Contains("energy") || catLower.Contains("power") || catLower.Contains("electric"))
        {
            return new BespokeContentPayload
            {
                Headline = $"Slash Energy Bills with Commercial & Residential Solar in {city}",
                Subheadline = $"Unlock clean renewable independence, lock in zero-carbon tax incentives, and safeguard your property against power grid outages with {bizName}.",
                HeroBadge = $"#1 Certified Solar EPC Contractor in {city}",
                CallToAction = "Request Free Solar Feasibility Audit",
                SecondaryCta = "Calculate ROI Savings",
                ConversionScore = 95,
                Metrics =
                [
                    new() { Value = "Up to 85%", Label = "Reduction in Monthly Electric Bills" },
                    new() { Value = "25 Yrs", Label = "Tier-1 Linear Performance Warranty" },
                    new() { Value = "8.4 MW+", Label = $"Solar Deployed in {city}" },
                    new() { Value = "1,800+", Label = "Satisfied Property Owners" }
                ],
                TrustBadges = ["Tier-1 Solar Panels", "Licensed Master Electricians", "Net Metering Certified", "NABCEP Accredited"],
                Services =
                [
                    new() { Icon = "fa-solid fa-solar-panel", Title = "Residential Rooftop Solar Installations", Description = "High-efficiency monocrystalline solar panels engineered for peak energy harvest, roof aesthetics, and 25-year reliability.", Cta = "Get Home Solar" },
                    new() { Icon = "fa-solid fa-industry", Title = "Commercial & Industrial Solar Arrays", Description = "High-yield commercial solar PV systems designed to reduce corporate operating expenses and capitalize on tax depreciation.", Cta = "Explore Commercial" },
                    new() { Icon = "fa-solid fa-car-battery", Title = "Smart Battery Backup & Storage", Description = "Seamless lithium battery storage systems (Tesla Powerwall / Enphase) providing instant emergency power during blackouts.", Cta = "View Storage Options" }
                ],
                Differentiators =
                [
                    new() { Icon = "fa-solid fa-chart-line", Title = "Guaranteed 3 to 4 Year Payback ROI", Description = "We design high-efficiency systems engineered to pay for themselves rapidly through net metering and electric bill credits." },
                    new() { Icon = "fa-solid fa-file-signature", Title = "Turnkey Net Metering & Permit Approvals", Description = $"Our dedicated compliance team handles all utility interconnect paperwork, municipal permits, and approvals in {city}." },
                    new() { Icon = "fa-solid fa-screwdriver-wrench", Title = "25-Year Equipment & Workmanship Coverage", Description = "Complete peace of mind backed by manufacturer warranties, storm durability guarantees, and ongoing monitoring." }
                ],
                CaseStudies =
                [
                    new() { ClientName = $"{city} Logistics Cold Storage Warehouse", Challenge = "Surging commercial electricity tariff exceeding $14,000 monthly", Solution = "Engineered 180kW rooftop array with smart load-shifting lithium storage", ResultMetric = "-78% Electric Bill", ResultDescription = "$130k saved annually with a 3.1-year total system payback period" },
                    new() { ClientName = "Suburban Residential Homeowner", Challenge = "Frequent summer grid outages spoiling food and cutting air conditioning", Solution = "10kW Tier-1 system paired with dual 13.5kWh smart home batteries", ResultMetric = "$18/mo Power Bill", ResultDescription = "100% blackout resilience achieved with zero grid disruptions" }
                ],
                PricingPlans =
                [
                    new() { Name = "Residential Essential (6kW)", Price = "$8,500", Period = "turnkey", Badge = "", Description = "Ideal for single-family homes with $200/mo electric bills", Features = ["14x Tier-1 High-Efficiency Monocrystalline Panels", "Smart Solar Inverter with Mobile Monitoring", "Turnkey Municipal Permitting & Net Metering", "25-Year Manufacturer Linear Warranty"], Cta = "Audit My Roof", IsPopular = false },
                    new() { Name = "Executive Solar + Battery (12kW)", Price = "$16,900", Period = "turnkey", Badge = "Most Popular", Description = "Complete energy independence & whole-home blackout protection", Features = ["28x Ultra-High Yield All-Black Panels", "13.5 kWh Lithium Smart Battery Storage", "24/7 Real-Time Cloud Grid Monitoring", "Whole-Home Seamless Blackout Transfer Switch"], Cta = "Lock In Incentives", IsPopular = true },
                    new() { Name = "Commercial & Industrial Array", Price = "Custom", Period = "project", Badge = "", Description = "High-yield commercial systems for business facilities", Features = ["50kW to 1MW+ Commercial Engineering", "Corporate Tax Depreciation Optimization", "Structural Rooftop Feasibility Certification", "Dedicated Preventive O&M Service Contract"], Cta = "Request Commercial Bid", IsPopular = false }
                ],
                Calculator = new BespokeCalculatorConfig
                {
                    Title = "Solar ROI & 25-Year Savings Calculator",
                    Subtitle = "Slide to your current monthly electric bill to estimate your cumulative lifetime solar savings.",
                    SliderLabel = "Current Average Monthly Electric Bill",
                    Unit = "$",
                    Min = 100,
                    Max = 2000,
                    DefaultValue = 350,
                    Multiplier = 240, // 20 years of ~80% savings
                    ResultLabel = "Estimated 20-Year Lifetime Energy Savings",
                    ResultSuffix = " saved"
                },
                LeaderBio = new BespokeLeaderBio
                {
                    Name = "Engineer Tariq Al-Mansoor",
                    Role = "Chief Renewable Energy Engineer",
                    Credentials = "NABCEP Certified PV Installation Professional • 16+ Yrs",
                    Bio = $"Tariq has overseen the engineering and grid interconnect of over 85 megawatts of commercial and residential solar across {city} and international markets.",
                    Quote = "Clean solar power isn't just an ecological choice; it's the smartest financial investment a property owner can make.",
                    AvatarInitials = "TM"
                },
                Testimonials =
                [
                    new() { Quote = $"{bizName} installed a 12kW system on our home in {city}. Our monthly power bill dropped from $480 down to the $18 connection fee! Clean, fast, professional installation.", Author = "Brian O'Connor", Role = $"Homeowner, {city}", Initials = "BO", Rating = 5 },
                    new() { Quote = $"We outfitted our 40,000 sq ft industrial warehouse with a commercial solar array. {bizName} delivered on budget and our operational electricity expenses dropped 70%.", Author = "Fatima Zahra", Role = "Operations Director, EcoLogistics", Initials = "FZ", Rating = 5 },
                    new() { Quote = $"From the initial 3D rooftop audit to the final utility meter swap, {bizName} handled everything seamlessly. Their real-time mobile monitoring app is fantastic.", Author = "Kevin Vance", Role = "Property Developer", Initials = "KV", Rating = 5 }
                ],
                Faqs =
                [
                    new() { Question = $"How does net metering work with solar systems in {city}?", Answer = "When your solar panels produce excess energy during peak daylight, it is sent back to the grid in exchange for billing credits that offset evening power consumption." },
                    new() { Question = "What is the expected lifespan and warranty on your solar equipment?", Answer = "We only install Tier-1 monocrystalline panels with a 25-year manufacturer performance warranty guaranteeing at least 85% output at year 25." },
                    new() { Question = "Can I power my air conditioning and essential appliances during a grid blackout?", Answer = "Yes, when paired with our smart lithium battery backup systems, your power transfers automatically within milliseconds during any grid outage." }
                ],
                LeadFormTitle = "Claim Your Free Solar Savings Assessment",
                LeadFormSubtitle = $"Enter your details and estimated monthly bill. Our {city} solar engineer will generate a complimentary 3D rooftop layout within 24 hours."
            };
        }

        // 5. Banking, Finance & Investment
        if (catLower.Contains("bank") || catLower.Contains("financ") || catLower.Contains("invest") || catLower.Contains("wealth") || catLower.Contains("account") || catLower.Contains("loan"))
        {
            return new BespokeContentPayload
            {
                Headline = $"Empowering Financial Growth, Enterprise Lending & Modern Banking in {city}",
                Subheadline = $"From commercial working capital and trade finance to high-yield deposit accounts, {bizName} provides secure institutional banking and advisory built for sustainable prosperity.",
                HeroBadge = $"#1 Rated Financial Institution in {city}",
                CallToAction = "Open an Account Online",
                SecondaryCta = "Speak to Private Banker",
                ConversionScore = 96,
                Metrics =
                [
                    new() { Value = "100%", Label = "Deposit Security & Compliance" },
                    new() { Value = "24/7", Label = "Encrypted Digital Banking" },
                    new() { Value = "120+", Label = "Branch & ATM Network" },
                    new() { Value = "4.8 ★", Label = "Customer Trust Rating" }
                ],
                TrustBadges = ["State Bank Regulated", "ISO 27001 Security", "256-Bit SSL Encrypted", "Chamber of Commerce"],
                Services =
                [
                    new() { Icon = "fa-solid fa-building-columns", Title = "Commercial Lending & SME Working Capital", Description = "Flexible corporate credit lines, machinery financing, and fast-track working capital designed to scale regional businesses.", Cta = "Explore SME Loans" },
                    new() { Icon = "fa-solid fa-money-bill-transfer", Title = "Digital Corporate Treasury & Payroll", Description = "Automated salary disbursement, bulk payment routing, and real-time multi-currency liquidity management.", Cta = "Setup Corporate Treasury" },
                    new() { Icon = "fa-solid fa-hand-holding-dollar", Title = "Wealth Advisory & High-Yield Deposits", Description = "Bespoke asset management, fixed-income instruments, and personalized wealth preservation strategies.", Cta = "Consult Wealth Advisor" }
                ],
                Differentiators =
                [
                    new() { Icon = "fa-solid fa-shield-halved", Title = "Institutional-Grade Cyber Defense", Description = "Multi-factor cryptographic authentication and biometric safeguards guaranteeing your enterprise funds remain invulnerable." },
                    new() { Icon = "fa-solid fa-user-tie", Title = "Dedicated Relationship Manager", Description = $"Direct cell phone access to your assigned senior banker in {city} for immediate approvals without call center hold times." },
                    new() { Icon = "fa-solid fa-bolt", Title = "Instant SWIFT & RTGS Remittance", Description = "High-velocity domestic clearing and global foreign exchange settlement at competitive interbank rates." }
                ],
                CaseStudies =
                [
                    new() { ClientName = $"{city} Manufacturing Export Group", Challenge = "Supply chain delays due to slow letter of credit issuance by legacy banks", Solution = "Structured $2.5M digital trade finance facility with 24-hour document turnaround", ResultMetric = "24-Hr Issuance", ResultDescription = "Eliminated port demurrage costs and accelerated factory import cycles by 65%" }
                ],
                PricingPlans =
                [
                    new() { Name = "Business Preferred", Price = "$0", Period = "no monthly fee", Badge = "", Description = "Complete checking for growing companies", Features = ["Zero Minimum Balance Maintenance", "Free Digital RTGS / IBFT Transfers", "Multi-User Corporate Portal Access", "Complimentary Business Debit Cards"], Cta = "Open Account", IsPopular = false },
                    new() { Name = "Commercial Enterprise", Price = "Tiered", Period = "/quarter", Badge = "Most Popular", Description = "Full-service treasury suite for mid-market firms", Features = ["Dedicated Corporate Relationship Director", "Tailored Trade Finance & Import LC Lines", "Automated Payroll Management System", "Preferential Foreign Exchange Margins"], Cta = "Request Meeting", IsPopular = true },
                    new() { Name = "Private Wealth Client", Price = "Custom", Period = "portfolio", Badge = "", Description = "Exclusive wealth management & family office", Features = ["Bespoke Capital Allocation & Bonds", "VIP Airport Lounge & Concierge Access", "Exclusive Private Banker Direct Hotline", "Estate Planning & Generational Trusts"], Cta = "Private Inquiry", IsPopular = false }
                ],
                LeaderBio = new BespokeLeaderBio
                {
                    Name = $"{bizName} Executive Leadership",
                    Role = "Chief Executive & Managing Director",
                    Credentials = "Fellow Chartered Banker • 22+ Yrs Financial Leadership",
                    Bio = $"Leading {bizName} with uncompromising integrity and digital innovation. Committed to powering economic resilience for families and enterprises across {city}.",
                    Quote = "Sound banking is built on an unbreakable foundation of trust, prudent risk stewardship, and relentless service to our depositors.",
                    AvatarInitials = "MD"
                },
                Testimonials =
                [
                    new() { Quote = $"{bizName} approved our trade credit line in days when other banks took months. Their digital portal and prompt service in {city} keep our supply chain moving flawlessly.", Author = "Haji Munir Ahmad", Role = "Managing Director, Khyber Logistics", Initials = "HM", Rating = 5 },
                    new() { Quote = "The most responsive relationship managers in the banking sector. Anytime we have a complex foreign exchange transaction, our private banker handles it within minutes.", Author = "Kashif Riaz", Role = "CFO, Horizon Textile Mills", Initials = "KR", Rating = 5 },
                    new() { Quote = "Seamless digital mobile app, zero hidden charges, and exceptional customer courtesy at the branch. Highly recommended for both personal and corporate accounts.", Author = "Amina Begum", Role = $"Entrepreneur, {city}", Initials = "AB", Rating = 5 }
                ],
                Faqs =
                [
                    new() { Question = $"What documentation is required to open a corporate business account with {bizName}?", Answer = "We require company registration certificates (NTN/Incorporation), board resolution, authorized signatory CNIC/IDs, and verifiable business address proof." },
                    new() { Question = "Are online fund transfers and RTGS payments processed on weekends?", Answer = "Yes, our digital banking portal and mobile app operate 24/7/365 with instant IBFT transfers across all domestic financial institutions." },
                    new() { Question = "How quickly are commercial working capital and trade finance lines approved?", Answer = "Once complete financial statements and security valuations are submitted, formal underwriting decisions are provided within 5 business days." }
                ],
                LeadFormTitle = "Schedule an Executive Corporate Banking Consultation",
                LeadFormSubtitle = $"Submit your company details below. A Senior Relationship Manager in {city} will contact you within 2 hours."
            };
        }

        // 6. Legal & Law Chambers
        if (catLower.Contains("law") || catLower.Contains("legal") || catLower.Contains("attorney") || catLower.Contains("lawyer") || catLower.Contains("court") || catLower.Contains("advocat"))
        {
            return new BespokeContentPayload
            {
                Headline = $"Formidable Legal Advocacy & Strategic Corporate Counsel in {city}",
                Subheadline = $"Protecting your commercial rights and assets. {bizName} delivers premier trial representation, corporate transaction advisory, and resolute dispute resolution across all judicial forums.",
                HeroBadge = $"Top-Ranked Legal Counsel in {city}",
                CallToAction = "Schedule Confidential Legal Case Evaluation",
                SecondaryCta = "Call Direct Legal Chambers",
                ConversionScore = 96,
                Metrics =
                [
                    new() { Value = "98.2%", Label = "Case Resolution Success Rate" },
                    new() { Value = "20+ Yrs", Label = "Courtroom & Trial Experience" },
                    new() { Value = "$45M+", Label = "Corporate Disputes Resolved" },
                    new() { Value = "100%", Label = "Strict Attorney-Client Privilege" }
                ],
                TrustBadges = ["High Court Bar Association", "Certified Arbitrators", "Corporate Counsel Network", "Licensed Legal Practice"],
                Services =
                [
                    new() { Icon = "fa-solid fa-scale-balanced", Title = "Commercial Litigation & Dispute Resolution", Description = "Fierce representation in contract breaches, shareholder disputes, debt recovery, and complex high-stakes commercial trials.", Cta = "Retain Trial Counsel" },
                    new() { Icon = "fa-solid fa-file-contract", Title = "Corporate Governance & M&A Agreements", Description = "Comprehensive contract drafting, cross-border joint ventures, regulatory compliance, and corporate restructuring.", Cta = "Draft Agreements" },
                    new() { Icon = "fa-solid fa-gavel", Title = "Real Estate, Title & Property Law", Description = "In-depth land title verification, property dispute defense, development zoning permits, and tenancy litigation.", Cta = "Protect Property" }
                ],
                Differentiators =
                [
                    new() { Icon = "fa-solid fa-chess-knight", Title = "Tactical Trial Preparation", Description = "We analyze every legal contingency before stepping into court, building bulletproof evidentiary strategies that compel favorable outcomes." },
                    new() { Icon = "fa-solid fa-user-shield", Title = "Uncompromising Discretion & Integrity", Description = "All consultations, corporate documents, and legal strategies are safeguarded by strict confidentiality and legal ethics." },
                    new() { Icon = "fa-solid fa-handshake", Title = "Predictable Fee Transparency", Description = "Transparent retainer agreements and fixed-fee consultation options with zero unexpected administrative billing surprises." }
                ],
                CaseStudies =
                [
                    new() { ClientName = $"{city} Real Estate Commercial Dispute", Challenge = "Unlawful encroachment and conflicting land title claim threatening $8M commercial plaza development", Solution = "Secured urgent High Court injunctive order and conducted forensic land revenue record verification", ResultMetric = "Full Injunction Won", ResultDescription = "Cleared title unencumbered and protected client development investment within 21 days" }
                ],
                PricingPlans =
                [
                    new() { Name = "Legal Evaluation", Price = "$95", Period = "consultation", Badge = "", Description = "Complete 60-minute case audit & merits review", Features = ["Detailed Document & Contract Review", "Legal Merits & Probability Assessment", "Procedural Roadmap & Timeline", "Strict Confidential Attorney Privilege"], Cta = "Book Evaluation", IsPopular = false },
                    new() { Name = "Corporate Legal Retainer", Price = "$750", Period = "/month", Badge = "Most Popular", Description = "Ongoing advisory for operational companies", Features = ["Unlimited Contract Vetting & Drafting", "Regulatory & Labor Compliance Guidance", "Priority Court & Chamber Availability", "Pre-Litigation Dispute Negotiation"], Cta = "Retain Counsel", IsPopular = true },
                    new() { Name = "Trial Litigation Defense", Price = "Custom", Period = "case retainer", Badge = "", Description = "Comprehensive high-stakes court representation", Features = ["Full Judicial Pleadings & Evidence Filing", "Senior Advocate Trial Appearance", "Appellate Court Representation", "24/7 Dedicated Legal Case Officer"], Cta = "Schedule Case Review", IsPopular = false }
                ],
                LeaderBio = new BespokeLeaderBio
                {
                    Name = $"Advocate Senior Counsel",
                    Role = "Managing Partner & Senior Trial Attorney",
                    Credentials = "Barrister-at-Law • Advocate Supreme Court • 20+ Yrs",
                    Bio = $"Founding Partner at {bizName}. Dedicated to defending client rights, upholding constitutional rule of law, and delivering formidable corporate advocacy in {city}.",
                    Quote = "Justice favors the prepared. In the courtroom, meticulous preparation and commanding legal knowledge win cases.",
                    AvatarInitials = "SC"
                },
                Testimonials =
                [
                    new() { Quote = $"{bizName} represented our firm in a multi-million-rupee commercial contract dispute. Their courtroom presence, legal precision, and tactical composure secured a complete victory for us.", Author = "Taimur Shah", Role = $"Managing Director, {city} Commercial Group", Initials = "TS", Rating = 5 },
                    new() { Quote = "Invaluable corporate counsel. They vet all our vendor contracts, employment agreements, and regulatory filings. Professional, sharp, and always accessible.", Author = "Barrister Daniyal Khan", Role = "Corporate General Counsel", Initials = "DK", Rating = 5 }
                ],
                Faqs =
                [
                    new() { Question = $"How do I schedule an urgent consultation with {bizName}?", Answer = "You can book directly via our online portal or call our chambers hotline. Initial consultations are scheduled within 24 hours." },
                    new() { Question = "Is our initial discussion protected by attorney-client privilege?", Answer = "Yes, strictly. Under legal ethics and statutory privilege, every detail shared with our chambers remains 100% confidential." }
                ],
                LeadFormTitle = "Schedule a Confidential Legal Case Assessment",
                LeadFormSubtitle = $"Provide a brief summary of your legal matter below. Our Senior Partner in {city} will review your inquiry under strict confidentiality."
            };
        }

        // 7. Hotels, Lodging & Hospitality
        if (catLower.Contains("hotel") || catLower.Contains("lodging") || catLower.Contains("resort") || catLower.Contains("stay") || catLower.Contains("motel") || catLower.Contains("guest house"))
        {
            return new BespokeContentPayload
            {
                Headline = $"Experience Elegance, World-Class Hospitality & Luxury Stays in {city}",
                Subheadline = $"Immerse yourself in refined comfort. {bizName} offers opulent executive suites, authentic fine dining, and grand event spaces tailored for discerning travelers and milestone banquets.",
                HeroBadge = $"5-Star Rated Luxury Destination in {city}",
                CallToAction = "Check Room Availability & Best Rates",
                SecondaryCta = "Book Banquet / Wedding Hall",
                ConversionScore = 95,
                Metrics =
                [
                    new() { Value = "5-Star", Label = "Luxury Hospitality Standard" },
                    new() { Value = "4.8 ★", Label = "Guest Review Average" },
                    new() { Value = "24/7", Label = "Executive Concierge & Room Service" },
                    new() { Value = "1,500+", Label = "Corporate & Wedding Banquets Hosted" }
                ],
                TrustBadges = ["Clean & Safe Certified", "Global Hospitality Partner", "TripAdvisor Travelers' Choice", $"{city} Tourism Board"],
                Services =
                [
                    new() { Icon = "fa-solid fa-bed", Title = "Deluxe Executive Suites & Rooms", Description = "Soundproofed luxury accommodations featuring orthopedic mattresses, high-speed Wi-Fi, marble bathrooms, and scenic panoramic views.", Cta = "View Suites" },
                    new() { Icon = "fa-solid fa-champagne-glasses", Title = "Grand Banquets & Corporate Event Halls", Description = "Magnificent pillar-less ballrooms equipped with state-of-the-art audiovisual systems for grand weddings, conferences, and exhibitions.", Cta = "Plan Your Event" },
                    new() { Icon = "fa-solid fa-utensils", Title = "International Fine Dining & Rooftop Lounge", Description = "Master-crafted culinary specialties spanning Continental, Asian, and regional barbecue cuisines paired with ambient live music.", Cta = "Reserve Dining Table" }
                ],
                Differentiators =
                [
                    new() { Icon = "fa-solid fa-bell-concierge", Title = "Unmatched 24/7 White-Glove Concierge", Description = "From private airport chauffeur transfers to bespoke city excursions, our dedicated hospitality team fulfills every request seamlessly." },
                    new() { Icon = "fa-solid fa-shield-halved", Title = "Gated Security & Biometric Peace of Mind", Description = "Multi-tier perimeter security, private secured parking, and 24-hour surveillance ensuring complete family privacy." },
                    new() { Icon = "fa-solid fa-tags", Title = "Guaranteed Best Rate Direct Booking", Description = "Enjoy complimentary gourmet breakfast, flexible cancellation, and priority early check-in when booking direct." }
                ],
                CaseStudies =
                [
                    new() { ClientName = "International Tech Summit (450 Delegates)", Challenge = "Accommodating 450 global delegates with continuous catering, translation equipment, and executive security", Solution = "Full grand ballroom buyout paired with dedicated floor suites and synchronized 5-course banquet dining", ResultMetric = "Flawless Execution", ResultDescription = "Delivered seamless 3-day conference with 100% positive executive feedback" }
                ],
                PricingPlans =
                [
                    new() { Name = "Deluxe King Room", Price = "$110", Period = "/night", Badge = "", Description = "Ideal for solo business executives & couples", Features = ["Plush King Bed with 400-Thread Linens", "Complimentary High-Speed Wi-Fi", "Daily Gourmet Breakfast Buffet Included", "Free Airport Shuttle & Secured Parking"], Cta = "Book Room", IsPopular = false },
                    new() { Name = "Executive Panorama Suite", Price = "$185", Period = "/night", Badge = "Most Popular", Description = "Spacious living lounge with panoramic views", Features = ["All Deluxe Features + Separate Living Room", "Exclusive Executive Lounge Access & High Tea", "Deep Marble Soaking Tub & Rain Shower", "Guaranteed Late 3:00 PM Check-Out"], Cta = "Reserve Suite", IsPopular = true },
                    new() { Name = "Presidential Luxury Suite", Price = "$350", Period = "/night", Badge = "", Description = "Unrivaled presidential luxury and privacy", Features = ["Master Bedroom + Dining Room + Private Kitchen", "Dedicated 24/7 Personal Butler Service", "Private Chauffeur Airport Limousine Transfer", "Complimentary In-Suite Spa Treatment"], Cta = "Book Presidential", IsPopular = false }
                ],
                LeaderBio = new BespokeLeaderBio
                {
                    Name = $"{bizName} General Management",
                    Role = "General Manager & Hospitality Director",
                    Credentials = "Swiss Hospitality Certified • 24+ Yrs Luxury Hotel Leadership",
                    Bio = $"Dedicated to elevating luxury hospitality standards in {city}. Ensuring that every guest experiences heartfelt warmth, impeccable culinary artistry, and restful tranquility.",
                    Quote = "True hospitality is not merely providing a room; it is crafting an atmosphere of grace, elegance, and unforgettable memories.",
                    AvatarInitials = "GM"
                },
                Testimonials =
                [
                    new() { Quote = $"{bizName} is easily the premier hotel in {city}. The suites are immaculately maintained, the breakfast buffet is delicious, and the staff treats you like royalty.", Author = "Zulqarnain Haider", Role = $"Corporate Traveler, {city}", Initials = "ZH", Rating = 5 },
                    new() { Quote = "We held our daughter's wedding in the Grand Ballroom. The lighting, decor, and live barbecue catering were out of this world. Guests are still praising the arrangements!", Author = "Mrs. Parveen Akhtar", Role = "Peshawar Resident", Initials = "PA", Rating = 5 }
                ],
                Faqs =
                [
                    new() { Question = $"What are the standard check-in and check-out times at {bizName}?", Answer = "Standard check-in is at 2:00 PM and check-out is at 12:00 PM. Early check-in and late check-out are accommodated based on availability." },
                    new() { Question = "Do you offer airport pickup and drop-off services?", Answer = "Yes, complimentary airport shuttle transfers are available for suite bookings, and private executive chauffeurs can be arranged upon request." }
                ],
                LeadFormTitle = "Check Room Rates & Request Event Availability",
                LeadFormSubtitle = $"Select your dates below. Our front desk reservations team in {city} will confirm your booking with instant best-rate guarantee."
            };
        }

        // 8. Real Estate, Builders & Construction
        if (catLower.Contains("real estate") || catLower.Contains("builder") || catLower.Contains("construct") || catLower.Contains("property") || catLower.Contains("architect"))
        {
            return new BespokeContentPayload
            {
                Headline = $"Premier Real Estate Development, Architectural Construction & Properties in {city}",
                Subheadline = $"Build with uncompromising structural excellence. {bizName} delivers turnkey architectural design, commercial construction, and verified high-yield property investments across prime locations.",
                HeroBadge = $"#1 Rated Construction & Real Estate Group in {city}",
                CallToAction = "Schedule Free Site & Project Consultation",
                SecondaryCta = "Explore Prime Property Listings",
                ConversionScore = 95,
                Metrics =
                [
                    new() { Value = "250+", Label = "Properties & Plazas Built" },
                    new() { Value = "100%", Label = "Earthquake-Resistant Engineering" },
                    new() { Value = "18+ Yrs", Label = $"Development Leadership in {city}" },
                    new() { Value = "Zero", Label = "Project Delivery Delay Record" }
                ],
                TrustBadges = ["Licensed Master Builder", "Structural Engineering Certified", "Zoning & CDA/PDA Approved", "ISO 9001 Construction"],
                Services =
                [
                    new() { Icon = "fa-solid fa-trowel-bricks", Title = "Turnkey Commercial & Residential Construction", Description = "End-to-end building construction covering structural engineering, premium grey structure, and bespoke luxury interior finishes.", Cta = "Build Your Property" },
                    new() { Icon = "fa-solid fa-compass-drafting", Title = "3D Architectural Design & Interior Planning", Description = "Award-winning modern architectural blueprints, structural load calculations, and photorealistic 3D interior renders.", Cta = "Commission Architecture" },
                    new() { Icon = "fa-solid fa-house-circle-check", Title = "Prime Property Sales & Verified Land Titles", Description = "Guaranteed unencumbered commercial plots, residential homes, and high-ROI rental investment portfolios with verified documentation.", Cta = "View Verified Deals" }
                ],
                Differentiators =
                [
                    new() { Icon = "fa-solid fa-award", Title = "Grade-60 Steel & Premium Certified Materials", Description = "We never cut corners. All foundations utilize laboratory-tested concrete mixes, Grade-60 deformed steel, and verified branded fittings." },
                    new() { Icon = "fa-solid fa-calendar-check", Title = "Milestone-Linked Escrow Payments", Description = "You pay only as each physical construction milestone is inspected, certified, and approved by your independent engineer." },
                    new() { Icon = "fa-solid fa-file-shield", Title = "100% Legal Title Due Diligence", Description = "Every property sold undergoes comprehensive revenue authority vetting, registry verification, and zero-litigation certification." }
                ],
                CaseStudies =
                [
                    new() { ClientName = $"{city} 7-Story Commercial Plaza", Challenge = "Complex urban foundation requiring deep piling near adjacent existing structures", Solution = "Engineered rotary bored pile foundation paired with seismic-resistant shear wall frame", ResultMetric = "Delivered on Budget", ResultDescription = "Completed 14 months from groundbreaking with 100% retail space pre-leased" }
                ],
                PricingPlans =
                [
                    new() { Name = "Grey Structure Package", Price = "$28", Period = "/sq ft", Badge = "", Description = "Heavy-duty structural framework & brickwork", Features = ["Grade-60 Deformed Steel Framing", "Termite & Anti-Damp Waterproofing Barrier", "Underground Concrete Water Tank Construction", "Full Architectural & Structural Working Drawings"], Cta = "Get Construction Quote", IsPopular = false },
                    new() { Name = "Turnkey Luxury Finish", Price = "$52", Period = "/sq ft", Badge = "Most Popular", Description = "Complete move-in ready architectural elegance", Features = ["Imported Porcelain Tile / Hardwood Flooring", "Designer Kitchen Cabinets & Quartz Countertops", "Modern False Ceilings with Ambient LED Lighting", "10-Year Structural Engineering Warranty"], Cta = "Build Turnkey", IsPopular = true },
                    new() { Name = "Architectural & Interior Design", Price = "Custom", Period = "design", Badge = "", Description = "Bespoke blueprint & 3D visualization suite", Features = ["Complete Structural & MEP Working Blueprints", "High-Definition 3D Exterior & Interior Walkthrough", "Municipal Authority Submission & NOC Approvals", "Weekly Onsite Construction Supervision Visits"], Cta = "Hire Architect", IsPopular = false }
                ],
                LeaderBio = new BespokeLeaderBio
                {
                    Name = $"Engr. Tariq Aziz",
                    Role = "Chief Structural Engineer & Developer",
                    Credentials = "PEC Certified Professional Engineer • MS Structural Engineering",
                    Bio = $"Over two decades building landmark residences, commercial towers, and housing communities in {city}. Renowned for engineering precision and uncompromising construction integrity.",
                    Quote = "A building is a monument to family legacy. We build every square foot as if our own children will live there.",
                    AvatarInitials = "TA"
                },
                Testimonials =
                [
                    new() { Quote = $"{bizName} constructed our 1-kanal dream residence in {city}. They finished 3 weeks ahead of schedule and the finishing quality is breathtaking. Honest, dependable builders.", Author = "Brig. (R) Javed Iqbal", Role = $"Homeowner, {city}", Initials = "JI", Rating = 5 },
                    new() { Quote = "We bought two commercial plaza units through their agency. All land documents were clean, transfer was painless, and rental yield is currently over 9% annually.", Author = "Shahid Mehmood", Role = "Commercial Investor", Initials = "SM", Rating = 5 }
                ],
                Faqs =
                [
                    new() { Question = $"What is included in the Turnkey Construction contract with {bizName}?", Answer = "Our turnkey contract covers everything from excavation, grey structure, plumbing, electrical, to designer flooring, woodwork, sanitary fixtures, and paint." },
                    new() { Question = "How do you protect clients against escalating raw material market prices?", Answer = "We offer guaranteed fixed-cost contracts once architectural blueprints and material specification sheets are locked in." }
                ],
                LeadFormTitle = "Request a Free Site Inspection & Construction Cost Estimate",
                LeadFormSubtitle = $"Enter your plot size and preferred construction specifications below. Our Senior Structural Engineer in {city} will provide a comprehensive bill of quantities."
            };
        }

        // 9. Pharmacy & Healthcare Supplies
        if (catLower.Contains("pharmacy") || catLower.Contains("chemist") || catLower.Contains("drug") || catLower.Contains("medicine"))
        {
            return new BespokeContentPayload
            {
                Headline = $"Certified Pharmaceuticals, Cold-Chain Vaccines & Surgical Supplies in {city}",
                Subheadline = $"Your health deserves uncompromised authenticity. {bizName} dispenses 100% genuine prescription medications, surgical consumables, and provides rapid 24/7 temperature-controlled home delivery.",
                HeroBadge = $"#1 Trusted Licensed Pharmacy in {city}",
                CallToAction = "Order Prescription Medicines Online",
                SecondaryCta = "Call 24/7 Urgent Pharmacy Hotline",
                ConversionScore = 96,
                Metrics =
                [
                    new() { Value = "100%", Label = "Verified Genuine Medicines" },
                    new() { Value = "24/7", Label = "Urgent Delivery Across {City}" },
                    new() { Value = "2° - 8°C", Label = "Active Cold-Chain Storage" },
                    new() { Value = "50,000+", Label = "Prescriptions Accurately Filled" }
                ],
                TrustBadges = ["DRAP Regulated", "Licensed Pharmacists", "Cold-Chain Certified", "100% Genuine Guarantee"],
                Services =
                [
                    new() { Icon = "fa-solid fa-pills", Title = "Genuine Prescription Dispensing & Consulting", Description = "Computerized prescription screening by licensed pharmacists to ensure accurate dosage, zero drug interactions, and manufacturer batch verification.", Cta = "Refill Prescription" },
                    new() { Icon = "fa-solid fa-truck-medical", Title = "24/7 Fast Temperature-Controlled Home Delivery", Description = "Insulated cold-chain delivery riders bringing your urgent medications and insulin directly to your doorstep in under 45 minutes.", Cta = "Order Delivery" },
                    new() { Icon = "fa-solid fa-box-tissue", Title = "Surgical, Orthopedic & Diagnostic Equipment", Description = "Complete surgical disposables, glucometers, digital blood pressure monitors, nebulizers, and orthopedic support braces.", Cta = "View Equipment" }
                ],
                Differentiators =
                [
                    new() { Icon = "fa-solid fa-snowflake", Title = "Unbroken Cold-Chain Guarantee", Description = "Biologics, vaccines, and insulins are stored in backup-powered pharmaceutical refrigeration and dispatched in monitored cold packs." },
                    new() { Icon = "fa-solid fa-prescription-bottle-medical", Title = "Direct Manufacturer Sourcing", Description = "We purchase exclusively from licensed multinational pharmaceutical distributors, completely eliminating counterfeit drug risks." },
                    new() { Icon = "fa-solid fa-user-doctor", Title = "Free Pharmacist Medication Counseling", Description = "Our attending clinical pharmacists explain dosage schedules, side-effect prevention, and dietary contraindications with every refill." }
                ],
                CaseStudies =
                [
                    new() { ClientName = "Urgent Midnight Pediatric Medication", Challenge = "A child developed high asthma distress at 2:00 AM requiring immediate nebulization solutions unavailable in local stores", Solution = "Dispatched emergency rider with pediatric nebulizer and medication in specialized cold bag within 25 minutes", ResultMetric = "25 Min Delivery", ResultDescription = "Immediate relief provided without requiring hospital emergency room admission" }
                ],
                PricingPlans =
                [
                    new() { Name = "Walk-In / Direct Refill", Price = "Retail -10%", Period = "discount", Badge = "", Description = "Standard genuine medicine purchase", Features = ["100% Guaranteed Manufacturer Genuine", "Free Blood Pressure Check Onsite", "Computerized Batch & Expiry Receipt", "Pharmacist Dosage Instructions"], Cta = "Visit Pharmacy", IsPopular = false },
                    new() { Name = "Monthly Chronic Refill Program", Price = "Retail -15%", Period = "recurring", Badge = "Most Popular", Description = "Automated monthly delivery for diabetes, BP & heart patients", Features = ["Automatic Scheduled Monthly Home Delivery", "Free Insulin Cold-Chain Cool Packs", "Priority Pharmacist Dedicated WhatsApp", "Free Quarterly Blood Glucose Checkups"], Cta = "Enroll in Chronic Care", IsPopular = true }
                ],
                LeaderBio = new BespokeLeaderBio
                {
                    Name = $"Dr. Faisal Farooq, PharmD",
                    Role = "Chief Clinical Pharmacist & Director",
                    Credentials = "Doctor of Pharmacy • Member Pharmacy Council • 15+ Yrs",
                    Bio = $"Founded {bizName} in {city} to protect families against counterfeit drugs and ensure accessible, high-standard pharmaceutical care 24 hours a day.",
                    Quote = "Medicine is life. Dispensing genuine drugs with absolute accuracy is not just our profession; it is our sacred responsibility.",
                    AvatarInitials = "FF"
                },
                Testimonials =
                [
                    new() { Quote = $"{bizName} is the only pharmacy in {city} I trust for my mother's diabetes injections. Their cold-chain delivery arrives on time in ice packs, and prices are fair.", Author = "Dr. Imran Khattak", Role = $"Consultant, {city}", Initials = "IK", Rating = 5 },
                    new() { Quote = "Needed critical surgery supplies on a Sunday night. Their rider delivered everything to the hospital room in 30 minutes flat. Exceptional service!", Author = "Sardar Ali", Role = $"{city} Resident", Initials = "SA", Rating = 5 }
                ],
                Faqs =
                [
                    new() { Question = $"How do I send my prescription for home delivery from {bizName}?", Answer = "Simply photograph your doctor's prescription and send it via WhatsApp to our hotline. Our pharmacist will review it and confirm your delivery address immediately." },
                    new() { Question = "Are your pharmacies open during late nights and public holidays?", Answer = "Yes, our main branches and urgent dispatch riders operate 24 hours a day, 7 days a week, 365 days a year." }
                ],
                LeadFormTitle = "Upload Prescription for Instant 24/7 Home Delivery",
                LeadFormSubtitle = $"Enter your contact details and medicine requirements below. A licensed pharmacist in {city} will confirm your order within 10 minutes."
            };
        }

        // 10. Agriculture, Chemicals & Agro Supplies
        if (catLower.Contains("agro") || catLower.Contains("agri") || catLower.Contains("seed") || catLower.Contains("fertiliz") || catLower.Contains("crop") || catLower.Contains("chem"))
        {
            return new BespokeContentPayload
            {
                Headline = $"Maximize Crop Yields & Farm Productivity with Certified Agro Supplies in {city}",
                Subheadline = $"Empowering progressive growers. {bizName} supplies certified high-yield hybrid seeds, balanced soil nutrients, and precision crop protection backed by expert agronomic field advisory.",
                HeroBadge = $"#1 Rated Agricultural Supply Partner in {city}",
                CallToAction = "Request Commercial Agro Supply Catalog",
                SecondaryCta = "Book Free Soil & Agronomy Audit",
                ConversionScore = 95,
                Metrics =
                [
                    new() { Value = "+35%", Label = "Average Acreage Yield Increase" },
                    new() { Value = "100%", Label = "Certified Germination Rate" },
                    new() { Value = "25k+", Label = "Acres Protected & Nourished" },
                    new() { Value = "15+ Yrs", Label = "Trusted Regional Farming Partner" }
                ],
                TrustBadges = ["Ministry of Agriculture Approved", "Certified Hybrid Seeds", "Lab-Tested Formulations", "Farmers Alliance Member"],
                Services =
                [
                    new() { Icon = "fa-solid fa-wheat-awn", Title = "Certified Hybrid Seeds & Seed Treatments", Description = "Disease-tolerant, drought-resilient certified seeds engineered for high germination vigor and maximum harvest tonnage.", Cta = "Browse Seeds" },
                    new() { Icon = "fa-solid fa-vial-circle-check", Title = "Precision Crop Nutrition & Bio-Fertilizers", Description = "Micro-nutrient foliar sprays, chelated zinc, soluble NPK formulations, and organic soil regenerators that restore soil vitality.", Cta = "Explore Fertilizers" },
                    new() { Icon = "fa-solid fa-shield-virus", Title = "Targeted Crop Protection & Pest Management", Description = "Selective herbicides, systemic fungicides, and environmental insecticides formulated to neutralize infestations with zero crop stress.", Cta = "Protect Crops" }
                ],
                Differentiators =
                [
                    new() { Icon = "fa-solid fa-flask-vial", Title = "Free On-Farm Soil & Salinity Testing", Description = $"Our mobile laboratory visits your farm in {city} and surrounding areas to map exact soil pH and nutrient deficiencies before planting." },
                    new() { Icon = "fa-solid fa-handshake-angle", Title = "Season-Long Agronomic Field Support", Description = "Our certified agronomists inspect your fields at critical growth stages, guiding fertilizer timing and spray calibration." },
                    new() { Icon = "fa-solid fa-truck-ramp-box", Title = "Bulk Wholesale Farmgate Delivery", Description = "Direct warehouse dispatch to your farm estate with transparent wholesale pricing and guaranteed batch authenticity." }
                ],
                CaseStudies =
                [
                    new() { ClientName = $"{city} Cotton & Wheat Farming Enterprise (300 Acres)", Challenge = "Severe whitefly resistance and nitrogen leaching resulting in falling yields", Solution = "Introduced targeted systemic IPM spray schedule paired with foliar chelated micronutrient booster", ResultMetric = "+42% Bumper Yield", ResultDescription = "Harvested 48 maunds per acre, yielding $28,000 in additional net farm revenue" }
                ],
                PricingPlans =
                [
                    new() { Name = "Smallholder Acre Package", Price = "$35", Period = "/acre", Badge = "", Description = "Complete seed-to-harvest basic nutrition", Features = ["Certified High-Yield Hybrid Seed Packet", "Pre-Emergence Weed Management Spray", "Balanced Starter Soluble NPK Boost", "Onsite Field Advisory Hotline Access"], Cta = "Order Acre Pack", IsPopular = false },
                    new() { Name = "Commercial Farm Maximizer", Price = "$65", Period = "/acre", Badge = "Most Popular", Description = "High-performance agronomy for maximum yield", Features = ["Premium Disease-Resistant Hybrid Seeds", "Complete 4-Stage Foliar Nutrient Program", "Full-Spectrum Targeted Pest & Blight Shield", "Bi-Weekly Onsite Agronomist Field Visits"], Cta = "Deploy Farm Plan", IsPopular = true }
                ],
                LeaderBio = new BespokeLeaderBio
                {
                    Name = $"Engr. Muhammad Tariq Khan",
                    Role = "Chief Agronomist & Technical Director",
                    Credentials = "M.Sc (Hons) Agronomy • Registered Agricultural Consultant",
                    Bio = $"Dedicated to revitalizing soil health and farmer prosperity in {city}. Has counseled thousands of progressive growers to achieve record harvests through precision science.",
                    Quote = "When a farmer succeeds, the entire nation thrives. We bring scientific agronomy to the soil so every acre produces its true potential.",
                    AvatarInitials = "MT"
                },
                Testimonials =
                [
                    new() { Quote = $"{bizName}'s hybrid seeds and foliar nutrients transformed my wheat crop. We achieved 52 maunds per acre where we previously struggled to get 36. Truly genuine products.", Author = "Chaudhry Riaz Ahmad", Role = $"Farm Owner, {city}", Initials = "CR", Rating = 5 },
                    new() { Quote = "Their agronomist visited our sugarcane fields, tested our tubewell water, and diagnosed our yellowing leaves accurately. Saved our crop!", Author = "Malik Sher Zaman", Role = "Progressive Farmer", Initials = "MS", Rating = 5 }
                ],
                Faqs =
                [
                    new() { Question = $"Are the agricultural chemicals and seeds from {bizName} guaranteed genuine?", Answer = "Yes, 100%. All products come in tamper-evident sealed packaging with official QR verification codes and government lot certifications." },
                    new() { Question = "How do I book an agronomy field inspection for my farm?", Answer = "Contact our agronomy desk via phone or form. An agronomist will schedule a complimentary farm visit within 48 hours." }
                ],
                LeadFormTitle = "Request Bulk Agro Supply Quote & Free Soil Advisory",
                LeadFormSubtitle = $"Enter your farm acreage and crop details below. Our technical agronomist in {city} will prepare a customized nutrient schedule."
            };
        }

        // 11. Default Commercial Enterprise
        return new BespokeContentPayload
        {
            Headline = $"Unlock Rapid Growth & Unrivaled {category} Excellence in {city}",
            Subheadline = $"Partner with {bizName} for exceptional execution, modern methodologies, and measurable commercial results tailored to your exact specifications.",
            HeroBadge = $"#1 Trusted {category} Partner in {city}",
            CallToAction = "Claim Your Free Strategy Session",
            SecondaryCta = "Call Direct Representative",
            ConversionScore = 94,
            Metrics =
            [
                new() { Value = "10+ Yrs", Label = $"Experience in {city}" },
                new() { Value = "99.4%", Label = "Client Satisfaction Score" },
                new() { Value = "500+", Label = "Commercial Deliverables" },
                new() { Value = "4.9 ★", Label = "Verified Review Rating" }
            ],
            TrustBadges = ["Google Guaranteed", "ISO 9001 Standard", "Licensed Chamber Member", "Top Rated Enterprise"],
            Services =
            [
                new() { Icon = "fa-solid fa-briefcase", Title = $"Comprehensive {category} Solutions", Description = "End-to-end execution utilizing modern standards, zero corner-cutting, and strict quality control.", Cta = "Learn More" },
                new() { Icon = "fa-solid fa-magnifying-glass-chart", Title = "Specialized Audit & Strategy", Description = "Deep-dive assessment pinpointing bottlenecks, opportunities, and high-impact revenue gains.", Cta = "Request Audit" },
                new() { Icon = "fa-solid fa-handshake-angle", Title = "Dedicated Retainer Support", Description = "Priority direct line, SLA emergency turnaround, and continuous optimization for peace of mind.", Cta = "Explore Retainers" }
            ],
            Differentiators =
            [
                new() { Icon = "fa-solid fa-check-circle", Title = "Proven Market Leadership", Description = $"Consistently recognized as a benchmark for quality, reliability, and client success throughout {city}." },
                new() { Icon = "fa-solid fa-clock", Title = "Rapid Turnaround Guarantees", Description = "Agile execution models ensure your project is initiated promptly and completed within agreed timelines." },
                new() { Icon = "fa-solid fa-shield", Title = "100% Satisfaction Warranty", Description = "We stand behind all deliverables with clear scope definitions and responsive revision support." }
            ],
            CaseStudies =
            [
                new() { ClientName = $"{city} Commercial Client", Challenge = "Inefficient operations and slow customer acquisition", Solution = "Modernized process workflows and launched targeted acquisition channel", ResultMetric = "+185% Inquiries", ResultDescription = "Delivered milestone project ahead of schedule with 99.4% satisfaction" }
            ],
            PricingPlans =
            [
                new() { Name = "Essential Service", Price = "$299", Period = "/package", Badge = "", Description = "Foundational commercial package", Features = ["Comprehensive Diagnostic Review", "Full Implementation Roadmap", "Core Deliverables Milestone", "30-Day Post-Launch Support"], Cta = "Get Started", IsPopular = false },
                new() { Name = "Growth Professional", Price = "$799", Period = "/package", Badge = "Most Popular", Description = "Accelerated delivery & priority execution", Features = ["All Essential Features Included", "Dedicated Senior Account Lead", "Expedited 7-Day Turnaround", "Quarterly Strategy Review Session"], Cta = "Select Growth", IsPopular = true },
                new() { Name = "Enterprise Retainer", Price = "$1,800", Period = "/month", Badge = "", Description = "Full dedicated agency capacity & continuous optimization", Features = ["Direct Emergency Hotline Access", "Unlimited Scope Revisions within SLA", "Continuous Multi-Channel Optimization", "Executive Monthly Reporting"], Cta = "Contact Sales", IsPopular = false }
            ],
            LeaderBio = new BespokeLeaderBio
            {
                Name = $"Director at {bizName}",
                Role = $"Managing Principal, {category}",
                Credentials = "Certified Industry Specialist • 15+ Yrs",
                Bio = $"Guiding ambitious organizations across {city} to achieve sustainable commercial dominance through precision execution and proven methodologies.",
                Quote = "Excellence is not an accident; it is the inevitable outcome of high standards and relentless execution.",
                AvatarInitials = "MP"
            },
            Calculator = new BespokeCalculatorConfig
            {
                Title = "Commercial Impact & Value Estimator",
                Subtitle = "Slide to adjust your estimated project scope and view projected commercial value return.",
                SliderLabel = "Estimated Project Scope / Volume",
                Unit = "$",
                Min = 1000,
                Max = 30000,
                DefaultValue = 7500,
                Multiplier = 3.2,
                ResultLabel = "Projected 12-Month Value Return",
                ResultSuffix = " generated"
            },
            Testimonials =
            [
                new() { Quote = $"{bizName} revolutionized our customer onboarding. The professionalism and speed in {city} were unlike anything we had experienced before.", Author = "Malik Asad", Role = $"Managing Director, Apex Global ({city})", Initials = "MA", Rating = 5 },
                new() { Quote = $"Flawless attention to detail. Our inquiries jumped 40% within the first month. They are truly the top {category} leaders in the region.", Author = "Dr. Sana Farooq", Role = "Founder, Horizon Enterprises", Initials = "SF", Rating = 5 },
                new() { Quote = $"Clear communication, zero hidden fees, and world-class execution. Look no further if you are based in {city} or abroad.", Author = "Tariq Rahman", Role = "Operations Head, Capital Group", Initials = "TR", Rating = 5 }
            ],
            Faqs =
            [
                new() { Question = $"How quickly can {bizName} initiate our project?", Answer = "We initiate discovery within 24 hours of onboarding and deliver initial milestones within 5-7 business days." },
                new() { Question = "Do you provide ongoing support after deployment?", Answer = "Yes! Every engagement includes a 30-day warranty period plus optional dedicated monthly retainer maintenance tiers." },
                new() { Question = "Are pricing packages fixed with no hidden fees?", Answer = "100% transparent. All deliverables, timelines, and costs are formalized upfront in a detailed Statement of Work." }
            ],
            LeadFormTitle = "Claim Your Free Strategy Consultation",
            LeadFormSubtitle = $"Fill in your details below. A senior {category} advisor will connect with you within 2 business hours."
        };
    }

    private static string BuildArchitectureBlueprint(
        string bizName,
        string category,
        string city,
        string country,
        LandingPageArchetype archetype,
        LandingPageDesignStyle style,
        List<string> modules,
        string headline,
        string subheadline,
        string? customInstructions)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Web Architecture Blueprint & Strategy Document");
        sb.AppendLine($"**Target Client:** {bizName} | **Sector:** {category} | **Metropolitan Market:** {city}, {country}");
        sb.AppendLine($"**Archetype:** {archetype} | **Design Palette:** {style} | **Generated by:** TheKhanSoft AI Studio");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("## 1. Executive Strategic Positioning & Value Proposition");
        sb.AppendLine($"- **Hero Headline:** \"{headline}\"");
        sb.AppendLine($"- **Subheadline:** \"{subheadline}\"");
        sb.AppendLine($"- **Strategic Intent:** Position {bizName} as the undeniable authority in {city} by eliminating friction in client discovery, answering top objections upfront, and deploying frictionless contact funnels.");
        sb.AppendLine();
        sb.AppendLine("## 2. Target Audience Personas & Conversion Triggers");
        sb.AppendLine($"1. **High-Intent Local Searchers:** Consumers and corporate buyers in {city} actively seeking vetted {category} providers. Trigger: Instant trust badges, local phone number, and Google Guaranteed seal.");
        sb.AppendLine("2. **Price-Conscious Evaluators:** Prospective clients comparing multiple quotes. Trigger: Upfront package clarity, comprehensive deliverable checklists, and transparent FAQs.");
        sb.AppendLine("3. **Urgent Need Callers:** Visitors needing immediate assistance. Trigger: Sticky WhatsApp float and 1-click telephone dialer.");
        sb.AppendLine();
        sb.AppendLine("## 3. Information Architecture & Active Modules Breakdown");
        int index = 1;
        foreach (var mod in modules)
        {
            sb.AppendLine($"### 3.{index} Module: `{mod}`");
            switch (mod)
            {
                case "mod-hero":
                    sb.AppendLine($"- **Purpose:** Capture visitor attention within 3 seconds, deliver the core brand promise tailored to archetype `{archetype}`, and present clear dual CTAs.");
                    break;
                case "mod-trust-bar":
                    sb.AppendLine("- **Purpose:** Overcome initial skepticism with recognized certifications, 4.9★ rating indicators, and compliance emblems.");
                    break;
                case "mod-services":
                    sb.AppendLine("- **Purpose:** Showcase distinct service offerings with concrete deliverables and clear business value.");
                    break;
                case "mod-before-after":
                    sb.AppendLine("- **Purpose:** High-impact client transformation benchmarks and before-and-after proof points.");
                    break;
                case "mod-team":
                    sb.AppendLine("- **Purpose:** Establish personal leadership authority, founder credentials, and executive accountability.");
                    break;
                case "mod-pricing":
                    sb.AppendLine("- **Purpose:** Multi-tier package clarity defusing price hesitation with explicit deliverables.");
                    break;
                case "mod-calculator":
                    sb.AppendLine("- **Purpose:** Client-side dynamic ROI and cost savings estimator increasing time-on-page and engagement.");
                    break;
                case "mod-testimonials":
                    sb.AppendLine("- **Purpose:** Provide authentic social proof from local regional clients with identifiable titles and satisfaction quotes.");
                    break;
                case "mod-faq":
                    sb.AppendLine("- **Purpose:** Defuse top conversion blockers (turnaround times, post-launch support, and scope boundaries) with interactive accordions.");
                    break;
                case "mod-lead-form":
                    sb.AppendLine("- **Purpose:** Zero-friction conversion form with client-side validation, WhatsApp/Phone priority field, and immediate confirmation.");
                    break;
                case "mod-whatsapp":
                    sb.AppendLine("- **Purpose:** Exploit modern conversational commerce; 70%+ of mobile users prefer direct WhatsApp messaging over web forms.");
                    break;
                case "mod-seo":
                    sb.AppendLine("- **Purpose:** Schema.org LocalBusiness structured data enabling Google Rich Snippets, Local Map Pack boost, and social sharing OpenGraph tags.");
                    break;
                default:
                    sb.AppendLine($"- **Purpose:** Enhances user engagement and conversion velocity for {category}.");
                    break;
            }
            index++;
        }
        sb.AppendLine();
        sb.AppendLine("## 4. Recommended Production Technology Stack");
        sb.AppendLine("- **Frontend Framework:** Next.js 14 (App Router) or Astro for static site generation with near-instant Time to First Byte (TTFB < 80ms).");
        sb.AppendLine("- **Styling & Design System:** Tailwind CSS with custom theme design tokens (matching the selected color scheme).");
        sb.AppendLine("- **Forms & Backend:** Server Actions + Zod schema validation; form submission routed to Resend / SendGrid / WhatsApp Business API.");
        sb.AppendLine("- **Hosting & Edge Delivery:** Vercel / Cloudflare Pages with edge caching, automated SSL, and 99.99% uptime guarantee.");
        sb.AppendLine();
        sb.AppendLine("## 5. Local Search & High-Intent SEO Keywords");
        sb.AppendLine($"- `Best {category} in {city}`");
        sb.AppendLine($"- `{bizName} reviews and contact`");
        sb.AppendLine($"- `{category} services near me {city}`");
        sb.AppendLine($"- `Affordable verified {category} {city}`");

        if (!string.IsNullOrWhiteSpace(customInstructions))
        {
            sb.AppendLine();
            sb.AppendLine("## 6. Operator Custom Directives Synthesized");
            sb.AppendLine($"> {customInstructions}");
        }

        return sb.ToString();
    }
}
