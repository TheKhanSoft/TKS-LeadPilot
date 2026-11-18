using System;
using System.Collections.Generic;

namespace TKS.LeadPilot.Core.Models;

public enum LandingPageArchetype
{
    LeadGenerationFunnel,      // High-converting lead magnet & booking funnel
    CorporateAgencyPortal,     // Authority, trust, services, enterprise credibility
    LuxuryBrandShowcase,       // High-end aesthetic, elegance, exclusivity
    LocalServiceBooking,       // Clinic, dental, repair, urgent appointment scheduler
    ModernSaaSTech,            // Product demo, feature highlights, dark mode tech
    EcommerceShowcase          // Product catalog, special offers, instant order
}

public enum LandingPageDesignStyle
{
    ModernSlateCyan,           // Tech, SaaS, Modern corporate (#0F172A slate & #06B6D4 cyan)
    MidnightLuxuryGold,        // High-end clinic, law firm, executive (#090D16 & #F59E0B amber/gold)
    TrustworthyEmerald,        // Healthcare, dental, solar, eco (#064E3B & #10B981 emerald)
    BoldVibrantIndigo,         // Creative agency, marketing, digital (#1E1B4B & #6366F1 indigo)
    MinimalistMonochrome       // Studio, architect, modern portfolio (#18181B & #FAFAFA)
}

public class LandingPageModuleOption
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = "Core";
    public bool IsSelected { get; set; } = true;
}

public class LandingPageGenerationRequest
{
    public string BusinessId { get; set; } = string.Empty;
    public string BusinessName { get; set; } = "Apex Innovations";
    public string Category { get; set; } = "Software & IT Services";
    public string City { get; set; } = "Islamabad";
    public string Country { get; set; } = "Pakistan";
    public string? ExistingWebsiteUrl { get; set; }
    public string? PhonePrimary { get; set; }
    public string? EmailPrimary { get; set; }
    public string TargetAudience { get; set; } = "Local businesses, enterprise directors, and consumers";

    public LandingPageArchetype Archetype { get; set; } = LandingPageArchetype.LeadGenerationFunnel;
    public LandingPageDesignStyle DesignStyle { get; set; } = LandingPageDesignStyle.ModernSlateCyan;
    public AiTone Tone { get; set; } = AiTone.Consultative;

    public string? CustomPromptInstructions { get; set; }
    public List<string> SelectedModules { get; set; } = [];
}

public class LandingPageGenerationResult
{
    public bool IsSuccess { get; set; } = true;
    public bool IsLocalFallback { get; set; } = false;
    public string BusinessName { get; set; } = string.Empty;
    public string Headline { get; set; } = string.Empty;
    public string Subheadline { get; set; } = string.Empty;
    public string CallToActionText { get; set; } = "Book Free Consultation";

    public string GeneratedHtml { get; set; } = string.Empty;
    public string ArchitectureBlueprintMarkdown { get; set; } = string.Empty;
    public List<string> MidjourneyPrompts { get; set; } = [];
    public string DeveloperPrompts { get; set; } = string.Empty;
    public string AdCopyPrompts { get; set; } = string.Empty;

    public int ConversionScore { get; set; } = 92;
    public int EstimatedSectionsCount { get; set; } = 8;
    public string Provider { get; set; } = "Google Gemini Free Tier (gemini-1.5-flash)";
    public string? ErrorMessage { get; set; }
    public TimeSpan ExecutionDuration { get; set; }
    public int RequestsUsedToday { get; set; }
    public int DailyLimit { get; set; } = 1200;
}

public class BespokeServiceItem
{
    public string Icon { get; set; } = "fa-solid fa-briefcase";
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Cta { get; set; } = "Learn More";
}

public class BespokeTestimonialItem
{
    public string Quote { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Initials { get; set; } = "CL";
    public int Rating { get; set; } = 5;
}

public class BespokeFaqItem
{
    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
}

public class BespokeMetricItem
{
    public string Value { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}

public class BespokeDifferentiatorItem
{
    public string Icon { get; set; } = "fa-solid fa-check";
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class BespokeContentPayload
{
    public string Headline { get; set; } = string.Empty;
    public string Subheadline { get; set; } = string.Empty;
    public string HeroBadge { get; set; } = string.Empty;
    public string CallToAction { get; set; } = string.Empty;
    public string SecondaryCta { get; set; } = string.Empty;
    public List<BespokeMetricItem> Metrics { get; set; } = [];
    public List<string> TrustBadges { get; set; } = [];
    public List<BespokeServiceItem> Services { get; set; } = [];
    public List<BespokeDifferentiatorItem> Differentiators { get; set; } = [];
    public List<BespokeTestimonialItem> Testimonials { get; set; } = [];
    public List<BespokeFaqItem> Faqs { get; set; } = [];
    public string LeadFormTitle { get; set; } = string.Empty;
    public string LeadFormSubtitle { get; set; } = string.Empty;
    public int ConversionScore { get; set; } = 94;
    public string ArchitectureBlueprintMarkdown { get; set; } = string.Empty;
    public string DeveloperPrompts { get; set; } = string.Empty;
    public string AdCopyPrompts { get; set; } = string.Empty;
    public List<string> MidjourneyPrompts { get; set; } = [];
}
