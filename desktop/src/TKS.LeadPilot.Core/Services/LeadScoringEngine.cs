using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TKS.LeadPilot.Core.Enums;
using TKS.LeadPilot.Core.Interfaces;
using TKS.LeadPilot.Core.Models;

namespace TKS.LeadPilot.Core.Services;

public class LeadScoringEngine : ILeadScoringEngine
{
    private readonly IBusinessRepository _businessRepository;
    private readonly Func<Task<ILeadScoreStore>> _storeProvider;

    public interface ILeadScoreStore
    {
        Task SaveAsync(LeadScore score, CancellationToken ct = default);
        Task<LeadScore?> GetLatestByBusinessIdAsync(string businessId, CancellationToken ct = default);
    }

    public LeadScoringEngine(IBusinessRepository businessRepository, Func<Task<ILeadScoreStore>>? storeProvider = null)
    {
        _businessRepository = businessRepository;
        _storeProvider = storeProvider ?? (() => Task.FromResult<ILeadScoreStore>(new InMemoryScoreStore()));
    }

    public async Task<LeadScore> CalculateScoreAsync(Business business, WebsiteAudit? latestAudit = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(business);

        var score = new LeadScore
        {
            BusinessId = business.Id,
            RuleVersion = "1.0.0",
            CalculatedAtUtc = DateTime.UtcNow
        };

        var components = new List<LeadScoreComponent>();

        // Component 1: Website Presence & Condition (Max 35 pts)
        var comp1 = new LeadScoreComponent
        {
            LeadScoreId = score.Id,
            ComponentName = "Website Presence & Condition",
            MaxPointsPossible = 35
        };

        if (string.IsNullOrWhiteSpace(business.WebsiteUrl) || business.WebsiteStatus == WebsiteStatus.NoWebsite)
        {
            comp1.PointsAwarded = 35;
            comp1.Explanation = "No web presence detected - prime commercial candidate for modern website development.";
        }
        else if (business.WebsiteStatus is WebsiteStatus.ParkedDomain or WebsiteStatus.UnderConstruction or WebsiteStatus.ConnectionError)
        {
            comp1.PointsAwarded = 25;
            comp1.Explanation = $"Website is non-functional or parked ({business.WebsiteStatus}). High need for redesign/rebuild.";
        }
        else if (latestAudit != null)
        {
            var overall = latestAudit.OverallScore ?? 50;
            if (overall < 50)
            {
                comp1.PointsAwarded = 20;
                comp1.Explanation = $"Website diagnosed with severe overall health defects ({overall}% score). Candidate for full overhaul.";
            }
            else if (overall < 70)
            {
                comp1.PointsAwarded = 10;
                comp1.Explanation = $"Website has moderate health issues ({overall}% score) with optimization headroom.";
            }
            else
            {
                comp1.PointsAwarded = 0;
                comp1.Explanation = $"Website is healthy ({overall}% score). Low urgency for core redesign.";
            }
        }
        else
        {
            comp1.PointsAwarded = 15;
            comp1.Explanation = "Website exists but has not been audited yet. Baseline discovery score assigned.";
        }
        components.Add(comp1);

        // Component 2: Performance Latency & Responsiveness (Max 25 pts)
        var comp2 = new LeadScoreComponent
        {
            LeadScoreId = score.Id,
            ComponentName = "Performance & Server Latency",
            MaxPointsPossible = 25
        };

        if (latestAudit != null)
        {
            var ttfb = latestAudit.TtfbMs ?? 0;
            if (ttfb > 1500)
            {
                comp2.PointsAwarded = 25;
                comp2.Explanation = $"Critical server response time ({ttfb:F0}ms TTFB). Severe mobile visitor abandonment risk.";
            }
            else if (ttfb > 800)
            {
                comp2.PointsAwarded = 15;
                comp2.Explanation = $"Sluggish server response time ({ttfb:F0}ms TTFB). Optimization needed.";
            }
            else if ((latestAudit.PerformanceScore ?? 100) < 60)
            {
                comp2.PointsAwarded = 10;
                comp2.Explanation = $"Performance score below benchmark ({latestAudit.PerformanceScore}%).";
            }
            else
            {
                comp2.PointsAwarded = 0;
                comp2.Explanation = "Server response latency and performance meet recommended benchmarks.";
            }
        }
        else
        {
            comp2.PointsAwarded = 0;
            comp2.Explanation = "Not audited. No penalty applied for uninspected metrics.";
        }
        components.Add(comp2);

        // Component 3: Security & Trust Hygiene (Max 15 pts)
        var comp3 = new LeadScoreComponent
        {
            LeadScoreId = score.Id,
            ComponentName = "Security & Encryption Hygiene",
            MaxPointsPossible = 15
        };

        if (latestAudit != null)
        {
            if (!latestAudit.SslValid || !latestAudit.HttpRedirectsToHttps)
            {
                comp3.PointsAwarded = 15;
                comp3.Explanation = "Missing or invalid SSL certificate, or lacks automatic HTTPS redirection (flagged 'Not Secure').";
            }
            else if ((latestAudit.SecurityScore ?? 100) < 70)
            {
                comp3.PointsAwarded = 8;
                comp3.Explanation = "SSL active, but missing critical security headers (HSTS, CSP, X-Frame).";
            }
            else
            {
                comp3.PointsAwarded = 0;
                comp3.Explanation = "Robust SSL encryption and modern security headers verified.";
            }
        }
        else
        {
            comp3.PointsAwarded = 0;
            comp3.Explanation = "Not audited. No penalty applied.";
        }
        components.Add(comp3);

        // Component 4: Search Visibility & SEO Hygiene (Max 10 pts)
        var comp4 = new LeadScoreComponent
        {
            LeadScoreId = score.Id,
            ComponentName = "Search Engine Visibility & Hygiene",
            MaxPointsPossible = 10
        };

        if (latestAudit != null)
        {
            var missingIndexFiles = !latestAudit.HasRobotsTxt || !latestAudit.HasSitemapXml;
            var missingMeta = string.IsNullOrWhiteSpace(latestAudit.MetaDescription) || string.IsNullOrWhiteSpace(latestAudit.PageTitle);

            if (missingIndexFiles && missingMeta)
            {
                comp4.PointsAwarded = 10;
                comp4.Explanation = "Missing both search crawler files (robots/sitemap) and meta tags. Organic traffic severely hindered.";
            }
            else if (missingIndexFiles || missingMeta)
            {
                comp4.PointsAwarded = 5;
                comp4.Explanation = "Partial SEO deficiency: missing either search crawler files or meta description tags.";
            }
            else
            {
                comp4.PointsAwarded = 0;
                comp4.Explanation = "Basic SEO metadata and search crawler files detected.";
            }
        }
        else
        {
            comp4.PointsAwarded = 0;
            comp4.Explanation = "Not audited. No penalty applied.";
        }
        components.Add(comp4);

        // Component 5: Google Places Commercial Profile (Max 15 pts)
        var comp5 = new LeadScoreComponent
        {
            LeadScoreId = score.Id,
            ComponentName = "Google Places Commercial Authority",
            MaxPointsPossible = 15
        };

        if (!business.IsGoogleVerified)
        {
            comp5.PointsAwarded = 12;
            comp5.Explanation = "Business is not verified on Google Places / Google Maps. Major opportunity to establish verified profile.";
        }
        else
        {
            var rating = business.GoogleRating ?? 0;
            var reviews = business.GoogleReviewCount ?? 0;

            if (rating < 4.0 || reviews < 10)
            {
                comp5.PointsAwarded = 8;
                comp5.Explanation = $"Google verified, but exhibits sub-optimal review traction ({rating:F1}★ with {reviews} reviews). Reputation opportunity.";
            }
            else
            {
                comp5.PointsAwarded = 0;
                comp5.Explanation = $"Established Google profile with strong authority ({rating:F1}★ with {reviews} reviews).";
            }
        }
        components.Add(comp5);

        // Calculate Total
        score.Components = components;
        score.TotalScore = Math.Min(100, Math.Max(0, components.Sum(c => c.PointsAwarded)));

        // Update Business entity
        business.LeadScore = score.TotalScore;
        business.UpdatedAtUtc = DateTime.UtcNow;
        await _businessRepository.UpdateAsync(business, ct);

        // Persist score
        await SaveScoreAsync(score, ct);
        return score;
    }

