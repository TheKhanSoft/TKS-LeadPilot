using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TKS.LeadPilot.Core.Enums;
using TKS.LeadPilot.Core.Interfaces;
using TKS.LeadPilot.Core.Models;

namespace TKS.LeadPilot.Core.Services;

public class TksServiceMatcher : ITksServiceMatcher
{
    private static readonly List<TksService> DefaultCatalog =
    [
        new()
        {
            Id = "svc-web-dev",
            Name = "Modern Business Website Development",
            Category = "Web Development",
            Description = "High-performance, responsive business website redesign built with modern frameworks, fast load speeds, and SEO readiness.",
            StartingPricePkr = 75000,
            StartingPriceUsd = 350,
            PitchTemplate = "We noticed {{business_name}} is currently missing a modern responsive web presence. We specialize in building fast, mobile-friendly websites that turn visitors into paying clients."
        },
        new()
        {
            Id = "svc-core-web-vitals",
            Name = "Website Speed & Core Web Vitals Optimization",
            Category = "Optimization",
            Description = "Comprehensive remediation of TTFB, render-blocking resources, and mobile optimization to drastically reduce bounce rates.",
            StartingPricePkr = 45000,
            StartingPriceUsd = 200,
            PitchTemplate = "Our technical audit of {{website_url}} identified slow mobile page load times (TTFB {{ttfb_ms}}ms). We optimize websites to achieve 90+ Lighthouse performance scores."
        },
        new()
        {
            Id = "svc-security-retainer",
            Name = "Website Security, SSL & Maintenance Retainer",
            Category = "Security & Maintenance",
            Description = "HTTPS/TLS enforcement, automated daily backups, security header hardening, and 24/7 uptime monitoring.",
            StartingPricePkr = 25000,
            StartingPriceUsd = 100,
            PitchTemplate = "Your website is currently flagged as 'Not Secure' by web browsers due to missing SSL certificates. We secure websites with automated SSL and security hygiene retainers."
        },
        new()
        {
            Id = "svc-custom-software",
            Name = "Custom Web Application & Workforce Solutions",
            Category = "Custom Software",
            Description = "Bespoke internal management systems, workforce attendance, booking portals, and CRM automation tailored for business operations.",
            StartingPricePkr = 150000,
            StartingPriceUsd = 700,
            PitchTemplate = "We design custom workflow and customer management systems tailored to streamline operations for {{category}} organizations in {{city}}."
        },
        new()
        {
            Id = "svc-gmb-local-seo",
            Name = "Google Business Profile (GMB) & Local Maps Booster",
            Category = "Local SEO & Reputation",
            Description = "Google Business Profile verification, local Maps SEO ranking optimization, review acceleration, and local search dominance.",
            StartingPricePkr = 35000,
            StartingPriceUsd = 150,
            PitchTemplate = "We noticed {{business_name}} does not have a verified Google Business Profile on Google Maps. Potential clients searching for {{category}} in {{city}} are discovering competitors instead. We claim, verify, and optimize your Google Maps presence."
        }
    ];

