using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Input.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using TKS.LeadPilot.Core.Interfaces;
using TKS.LeadPilot.Core.Models;
using TKS.LeadPilot.UI.Views;

namespace TKS.LeadPilot.UI.ViewModels;

public partial class DiscoveryViewModel : ViewModelBase
{
    private readonly IDiscoveryService _discoveryService;
    private readonly IGeoLocationService _geoLocationService;
    private readonly IBusinessRepository _businessRepository;
    private readonly IDeduplicationService _dedupService;
    private readonly ISecretVault _secretVault;
    private readonly IServiceProvider? _serviceProvider;

    private bool _isSuppressingLocationCascades = false;
    private CancellationTokenSource? _scanCts;

    // =========================================================================
    // 1. CASCADING GEOGRAPHIC HIERARCHY
    // =========================================================================

    [ObservableProperty]
    private ObservableCollection<string> _countries = [];

    [ObservableProperty]
    private string _selectedCountry = "All Countries";

    [ObservableProperty]
    private ObservableCollection<string> _states = [];

    [ObservableProperty]
    private string _selectedState = "All States/Provinces";

    [ObservableProperty]
    private ObservableCollection<string> _cities = [];

    [ObservableProperty]
    private string _selectedCity = "All Cities";

    [ObservableProperty]
    private ObservableCollection<string> _suggestedNiches = [];

    // =========================================================================
    // 2. PROXIMITY GEOLOCATION & RADIUS
    // =========================================================================

    [ObservableProperty]
    private bool _isNearbyActive = false;

    [ObservableProperty]
    private double _nearbyRadiusKm = 15.0;

    public double[] AvailableRadii { get; } = [5.0, 10.0, 15.0, 25.0, 50.0, 100.0];

    // =========================================================================
    // 3. MULTI-SOURCE ENGINES & CATEGORIES
    // =========================================================================

    public ObservableCollection<string> AvailableEngines { get; } =
    [
        "🌐 Unified Multi-Engine Aggregator (All Active)",
        "⚡ Photon Geosearch (Komoot OpenPOI - 100% Free)",
        "🦆 DuckDuckGo Commercial Web Directory (100% Free)",
        "🛰️ OpenStreetMap Live Overpass (Geospatial QL)",
        "🗺️ OSM Nominatim (Natural POI Search)",
        "✨ Google Gemini AI Discovery (Free 1,500 RPD)",
        "⚡ Groq Cloud AI Engine (Free Fast Llama-3)",
        "📍 Google Places API (Direct Commercial)",
        "🔎 Serper.dev Google Places (Free 2,500 Queries)",
        "💾 High-Density Offline Enterprise Vault"
    ];

    [ObservableProperty]
    private string _selectedEngine = "🌐 Unified Multi-Engine Aggregator (All Active)";

    [ObservableProperty]
    private ObservableCollection<string> _categories = [];

    [ObservableProperty]
    private string _selectedCategory = "⭐ All Commercial Businesses & Enterprises";

    [ObservableProperty]
    private string _customSearchKeyword = string.Empty;

    // Filter toggles
    [ObservableProperty]
    private bool _targetMissingWebsite = false;

    [ObservableProperty]
    private bool _targetMissingGmb = false;

    [ObservableProperty]
    private bool _requirePhone = false;

    // =========================================================================
    // 4. SCAN TELEMETRY & RESULTS
    // =========================================================================

    [ObservableProperty]
    private bool _isScanning = false;

    [ObservableProperty]
    private bool _isImporting = false;

    [ObservableProperty]
    private string _statusMessage = "Ready to discover enterprise leads across global markets and local proximities.";

    [ObservableProperty]
    private string _executionDurationText = string.Empty;

    [ObservableProperty]
    private int _totalFoundCount = 0;

    [ObservableProperty]
    private int _newLeadsCount = 0;

    [ObservableProperty]
    private int _duplicateLeadsCount = 0;

    [ObservableProperty]
    private int _selectedForImportCount = 0;

    [ObservableProperty]
    private bool _isImportSuccessAlertOpen = false;

    [ObservableProperty]
    private string _importAlertTitle = string.Empty;

    [ObservableProperty]
    private string _importAlertMessage = string.Empty;

    [ObservableProperty]
    private int _lastImportedCount = 0;

