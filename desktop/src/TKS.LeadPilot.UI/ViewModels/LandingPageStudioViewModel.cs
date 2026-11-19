using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TKS.LeadPilot.Core.Common;
using TKS.LeadPilot.Core.Interfaces;
using TKS.LeadPilot.Core.Models;
using TKS.LeadPilot.UI.Views;

namespace TKS.LeadPilot.UI.ViewModels;

public partial class LandingPageStudioViewModel : ViewModelBase
{
    public static Business? TargetBusinessForStudio { get; set; }

    private readonly ILandingPageGeneratorService _generatorService;
    private readonly IBusinessRepository _businessRepository;
    private readonly ISecretVault _secretVault;

    [ObservableProperty]
    private ObservableCollection<Business> _savedBusinesses = [];

    [ObservableProperty]
    private Business? _selectedSavedBusiness;

    [ObservableProperty]
    private string _businessName = "Horizon Dental & Aesthetic Clinic";

    [ObservableProperty]
    private string _category = "Healthcare & Dental";

    [ObservableProperty]
    private string _city = "Islamabad";

    [ObservableProperty]
    private string _country = "Pakistan";

    [ObservableProperty]
    private string _phonePrimary = "+92 300 8554433";

    [ObservableProperty]
    private string _emailPrimary = "consult@horizondental.pk";

    [ObservableProperty]
    private string _targetAudience = "Discerning patients, families, and professionals seeking premium cosmetic dentistry and painless care";

    [ObservableProperty]
    private ObservableCollection<string> _archetypes =
    [
        "Lead Generation Funnel (High-Converting)",
        "Local Service Booking & Clinic Funnel",
        "Corporate Agency & Authority Portal",
        "Luxury Brand & Portfolio Showcase",
        "Modern SaaS & Tech Product",
        "E-Commerce & Special Offer Showcase"
    ];

    [ObservableProperty]
    private string _selectedArchetype = "Local Service Booking & Clinic Funnel";

    partial void OnSelectedArchetypeChanged(string value)
    {
        ApplyArchetypeRecommendedModules(value);
        UpdateThemeBrushes();
    }

    [ObservableProperty]
    private ObservableCollection<string> _designStyles =
    [
        "Trustworthy Emerald (Healthcare, Dental & Eco)",
        "Modern Slate & Cyan (Tech, SaaS & Corporate)",
        "Midnight Luxury Gold (Clinics, Law & Executive)",
        "Bold Vibrant Indigo (Creative Agency & Media)",
        "Minimalist Monochrome (Architect & Studio)"
    ];

    [ObservableProperty]
    private string _selectedDesignStyle = "Trustworthy Emerald (Healthcare, Dental & Eco)";

    partial void OnSelectedDesignStyleChanged(string value)
    {
        UpdateThemeBrushes();
        RebuildHtmlForActiveModules();
    }

    [ObservableProperty]
    private string _customInstructions = "Highlight painless dental implants, Hollywood veneers, 24/7 emergency appointments, UK-trained doctors, transparent pricing, and instant WhatsApp consultation booking.";

    [ObservableProperty]
    private ObservableCollection<LandingPageModuleOption> _availableModules = [];

    partial void OnAvailableModulesChanged(ObservableCollection<LandingPageModuleOption> value)
    {
        SubscribeToModuleChanges();
        UpdateSynthesizedModulesFlow();
        UpdateModuleVisibilityProperties();
    }

    [ObservableProperty]
    private ObservableCollection<SynthesizedModuleItem> _synthesizedModulesFlow = [];

    [ObservableProperty]
    private ObservableCollection<string> _previewTrustBadges = [];

    [ObservableProperty]
    private ObservableCollection<string> _previewServicesList = [];

    // Theme Preview Brushes & Colors
    [ObservableProperty]
    private string _previewPrimaryColor = "#059669";

    [ObservableProperty]
    private string _previewAccentColor = "#10B981";

    [ObservableProperty]
    private string _previewBgColor = "#051610";

    [ObservableProperty]
    private string _previewCardBgColor = "#0B261D";

    [ObservableProperty]
    private string _previewBadgeBg = "#064E3B";

    [ObservableProperty]
    private string _previewBadgeBorder = "#059669";

    [ObservableProperty]
    private string _previewBadgeText = "#34D399";

    [ObservableProperty]
    private string _previewButtonGradient = "#059669";

    // Dynamic Module Visibility for Simulated In-App Canvas
    public bool HasHeroModule => IsModuleActive("mod-hero");
    public bool HasTrustBarModule => IsModuleActive("mod-trust-bar");
    public bool HasServicesModule => IsModuleActive("mod-services");
    public bool HasBeforeAfterModule => IsModuleActive("mod-before-after");
    public bool HasTeamModule => IsModuleActive("mod-team");
    public bool HasTestimonialsModule => IsModuleActive("mod-testimonials");
    public bool HasPricingModule => IsModuleActive("mod-pricing");
    public bool HasCalculatorModule => IsModuleActive("mod-calculator");
    public bool HasFaqModule => IsModuleActive("mod-faq");
    public bool HasLeadFormModule => IsModuleActive("mod-lead-form");
    public bool HasWhatsAppModule => IsModuleActive("mod-whatsapp");
    public bool HasSeoModule => IsModuleActive("mod-seo");

    public bool IsModuleActive(string id) => AvailableModules.Any(m => m.Id == id && m.IsSelected);