    public async Task<LeadScore> SaveScoreAsync(LeadScore score, CancellationToken ct = default)
    {
        var store = await _storeProvider();
        await store.SaveAsync(score, ct);
        return score;
    }

    public async Task<LeadScore?> GetLatestScoreForBusinessAsync(string businessId, CancellationToken ct = default)
    {
        var store = await _storeProvider();
        return await store.GetLatestByBusinessIdAsync(businessId, ct);
    }

    public async Task<LeadScore> ApplyManualOverrideAsync(string businessId, int overriddenScore, string reason, CancellationToken ct = default)
    {
        var business = await _businessRepository.GetByIdAsync(businessId, ct)
            ?? throw new ArgumentException($"Business with ID '{businessId}' not found.");

        var clampedScore = Math.Clamp(overriddenScore, 0, 100);

        var score = new LeadScore
        {
            BusinessId = businessId,
            TotalScore = clampedScore,
            IsManualOverride = true,
            OverrideReason = reason,
            CalculatedAtUtc = DateTime.UtcNow,
            RuleVersion = "1.0.0-override",
            Components =
            [
                new LeadScoreComponent
                {
                    ComponentName = "Manual Operator Override",
                    PointsAwarded = clampedScore,
                    MaxPointsPossible = 100,
                    Explanation = $"Score manually set by sales operator. Reason: {reason}"
                }
            ]
        };

        business.LeadScore = clampedScore;
        business.UpdatedAtUtc = DateTime.UtcNow;
        await _businessRepository.UpdateAsync(business, ct);

        await SaveScoreAsync(score, ct);
        return score;
    }

    public class InMemoryScoreStore : ILeadScoreStore
    {
        private static readonly Dictionary<string, List<LeadScore>> ScoresByBusiness = new();

        public Task SaveAsync(LeadScore score, CancellationToken ct = default)
        {
            if (!ScoresByBusiness.TryGetValue(score.BusinessId, out var list))
            {
                list = [];
                ScoresByBusiness[score.BusinessId] = list;
            }
            list.Add(score);
            return Task.CompletedTask;
        }

        public Task<LeadScore?> GetLatestByBusinessIdAsync(string businessId, CancellationToken ct = default)
        {
            if (ScoresByBusiness.TryGetValue(businessId, out var list) && list.Count > 0)
            {
                return Task.FromResult<LeadScore?>(list.OrderByDescending(s => s.CalculatedAtUtc).FirstOrDefault());
            }
            return Task.FromResult<LeadScore?>(null);
        }
    }
}
