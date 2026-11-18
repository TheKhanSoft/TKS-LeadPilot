using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        string[] candidateModels = ["gemini-3.5-flash-lite", "gemini-flash-lite-latest", "gemini-3.1-flash-lite", "gemini-3.8-flash"];
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
                        temperature = 0.3,
                        topK = 40,
                        topP = 0.95,
                        maxOutputTokens = 4096,
                        responseMimeType = "application/json"
                    }
                };

                var jsonContent = new StringContent(
                    JsonSerializer.Serialize(requestBody),
                    Encoding.UTF8,
                    "application/json");

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(15));

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
                lastErrorDetail = $"{modelName}: Request timed out after 15s.";
                continue;
            }
            catch (HttpRequestException ex)
            {
                // Network unreachable or DNS resolution failure - fail fast to offline neural synthesizer
                lastErrorDetail = $"{modelName}: Network connectivity error ({ex.Message}).";
                break;
            }
            catch (Exception ex)
            {
                lastErrorDetail = $"{modelName}: {ex.Message}";
                continue;
            }
        }

        var fallback = GenerateDeterministicLandingPage(request);
        var statusStr = lastResponse != null ? lastResponse.StatusCode.ToString() : "NetworkError";
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
            : "Hero Banner, Trust Badges, Services Grid, Testimonials, FAQ Accordion, Lead Capture Form, WhatsApp Float, SEO Schema";

        var sb = new StringBuilder();
        sb.AppendLine("You are TheKhanSoft's Principal Conversion Rate Optimization (CRO) Architect and Lead Copywriter.");
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
        sb.AppendLine("CRITICAL INSTRUCTIONS FOR BESPOKE GENERATION:");
        sb.AppendLine("1. NEVER generate generic placeholders like 'Premium Delivery', 'Specialized Diagnostics & Audit', or fake corporate filler.");
        sb.AppendLine("2. The services must be 4 to 6 REAL, CONCRETE, bespoke commercial offerings tailored to this exact business and niche.");
        sb.AppendLine($"3. Testimonials must be 3 REALISTIC reviews featuring actual local customer names, neighborhood/title context in {request.City}, and specific authentic situations.");
        sb.AppendLine("4. FAQs must resolve 4 to 5 genuine customer objections regarding turnaround, pricing, scope, and process for this exact trade.");
        sb.AppendLine("5. Output MUST be strictly valid JSON conforming to the following structure:");
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
                RequestsUsedToday = _dailyRequestsUsed
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
            RequestsUsedToday = _dailyRequestsUsed
        };
    }

    private static (string primaryHex, string accentHex, string bgHex, string cardBgHex, string textHex, string buttonClass) GetThemeTokens(LandingPageDesignStyle style)
    {
        return style switch
        {
            LandingPageDesignStyle.MidnightLuxuryGold =>
                ("#D97706", "#F59E0B", "#0A0E17", "#131B2A", "#F8FAFC", "bg-amber-500 hover:bg-amber-600 text-slate-950 font-bold"),
            LandingPageDesignStyle.TrustworthyEmerald =>
                ("#059669", "#10B981", "#022C22", "#064E3B", "#ECFDF5", "bg-emerald-500 hover:bg-emerald-600 text-slate-950 font-bold"),
            LandingPageDesignStyle.BoldVibrantIndigo =>
                ("#4F46E5", "#6366F1", "#0F0D24", "#1E1B4B", "#F5F3FF", "bg-indigo-600 hover:bg-indigo-700 text-white font-bold"),
            LandingPageDesignStyle.MinimalistMonochrome =>
                ("#E4E4E7", "#FAFAFA", "#09090B", "#18181B", "#FAFAFA", "bg-white hover:bg-slate-200 text-slate-950 font-bold"),
            _ => // ModernSlateCyan
                ("#0284C7", "#06B6D4", "#0B1322", "#131F33", "#F8FAFC", "bg-cyan-500 hover:bg-cyan-600 text-slate-950 font-bold")
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
        var modules = request.SelectedModules.Count > 0 ? request.SelectedModules : ["mod-hero", "mod-trust-bar", "mod-services", "mod-testimonials", "mod-faq", "mod-lead-form", "mod-whatsapp", "mod-seo"];

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
        sb.AppendLine("    <!-- Google Fonts: Plus Jakarta Sans & Inter -->");
        sb.AppendLine("    <link rel=\"preconnect\" href=\"https://fonts.googleapis.com\">");
        sb.AppendLine("    <link rel=\"preconnect\" href=\"https://fonts.gstatic.com\" crossorigin>");
        sb.AppendLine("    <link href=\"https://fonts.googleapis.com/css2?family=Plus+Jakarta+Sans:wght@400;500;600;700;800&family=Inter:wght@300;400;500;600;700&display=swap\" rel=\"stylesheet\">");
        sb.AppendLine("    <!-- FontAwesome 6 Icons CDN -->");
        sb.AppendLine("    <link rel=\"stylesheet\" href=\"https://cdnjs.cloudflare.com/ajax/libs/font-awesome/6.5.1/css/all.min.css\">");
        sb.AppendLine("    <script>");
        sb.AppendLine("        tailwind.config = {");
        sb.AppendLine("            darkMode: 'class',");
        sb.AppendLine("            theme: {");
        sb.AppendLine("                extend: {");
        sb.AppendLine("                    fontFamily: { sans: ['Plus Jakarta Sans', 'Inter', 'sans-serif'] },");
        sb.AppendLine($"                    colors: {{ brand: '{primaryHex}', brandAccent: '{accentHex}', brandDark: '{bgHex}', brandCard: '{cardBgHex}' }}");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine("    </script>");
        sb.AppendLine("    <style>");
        sb.AppendLine($"        body {{ background-color: {bgHex}; color: {textHex}; font-family: 'Plus Jakarta Sans', sans-serif; }}");
        sb.AppendLine("        .glow-accent { text-shadow: 0 0 20px rgba(6, 182, 212, 0.4); }");
        sb.AppendLine("        .glass-card { background: rgba(19, 31, 51, 0.7); backdrop-filter: blur(12px); border: 1px solid rgba(255, 255, 255, 0.08); }");
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
        sb.AppendLine("                <a href=\"#why-us\" class=\"hover:text-white transition\">Why Choose Us</a>");
        sb.AppendLine("                <a href=\"#testimonials\" class=\"hover:text-white transition\">Reviews</a>");
        sb.AppendLine("                <a href=\"#faq\" class=\"hover:text-white transition\">FAQ</a>");
        sb.AppendLine("            </nav>");
        sb.AppendLine("            <div class=\"flex items-center space-x-4\">");
        sb.AppendLine($"                <a href=\"#contact\" class=\"px-5 py-2.5 rounded-lg text-sm shadow-lg transition-all duration-200 transform hover:-translate-y-0.5 {buttonClass}\">{payload.CallToAction}</a>");
        sb.AppendLine("            </div>");
        sb.AppendLine("        </div>");
        sb.AppendLine("    </header>");

        sb.AppendLine("    <main class=\"flex-grow\">");

        // Hero Section
        if (modules.Contains("mod-hero"))
        {
            sb.AppendLine("        <!-- Hero Section -->");
            sb.AppendLine("        <section class=\"relative pt-20 pb-24 md:pt-28 md:pb-36 overflow-hidden\">");
            sb.AppendLine($"            <div class=\"absolute -top-40 left-1/2 transform -translate-x-1/2 w-[800px] h-[500px] rounded-full blur-3xl opacity-20 pointer-events-none\" style=\"background: radial-gradient(circle, {accentHex}, transparent 70%);\"></div>");
            sb.AppendLine("            <div class=\"max-w-5xl mx-auto px-4 sm:px-6 lg:px-8 text-center relative z-10\">");
            sb.AppendLine($"                <div class=\"inline-flex items-center space-x-2 px-4 py-1.5 rounded-full border border-slate-700/80 bg-slate-900/60 text-xs font-semibold uppercase tracking-wider mb-8 text-slate-300\">");
            sb.AppendLine($"                    <span class=\"w-2 h-2 rounded-full animate-pulse\" style=\"background-color: {accentHex};\"></span>");
            sb.AppendLine($"                    <span>{payload.HeroBadge}</span>");
            sb.AppendLine("                </div>");
            sb.AppendLine($"                <h1 class=\"text-4xl sm:text-6xl md:text-7xl font-extrabold tracking-tight text-white leading-[1.1] mb-6\">");
            sb.AppendLine($"                    {payload.Headline}");
            sb.AppendLine("                </h1>");
            sb.AppendLine($"                <p class=\"max-w-3xl mx-auto text-lg sm:text-xl text-slate-300 font-normal leading-relaxed mb-10\">");
            sb.AppendLine($"                    {payload.Subheadline}");
            sb.AppendLine("                </p>");
            sb.AppendLine("                <div class=\"flex flex-col sm:flex-row items-center justify-center gap-4\">");
            sb.AppendLine($"                    <a href=\"#contact\" class=\"w-full sm:w-auto px-8 py-4 rounded-xl text-base shadow-2xl transition transform hover:scale-105 {buttonClass}\">");
            sb.AppendLine($"                        <i class=\"fa-solid fa-calendar-check mr-2\"></i>{payload.CallToAction}");
            sb.AppendLine("                    </a>");
            sb.AppendLine($"                    <a href=\"tel:{phone}\" class=\"w-full sm:w-auto px-8 py-4 rounded-xl text-base font-semibold border border-slate-700 bg-slate-900/80 hover:bg-slate-800 text-white transition\">");
            sb.AppendLine($"                        <i class=\"fa-solid fa-phone mr-2 text-cyan-400\"></i>{phone}");
            sb.AppendLine("                    </a>");
            sb.AppendLine("                </div>");

            // Key Metrics Grid
            if (payload.Metrics.Count > 0)
            {
                sb.AppendLine("                <!-- Trust Metrics Grid -->");
                sb.AppendLine($"                <div class=\"mt-16 grid grid-cols-2 sm:grid-cols-{Math.Min(4, payload.Metrics.Count)} gap-4 max-w-4xl mx-auto\">");
                foreach (var metric in payload.Metrics)
                {
                    sb.AppendLine("                    <div class=\"p-4 rounded-lg bg-slate-900/40 border border-slate-800\">");
                    sb.AppendLine($"                        <div class=\"text-3xl font-extrabold text-white\">{metric.Value}</div>");
                    sb.AppendLine($"                        <div class=\"text-xs text-slate-400 mt-1\">{metric.Label}</div>");
                    sb.AppendLine("                    </div>");
                }
                sb.AppendLine("                </div>");
            }

            sb.AppendLine("            </div>");
            sb.AppendLine("        </section>");
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

        // Services Grid
        if (modules.Contains("mod-services") && payload.Services.Count > 0)
        {
            sb.AppendLine("        <!-- Services Section -->");
            sb.AppendLine("        <section id=\"services\" class=\"py-24 relative\">");
            sb.AppendLine("            <div class=\"max-w-7xl mx-auto px-4 sm:px-6 lg:px-8\">");
            sb.AppendLine("                <div class=\"text-center max-w-3xl mx-auto mb-16\">");
            sb.AppendLine($"                    <h2 class=\"text-xs font-bold uppercase tracking-widest text-cyan-400 mb-3\">Tailored Offerings</h2>");
            sb.AppendLine($"                    <p class=\"text-3xl sm:text-4xl font-extrabold text-white tracking-tight\">Everything Your Project Demands Under One Roof</p>");
            sb.AppendLine($"                    <p class=\"text-slate-400 mt-4 text-base\">Engineered with precision, industry-leading methodology, and personalized care for every {city} client.</p>");
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

        // Why Choose Us / Differentiators
        if (payload.Differentiators.Count > 0)
        {
            sb.AppendLine("        <!-- Why Choose Us / Differentiators -->");
            sb.AppendLine("        <section id=\"why-us\" class=\"py-20 bg-slate-900/30 border-y border-slate-800/60\">");
            sb.AppendLine("            <div class=\"max-w-7xl mx-auto px-4 sm:px-6 lg:px-8\">");
            sb.AppendLine("                <div class=\"text-center max-w-2xl mx-auto mb-14\">");
            sb.AppendLine($"                    <h2 class=\"text-xs font-bold uppercase tracking-widest text-emerald-400 mb-2\">The {bizName} Advantage</h2>");
            sb.AppendLine($"                    <p class=\"text-3xl font-extrabold text-white tracking-tight\">Why Clients in {city} Choose Us</p>");
            sb.AppendLine("                </div>");
            sb.AppendLine("                <div class=\"grid grid-cols-1 md:grid-cols-3 gap-8\">");
            foreach (var diff in payload.Differentiators)
            {
                sb.AppendLine("                    <div class=\"p-6 rounded-2xl glass-card flex items-start space-x-4\">");
                sb.AppendLine($"                        <div class=\"w-12 h-12 rounded-xl flex-shrink-0 flex items-center justify-center text-xl bg-emerald-500/10 text-emerald-400\"><i class=\"{diff.Icon}\"></i></div>");
                sb.AppendLine("                        <div>");
                sb.AppendLine($"                            <h4 class=\"text-base font-bold text-white mb-1.5\">{diff.Title}</h4>");
                sb.AppendLine($"                            <p class=\"text-slate-400 text-xs leading-relaxed\">{diff.Description}</p>");
                sb.AppendLine("                        </div>");
                sb.AppendLine("                    </div>");
            }
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
        sb.AppendLine("        function handleFormSubmit(e) {");
        sb.AppendLine("            e.preventDefault();");
        sb.AppendLine($"            alert('Thank you! Your inquiry has been received by {bizName}. An advisor will contact you within 2 hours.');");
        sb.AppendLine("            e.target.reset();");
        sb.AppendLine("        }");
        sb.AppendLine("    </script>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
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

        // 5. Default Commercial Enterprise
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
                    sb.AppendLine("- **Purpose:** Capture visitor attention within 3 seconds, deliver the core brand promise, and present clear dual CTAs (Booking + Phone).");
                    break;
                case "mod-trust-bar":
                    sb.AppendLine("- **Purpose:** Overcome initial skepticism with recognized certifications, 4.9★ rating indicators, and compliance emblems.");
                    break;
                case "mod-services":
                    sb.AppendLine("- **Purpose:** Showcase distinct service offerings with concrete deliverables and clear business value.");
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