    public void UpdateModuleVisibilityProperties()
    {
        OnPropertyChanged(nameof(HasHeroModule));
        OnPropertyChanged(nameof(HasTrustBarModule));
        OnPropertyChanged(nameof(HasServicesModule));
        OnPropertyChanged(nameof(HasBeforeAfterModule));
        OnPropertyChanged(nameof(HasTeamModule));
        OnPropertyChanged(nameof(HasTestimonialsModule));
        OnPropertyChanged(nameof(HasPricingModule));
        OnPropertyChanged(nameof(HasCalculatorModule));
        OnPropertyChanged(nameof(HasFaqModule));
        OnPropertyChanged(nameof(HasLeadFormModule));
        OnPropertyChanged(nameof(HasWhatsAppModule));
        OnPropertyChanged(nameof(HasSeoModule));
    }

    // Dynamic Simulated Canvas Preview Content
    [ObservableProperty]
    private string _previewCaseStudyTitle = "Regional Market Transformation";

    [ObservableProperty]
    private string _previewCaseStudyResult = "+140% Growth";

    [ObservableProperty]
    private string _previewCaseStudyDetail = "Modernized full conversion infrastructure and same-day booking response funnel.";

    [ObservableProperty]
    private string _previewLeaderName = "Leadership & Practice Directorate";

    [ObservableProperty]
    private string _previewLeaderRole = "Principal Director & Lead Specialist";

    [ObservableProperty]
    private string _previewLeaderQuote = "Uncompromising standards and customer care are the cornerstones of lasting trust.";

    [ObservableProperty]
    private string _previewTestimonialQuote = "The highest caliber of service in the region. Highly professional, responsive, and seamless experience.";

    [ObservableProperty]
    private string _previewTestimonialAuthor = "Satisfied Client";

    [ObservableProperty]
    private string _previewTestimonialRole = "Verified Regional Customer";

    [ObservableProperty]
    private string _previewPricingTier1 = "Essential Retainer";

    [ObservableProperty]
    private string _previewPricingTier2 = "Premier Enterprise (Most Popular)";

    [ObservableProperty]
    private string _previewFaqQuestion = "How quickly can appointments or orders be scheduled?";

    [ObservableProperty]
    private string _previewFaqAnswer = "Our priority desk responds immediately with confirmation within minutes via SMS and WhatsApp.";

    [ObservableProperty]
    private string _previewLeadFormTitle = "Schedule Priority Booking & Consultation";

    [ObservableProperty]
    private string _previewLeadFormSubtitle = "Direct confirmation dispatched to your phone within 15 minutes.";

    // Generation State
    [ObservableProperty]
    private bool _isGenerating = false;

    [ObservableProperty]
    private string _statusMessage = "Ready. Configure your requirements and click 'Generate Landing Page & Blueprint'.";

    [ObservableProperty]
    private LandingPageGenerationResult? _currentResult;

    [ObservableProperty]
    private string _activeResultTab = "Preview";

    public bool IsPreviewTab => ActiveResultTab == "Preview";
    public bool IsBlueprintTab => ActiveResultTab == "Blueprint";
    public bool IsHtmlTab => ActiveResultTab == "Html";
    public bool IsPromptsTab => ActiveResultTab == "Prompts";

    partial void OnActiveResultTabChanged(string value)
    {
        OnPropertyChanged(nameof(IsPreviewTab));
        OnPropertyChanged(nameof(IsBlueprintTab));
        OnPropertyChanged(nameof(IsHtmlTab));
        OnPropertyChanged(nameof(IsPromptsTab));
    }

    [ObservableProperty]
    private string _htmlPreviewNotice = "💡 Complete self-contained HTML5 file generated with Tailwind CSS CDN, interactive JavaScript accordions, and WhatsApp floating widget. Click '🌐 Open in Web Browser' to test live interactivity.";

    public LandingPageStudioViewModel(
        ILandingPageGeneratorService generatorService,
        IBusinessRepository businessRepository,
        ISecretVault secretVault)
    {
        _generatorService = generatorService;
        _businessRepository = businessRepository;
        _secretVault = secretVault;

        var modules = _generatorService.GetAvailableModules();
        AvailableModules = new ObservableCollection<LandingPageModuleOption>(modules);
        SubscribeToModuleChanges();
        UpdateThemeBrushes();
        UpdateSynthesizedModulesFlow();
        UpdateModuleVisibilityProperties();

        if (TargetBusinessForStudio != null)
        {
            PopulateFromBusiness(TargetBusinessForStudio);
        }

        _ = LoadSavedBusinessesAsync();
    }

