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
                Category = "Technical",
                IsSelected = true
            }
        ];
    }

    public async Task<LandingPageGenerationResult> GenerateLandingPageAsync(LandingPageGenerationRequest request, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        // 1. Check for Gemini API key
        var apiKey = _secretVault.GetSecret(ApiKeyVaultKey) ?? _secretVault.GetSecret("GeminiApiKey");

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            var fallback = GenerateDeterministicLandingPage(request);
            fallback.Provider = "TKS Local Neural Synthesizer (Offline Engine)";
            fallback.ErrorMessage = "No Gemini API Key found in Settings & Vault. Generated ultra-high-fidelity offline landing page and architecture.";
            fallback.ExecutionDuration = sw.Elapsed;
            return fallback;
        }

        // 2. Build Gemini Prompt
        var prompt = BuildGeminiPrompt(request);

        try
        {
            var modelName = "gemini-1.5-flash";
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
                    temperature = 0.5,
                    topK = 40,
                    topP = 0.95,
                    maxOutputTokens = 8192
                }
            };

            var jsonContent = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json");

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(45));

            var response = await _httpClient.PostAsync(requestUri, jsonContent, cts.Token);

            await ConfigLock.WaitAsync(ct);
            try
            {
                _dailyRequestsUsed++;
            }
            finally
            {
                ConfigLock.Release();
            }

            if (!response.IsSuccessStatusCode)
            {
                var fallback = GenerateDeterministicLandingPage(request);
                fallback.Provider = $"TKS Local Neural Synthesizer (Gemini HTTP {response.StatusCode} Fallback)";
                fallback.ErrorMessage = $"Gemini API returned status code {response.StatusCode}. Instant offline blueprint deployed.";
                fallback.ExecutionDuration = sw.Elapsed;
                fallback.RequestsUsedToday = _dailyRequestsUsed;
                return fallback;
            }

            var responseJson = await response.Content.ReadAsStringAsync(ct);
            var parsed = ParseGeminiResponse(responseJson, request);

            parsed.ExecutionDuration = sw.Elapsed;
            parsed.RequestsUsedToday = _dailyRequestsUsed;
            return parsed;
        }
        catch (Exception ex)
        {
            var fallback = GenerateDeterministicLandingPage(request);
            fallback.Provider = "TKS Local Neural Synthesizer (Network Exception Fallback)";
            fallback.ErrorMessage = $"AI connection notice: {ex.Message}. Local deterministic landing page generated.";
            fallback.ExecutionDuration = sw.Elapsed;
            fallback.RequestsUsedToday = _dailyRequestsUsed;
            return fallback;
        }
    }

    private static string BuildGeminiPrompt(LandingPageGenerationRequest request)
    {
        var modulesList = request.SelectedModules.Count > 0
            ? string.Join(", ", request.SelectedModules)
            : "Hero Banner, Trust Badges, Services Grid, Testimonials, FAQ Accordion, Lead Capture Form, WhatsApp Float, SEO Schema";

        var sb = new StringBuilder();
        sb.AppendLine("You are TheKhanSoft's Principal Conversion Rate Optimization (CRO) Architect and Senior Frontend Engineer.");
        sb.AppendLine("Generate a complete, bespoke commercial landing page architecture blueprint and complete, production-ready, beautiful HTML5 code with Tailwind CSS CDN for the following client:");
        sb.AppendLine();
        sb.AppendLine($"- Business Name: {request.BusinessName}");
        sb.AppendLine($"- Category / Industry: {request.Category}");
        sb.AppendLine($"- Location: {request.City}, {request.Country}");
        sb.AppendLine($"- Target Audience: {request.TargetAudience}");
        sb.AppendLine($"- Page Archetype: {request.Archetype}");
        sb.AppendLine($"- Design Style & Color Scheme: {request.DesignStyle}");
        sb.AppendLine($"- Phone / Contact: {request.PhonePrimary ?? "+92 300 1234567"}");
        sb.AppendLine($"- Email: {request.EmailPrimary ?? "info@" + request.BusinessName.ToLowerInvariant().Replace(" ", "") + ".com"}");
        sb.AppendLine($"- Modules Required: {modulesList}");

        if (!string.IsNullOrWhiteSpace(request.CustomPromptInstructions))
        {
            sb.AppendLine();
            sb.AppendLine($"SPECIAL OPERATOR PROMPT / INSTRUCTIONS:");
            sb.AppendLine($"\"\"\"{request.CustomPromptInstructions}\"\"\"");
        }

        sb.AppendLine();
        sb.AppendLine("CRITICAL INSTRUCTIONS FOR OUTPUT:");
        sb.AppendLine("You MUST respond in pure JSON format (valid JSON, no surrounding markdown, or wrapped in a ```json code block).");
        sb.AppendLine("The JSON must have the following keys:");
        sb.AppendLine("1. headline: (Punchy, high-converting hero headline tailored to the business)");
        sb.AppendLine("2. subheadline: (Empathetic, benefit-focused subheadline addressing the visitor's pain points)");
        sb.AppendLine("3. call_to_action: (Actionable button text e.g. 'Claim Free Consultation', 'Schedule Priority Appointment')");
        sb.AppendLine("4. conversion_score: (Integer between 88 and 98)");
        sb.AppendLine("5. architecture_blueprint_markdown: (Markdown string with sections: # Executive Strategy & Positioning, ## Target Audience Persona, ## Information Architecture & Conversion Flow, ## Module-by-Module Technical Specs, ## Recommended Production Tech Stack e.g. Next.js 14 / Tailwind / Vercel, ## High-Intent SEO Keywords)");
        sb.AppendLine("6. generated_html: (A complete, standalone HTML5 document starting with <!DOCTYPE html><html> and ending with </html>. Include Tailwind CSS CDN <script src=\"https://cdn.tailwindcss.com\"></script>, FontAwesome 6 icons CDN, Inter font, responsive layout, modern dark/clean gradient cards matching the specified design style, working mobile navigation, interactive JavaScript FAQ accordion, interactive simulated lead form with instant validation modal, floating WhatsApp button, and SEO Schema.org script)");
        sb.AppendLine("7. midjourney_prompts: (Array of 3-4 photorealistic image generation prompts for Hero background, service cards, and founder portrait)");
        sb.AppendLine("8. developer_prompts: (A prompt for frontend engineers to convert this HTML into reusable React/Next.js/Tailwind components)");
        sb.AppendLine("9. ad_copy_prompts: (3 Google Search ad headlines & descriptions, plus 2 Facebook/Instagram ad copy variants)");

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

            using var innerDoc = JsonDocument.Parse(cleanJson);
            var obj = innerDoc.RootElement;

            var result = new LandingPageGenerationResult
            {
                IsSuccess = true,
                IsLocalFallback = false,
                BusinessName = request.BusinessName,
                Provider = "Google Gemini Free Tier (gemini-1.5-flash)",
                Headline = obj.TryGetProperty("headline", out var hl) ? hl.GetString() ?? $"{request.BusinessName} Digital Hub" : $"{request.BusinessName} Digital Hub",
                Subheadline = obj.TryGetProperty("subheadline", out var shl) ? shl.GetString() ?? "Engineered for maximum client acquisition and trust." : "Engineered for maximum client acquisition and trust.",
                CallToActionText = obj.TryGetProperty("call_to_action", out var cta) ? cta.GetString() ?? "Book Free Consultation" : "Book Free Consultation",
                ConversionScore = obj.TryGetProperty("conversion_score", out var cs) && cs.TryGetInt32(out var csVal) ? csVal : 94,
                ArchitectureBlueprintMarkdown = obj.TryGetProperty("architecture_blueprint_markdown", out var ab) ? ab.GetString() ?? string.Empty : string.Empty,
                GeneratedHtml = obj.TryGetProperty("generated_html", out var gh) ? gh.GetString() ?? string.Empty : string.Empty,
                DeveloperPrompts = obj.TryGetProperty("developer_prompts", out var dp) ? dp.GetString() ?? string.Empty : string.Empty,
                AdCopyPrompts = obj.TryGetProperty("ad_copy_prompts", out var ac) ? ac.GetString() ?? string.Empty : string.Empty,
                EstimatedSectionsCount = request.SelectedModules.Count > 0 ? request.SelectedModules.Count : 8
            };

            if (obj.TryGetProperty("midjourney_prompts", out var mjp) && mjp.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in mjp.EnumerateArray())
                {
                    var s = item.GetString();
                    if (!string.IsNullOrWhiteSpace(s)) result.MidjourneyPrompts.Add(s);
                }
            }

            // If Gemini didn't supply complete HTML, supplement with deterministic HTML
            if (string.IsNullOrWhiteSpace(result.GeneratedHtml) || !result.GeneratedHtml.Contains("<!DOCTYPE html>"))
            {
                var det = GenerateDeterministicLandingPage(request);
                result.GeneratedHtml = det.GeneratedHtml;
                if (string.IsNullOrWhiteSpace(result.ArchitectureBlueprintMarkdown))
                {
                    result.ArchitectureBlueprintMarkdown = det.ArchitectureBlueprintMarkdown;
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            var fallback = GenerateDeterministicLandingPage(request);
            fallback.ErrorMessage = $"Failed to parse Gemini response: {ex.Message}. Local deterministic template deployed.";
            return fallback;
        }
    }

    public LandingPageGenerationResult GenerateDeterministicLandingPage(LandingPageGenerationRequest request)
    {
        var bizName = !string.IsNullOrWhiteSpace(request.BusinessName) ? request.BusinessName.Trim() : "Enterprise Client";
        var category = !string.IsNullOrWhiteSpace(request.Category) ? request.Category.Trim() : "Commercial Business";
        var city = !string.IsNullOrWhiteSpace(request.City) ? request.City.Trim() : "Metropolitan Area";
        var country = !string.IsNullOrWhiteSpace(request.Country) ? request.Country.Trim() : "Global";
        var phone = !string.IsNullOrWhiteSpace(request.PhonePrimary) ? request.PhonePrimary.Trim() : "+92 300 1234567";
        var email = !string.IsNullOrWhiteSpace(request.EmailPrimary) ? request.EmailPrimary.Trim() : $"contact@{bizName.ToLowerInvariant().Replace(" ", "")}.com";
        var modules = request.SelectedModules.Count > 0 ? request.SelectedModules : ["mod-hero", "mod-trust-bar", "mod-services", "mod-testimonials", "mod-faq", "mod-lead-form", "mod-whatsapp", "mod-seo"];

        // Color theme styling
        var (primaryHex, accentHex, bgHex, cardBgHex, textHex, buttonClass) = GetThemeTokens(request.DesignStyle);

        // Generate tailored headlines
        var (headline, subheadline, ctaText) = GetTailoredCopy(request.Archetype, category, bizName, city);

        // Generate complete production HTML
        var html = BuildProductionHtml(bizName, category, city, country, phone, email, headline, subheadline, ctaText, modules, request.DesignStyle, primaryHex, accentHex, bgHex, cardBgHex, textHex, buttonClass, request.CustomPromptInstructions);

        // Generate comprehensive Blueprint Markdown
        var blueprint = BuildArchitectureBlueprint(bizName, category, city, country, request.Archetype, request.DesignStyle, modules, headline, subheadline, request.CustomPromptInstructions);

        // Midjourney Prompts
        var midjourney = new List<string>
        {
            $"Commercial architectural photography, high-end {category} clinic/office interior in {city}, floor-to-ceiling glass, warm cinematic lighting, minimal modern furniture, 8k resolution, photorealistic, Hasselblad X2D 100C --ar 16:9 --style raw",
            $"Editorial portrait of lead {category} specialist director, friendly confident smile, tailored business suit, blurred luxury office background, soft studio lighting, sharp focus, 35mm lens --ar 4:5",
            $"Minimalist 3D isometric render of modern digital workflow solutions, glowing {primaryHex} neon accents, translucent frosted glass cards, clean dark background --ar 1:1"
        };

        var developerPrompts = $"Create a responsive Next.js 14 App Router landing page for {bizName} ({category} in {city}).\n" +
                               $"Use Tailwind CSS for styling, Lucide React icons, and Framer Motion for scroll reveal animations.\n" +
                               $"Include components for: HeroSection, TrustLogos, ServiceGrid, PricingTable, FaqAccordion, LeadCaptureForm, and FloatingWhatsApp.\n" +
                               $"Integrate Zod schema validation for the lead capture form with server action email dispatch via Resend.";

        var adCopy = $"### Google Search Ads:\n" +
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
            Headline = headline,
            Subheadline = subheadline,
            CallToActionText = ctaText,
            ConversionScore = 93,
            EstimatedSectionsCount = modules.Count,
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

    private static (string headline, string subheadline, string ctaText) GetTailoredCopy(LandingPageArchetype archetype, string category, string bizName, string city)
    {
        return archetype switch
        {
            LandingPageArchetype.LuxuryBrandShowcase =>
                ($"The Pinnacle of {category} Excellence in {city}",
                 $"Experience bespoke service, peerless craftsmanship, and discretion tailored exclusively for discerning clients at {bizName}.",
                 "Schedule Private Consultation"),
            LandingPageArchetype.LocalServiceBooking =>
                ($"Trusted {category} Services in {city} — Fast, Reliable & Guaranteed",
                 $"Don't compromise on quality. {bizName} provides same-day appointments, licensed specialists, and transparent upfront pricing.",
                 "Book Urgent Appointment"),
            LandingPageArchetype.ModernSaaSTech =>
                ($"Accelerate Your Enterprise with Next-Gen {category} Intelligence",
                 $"Automate complex operations, streamline team collaboration, and unlock 3x higher throughput with {bizName}'s cloud infrastructure.",
                 "Start Free 14-Day Pilot"),
            LandingPageArchetype.CorporateAgencyPortal =>
                ($"Transforming Commercial Ambition into Measurable Market Dominance",
                 $"We partner with ambitious mid-market and enterprise leaders in {city} to architect scalable growth, digital resilience, and sustainable profit.",
                 "Request Executive Briefing"),
            LandingPageArchetype.EcommerceShowcase =>
                ($"Premium {category} Handcrafted for Performance & Longevity",
                 $"Explore {bizName}'s curated collection. Backed by a 100% satisfaction guarantee, express delivery, and dedicated customer support.",
                 "Explore Exclusive Offers"),
            _ => // LeadGenerationFunnel
                ($"Unlock Rapid Growth & Unrivaled {category} Results in {city}",
                 $"Join over 1,500+ satisfied clients who rely on {bizName} for exceptional execution, modern methodologies, and measurable returns.",
                 "Claim Your Free Strategy Session")
        };
    }

    private static string BuildProductionHtml(
        string bizName,
        string category,
        string city,
        string country,
        string phone,
        string email,
        string headline,
        string subheadline,
        string ctaText,
        List<string> modules,
        LandingPageDesignStyle style,
        string primaryHex,
        string accentHex,
        string bgHex,
        string cardBgHex,
        string textHex,
        string buttonClass,
        string? customInstructions)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\" class=\"scroll-smooth\">");
        sb.AppendLine("<head>");
        sb.AppendLine("    <meta charset=\"UTF-8\">");
        sb.AppendLine("    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        sb.AppendLine($"    <title>{bizName} — Leading {category} in {city}</title>");
        sb.AppendLine($"    <meta name=\"description\" content=\"{subheadline}\">");
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
            sb.AppendLine($"      \"description\": \"{subheadline}\",");
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
        sb.AppendLine($"                <a href=\"#contact\" class=\"px-5 py-2.5 rounded-lg text-sm shadow-lg transition-all duration-200 transform hover:-translate-y-0.5 {buttonClass}\">{ctaText}</a>");
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
            sb.AppendLine("                <div class=\"inline-flex items-center space-x-2 px-4 py-1.5 rounded-full text-xs font-semibold uppercase tracking-wider mb-8 border border-slate-700 bg-slate-900/60 text-slate-300\">");
            sb.AppendLine($"                    <span class=\"w-2 h-2 rounded-full bg-emerald-400 animate-pulse\"></span>");
            sb.AppendLine($"                    <span>Top Rated {category} in {city} • Google Verified</span>");
            sb.AppendLine("                </div>");
            sb.AppendLine($"                <h1 class=\"text-4xl sm:text-6xl font-extrabold tracking-tight text-white mb-6 leading-tight max-w-4xl mx-auto\">{headline}</h1>");
            sb.AppendLine($"                <p class=\"text-lg sm:text-xl text-slate-300 mb-10 max-w-3xl mx-auto leading-relaxed font-normal\">{subheadline}</p>");
            sb.AppendLine("                <div class=\"flex flex-col sm:flex-row items-center justify-center gap-4\">");
            sb.AppendLine($"                    <a href=\"#contact\" class=\"w-full sm:w-auto px-8 py-4 rounded-xl text-base shadow-2xl transition-all duration-200 transform hover:scale-105 {buttonClass}\">");
            sb.AppendLine($"                        <i class=\"fa-solid fa-calendar-check mr-2\"></i>{ctaText}");
            sb.AppendLine("                    </a>");
            sb.AppendLine($"                    <a href=\"tel:{phone}\" class=\"w-full sm:w-auto px-8 py-4 rounded-xl text-base font-semibold border border-slate-700 bg-slate-900/80 hover:bg-slate-800 text-white transition-all duration-200\">");
            sb.AppendLine($"                        <i class=\"fa-solid fa-phone mr-2 text-cyan-400\"></i>{phone}");
            sb.AppendLine("                    </a>");
            sb.AppendLine("                </div>");
            sb.AppendLine("                <!-- Key metrics pill -->");
            sb.AppendLine("                <div class=\"grid grid-cols-2 md:grid-cols-4 gap-6 mt-16 pt-12 border-t border-slate-800/80 text-left\">");
            sb.AppendLine("                    <div class=\"p-4 rounded-lg bg-slate-900/40 border border-slate-800\">");
            sb.AppendLine("                        <div class=\"text-3xl font-extrabold text-white\">99.4%</div>");
            sb.AppendLine("                        <div class=\"text-xs text-slate-400 mt-1\">Client Satisfaction</div>");
            sb.AppendLine("                    </div>");
            sb.AppendLine("                    <div class=\"p-4 rounded-lg bg-slate-900/40 border border-slate-800\">");
            sb.AppendLine("                        <div class=\"text-3xl font-extrabold text-white\">1,400+</div>");
            sb.AppendLine($"                        <div class=\"text-xs text-slate-400 mt-1\">Clients in {city}</div>");
            sb.AppendLine("                    </div>");
            sb.AppendLine("                    <div class=\"p-4 rounded-lg bg-slate-900/40 border border-slate-800\">");
            sb.AppendLine("                        <div class=\"text-3xl font-extrabold text-white\">10+ Yrs</div>");
            sb.AppendLine("                        <div class=\"text-xs text-slate-400 mt-1\">Industry Leadership</div>");
            sb.AppendLine("                    </div>");
            sb.AppendLine("                    <div class=\"p-4 rounded-lg bg-slate-900/40 border border-slate-800\">");
            sb.AppendLine("                        <div class=\"text-3xl font-extrabold text-white\">4.9 ★</div>");
            sb.AppendLine("                        <div class=\"text-xs text-slate-400 mt-1\">Over 250+ Reviews</div>");
            sb.AppendLine("                    </div>");
            sb.AppendLine("                </div>");
            sb.AppendLine("            </div>");
            sb.AppendLine("        </section>");
        }

        // Trust Bar
        if (modules.Contains("mod-trust-bar"))
        {
            sb.AppendLine("        <!-- Trust & Logos Bar -->");
            sb.AppendLine("        <section class=\"py-8 border-y border-slate-800/60 bg-slate-950/40\">");
            sb.AppendLine("            <div class=\"max-w-7xl mx-auto px-4 text-center\">");
            sb.AppendLine("                <p class=\"text-xs uppercase tracking-widest text-slate-500 font-semibold mb-6\">Accredited &amp; Trusted By Leading Industry Organizations</p>");
            sb.AppendLine("                <div class=\"flex flex-wrap items-center justify-center gap-8 md:gap-16 opacity-70 grayscale hover:grayscale-0 transition-all duration-300\">");
            sb.AppendLine("                    <div class=\"flex items-center space-x-2 text-slate-300 font-bold text-sm\"><i class=\"fa-brands fa-google text-lg text-emerald-400\"></i><span>Google Guaranteed</span></div>");
            sb.AppendLine("                    <div class=\"flex items-center space-x-2 text-slate-300 font-bold text-sm\"><i class=\"fa-solid fa-shield-halved text-lg text-cyan-400\"></i><span>ISO 9001 Certified</span></div>");
            sb.AppendLine("                    <div class=\"flex items-center space-x-2 text-slate-300 font-bold text-sm\"><i class=\"fa-solid fa-award text-lg text-amber-400\"></i><span>Excellence Award 2025</span></div>");
            sb.AppendLine("                    <div class=\"flex items-center space-x-2 text-slate-300 font-bold text-sm\"><i class=\"fa-solid fa-certificate text-lg text-indigo-400\"></i><span>Licensed Chamber Member</span></div>");
            sb.AppendLine("                </div>");
            sb.AppendLine("            </div>");
            sb.AppendLine("        </section>");
        }

        // Services Grid
        if (modules.Contains("mod-services"))
        {
            sb.AppendLine("        <!-- Services Section -->");
            sb.AppendLine("        <section id=\"services\" class=\"py-24 relative\">");
            sb.AppendLine("            <div class=\"max-w-7xl mx-auto px-4 sm:px-6 lg:px-8\">");
            sb.AppendLine("                <div class=\"text-center max-w-3xl mx-auto mb-16\">");
            sb.AppendLine($"                    <h2 class=\"text-xs font-bold uppercase tracking-widest text-cyan-400 mb-3\">Tailored Solutions</h2>");
            sb.AppendLine($"                    <p class=\"text-3xl sm:text-4xl font-extrabold text-white tracking-tight\">Everything Your Project Demands Under One Roof</p>");
            sb.AppendLine($"                    <p class=\"text-slate-400 mt-4 text-base\">Engineered with precision, industry-leading methodology, and personalized care for every {city} client.</p>");
            sb.AppendLine("                </div>");
            sb.AppendLine("                <div class=\"grid grid-cols-1 md:grid-cols-3 gap-8\">");
            sb.AppendLine($"                    <div class=\"p-8 rounded-2xl glass-card hover:border-cyan-500/50 transition duration-300 group\">");
            sb.AppendLine($"                        <div class=\"w-14 h-14 rounded-xl flex items-center justify-center text-2xl mb-6 bg-cyan-500/10 text-cyan-400 group-hover:scale-110 transition\">");
            sb.AppendLine("                            <i class=\"fa-solid fa-bolt\"></i>");
            sb.AppendLine("                        </div>");
            sb.AppendLine($"                        <h3 class=\"text-xl font-bold text-white mb-3\">Premium {category} Delivery</h3>");
            sb.AppendLine($"                        <p class=\"text-slate-400 text-sm leading-relaxed mb-6\">Comprehensive end-to-end execution utilizing modern standards, zero corner-cutting, and strict quality control.</p>");
            sb.AppendLine($"                        <a href=\"#contact\" class=\"inline-flex items-center text-sm font-semibold text-cyan-400 hover:text-cyan-300\">Learn More <i class=\"fa-solid fa-arrow-right ml-2 text-xs\"></i></a>");
            sb.AppendLine("                    </div>");
            sb.AppendLine($"                    <div class=\"p-8 rounded-2xl glass-card hover:border-cyan-500/50 transition duration-300 group\">");
            sb.AppendLine($"                        <div class=\"w-14 h-14 rounded-xl flex items-center justify-center text-2xl mb-6 bg-emerald-500/10 text-emerald-400 group-hover:scale-110 transition\">");
            sb.AppendLine("                            <i class=\"fa-solid fa-stethoscope\"></i>");
            sb.AppendLine("                        </div>");
            sb.AppendLine($"                        <h3 class=\"text-xl font-bold text-white mb-3\">Specialized Diagnostics &amp; Audit</h3>");
            sb.AppendLine($"                        <p class=\"text-slate-400 text-sm leading-relaxed mb-6\">Deep-dive assessment pinpointing friction points, structural bottlenecks, and high-impact revenue opportunities.</p>");
            sb.AppendLine($"                        <a href=\"#contact\" class=\"inline-flex items-center text-sm font-semibold text-emerald-400 hover:text-emerald-300\">Request Assessment <i class=\"fa-solid fa-arrow-right ml-2 text-xs\"></i></a>");
            sb.AppendLine("                    </div>");
            sb.AppendLine($"                    <div class=\"p-8 rounded-2xl glass-card hover:border-cyan-500/50 transition duration-300 group\">");
            sb.AppendLine($"                        <div class=\"w-14 h-14 rounded-xl flex items-center justify-center text-2xl mb-6 bg-indigo-500/10 text-indigo-400 group-hover:scale-110 transition\">");
            sb.AppendLine("                            <i class=\"fa-solid fa-handshake-angle\"></i>");
            sb.AppendLine("                        </div>");
            sb.AppendLine($"                        <h3 class=\"text-xl font-bold text-white mb-3\">VIP Dedicated Retainer Support</h3>");
            sb.AppendLine($"                        <p class=\"text-slate-400 text-sm leading-relaxed mb-6\">Priority direct hotline, 24/7 SLA emergency turnaround, and continuous optimization for ongoing peace of mind.</p>");
            sb.AppendLine($"                        <a href=\"#contact\" class=\"inline-flex items-center text-sm font-semibold text-indigo-400 hover:text-indigo-300\">Explore Retainers <i class=\"fa-solid fa-arrow-right ml-2 text-xs\"></i></a>");
            sb.AppendLine("                    </div>");
            sb.AppendLine("                </div>");
            sb.AppendLine("            </div>");
            sb.AppendLine("        </section>");
        }

        // Testimonials Section
        if (modules.Contains("mod-testimonials"))
        {
            sb.AppendLine("        <!-- Testimonials Section -->");
            sb.AppendLine("        <section id=\"testimonials\" class=\"py-24 bg-slate-950/50 border-t border-slate-800/80\">");
            sb.AppendLine("            <div class=\"max-w-7xl mx-auto px-4 sm:px-6 lg:px-8\">");
            sb.AppendLine("                <div class=\"text-center max-w-2xl mx-auto mb-16\">");
            sb.AppendLine($"                    <div class=\"text-amber-400 text-lg mb-2\"><i class=\"fa-solid fa-star\"></i><i class=\"fa-solid fa-star\"></i><i class=\"fa-solid fa-star\"></i><i class=\"fa-solid fa-star\"></i><i class=\"fa-solid fa-star\"></i></div>");
            sb.AppendLine($"                    <h2 class=\"text-3xl sm:text-4xl font-extrabold text-white tracking-tight\">Real Results from Real Clients in {city}</h2>");
            sb.AppendLine("                </div>");
            sb.AppendLine("                <div class=\"grid grid-cols-1 md:grid-cols-3 gap-8\">");
            sb.AppendLine("                    <div class=\"p-6 rounded-2xl glass-card\">");
            sb.AppendLine("                        <div class=\"flex items-center space-x-1 text-amber-400 text-xs mb-4\"><i class=\"fa-solid fa-star\"></i><i class=\"fa-solid fa-star\"></i><i class=\"fa-solid fa-star\"></i><i class=\"fa-solid fa-star\"></i><i class=\"fa-solid fa-star\"></i></div>");
            sb.AppendLine($"                        <p class=\"text-slate-300 text-sm italic mb-6 leading-relaxed\">\"{bizName} revolutionized our customer onboarding. The professionalism and speed in {city} were unlike anything we had experienced before.\"</p>");
            sb.AppendLine("                        <div class=\"flex items-center space-x-3\">");
            sb.AppendLine("                            <div class=\"w-10 h-10 rounded-full bg-slate-800 flex items-center justify-center font-bold text-white\">MA</div>");
            sb.AppendLine("                            <div><div class=\"text-sm font-bold text-white\">Malik Asad</div><div class=\"text-xs text-slate-400\">Managing Director, Apex Global</div></div>");
            sb.AppendLine("                        </div>");
            sb.AppendLine("                    </div>");
            sb.AppendLine("                    <div class=\"p-6 rounded-2xl glass-card\">");
            sb.AppendLine("                        <div class=\"flex items-center space-x-1 text-amber-400 text-xs mb-4\"><i class=\"fa-solid fa-star\"></i><i class=\"fa-solid fa-star\"></i><i class=\"fa-solid fa-star\"></i><i class=\"fa-solid fa-star\"></i><i class=\"fa-solid fa-star\"></i></div>");
            sb.AppendLine($"                        <p class=\"text-slate-300 text-sm italic mb-6 leading-relaxed\">\"Flawless attention to detail. Our inquiries jumped 40% within the first month. They are truly the top {category} leaders.\"</p>");
            sb.AppendLine("                        <div class=\"flex items-center space-x-3\">");
            sb.AppendLine("                            <div class=\"w-10 h-10 rounded-full bg-slate-800 flex items-center justify-center font-bold text-white\">SF</div>");
            sb.AppendLine("                            <div><div class=\"text-sm font-bold text-white\">Dr. Sana Farooq</div><div class=\"text-xs text-slate-400\">Founder, Horizon Health</div></div>");
            sb.AppendLine("                        </div>");
            sb.AppendLine("                    </div>");
            sb.AppendLine("                    <div class=\"p-6 rounded-2xl glass-card\">");
            sb.AppendLine("                        <div class=\"flex items-center space-x-1 text-amber-400 text-xs mb-4\"><i class=\"fa-solid fa-star\"></i><i class=\"fa-solid fa-star\"></i><i class=\"fa-solid fa-star\"></i><i class=\"fa-solid fa-star\"></i><i class=\"fa-solid fa-star\"></i></div>");
            sb.AppendLine($"                        <p class=\"text-slate-300 text-sm italic mb-6 leading-relaxed\">\"Clear communication, zero hidden fees, and world-class execution. Look no further if you are based in {city} or abroad.\"</p>");
            sb.AppendLine("                        <div class=\"flex items-center space-x-3\">");
            sb.AppendLine("                            <div class=\"w-10 h-10 rounded-full bg-slate-800 flex items-center justify-center font-bold text-white\">TR</div>");
            sb.AppendLine("                            <div><div class=\"text-sm font-bold text-white\">Tariq Rahman</div><div class=\"text-xs text-slate-400\">Operations Head, Capital Group</div></div>");
            sb.AppendLine("                        </div>");
            sb.AppendLine("                    </div>");
            sb.AppendLine("                </div>");
            sb.AppendLine("            </div>");
            sb.AppendLine("        </section>");
        }

        // FAQ Accordion
        if (modules.Contains("mod-faq"))
        {
            sb.AppendLine("        <!-- FAQ Accordion -->");
            sb.AppendLine("        <section id=\"faq\" class=\"py-20 max-w-4xl mx-auto px-4 sm:px-6\">");
            sb.AppendLine("            <div class=\"text-center mb-12\">");
            sb.AppendLine("                <h2 class=\"text-2xl sm:text-3xl font-extrabold text-white\">Frequently Asked Questions</h2>");
            sb.AppendLine("                <p class=\"text-slate-400 text-sm mt-2\">Got questions? We have clear answers.</p>");
            sb.AppendLine("            </div>");
            sb.AppendLine("            <div class=\"space-y-4\">");
            sb.AppendLine("                <div class=\"border border-slate-800 rounded-xl bg-slate-900/60 p-5 cursor-pointer\" onclick=\"toggleFaq(this)\">");
            sb.AppendLine($"                    <div class=\"flex justify-between items-center font-semibold text-white text-base\"><span>How quickly can {bizName} initiate our project?</span><i class=\"fa-solid fa-chevron-down text-xs transition transform\"></i></div>");
            sb.AppendLine("                    <p class=\"text-slate-400 text-sm mt-3 hidden leading-relaxed\">We initiate discovery within 24 hours of onboarding and deliver initial milestones within 5-7 business days.</p>");
            sb.AppendLine("                </div>");
            sb.AppendLine("                <div class=\"border border-slate-800 rounded-xl bg-slate-900/60 p-5 cursor-pointer\" onclick=\"toggleFaq(this)\">");
            sb.AppendLine($"                    <div class=\"flex justify-between items-center font-semibold text-white text-base\"><span>Do you provide ongoing support after deployment?</span><i class=\"fa-solid fa-chevron-down text-xs transition transform\"></i></div>");
            sb.AppendLine("                    <p class=\"text-slate-400 text-sm mt-3 hidden leading-relaxed\">Yes! Every package includes a 30-day warranty period plus optional dedicated monthly retainer maintenance tiers.</p>");
            sb.AppendLine("                </div>");
            sb.AppendLine("                <div class=\"border border-slate-800 rounded-xl bg-slate-900/60 p-5 cursor-pointer\" onclick=\"toggleFaq(this)\">");
            sb.AppendLine($"                    <div class=\"flex justify-between items-center font-semibold text-white text-base\"><span>Are pricing packages fixed with no hidden fees?</span><i class=\"fa-solid fa-chevron-down text-xs transition transform\"></i></div>");
            sb.AppendLine("                    <p class=\"text-slate-400 text-sm mt-3 hidden leading-relaxed\">100% transparent. All deliverables, timelines, and costs are formalized upfront in a detailed Statement of Work.</p>");
            sb.AppendLine("                </div>");
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
            sb.AppendLine($"                        <h2 class=\"text-3xl font-extrabold text-white tracking-tight\">{ctaText}</h2>");
            sb.AppendLine($"                        <p class=\"text-slate-400 text-sm mt-2\">Fill in your details below. A senior {category} advisor will connect within 2 hours.</p>");
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
                    sb.AppendLine("- **Purpose:** Showcase 3 distinct service offerings with concrete deliverables and clear business value.");
                    break;
                case "mod-testimonials":
                    sb.AppendLine("- **Purpose:** Provide authentic social proof from local regional clients with identifiable titles and satisfaction quotes.");
                    break;
                case "mod-faq":
                    sb.AppendLine("- **Purpose:** Defuse top 3 conversion blockers (turnaround times, post-launch support, and scope boundaries) with interactive accordions.");
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
