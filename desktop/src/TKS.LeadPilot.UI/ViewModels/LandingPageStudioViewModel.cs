using System;
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

    [ObservableProperty]
    private string _customInstructions = "Highlight painless dental implants, Hollywood veneers, 24/7 emergency appointments, UK-trained doctors, transparent pricing, and instant WhatsApp consultation booking.";

    [ObservableProperty]
    private ObservableCollection<LandingPageModuleOption> _availableModules = [];

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

            var archetype = SelectedArchetype switch
            {
                var s when s.Contains("Clinic") || s.Contains("Service Booking") => LandingPageArchetype.LocalServiceBooking,
                var s when s.Contains("Corporate") || s.Contains("Agency") => LandingPageArchetype.CorporateAgencyPortal,
                var s when s.Contains("Luxury") || s.Contains("Showcase") => LandingPageArchetype.LuxuryBrandShowcase,
                var s when s.Contains("SaaS") || s.Contains("Tech") => LandingPageArchetype.ModernSaaSTech,
                var s when s.Contains("E-Commerce") => LandingPageArchetype.EcommerceShowcase,
                _ => LandingPageArchetype.LeadGenerationFunnel
            };

            var style = SelectedDesignStyle switch
            {
                var s when s.Contains("Midnight") || s.Contains("Gold") => LandingPageDesignStyle.MidnightLuxuryGold,
                var s when s.Contains("Emerald") || s.Contains("Healthcare") => LandingPageDesignStyle.TrustworthyEmerald,
                var s when s.Contains("Indigo") || s.Contains("Creative") => LandingPageDesignStyle.BoldVibrantIndigo,
                var s when s.Contains("Monochrome") => LandingPageDesignStyle.MinimalistMonochrome,
                _ => LandingPageDesignStyle.ModernSlateCyan
            };

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
            var filePath = Path.Combine(tempDir, $"{cleanName}_LandingPage.html");

            await File.WriteAllTextAsync(filePath, CurrentResult.GeneratedHtml);

            Process.Start(new ProcessStartInfo
            {
                FileName = filePath,
                UseShellExecute = true
            });

            StatusMessage = $"🌐 Launching landing page preview in web browser ({filePath})...";
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
            var filename = $"{cleanName}_LandingPage_{DateTime.Now:yyyyMMdd_HHmmss}.html";
            var filePath = Path.Combine(downloadsDir, filename);

            await File.WriteAllTextAsync(filePath, CurrentResult.GeneratedHtml);
            StatusMessage = $"💾 Saved production HTML to {filePath}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Save Error: {ex.Message}";
        }
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
