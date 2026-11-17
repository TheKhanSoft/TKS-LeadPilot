using Microsoft.EntityFrameworkCore;
using TKS.LeadPilot.Core.Enums;
using TKS.LeadPilot.Core.Interfaces;
using TKS.LeadPilot.Core.Models;

namespace TKS.LeadPilot.Data;

public class DatabaseInitializer : IDatabaseInitializer
{
    private readonly AppDbContext _context;

    public DatabaseInitializer(AppDbContext context)
    {
        _context = context;
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await _context.Database.EnsureCreatedAsync(ct);

        // Seed Pipeline Stages if empty
        if (!await _context.PipelineStages.AnyAsync(ct))
        {
            var stages = new List<PipelineStage>
            {
                new() { Id = "stage-discovered", Name = "Discovered", Code = "DISCOVERED", DisplayOrder = 1, ColorHex = "#94A3B8" },
                new() { Id = "stage-to-review", Name = "To Review", Code = "TO_REVIEW", DisplayOrder = 2, ColorHex = "#64748B" },
                new() { Id = "stage-qualified", Name = "Qualified", Code = "QUALIFIED", DisplayOrder = 3, ColorHex = "#3B82F6" },
                new() { Id = "stage-researching", Name = "Researching", Code = "RESEARCHING", DisplayOrder = 4, ColorHex = "#0EA5E9" },
                new() { Id = "stage-contact-drafted", Name = "Contact Drafted", Code = "CONTACT_DRAFTED", DisplayOrder = 5, ColorHex = "#F59E0B" },
                new() { Id = "stage-awaiting-approval", Name = "Awaiting Approval", Code = "AWAITING_APPROVAL", DisplayOrder = 6, ColorHex = "#D97706" },
                new() { Id = "stage-contacted", Name = "Contacted", Code = "CONTACTED", DisplayOrder = 7, ColorHex = "#8B5CF6" },
                new() { Id = "stage-responded", Name = "Responded", Code = "RESPONDED", DisplayOrder = 8, ColorHex = "#A855F7" },
                new() { Id = "stage-meeting-scheduled", Name = "Meeting Scheduled", Code = "MEETING_SCHEDULED", DisplayOrder = 9, ColorHex = "#6366F1" },
                new() { Id = "stage-proposal-sent", Name = "Proposal Sent", Code = "PROPOSAL_SENT", DisplayOrder = 10, ColorHex = "#EC4899" },
                new() { Id = "stage-negotiation", Name = "Negotiation", Code = "NEGOTIATION", DisplayOrder = 11, ColorHex = "#14B8A6" },
                new() { Id = "stage-won", Name = "Won", Code = "WON", DisplayOrder = 12, ColorHex = "#10B981", IsWonStage = true },
                new() { Id = "stage-lost", Name = "Lost", Code = "LOST", DisplayOrder = 13, ColorHex = "#EF4444", IsLostStage = true },
                new() { Id = "stage-do-not-contact", Name = "Do Not Contact", Code = "DO_NOT_CONTACT", DisplayOrder = 14, ColorHex = "#475569", IsLostStage = true }
            };

            await _context.PipelineStages.AddRangeAsync(stages, ct);
            await _context.SaveChangesAsync(ct);
        }

        // Seed TKS Services Catalogue if empty
        if (!await _context.TksServices.AnyAsync(ct))
        {
            var services = new List<TksService>
            {
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
                    Description = "Comprehensive remediation of LCP, CLS, render-blocking resources, and mobile optimization to drastically reduce bounce rates.",
                    StartingPricePkr = 45000,
                    StartingPriceUsd = 200,
                    PitchTemplate = "Our technical audit of {{website_url}} identified slow mobile page load times (LCP {{lcp_value}}s). We optimize websites to achieve 90+ Lighthouse performance scores."
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
                    PitchTemplate = "We design custom workflow and customer management systems tailored to streamline operations for {{category}} organizations."
                }
            };

            await _context.TksServices.AddRangeAsync(services, ct);
            await _context.SaveChangesAsync(ct);
        }