    public async Task LoadSavedBusinessesAsync()
    {
        try
        {
            var list = await _businessRepository.GetAllAsync();
            SavedBusinesses = new ObservableCollection<Business>(list.OrderBy(b => b.Name));

            if (TargetBusinessForStudio != null)
            {
                var target = TargetBusinessForStudio;
                TargetBusinessForStudio = null;

                var match = SavedBusinesses.FirstOrDefault(b => b.Id == target.Id || b.Name.Equals(target.Name, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                {
                    SelectedSavedBusiness = match;
                }
                else
                {
                    SavedBusinesses.Insert(0, target);
                    SelectedSavedBusiness = target;
                }
                PopulateFromBusiness(SelectedSavedBusiness);
            }
        }
        catch { }
    }

    partial void OnSelectedSavedBusinessChanged(Business? value)
    {
        if (value == null) return;
        PopulateFromBusiness(value);
    }

    public void PopulateFromBusiness(Business b)
    {
        BusinessName = b.Name;
        Category = b.Category;
        City = !string.IsNullOrWhiteSpace(b.City) ? b.City : "Islamabad";
        Country = !string.IsNullOrWhiteSpace(b.Country) ? b.Country : "Pakistan";
        PhonePrimary = !string.IsNullOrWhiteSpace(b.PhonePrimary) ? b.PhonePrimary : "+92 300 1234567";
        EmailPrimary = !string.IsNullOrWhiteSpace(b.EmailPrimary) ? b.EmailPrimary : $"info@{b.Name.ToLowerInvariant().Replace(" ", "")}.com";

        // Auto-match archetype, style & audience based on category
        var cat = (b.Category ?? string.Empty).ToLowerInvariant();
        if (cat.Contains("health") || cat.Contains("dent") || cat.Contains("clinic") || cat.Contains("medic"))
        {
            SelectedArchetype = "Local Service Booking & Clinic Funnel";
            SelectedDesignStyle = "Trustworthy Emerald (Healthcare, Dental & Eco)";
            TargetAudience = $"Patients and families in {City} seeking trusted, painless clinical care.";
            CustomInstructions = $"Emphasize verified practitioner credentials, same-day appointments in {City}, patient comfort, and an urgent consultation CTA.";
        }
        else if (cat.Contains("law") || cat.Contains("legal") || cat.Contains("consult"))
        {
            SelectedArchetype = "Corporate Agency & Authority Portal";
            SelectedDesignStyle = "Midnight Luxury Gold (Clinics, Law & Executive)";
            TargetAudience = $"Corporate clients, executives, and individuals in {City} requiring high-calibre legal advisory.";
            CustomInstructions = $"Position as high-calibre corporate counsel in {City}. Emphasize track record, regulatory compliance, and confidential advisory.";
        }
        else if (cat.Contains("soft") || cat.Contains("it") || cat.Contains("tech") || cat.Contains("saas"))
        {
            SelectedArchetype = "Modern SaaS & Tech Product";
            SelectedDesignStyle = "Modern Slate & Cyan (Tech, SaaS & Corporate)";
            TargetAudience = $"CTOs, tech leaders, and enterprises seeking scalable digital infrastructure.";
            CustomInstructions = "Focus on cloud scalability, enterprise throughput, dedicated SLA, and interactive pilot booking.";
        }
        else if (cat.Contains("solar") || cat.Contains("energy"))
        {
            SelectedArchetype = "Lead Generation Funnel (High-Converting)";
            SelectedDesignStyle = "Trustworthy Emerald (Healthcare, Dental & Eco)";
            TargetAudience = $"Commercial property owners and residents looking to cut utility costs with green power.";
            CustomInstructions = "Highlight 3-year ROI payback, Tier-1 solar panels, net-metering approval, and free energy audit.";
        }
        else if (cat.Contains("real") || cat.Contains("prop"))
        {
            SelectedArchetype = "Luxury Brand & Portfolio Showcase";
            SelectedDesignStyle = "Midnight Luxury Gold (Clinics, Law & Executive)";
            TargetAudience = $"Investors, home buyers, and commercial property seekers in {City}.";
            CustomInstructions = $"Signature architectural aesthetics, prime {City} zoning, VIP walkthrough scheduling, and verified ownership records.";
        }
        else if (cat.Contains("restaurant") || cat.Contains("food") || cat.Contains("hospitality") || cat.Contains("hotel"))
        {
            SelectedArchetype = "Local Service Booking & Clinic Funnel";
            SelectedDesignStyle = "Midnight Luxury Gold (Clinics, Law & Executive)";
            TargetAudience = $"Food lovers, families, tourists, and event organizers in {City}.";
            CustomInstructions = $"Highlight signature cuisine, private VIP banquet reservations, hygienic open-kitchen prep, and instant table booking via WhatsApp.";
        }
        else if (cat.Contains("educat") || cat.Contains("school") || cat.Contains("attend") || cat.Contains("academ") || cat.Contains("train"))
        {
            SelectedArchetype = "Modern SaaS & Tech Product";
            SelectedDesignStyle = "Modern Slate & Cyan (Tech, SaaS & Corporate)";
            TargetAudience = $"School principals, administrators, and campus directors in {City}.";
            CustomInstructions = $"Highlight automated biometric attendance, instant SMS parent alerts, cloud school management, and a live pilot demonstration in {City}.";
        }
        else if (cat.Contains("department") || cat.Contains("retail") || cat.Contains("store") || cat.Contains("supermarket") || cat.Contains("mart") || cat.Contains("shop") || cat.Contains("mall") || cat.Contains("boutique") || cat.Contains("apparel"))
        {
            SelectedArchetype = "E-Commerce & Special Offer Showcase";
            SelectedDesignStyle = "Midnight Luxury Gold (Clinics, Law & Executive)";
            TargetAudience = $"Shoppers, families, and residents in {City} seeking premium lifestyle, apparel, and grocery retail.";
            CustomInstructions = $"Spotlight genuine branded collections, multi-floor air-conditioned shopping in {City}, weekly promotional bundles, and express same-day doorstep delivery via WhatsApp.";
        }
        else if (b.Name.ToLowerInvariant().Contains("soft") || b.Name.ToLowerInvariant().Contains("tech") || b.Name.ToLowerInvariant().Contains("nexa"))
        {
            SelectedArchetype = "Modern SaaS & Tech Product";
            SelectedDesignStyle = "Modern Slate & Cyan (Tech, SaaS & Corporate)";
            TargetAudience = $"Tech founders, enterprises, and institutions in {City} requiring custom digital infrastructure.";
            CustomInstructions = "Modern web applications, high-throughput cloud architecture, dedicated SLA, and agile project delivery.";
        }
        else
        {
            SelectedArchetype = "Lead Generation Funnel (High-Converting)";
            SelectedDesignStyle = "Modern Slate & Cyan (Tech, SaaS & Corporate)";
            TargetAudience = $"Clients, businesses, and discerning customers in {City} seeking reliable {b.Category} solutions.";
            CustomInstructions = $"Tailored commercial website for {b.Name} in {City} designed to turn search visitors into paying clients.";
        }

        StatusMessage = $"Pre-filled specifications from database lead '{b.Name}'.";
    }

    [RelayCommand]
    public void SelectIdeaPreset(string preset)
    {
        switch (preset)
        {
            case "DentalClinic":
                BusinessName = "Elite Dental & Implant Clinic";
                Category = "Healthcare & Dental";
                City = "Islamabad";
                Country = "Pakistan";
                PhonePrimary = "+92 300 8554433";
                EmailPrimary = "info@elitedental.pk";
                SelectedArchetype = "Local Service Booking & Clinic Funnel";
                SelectedDesignStyle = "Trustworthy Emerald (Healthcare, Dental & Eco)";
                CustomInstructions = "Cosmetic dentistry, painless implants, teeth whitening, UK-trained surgeons, 24/7 WhatsApp emergency booking, and 0% installment plans.";
                break;

            case "CorporateLaw":
                BusinessName = "Khan & Associates Legal Chambers";
                Category = "Lawyers & Legal";
                City = "Lahore";
                Country = "Pakistan";
                PhonePrimary = "+92 42 35789000";
                EmailPrimary = "counsel@khanlaw.com.pk";
                SelectedArchetype = "Corporate Agency & Authority Portal";
                SelectedDesignStyle = "Midnight Luxury Gold (Clinics, Law & Executive)";
                CustomInstructions = "Commercial litigation, corporate M&A, cross-border arbitration, intellectual property, and confidential client advisory.";
                break;

            case "SolarEnergy":
                BusinessName = "SunPower Green Energy Solutions";
                Category = "Solar & Renewable Energy";
                City = "Rawalpindi";
                Country = "Pakistan";
                PhonePrimary = "+92 311 5544332";
                EmailPrimary = "sales@sunpowergreen.com";
                SelectedArchetype = "Lead Generation Funnel (High-Converting)";
                SelectedDesignStyle = "Trustworthy Emerald (Healthcare, Dental & Eco)";
                CustomInstructions = "Residential & industrial solar installation, Tier-1 German inverters, guaranteed Net-Metering processing, and free onsite engineering survey.";
                break;

            case "SaaSAgency":
                BusinessName = "NexusCloud Enterprise Software";
                Category = "Software & IT Services";
                City = "Karachi";
                Country = "Pakistan";
                PhonePrimary = "+92 21 34567890";
                EmailPrimary = "enterprise@nexuscloud.io";
                SelectedArchetype = "Modern SaaS & Tech Product";
                SelectedDesignStyle = "Modern Slate & Cyan (Tech, SaaS & Corporate)";
                CustomInstructions = "AI workflow automation, multi-tenant ERP cloud infrastructure, SOC2 Type II compliance, and interactive live demo scheduler.";
                break;

            case "LuxuryRealEstate":
                BusinessName = "Margalla Heights Signature Living";
                Category = "Real Estate & Property";
                City = "Islamabad";
                Country = "Pakistan";
                PhonePrimary = "+92 333 9988776";
                EmailPrimary = "penthouse@margallaheights.pk";
                SelectedArchetype = "Luxury Brand & Portfolio Showcase";
                SelectedDesignStyle = "Midnight Luxury Gold (Clinics, Law & Executive)";
                CustomInstructions = "Ultra-luxury penthouses, smart home automation, infinity rooftop pool, panoramic Margalla views, and private VIP walkthrough scheduling.";
                break;
        }

        StatusMessage = $"Loaded preset: {BusinessName}";
    }

    [RelayCommand]
    public async Task GenerateLandingPage()
    {
        if (string.IsNullOrWhiteSpace(BusinessName))
        {
            StatusMessage = "⚠️ Business Name is required.";
            return;
        }

        try
        {
            IsGenerating = true;
            StatusMessage = $"🤖 Consulting Gemini AI & TheKhanSoft Architecture Engine for '{BusinessName}'...";

            var (archetype, style) = MapSelectionsToEnums();
            var activeModules = AvailableModules.Where(m => m.IsSelected).Select(m => m.Id).ToList();

            var req = new LandingPageGenerationRequest
            {
                BusinessName = BusinessName.Trim(),
                Category = Category.Trim(),
                City = City.Trim(),
                Country = Country.Trim(),
                PhonePrimary = PhonePrimary?.Trim(),
                EmailPrimary = EmailPrimary?.Trim(),
                TargetAudience = TargetAudience?.Trim() ?? "Discerning clients",
                Archetype = archetype,
                DesignStyle = style,
                CustomPromptInstructions = CustomInstructions?.Trim(),
                SelectedModules = activeModules
            };

            var result = await _generatorService.GenerateLandingPageAsync(req);
            CurrentResult = result;
            UpdateSynthesizedModulesFlow();
            UpdateModuleVisibilityProperties();

            StatusMessage = $"✅ Landing Page & Architecture generated in {result.ExecutionDuration.TotalSeconds:F1}s via {result.Provider} (Conversion Score: {result.ConversionScore}/100)";
            ActiveResultTab = "Preview";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Generation Error: {ex.Message}";
        }
        finally
        {
            IsGenerating = false;
        }
    }

    [RelayCommand]
    public void SetTab(string tab)
    {
        ActiveResultTab = tab;
    }

    [RelayCommand]
    public async Task OpenInBrowser()
    {
        if (CurrentResult == null || string.IsNullOrWhiteSpace(CurrentResult.GeneratedHtml))
        {
            StatusMessage = "⚠️ Please generate a landing page first.";
            return;
        }

        try
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "TKS_LeadPilot_Pages");
            if (!Directory.Exists(tempDir)) Directory.CreateDirectory(tempDir);

            var cleanName = string.Concat(BusinessName.Where(char.IsLetterOrDigit));
            if (string.IsNullOrWhiteSpace(cleanName)) cleanName = "LandingPage";

            // Clean up older preview files for this business to prevent temp directory bloat and cache pollution
            try
            {
                var oldFiles = Directory.GetFiles(tempDir, $"{cleanName}_LandingPage*.html");
                foreach (var old in oldFiles)
                {
                    try { File.Delete(old); } catch { }
                }
            }
            catch { }

            // Timestamped unique file name guarantees browser cache-busting
            var timestamp = TimeHelper.NowPkt.ToString("yyyyMMdd_HHmmss");
            var filePath = Path.Combine(tempDir, $"{cleanName}_LandingPage_{timestamp}_PKT.html");

            // Inject anti-cache meta headers into HTML so browser never serves stale static assets
            var html = CurrentResult.GeneratedHtml;
            if (html.Contains("<head>", StringComparison.OrdinalIgnoreCase))
            {
                var cacheBuster = "\n    <meta http-equiv=\"Cache-Control\" content=\"no-cache, no-store, must-revalidate\" />\n    <meta http-equiv=\"Pragma\" content=\"no-cache\" />\n    <meta http-equiv=\"Expires\" content=\"0\" />\n";
                var headIdx = html.IndexOf("<head>", StringComparison.OrdinalIgnoreCase);
                html = html.Insert(headIdx + 6, cacheBuster);
            }

            await File.WriteAllTextAsync(filePath, html);

            Process.Start(new ProcessStartInfo
            {
                FileName = filePath,
                UseShellExecute = true
            });

            StatusMessage = $"🌐 Fresh landing page preview opened in web browser ({Path.GetFileName(filePath)} at {TimeHelper.NowPkt:HH:mm:ss} PKT)...";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Browser Error: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task CopyHtml()
    {
        if (CurrentResult == null || string.IsNullOrWhiteSpace(CurrentResult.GeneratedHtml)) return;
        await CopyToClipboardAsync(CurrentResult.GeneratedHtml);
        StatusMessage = "📋 Full HTML & Tailwind CSS code copied to clipboard!";
    }

    [RelayCommand]
    public async Task CopyBlueprint()
    {
        if (CurrentResult == null || string.IsNullOrWhiteSpace(CurrentResult.ArchitectureBlueprintMarkdown)) return;
        await CopyToClipboardAsync(CurrentResult.ArchitectureBlueprintMarkdown);
        StatusMessage = "📋 Architecture Blueprint (Markdown) copied to clipboard!";
    }

    [RelayCommand]
    public async Task CopyDeveloperPrompt()
    {
        if (CurrentResult == null || string.IsNullOrWhiteSpace(CurrentResult.DeveloperPrompts)) return;
        await CopyToClipboardAsync(CurrentResult.DeveloperPrompts);
        StatusMessage = "📋 Frontend Developer Prompt copied to clipboard!";
    }

    [RelayCommand]
    public async Task SaveHtmlFile()
    {
        if (CurrentResult == null || string.IsNullOrWhiteSpace(CurrentResult.GeneratedHtml)) return;

        try
        {
            var downloadsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            if (!Directory.Exists(downloadsDir)) downloadsDir = AppContext.BaseDirectory;

            var cleanName = string.Concat(BusinessName.Where(char.IsLetterOrDigit));
            var filename = $"{cleanName}_LandingPage_{TimeHelper.NowPkt:yyyyMMdd_HHmmss}_PKT.html";
            var filePath = Path.Combine(downloadsDir, filename);

            await File.WriteAllTextAsync(filePath, CurrentResult.GeneratedHtml);
            StatusMessage = $"💾 Saved production HTML to {filePath}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Save Error: {ex.Message}";
        }
    }


    public void UpdateSynthesizedModulesFlow()
    {
        var list = new List<SynthesizedModuleItem>();
        int idx = 1;
        var active = AvailableModules.Where(m => m.IsSelected).Select(m => m.Id).ToHashSet();
        if (active.Count == 0)
        {
            active = ["mod-hero", "mod-trust-bar", "mod-services", "mod-before-after", "mod-team", "mod-testimonials", "mod-pricing", "mod-calculator", "mod-faq", "mod-lead-form", "mod-whatsapp", "mod-seo"];
        }

        var payload = CurrentResult?.ContentPayload;

        // Update live preview collections & fields for in-app visual studio
        if (payload != null)
        {
            if (payload.TrustBadges.Count > 0)
                PreviewTrustBadges = new ObservableCollection<string>(payload.TrustBadges);
            if (payload.Services.Count > 0)
                PreviewServicesList = new ObservableCollection<string>(payload.Services.Select(s => s.Title));
            if (payload.CaseStudies.Count > 0)
            {
                PreviewCaseStudyTitle = payload.CaseStudies[0].ClientName;
                PreviewCaseStudyResult = payload.CaseStudies[0].ResultMetric;
                PreviewCaseStudyDetail = payload.CaseStudies[0].ResultDescription;
            }
            if (payload.LeaderBio != null)
            {
                PreviewLeaderName = payload.LeaderBio.Name;
                PreviewLeaderRole = payload.LeaderBio.Role;
                PreviewLeaderQuote = payload.LeaderBio.Quote;
            }
            if (payload.Testimonials.Count > 0)
            {
                PreviewTestimonialQuote = payload.Testimonials[0].Quote;
                PreviewTestimonialAuthor = payload.Testimonials[0].Author;
                PreviewTestimonialRole = payload.Testimonials[0].Role;
            }
            if (payload.PricingPlans.Count > 0)
            {
                PreviewPricingTier1 = $"{payload.PricingPlans[0].Name} ({payload.PricingPlans[0].Price})";
                if (payload.PricingPlans.Count > 1)
                {
                    PreviewPricingTier2 = $"{payload.PricingPlans[1].Name} ({payload.PricingPlans[1].Price})";
                }
            }
            if (payload.Faqs.Count > 0)
            {
                PreviewFaqQuestion = payload.Faqs[0].Question;
                PreviewFaqAnswer = payload.Faqs[0].Answer;
            }
            if (!string.IsNullOrWhiteSpace(payload.LeadFormTitle))
            {
                PreviewLeadFormTitle = payload.LeadFormTitle;
            }
            if (!string.IsNullOrWhiteSpace(payload.LeadFormSubtitle))
            {
                PreviewLeadFormSubtitle = payload.LeadFormSubtitle;
            }
        }
        else
        {
            PreviewTrustBadges = new ObservableCollection<string>(["Verified Local Enterprise", "4.9/5 Client Score", "ISO 9001 Standard", "Instant Response Guarantee"]);
            PreviewServicesList = new ObservableCollection<string>(["Bespoke Architecture", "Strategic Optimization", "VIP Retainer"]);
        }

        var heroDesc = CurrentResult?.Headline != null
            ? $"{CurrentResult.Headline} — {CurrentResult.Subheadline}"
            : "High-converting hero value proposition tailored to target market";

        var trustDesc = payload != null && payload.TrustBadges.Count > 0
            ? string.Join(" • ", payload.TrustBadges)
            : "Google Guaranteed, ISO 9001, Chamber of Commerce & Verified rating emblems";

        var servicesDesc = payload != null && payload.Services.Count > 0
            ? string.Join(" • ", payload.Services.Select(s => s.Title))
            : "Interactive cards detailing key commercial offerings, deliverables & CTAs";

        var caseDesc = payload != null && payload.CaseStudies.Count > 0
            ? $"{payload.CaseStudies[0].ClientName}: {payload.CaseStudies[0].ResultMetric} ({payload.CaseStudies[0].Solution})"
            : "Measurable client before & after benchmarks with quantified metrics";

        var teamDesc = payload?.LeaderBio != null
            ? $"{payload.LeaderBio.Name} ({payload.LeaderBio.Role}) — {payload.LeaderBio.Credentials}"
            : "Executive credentials, founder story, certifications & personal quote";

        var testDesc = payload != null && payload.Testimonials.Count > 0
            ? $"\"{payload.Testimonials[0].Quote}\" — {payload.Testimonials[0].Author} ({payload.Testimonials[0].Role})"
            : "4.9★ composite review score with verified regional customer feedback";

        var pricingDesc = payload != null && payload.PricingPlans.Count > 0
            ? string.Join(" • ", payload.PricingPlans.Select(p => $"{p.Name} ({p.Price})"))
            : "Multi-tier commercial package matrix with feature checklists & badges";

        var calcDesc = payload?.Calculator != null
            ? $"{payload.Calculator.Title}: {payload.Calculator.SliderLabel} -> {payload.Calculator.ResultLabel}"
            : "Client-side interactive budget or savings slider computing real-time value";

        var faqDesc = payload != null && payload.Faqs.Count > 0
            ? $"{payload.Faqs[0].Question} — {payload.Faqs[0].Answer}"
            : "Expandable objection-handling resolving pricing, timeline & scope doubts";

        var leadFormDesc = payload != null && !string.IsNullOrWhiteSpace(payload.LeadFormTitle)
            ? $"{payload.LeadFormTitle} • Priority hotline: {PhonePrimary ?? "+92 300 1234567"}"
            : "Low-friction lead capture form with instant validation & reassurance";

        var whatsappDesc = $"Sticky WhatsApp float & instant hotline direct to {BusinessName} ({PhonePrimary ?? "+92 300 1234567"})";
        var seoDesc = $"Schema.org LocalBusiness JSON-LD, OpenGraph & meta tags for {BusinessName} in {City}";

        var moduleDict = new Dictionary<string, (string Title, string Desc, string Icon)>
        {
            ["mod-hero"] = ("Hero & Value Proposition", heroDesc, "🎯"),
            ["mod-trust-bar"] = ("Trust & Certification Bar", trustDesc, "🛡️"),
            ["mod-services"] = ("Core Solutions & Services Grid", servicesDesc, "💼"),
            ["mod-before-after"] = ("Transformation & Case Studies", caseDesc, "📈"),
            ["mod-team"] = ("Leadership & Authority Bio", teamDesc, "👔"),
            ["mod-testimonials"] = ("Testimonials & Social Proof", testDesc, "⭐"),
            ["mod-pricing"] = ("Transparent Pricing & Packages", pricingDesc, "🏷️"),
            ["mod-calculator"] = ("Interactive ROI / Cost Estimator", calcDesc, "🧮"),
            ["mod-faq"] = ("Interactive FAQ Accordion", faqDesc, "❓"),
            ["mod-lead-form"] = ("Lead Capture Booking Form", leadFormDesc, "📝"),
            ["mod-whatsapp"] = ("Floating WhatsApp & Direct Call", whatsappDesc, "💬"),
            ["mod-seo"] = ("LocalBusiness Schema & Meta Tags", seoDesc, "🔍")
        };

        foreach (var (modId, (title, desc, icon)) in moduleDict)
        {
            if (active.Contains(modId))
            {
                list.Add(new SynthesizedModuleItem
                {
                    Number = $"{idx:D2}",
                    Title = $"{idx:D2}. {title}",
                    Description = desc,
                    Icon = icon
                });
                idx++;
            }
        }

        SynthesizedModulesFlow = new ObservableCollection<SynthesizedModuleItem>(list);
    }

    private void SubscribeToModuleChanges()
    {
        foreach (var mod in AvailableModules)
        {
            mod.PropertyChanged -= OnModuleOptionPropertyChanged;
            mod.PropertyChanged += OnModuleOptionPropertyChanged;
        }
    }

    private void OnModuleOptionPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LandingPageModuleOption.IsSelected))
        {
            OnModuleSelectionToggled();
        }
    }

    private void OnModuleSelectionToggled()
    {
        UpdateSynthesizedModulesFlow();
        UpdateModuleVisibilityProperties();
        RebuildHtmlForActiveModules();
    }

    private void ApplyArchetypeRecommendedModules(string archetypeText)
    {
        HashSet<string> recommended = archetypeText switch
        {
            var s when s.Contains("Clinic") || s.Contains("Booking") =>
                ["mod-hero", "mod-trust-bar", "mod-services", "mod-team", "mod-testimonials", "mod-faq", "mod-lead-form", "mod-whatsapp", "mod-seo"],
            var s when s.Contains("Corporate") || s.Contains("Authority") =>
                ["mod-hero", "mod-trust-bar", "mod-services", "mod-before-after", "mod-team", "mod-testimonials", "mod-faq", "mod-lead-form", "mod-seo"],
            var s when s.Contains("SaaS") || s.Contains("Tech") =>
                ["mod-hero", "mod-trust-bar", "mod-services", "mod-before-after", "mod-pricing", "mod-calculator", "mod-faq", "mod-lead-form", "mod-seo"],
            var s when s.Contains("E-Commerce") || s.Contains("Retail") || s.Contains("Special Offer") =>
                ["mod-hero", "mod-trust-bar", "mod-services", "mod-testimonials", "mod-pricing", "mod-faq", "mod-lead-form", "mod-whatsapp", "mod-seo"],
            var s when s.Contains("Luxury") || s.Contains("Brand") =>
                ["mod-hero", "mod-trust-bar", "mod-services", "mod-before-after", "mod-team", "mod-testimonials", "mod-lead-form", "mod-whatsapp", "mod-seo"],
            _ => // Lead Generation Funnel
                ["mod-hero", "mod-trust-bar", "mod-services", "mod-testimonials", "mod-calculator", "mod-faq", "mod-lead-form", "mod-whatsapp", "mod-seo"]
        };

        foreach (var mod in AvailableModules)
        {
            mod.IsSelected = recommended.Contains(mod.Id);
        }

        UpdateSynthesizedModulesFlow();
        UpdateModuleVisibilityProperties();
        RebuildHtmlForActiveModules();
    }

    private void UpdateThemeBrushes()
    {
        var style = SelectedDesignStyle switch
        {
            var s when s.Contains("Luxury") || s.Contains("Gold") => LandingPageDesignStyle.MidnightLuxuryGold,
            var s when s.Contains("Emerald") || s.Contains("Healthcare") => LandingPageDesignStyle.TrustworthyEmerald,
            var s when s.Contains("Indigo") || s.Contains("Vibrant") => LandingPageDesignStyle.BoldVibrantIndigo,
            var s when s.Contains("Monochrome") || s.Contains("Minimalist") => LandingPageDesignStyle.MinimalistMonochrome,
            _ => LandingPageDesignStyle.ModernSlateCyan
        };

        switch (style)
        {
            case LandingPageDesignStyle.MidnightLuxuryGold:
                PreviewPrimaryColor = "#D97706";
                PreviewAccentColor = "#F59E0B";
                PreviewBgColor = "#0C0F17";
                PreviewCardBgColor = "#141B29";
                PreviewBadgeBg = "#2E1B05";
                PreviewBadgeBorder = "#D97706";
                PreviewBadgeText = "#FBBF24";
                PreviewButtonGradient = "#D97706";
                break;
            case LandingPageDesignStyle.TrustworthyEmerald:
                PreviewPrimaryColor = "#059669";
                PreviewAccentColor = "#10B981";
                PreviewBgColor = "#051610";
                PreviewCardBgColor = "#0B261D";
                PreviewBadgeBg = "#064E3B";
                PreviewBadgeBorder = "#059669";
                PreviewBadgeText = "#34D399";
                PreviewButtonGradient = "#059669";
                break;
            case LandingPageDesignStyle.BoldVibrantIndigo:
                PreviewPrimaryColor = "#4F46E5";
                PreviewAccentColor = "#6366F1";
                PreviewBgColor = "#0B091B";
                PreviewCardBgColor = "#141133";
                PreviewBadgeBg = "#1E1B4B";
                PreviewBadgeBorder = "#4338CA";
                PreviewBadgeText = "#A5B4FC";
                PreviewButtonGradient = "#4F46E5";
                break;
            case LandingPageDesignStyle.MinimalistMonochrome:
                PreviewPrimaryColor = "#E4E4E7";
                PreviewAccentColor = "#FAFAFA";
                PreviewBgColor = "#09090B";
                PreviewCardBgColor = "#18181B";
                PreviewBadgeBg = "#27272A";
                PreviewBadgeBorder = "#3F3F46";
                PreviewBadgeText = "#F4F4F5";
                PreviewButtonGradient = "#27272A";
                break;
            default: // ModernSlateCyan
                PreviewPrimaryColor = "#0284C7";
                PreviewAccentColor = "#06B6D4";
                PreviewBgColor = "#070D18";
                PreviewCardBgColor = "#0D1626";
                PreviewBadgeBg = "#0C2D48";
                PreviewBadgeBorder = "#0284C7";
                PreviewBadgeText = "#38BDF8";
                PreviewButtonGradient = "#0284C7";
                break;
        }
    }

    private (LandingPageArchetype Archetype, LandingPageDesignStyle Style) MapSelectionsToEnums()
    {
        var archetype = SelectedArchetype switch
        {
            var s when s.Contains("Clinic") || s.Contains("Service Booking") => LandingPageArchetype.LocalServiceBooking,
            var s when s.Contains("Corporate") || s.Contains("Agency") => LandingPageArchetype.CorporateAgencyPortal,
            var s when s.Contains("Luxury") || s.Contains("Showcase") || s.Contains("Brand") => LandingPageArchetype.LuxuryBrandShowcase,
            var s when s.Contains("SaaS") || s.Contains("Tech") => LandingPageArchetype.ModernSaaSTech,
            var s when s.Contains("E-Commerce") || s.Contains("Retail") || s.Contains("Special Offer") => LandingPageArchetype.EcommerceShowcase,
            _ => LandingPageArchetype.LeadGenerationFunnel
        };

        var style = SelectedDesignStyle switch
        {
            var s when s.Contains("Midnight") || s.Contains("Gold") || s.Contains("Luxury") => LandingPageDesignStyle.MidnightLuxuryGold,
            var s when s.Contains("Emerald") || s.Contains("Healthcare") => LandingPageDesignStyle.TrustworthyEmerald,
            var s when s.Contains("Indigo") || s.Contains("Creative") || s.Contains("Vibrant") => LandingPageDesignStyle.BoldVibrantIndigo,
            var s when s.Contains("Monochrome") || s.Contains("Minimalist") => LandingPageDesignStyle.MinimalistMonochrome,
            _ => LandingPageDesignStyle.ModernSlateCyan
        };

        return (archetype, style);
    }

    public void RebuildHtmlForActiveModules()
    {
        if (CurrentResult == null || CurrentResult.ContentPayload == null) return;
        try
        {
            var activeModIds = AvailableModules.Where(m => m.IsSelected).Select(m => m.Id).ToList();
            var (archetype, style) = MapSelectionsToEnums();
            var req = new LandingPageGenerationRequest
            {
                BusinessName = !string.IsNullOrWhiteSpace(BusinessName) ? BusinessName.Trim() : "Enterprise Client",
                Category = !string.IsNullOrWhiteSpace(Category) ? Category.Trim() : "Commercial Enterprise",
                City = !string.IsNullOrWhiteSpace(City) ? City.Trim() : "Metropolitan Area",
                Country = !string.IsNullOrWhiteSpace(Country) ? Country.Trim() : "Pakistan",
                PhonePrimary = PhonePrimary?.Trim(),
                EmailPrimary = EmailPrimary?.Trim(),
                TargetAudience = TargetAudience?.Trim() ?? "Discerning clients",
                Archetype = archetype,
                DesignStyle = style,
                SelectedModules = activeModIds
            };
            var newHtml = _generatorService.RenderHtml(CurrentResult.ContentPayload, req);
            CurrentResult.GeneratedHtml = newHtml;
            OnPropertyChanged(nameof(CurrentResult));
        }
        catch { }
    }

    private static async Task CopyToClipboardAsync(string text)
    {
        try
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow?.Clipboard != null)
            {
                await desktop.MainWindow.Clipboard.SetTextAsync(text);
            }
        }
        catch { }
    }
}

public class SynthesizedModuleItem
{
    public string Number { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Icon { get; set; } = "⚡";
}