    public Task<List<TksServiceMatch>> MatchServicesAsync(
        Business business,
        WebsiteAudit? audit,
        LeadScore? score,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(business);

        var matches = new List<TksServiceMatch>();

        var webDevService = DefaultCatalog.First(s => s.Id == "svc-web-dev");
        var speedService = DefaultCatalog.First(s => s.Id == "svc-core-web-vitals");
        var securityService = DefaultCatalog.First(s => s.Id == "svc-security-retainer");
        var customSoftwareService = DefaultCatalog.First(s => s.Id == "svc-custom-software");

        // 1. Web Development Match
        if (string.IsNullOrWhiteSpace(business.WebsiteUrl) || business.WebsiteStatus == WebsiteStatus.NoWebsite)
        {
            matches.Add(new TksServiceMatch
            {
                Service = webDevService,
                MatchConfidence = 95,
                MatchReason = "Business has no detected website. Prime candidate for full web design & deployment.",
                GeneratedPitch = RenderPitch(webDevService.PitchTemplate, business, audit)
            });
        }
        else if (business.WebsiteStatus is WebsiteStatus.ParkedDomain or WebsiteStatus.UnderConstruction or WebsiteStatus.ConnectionError)
        {
            matches.Add(new TksServiceMatch
            {
                Service = webDevService,
                MatchConfidence = 90,
                MatchReason = $"Existing domain is inactive or parked ({business.WebsiteStatus}). Needs re-architecting.",
                GeneratedPitch = RenderPitch(webDevService.PitchTemplate, business, audit)
            });
        }
        else if (audit != null && (audit.OverallScore ?? 100) < 60)
        {
            matches.Add(new TksServiceMatch
            {
                Service = webDevService,
                MatchConfidence = 85,
                MatchReason = $"Website exhibits low overall technical health ({audit.OverallScore}%). High candidate for modernization.",
                GeneratedPitch = RenderPitch(webDevService.PitchTemplate, business, audit)
            });
        }

        // 2. Speed & Performance Match
        if (audit != null && ((audit.TtfbMs ?? 0) > 800 || (audit.PerformanceScore ?? 100) < 70))
        {
            var ttfb = audit.TtfbMs ?? 0;
            matches.Add(new TksServiceMatch
            {
                Service = speedService,
                MatchConfidence = ttfb > 1500 ? 95 : 85,
                MatchReason = $"High server latency ({ttfb:F0}ms TTFB) and sub-par performance ({audit.PerformanceScore}%).",
                GeneratedPitch = RenderPitch(speedService.PitchTemplate, business, audit)
            });
        }

        // 3. Security & Maintenance Match
        if (audit != null && (!audit.SslValid || !audit.HttpRedirectsToHttps || (audit.SecurityScore ?? 100) < 70))
        {
            matches.Add(new TksServiceMatch
            {
                Service = securityService,
                MatchConfidence = !audit.SslValid ? 98 : 88,
                MatchReason = !audit.SslValid
                    ? "SSL certificate is invalid or missing (browser security alert)."
                    : "Missing modern security headers and automatic HTTPS redirection.",
                GeneratedPitch = RenderPitch(securityService.PitchTemplate, business, audit)
            });
        }

        // 4. Custom Enterprise Solutions Match
        var enterpriseCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Healthcare", "Clinic", "Hospital", "Retail", "Manufacturing", "Automotive", "Logistics", "Real Estate"
        };

        if (enterpriseCategories.Any(c => business.Category.Contains(c, StringComparison.OrdinalIgnoreCase)))
        {
            matches.Add(new TksServiceMatch
            {
                Service = customSoftwareService,
                MatchConfidence = 80,
                MatchReason = $"Industry vertical '{business.Category}' has high demand for tailored portal and workflow systems.",
                GeneratedPitch = RenderPitch(customSoftwareService.PitchTemplate, business, audit)
            });
        }

        // 5. Google My Business (GMB) & Local Maps Booster Match
        if (!business.IsGoogleVerified && string.IsNullOrWhiteSpace(business.GooglePlaceId))
        {
            var gmbService = DefaultCatalog.First(s => s.Id == "svc-gmb-local-seo");
            matches.Add(new TksServiceMatch
            {
                Service = gmbService,
                MatchConfidence = 92,
                MatchReason = "Business has no verified Google Business Profile or Google Maps presence. Prime prospect for local search optimization.",
                GeneratedPitch = RenderPitch(gmbService.PitchTemplate, business, audit)
            });
        }

        // Default fallback if no specific match
        if (matches.Count == 0)
        {
            matches.Add(new TksServiceMatch
            {
                Service = customSoftwareService,
                MatchConfidence = 70,
                MatchReason = "General technology advisory and bespoke software consulting.",
                GeneratedPitch = RenderPitch(customSoftwareService.PitchTemplate, business, audit)
            });
        }

        return Task.FromResult(matches.OrderByDescending(m => m.MatchConfidence).ToList());
    }

    private static string RenderPitch(string template, Business business, WebsiteAudit? audit)
    {
        var pitch = template
            .Replace("{{business_name}}", !string.IsNullOrWhiteSpace(business.Name) ? business.Name : "your organization")
            .Replace("{{website_url}}", !string.IsNullOrWhiteSpace(business.WebsiteUrl) ? business.WebsiteUrl : "your website")
            .Replace("{{city}}", !string.IsNullOrWhiteSpace(business.City) ? business.City : "your region")
            .Replace("{{category}}", !string.IsNullOrWhiteSpace(business.Category) ? business.Category : "enterprise");

        if (audit != null)
        {
            pitch = pitch.Replace("{{ttfb_ms}}", $"{audit.TtfbMs:F0}")
                         .Replace("{{lcp_value}}", $"{((audit.TtfbMs ?? 1000) / 1000.0 * 2.2):F1}");
        }
        else
        {
            pitch = pitch.Replace("{{ttfb_ms}}", "1200")
                         .Replace("{{lcp_value}}", "3.5");
        }

        return pitch;
    }
}