    [ObservableProperty]
    private int _lastSkippedCount = 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDiscoveredLeads))]
    [NotifyPropertyChangedFor(nameof(HasNoDiscoveredLeads))]
    private ObservableCollection<DiscoveredLead> _discoveredLeads = [];

    public bool HasDiscoveredLeads => DiscoveredLeads != null && DiscoveredLeads.Count > 0;
    public bool HasNoDiscoveredLeads => !HasDiscoveredLeads;

    [ObservableProperty]
    private DiscoveredLead? _selectedLead;

    public DiscoveryViewModel(
        IDiscoveryService discoveryService,
        IGeoLocationService geoLocationService,
        IBusinessRepository businessRepository,
        IDeduplicationService dedupService,
        ISecretVault secretVault,
        IServiceProvider? serviceProvider = null)
    {
        _discoveryService = discoveryService;
        _geoLocationService = geoLocationService;
        _businessRepository = businessRepository;
        _dedupService = dedupService;
        _secretVault = secretVault;
        _serviceProvider = serviceProvider;

        InitializeCatalog();
    }

    private void InitializeCatalog()
    {
        var countryNames = _geoLocationService.GetAllCountries().Select(c => c.Name).OrderBy(c => c).ToList();
        Countries = new ObservableCollection<string>(["All Countries", .. countryNames]);
        SelectedCountry = "All Countries";

        UpdateStates();
        UpdateCategories();
        UpdateSuggestedNiches();
    }

    partial void OnSelectedCountryChanged(string value)
    {
        if (_isSuppressingLocationCascades) return;
        UpdateStates();
        UpdateSuggestedNiches();
    }

    partial void OnSelectedStateChanged(string value)
    {
        if (_isSuppressingLocationCascades) return;
        UpdateCities();
        UpdateSuggestedNiches();
    }

    partial void OnSelectedCityChanged(string value)
    {
        if (_isSuppressingLocationCascades) return;
        UpdateSuggestedNiches();
    }

    private void UpdateStates()
    {
        if (SelectedCountry == "All Countries" || string.IsNullOrWhiteSpace(SelectedCountry))
        {
            States = new ObservableCollection<string>(["All States/Provinces"]);
            SelectedState = "All States/Provinces";
            UpdateCities();
            return;
        }

        var country = _geoLocationService.GetCountry(SelectedCountry);
        if (country == null || country.States.Count == 0)
        {
            States = new ObservableCollection<string>(["All States/Provinces"]);
            SelectedState = "All States/Provinces";
            UpdateCities();
            return;
        }

        var stateNames = country.States.Select(s => s.Name).OrderBy(s => s).ToList();
        States = new ObservableCollection<string>(["All States/Provinces", .. stateNames]);
        SelectedState = "All States/Provinces";
        UpdateCities();
    }

    private void UpdateCities()
    {
        if (SelectedCountry == "All Countries" || string.IsNullOrWhiteSpace(SelectedCountry))
        {
            Cities = new ObservableCollection<string>(["All Cities"]);
            SelectedCity = "All Cities";
            return;
        }

        var country = _geoLocationService.GetCountry(SelectedCountry);
        if (country == null)
        {
            Cities = new ObservableCollection<string>(["All Cities"]);
            SelectedCity = "All Cities";
            return;
        }

        IEnumerable<CityInfo> citiesPool;
        if (SelectedState == "All States/Provinces" || string.IsNullOrWhiteSpace(SelectedState))
        {
            citiesPool = country.States.SelectMany(s => s.Cities);
        }
        else
        {
            var state = country.States.FirstOrDefault(s => s.Name.Equals(SelectedState, StringComparison.OrdinalIgnoreCase));
            citiesPool = state?.Cities ?? [];
        }

        var cityNames = citiesPool.Select(c => c.Name).Distinct().OrderBy(c => c).ToList();
        Cities = new ObservableCollection<string>(["All Cities", .. cityNames]);
        SelectedCity = cityNames.Count > 0 ? cityNames[0] : "All Cities";
    }

    private void UpdateCategories()
    {
        var presets = _discoveryService.GetPresetCategories().Keys.Distinct().ToList();
        Categories = new ObservableCollection<string>(presets);
        SelectedCategory = presets.FirstOrDefault() ?? "⭐ All Commercial Businesses & Enterprises";
    }

    private void UpdateSuggestedNiches()
    {
        var targetCountry = SelectedCountry != "All Countries" ? SelectedCountry : null;
        var targetCity = SelectedCity != "All Cities" ? SelectedCity : null;
        var niches = _geoLocationService.GetSuggestedNiches(targetCountry, targetCity);
        SuggestedNiches = new ObservableCollection<string>(niches);
    }

    // =========================================================================
    // 5. COMMANDS
    // =========================================================================

    [RelayCommand]
    public async Task DetectNearbyLocationAsync()
    {
        if (IsScanning) return;

        StatusMessage = "📍 Contacting high-precision geolocation satellite & network probes...";
        try
        {
            var result = await _geoLocationService.DetectNearbyLocationAsync(NearbyRadiusKm);
            if (!string.IsNullOrWhiteSpace(result.Country))
            {
                _isSuppressingLocationCascades = true;

                if (Countries.Contains(result.Country))
                {
                    SelectedCountry = result.Country;
                }

                // Populate states for country
                var country = _geoLocationService.GetCountry(SelectedCountry);
                if (country != null)
                {
                    var stateNames = country.States.Select(s => s.Name).OrderBy(s => s).ToList();
                    States = new ObservableCollection<string>(["All States/Provinces", .. stateNames]);

                    if (!string.IsNullOrWhiteSpace(result.StateProvince) && States.Contains(result.StateProvince))
                    {
                        SelectedState = result.StateProvince;
                    }
                    else
                    {
                        SelectedState = "All States/Provinces";
                    }

                    // Populate cities
                    IEnumerable<CityInfo> citiesPool = SelectedState == "All States/Provinces"
                        ? country.States.SelectMany(s => s.Cities)
                        : (country.States.FirstOrDefault(s => s.Name == SelectedState)?.Cities ?? []);

                    var cityNames = citiesPool.Select(c => c.Name).Distinct().OrderBy(c => c).ToList();
                    Cities = new ObservableCollection<string>(["All Cities", .. cityNames]);

                    if (!string.IsNullOrWhiteSpace(result.City) && Cities.Contains(result.City))
                    {
                        SelectedCity = result.City;
                    }
                    else if (cityNames.Count > 0)
                    {
                        SelectedCity = cityNames[0];
                    }
                }

                _isSuppressingLocationCascades = false;
                IsNearbyActive = true;
                UpdateSuggestedNiches();
                StatusMessage = result.Message;
            }
            else
            {
                StatusMessage = "Could not resolve live network location. Please select country and city manually.";
            }
        }
        catch (Exception ex)
        {
            _isSuppressingLocationCascades = false;
            StatusMessage = $"Proximity detection notice: {ex.Message}";
        }
    }

    [RelayCommand]
    public void ClearNearby()
    {
        IsNearbyActive = false;
        StatusMessage = "Proximity filter cleared.";
    }

    [RelayCommand]
    public void SelectSuggestedNiche(string niche)
    {
        if (string.IsNullOrWhiteSpace(niche)) return;
        CustomSearchKeyword = niche;
        StatusMessage = $"Refined discovery query to regional niche: \"{niche}\"";
    }

    [RelayCommand]
    public async Task StartScanAsync()
    {
        if (IsScanning) return;

        var targetCity = SelectedCity != "All Cities" ? SelectedCity : string.Empty;
        var targetCountry = SelectedCountry != "All Countries" ? SelectedCountry : string.Empty;

        if (string.IsNullOrWhiteSpace(targetCity) && string.IsNullOrWhiteSpace(CustomSearchKeyword))
        {
            StatusMessage = "⚠️ Please select a Target City or enter a Custom Search Keyword to initiate scan.";
            return;
        }

        IsScanning = true;
        StatusMessage = $"Scanning {SelectedEngine} for {SelectedCategory} in {targetCity}, {targetCountry}...";
        ExecutionDurationText = string.Empty;

        _scanCts = new CancellationTokenSource();

        try
        {
            var sw = Stopwatch.StartNew();

            var query = new DiscoveryQuery
            {
                City = targetCity,
                Country = targetCountry,
                StateProvince = SelectedState != "All States/Provinces" ? SelectedState : string.Empty,
                CategoryPreset = SelectedCategory,
                CustomSearchTerm = CustomSearchKeyword,
                SearchEngine = SelectedEngine,
                TargetMissingWebsiteOnly = TargetMissingWebsite,
                TargetMissingGmbOnly = TargetMissingGmb,
                RequirePhone = RequirePhone,
                Limit = 60,
                TimeoutSeconds = 30
            };

            // Calculate bounding box if city is specified
            if (!string.IsNullOrWhiteSpace(targetCity))
            {
                var cityInfo = _geoLocationService.FindCity(targetCity, targetCountry);
                if (cityInfo != null)
                {
                    query.BBox = _geoLocationService.CalculateProximityBoundingBox(cityInfo.Latitude, cityInfo.Longitude, NearbyRadiusKm);
                }
            }

            var result = await Task.Run(() => _discoveryService.DiscoverLeadsAsync(query, _scanCts.Token), _scanCts.Token);

            sw.Stop();
            ExecutionDurationText = $"{sw.Elapsed.TotalSeconds:F1}s";

            if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
            {
                StatusMessage = result.ErrorMessage;
                DiscoveredLeads.Clear();
                TotalFoundCount = 0;
                NewLeadsCount = 0;
                DuplicateLeadsCount = 0;
                SelectedForImportCount = 0;
            }
            else
            {
                DiscoveredLeads = new ObservableCollection<DiscoveredLead>(result.Leads);
                TotalFoundCount = result.Leads.Count;
                NewLeadsCount = result.NewCount;
                DuplicateLeadsCount = result.DuplicateCount;
                SelectedForImportCount = result.Leads.Count(l => l.IsSelectedForImport);

                StatusMessage = $"Scan complete in {ExecutionDurationText} via {result.Provider}: {TotalFoundCount} discovered ({NewLeadsCount} new, {DuplicateLeadsCount} existing in database).";
            }
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Scan cancelled by operator.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Scan encountered an error: {ex.Message}";
        }
        finally
        {
            IsScanning = false;
            _scanCts?.Dispose();
            _scanCts = null;
        }
    }

    [RelayCommand]
    public void CancelScan()
    {
        _scanCts?.Cancel();
    }

    [RelayCommand]
    public void SelectAll()
    {
        foreach (var lead in DiscoveredLeads)
        {
            lead.IsSelectedForImport = true;
        }
        UpdateSelectionCount();
    }

    [RelayCommand]
    public void UnselectAll()
    {
        foreach (var lead in DiscoveredLeads)
        {
            lead.IsSelectedForImport = false;
        }
        UpdateSelectionCount();
    }

    [RelayCommand]
    public void SelectOnlyNew()
    {
        foreach (var lead in DiscoveredLeads)
        {
            lead.IsSelectedForImport = !lead.IsDuplicate;
        }
        UpdateSelectionCount();
    }

    [RelayCommand]
    public void ToggleLeadSelection(DiscoveredLead lead)
    {
        if (lead == null) return;
        lead.IsSelectedForImport = !lead.IsSelectedForImport;
        UpdateSelectionCount();
    }

    public void UpdateSelectionCount()
    {
        SelectedForImportCount = DiscoveredLeads.Count(l => l.IsSelectedForImport);
    }

    [RelayCommand]
    public async Task ImportSelectedLeadsAsync()
    {
        if (IsImporting || IsScanning) return;

        var selected = DiscoveredLeads.Where(l => l.IsSelectedForImport).ToList();
        if (selected.Count == 0)
        {
            StatusMessage = "⚠️ No leads selected for import. Check at least one lead from the table.";
            return;
        }

        IsImporting = true;
        StatusMessage = $"Importing {selected.Count} leads into local SQLite database...";

        try
        {
            var res = await _discoveryService.ImportDiscoveredLeadsAsync(selected, DeduplicationStrategy.Skip);
            StatusMessage = $"✅ Successfully imported {res.CreatedCount} new leads ({res.SkippedCount} skipped as duplicates) into SQLite database!";

            // Refresh duplicate flags
            var existing = await _businessRepository.GetAllAsync();
            foreach (var lead in DiscoveredLeads)
            {
                var dup = _dedupService.CheckDuplicate(lead.Business, existing);
                lead.DuplicateMatch = dup.IsDuplicate ? dup : null;
                if (dup.IsDuplicate)
                {
                    lead.IsSelectedForImport = false;
                }
            }

            NewLeadsCount = DiscoveredLeads.Count(l => !l.IsDuplicate);
            DuplicateLeadsCount = DiscoveredLeads.Count(l => l.IsDuplicate);
            UpdateSelectionCount();

            LastImportedCount = res.CreatedCount;
            LastSkippedCount = res.SkippedCount;

            ImportAlertTitle = res.CreatedCount > 0
                ? $"🎉 Import Complete: {res.CreatedCount} Leads Stored"
                : "Import Complete: No New Leads Added";

            ImportAlertMessage = res.CreatedCount > 0
                ? $"Successfully added {res.CreatedCount} new enterprise lead(s) into your local SQLite Business Database.\n\n" +
                  (res.SkippedCount > 0
                      ? $"⚠️ {res.SkippedCount} duplicate record(s) were automatically skipped to maintain clean records.\n\n"
                      : string.Empty) +
                  $"Total selected records processed: {selected.Count}."
                : $"All {selected.Count} selected lead(s) already exist in your local SQLite Business Database.\n\nNo duplicate records were added.";

            IsImportSuccessAlertOpen = true;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Import failed: {ex.Message}";
        }
        finally
        {
            IsImporting = false;
        }
    }

    [RelayCommand]
    public void ClearImportedFromDiscovery()
    {
        // Remove leads that are now in the database
        var remaining = DiscoveredLeads.Where(l => !l.IsDuplicate && l.DuplicateMatch == null).ToList();
        var removedCount = DiscoveredLeads.Count - remaining.Count;

        DiscoveredLeads = new ObservableCollection<DiscoveredLead>(remaining);
        TotalFoundCount = DiscoveredLeads.Count;
        NewLeadsCount = DiscoveredLeads.Count(l => !l.IsDuplicate);
        DuplicateLeadsCount = 0;
        UpdateSelectionCount();

        IsImportSuccessAlertOpen = false;
        StatusMessage = $"🧹 Workspace cleared. Removed {removedCount} imported/existing lead(s). {remaining.Count} unimported lead(s) remain in discovery view.";
    }

    [RelayCommand]
    public void KeepAllInDiscovery()
    {
        IsImportSuccessAlertOpen = false;
        StatusMessage = $"All {DiscoveredLeads.Count} lead(s) retained in discovery table with 'IN DATABASE' status badges.";
    }

    [RelayCommand]
    public async Task CopyPitch(DiscoveredLead lead)
    {
        if (lead == null) return;
        var b = lead.Business;
        var text = $"Hi {b.Name},\n\nWe noticed your business in {b.City} could significantly benefit from a modernized high-conversion digital web presence and Google Local optimization. We specialize in enterprise digital solutions for {b.Category} companies.\n\nLet's schedule a brief 10-minute briefing.\n\nBest regards,\nTheKhanSoft Enterprise Team";

        if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow?.Clipboard != null)
        {
            await desktop.MainWindow.Clipboard.SetTextAsync(text);
        }
        StatusMessage = $"📋 Cold outreach pitch for \"{b.Name}\" copied to clipboard!";
    }

    [RelayCommand]
    public async Task CopyPhone(DiscoveredLead lead)
    {
        if (lead?.Business?.PhonePrimary == null) return;
        if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow?.Clipboard != null)
        {
            await desktop.MainWindow.Clipboard.SetTextAsync(lead.Business.PhonePrimary);
        }
        StatusMessage = $"📞 Phone \"{lead.Business.PhonePrimary}\" copied to clipboard!";
    }

    [RelayCommand]
    public void OpenWebsite(DiscoveredLead lead)
    {
        if (string.IsNullOrWhiteSpace(lead?.Business?.WebsiteUrl)) return;
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = lead.Business.WebsiteUrl,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not launch browser: {ex.Message}";
        }
    }

    [RelayCommand]
    public void OpenWhatsApp(DiscoveredLead? lead)
    {
        var b = lead?.Business ?? SelectedLead?.Business;
        if (b == null) return;

        var phone = b.PhonePrimary ?? string.Empty;
        var cleanPhone = new string(phone.Where(char.IsDigit).ToArray());
        if (string.IsNullOrWhiteSpace(cleanPhone))
        {
            StatusMessage = $"⚠️ No valid phone number available for {b.Name}.";
            return;
        }

        try
        {
            var defaultMsg = Uri.EscapeDataString($"Hi {b.Name}, I came across your business in {b.City} and would like to share how we can help elevate your digital presence.");
            var url = $"https://wa.me/{cleanPhone}?text={defaultMsg}";
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
            StatusMessage = $"💬 Opened WhatsApp chat for {b.Name}.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not launch WhatsApp: {ex.Message}";
        }
    }

    [RelayCommand]
    public void OpenEmailComposer(DiscoveredLead? lead)
    {
        var b = lead?.Business ?? SelectedLead?.Business;
        if (b == null) return;

        if (string.IsNullOrWhiteSpace(b.EmailPrimary))
        {
            StatusMessage = $"⚠️ No email address available for {b.Name}.";
            return;
        }

        try
        {
            var subject = Uri.EscapeDataString($"Partnership & Digital Growth Opportunity for {b.Name}");
            var body = Uri.EscapeDataString($"Hi {b.Name},\n\nWe noticed your business in {b.City} has strong potential for growth through modern web optimization.\n\nBest regards,\nTheKhanSoft Enterprise Team");
            var mailto = $"mailto:{b.EmailPrimary}?subject={subject}&body={body}";
            Process.Start(new ProcessStartInfo { FileName = mailto, UseShellExecute = true });
            StatusMessage = $"✉️ Opened email composer for {b.Name}.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not launch email client: {ex.Message}";
        }
    }

    [RelayCommand]
    public void OpenPhoneDialer(DiscoveredLead? lead)
    {
        var b = lead?.Business ?? SelectedLead?.Business;
        if (b == null) return;

        var phone = b.PhonePrimary;
        if (string.IsNullOrWhiteSpace(phone))
        {
            StatusMessage = $"⚠️ No phone number available for {b.Name}.";
            return;
        }

        try
        {
            var clean = new string(phone.Where(c => char.IsDigit(c) || c == '+').ToArray());
            Process.Start(new ProcessStartInfo { FileName = $"tel:{clean}", UseShellExecute = true });
            StatusMessage = $"📞 Dialing {b.Name} ({phone})...";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not launch dialer: {ex.Message}";
        }
    }

    [RelayCommand]
    public void OpenGoogleMaps(DiscoveredLead? lead)
    {
        var b = lead?.Business ?? SelectedLead?.Business;
        if (b == null) return;

        try
        {
            var query = Uri.EscapeDataString($"{b.Name} {b.AddressLine1} {b.City} {b.Country}".Trim());
            var url = $"https://www.google.com/maps/search/?api=1&query={query}";
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
            StatusMessage = $"🗺️ Opened Google Maps search for {b.Name}.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not launch maps: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task CopyContactCardAsync(DiscoveredLead? lead)
    {
        var b = lead?.Business ?? SelectedLead?.Business;
        if (b == null) return;

        var card = $"BUSINESS CONTACT CARD\n" +
                   $"=====================\n" +
                   $"Name:     {b.Name}\n" +
                   $"Category: {b.Category}\n" +
                   $"Phone:    {b.PhonePrimary ?? "N/A"}\n" +
                   $"Email:    {b.EmailPrimary ?? "N/A"}\n" +
                   $"Website:  {b.WebsiteUrl ?? "N/A"}\n" +
                   $"Address:  {b.AddressLine1 ?? "N/A"}, {b.City}, {b.Country}\n" +
                   $"Source:   {b.SourceProvider ?? "Discovery Station"}";

        if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow?.Clipboard != null)
        {
            await desktop.MainWindow.Clipboard.SetTextAsync(card);
            StatusMessage = $"📋 Contact card for {b.Name} copied to clipboard!";
        }
    }

    [RelayCommand]
    public async Task ImportSingleLeadAsync(DiscoveredLead? lead)
    {
        if (lead == null) return;
        lead.IsSelectedForImport = true;
        UpdateSelectionCount();
        await ImportSelectedLeadsAsync();
    }

    [RelayCommand]
    public void OpenLandingPageStudio(DiscoveredLead? lead)
    {
        var b = lead?.Business ?? SelectedLead?.Business;
        if (b == null) return;

        if (_serviceProvider == null)
        {
            StatusMessage = "Service provider unavailable to launch Studio.";
            return;
        }

        try
        {
            var studioVm = _serviceProvider.GetRequiredService<LandingPageStudioViewModel>();
            studioVm.PopulateFromBusiness(b);

            var window = new LandingPageStudioWindow
            {
                DataContext = studioVm
            };
            window.Show();
            StatusMessage = $"🎨 Launched AI Landing Page Studio for {b.Name}!";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not launch AI Landing Page Studio: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task LaunchPresetScanAsync(string presetKey)
    {
        if (IsScanning) return;

        switch (presetKey.ToLowerInvariant())
        {
            case "healthcare":
                CustomSearchKeyword = "dental clinic, hospital, diagnostic center";
                break;
            case "realestate":
                CustomSearchKeyword = "real estate, property, developer, architecture";
                break;
            case "restaurants":
                CustomSearchKeyword = "restaurant, cafe, bistro, fine dining";
                break;
            case "corporate":
                CustomSearchKeyword = "law firm, legal chambers, corporate consultant, tax audit";
                break;
            default:
                CustomSearchKeyword = presetKey;
                break;
        }

        await StartScanAsync();
    }
}