        // Seed initial initial sample businesses if database has zero businesses
        if (!await _context.Businesses.AnyAsync(ct))
        {
            var defaultStage = await _context.PipelineStages.FirstAsync(s => s.Code == "QUALIFIED", ct);

            var sampleBusinesses = new List<Business>
            {
                new()
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = "Apex Dental & Implant Care",
                    Category = "Dentist & Dental Clinic",
                    City = "Lahore",
                    AddressLine1 = "Gulberg III, Main Boulevard",
                    PhonePrimary = "+92 42 35789012",
                    EmailPrimary = "info@apexdental.pk",
                    WebsiteUrl = "http://apexdental.pk",
                    WebsiteStatus = WebsiteStatus.ActiveWebsite,
                    PipelineStageId = defaultStage.Id,
                    LeadScore = 88,
                    LeadStatus = LeadStatus.Qualified,
                    Tags = "Dental, High Opportunity, Needs SSL",
                    IsStarred = true,
                    SourceProvider = "OpenStreetMap",
                    Contacts =
                    [
                        new()
                        {
                            FullName = "Dr. Tariq Mahmood",
                            JobTitle = "Principal Dentist",
                            Phone = "+92 300 1234567",
                            Email = "dr.tariq@apexdental.pk",
                            IsPrimary = true
                        }
                    ]
                },
                new()
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = "Careway Pharmacy & Diagnostic",
                    Category = "Pharmacy & Healthcare",
                    City = "Karachi",
                    AddressLine1 = "Clifton Block 5",
                    PhonePrimary = "+92 21 35876543",
                    EmailPrimary = "contact@careway.pk",
                    WebsiteUrl = null,
                    WebsiteStatus = WebsiteStatus.NoWebsite,
                    PipelineStageId = defaultStage.Id,
                    LeadScore = 78,
                    LeadStatus = LeadStatus.Qualified,
                    Tags = "No Website, Healthcare",
                    IsStarred = false,
                    SourceProvider = "Manual",
                    Contacts =
                    [
                        new()
                        {
                            FullName = "Muhammad Bilal",
                            JobTitle = "Managing Director",
                            Phone = "+92 321 7654321",
                            IsPrimary = true
                        }
                    ]
                },
                new()
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = "Sunrise Grand Hotel",
                    Category = "Hotels & Hospitality",
                    City = "Islamabad",
                    AddressLine1 = "F-7 Markaz",
                    PhonePrimary = "+92 51 2654321",
                    EmailPrimary = "reservations@sunrisegrand.pk",
                    WebsiteUrl = "https://sunrisegrand.pk",
                    WebsiteStatus = WebsiteStatus.ActiveWebsite,
                    PipelineStageId = (await _context.PipelineStages.FirstAsync(s => s.Code == "TO_REVIEW", ct)).Id,
                    LeadScore = 48,
                    LeadStatus = LeadStatus.ToReview,
                    Tags = "Hospitality, Moderate",
                    IsStarred = false,
                    SourceProvider = "CSV Import"
                }
            };

            await _context.Businesses.AddRangeAsync(sampleBusinesses, ct);
            await _context.SaveChangesAsync(ct);
        }

        // Seed Outreach Templates if empty (Stage 10)
        if (!await _context.OutreachTemplates.AnyAsync(ct))
        {
            var templates = new List<OutreachTemplate>
            {
                new()
                {
                    Id = "tpl-audit-email",
                    Name = "Technical Audit & Modernization Proposal",
                    Channel = OutreachChannel.Email,
                    Category = "Audit Discovery",
                    SubjectTemplate = "Technical assessment and growth opportunities for {{business_name}}",
                    BodyTemplate = "Hi {{contact_name}},\n\nWe recently conducted a technical assessment of {{business_name}} in {{city}}. While analyzing {{website_url}}, our diagnostics identified key friction points ({{top_audit_issue}}).\n\nAt TheKhanSoft, we specialize in {{suggested_service}}. We prepared an executive audit report outlining actionable solutions to elevate your digital presence.\n\nWould you be open to a brief 10-minute conversation this week?\n\nBest regards,\nTheKhanSoft Outreach Team"
                },
                new()
                {
                    Id = "tpl-speed-email",
                    Name = "Speed & Core Web Vitals Optimization",
                    Channel = OutreachChannel.Email,
                    Category = "Performance",
                    SubjectTemplate = "Mobile load speed and search ranking for {{business_name}}",
                    BodyTemplate = "Hi {{contact_name}},\n\nOur diagnostic scan of {{website_url}} registered server latency of {{ttfb_ms}}ms. Under Google's search algorithms, slow response times directly impact client acquisition in {{city}}.\n\nThrough our {{suggested_service}}, we optimize websites to load in under 1 second.\n\nCan we share our 1-page remediation plan with you?\n\nRegards,\nTheKhanSoft Engineering"
                },
                new()
                {
                    Id = "tpl-quick-wa",
                    Name = "Quick Website Diagnostic Notice",
                    Channel = OutreachChannel.WhatsApp,
                    Category = "Quick Outreach",
                    BodyTemplate = "Hello {{contact_name}}! 👋 I'm reaching out from TheKhanSoft. We ran a routine performance scan on {{business_name}}'s site ({{website_url}}) and identified a key bottleneck ({{top_audit_issue}}). We prepared a complimentary 1-page report for your team. Would you like me to share it here?"
                },
                new()
                {
                    Id = "tpl-noweb-wa",
                    Name = "New Website & Digital Presence Offer",
                    Channel = OutreachChannel.WhatsApp,
                    Category = "New Website",
                    BodyTemplate = "Assalam-o-Alaikum {{contact_name}}! We noticed {{business_name}} in {{city}} is missing an official mobile-friendly website. Potential clients searching for {{category}} in {{city}} are finding competitors instead. TheKhanSoft builds modern high-speed websites tailored for your sector. Would you be interested in viewing a sample mockup?"
                }
            };

            await _context.OutreachTemplates.AddRangeAsync(templates, ct);
            await _context.SaveChangesAsync(ct);
        }
    }
}
