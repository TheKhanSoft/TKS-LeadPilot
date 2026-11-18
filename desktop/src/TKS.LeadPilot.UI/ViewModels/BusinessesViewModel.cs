using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Input;
using Avalonia.Input.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using TKS.LeadPilot.Core.Enums;
using TKS.LeadPilot.Core.Interfaces;
using TKS.LeadPilot.Core.Models;
using TKS.LeadPilot.UI.Views;

namespace TKS.LeadPilot.UI.ViewModels;

public partial class BusinessesViewModel : ViewModelBase
{
    public static event Action<string>? GlobalNavigationRequested;

    public static void RequestGlobalNavigation(string destination)
    {
        GlobalNavigationRequested?.Invoke(destination);
    }

    [RelayCommand]
    public void OpenDiscoveryStation()
    {
        GlobalNavigationRequested?.Invoke("Discovery");
    }

    private readonly IBusinessRepository _businessRepository;
    private readonly IPipelineStageRepository _pipelineStageRepository;
    private readonly ICsvImporterService _csvImporterService;
    private readonly IDeduplicationService _deduplicationService;
    private readonly IDiscoveryService _discoveryService;
    private readonly IGooglePlacesEnrichmentService _placesService;
    private readonly IGeoLocationService _geoLocationService;
    private readonly IServiceProvider _serviceProvider;

    // Collections
    [ObservableProperty]
    private ObservableCollection<Business> _businesses = [];

    [ObservableProperty]
    private Business? _selectedBusiness;

    // Cascading Geographic Hierarchies
    [ObservableProperty]
    private ObservableCollection<string> _countries = ["All Countries"];

    [ObservableProperty]
    private string _selectedCountry = "All Countries";

    [ObservableProperty]
    private ObservableCollection<string> _states = ["All States/Provinces"];

    [ObservableProperty]
    private string _selectedState = "All States/Provinces";

    [ObservableProperty]
    private ObservableCollection<string> _cities = ["All Cities"];

    [ObservableProperty]
    private string _selectedCity = "All Cities";

    // Proximity / Nearby Me
    [ObservableProperty]
    private bool _isNearbyActive = false;

    [ObservableProperty]
    private double _nearbyRadiusKm = 15.0;

    [ObservableProperty]
    private bool _isDetectingLocation = false;

    // Category and Filter Dropdowns
    [ObservableProperty]
    private ObservableCollection<string> _categories = ["All Categories"];

    [ObservableProperty]
    private string _selectedCategory = "All Categories";

    [ObservableProperty]
    private ObservableCollection<PipelineStage> _pipelineStages = [];

    [ObservableProperty]
    private PipelineStage? _selectedStage;

    [ObservableProperty]
    private ObservableCollection<string> _websiteFilters = ["All Sites", "Has Website", "No Website"];

    [ObservableProperty]
    private string _selectedWebsiteFilter = "All Sites";

    // Opportunity Score Filter
    [ObservableProperty]
    private ObservableCollection<string> _scoreFilters =
    [
        "All Scores (0–100)",
        "🔥 High Opportunity (70–100)",
        "⚡ Medium Opportunity (40–69)",
        "🌱 Low Opportunity (0–39)"
    ];

    [ObservableProperty]
    private string _selectedScoreFilter = "All Scores (0–100)";

    // Contact Availability Filter
    [ObservableProperty]
    private ObservableCollection<string> _contactFilters =
    [
        "All Contacts",
        "📞 Has Phone",
        "✉️ Has Email",
        "❌ Missing Contact"
    ];

    [ObservableProperty]
    private string _selectedContactFilter = "All Contacts";

    // Sorting & Ordering
    [ObservableProperty]
    private ObservableCollection<string> _sortOptions =
    [
        "Score: High to Low",
        "Score: Low to High",
        "Name: A to Z",
        "Name: Z to A",
        "Newest Discovered"
    ];

    [ObservableProperty]
    private string _selectedSortOption = "Score: High to Low";

    [ObservableProperty]
    private ObservableCollection<double> _availableRadii = [5.0, 10.0, 15.0, 25.0, 50.0];

    // Quick Filter & Suggested Niches
    [ObservableProperty]
    private QuickFilterType _selectedQuickFilter = QuickFilterType.All;

    [ObservableProperty]
    private string _activeQuickFilterName = "All";

    [ObservableProperty]
    private ObservableCollection<string> _suggestedNiches = [];

    // Search and State
    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private bool _isLoading = false;

    [ObservableProperty]
    private int _totalCount = 0;

    [ObservableProperty]
    private int _filteredCount = 0;

    [ObservableProperty]
    private int _hotLeadsCount = 0;

    [ObservableProperty]
    private int _verifiedCount = 0;

    [ObservableProperty]
    private int _missingGmbCount = 0;

    [ObservableProperty]
    private int _noWebsiteCount = 0;

    public string ShowingSummaryText => $"Showing: {FilteredCount} of {TotalCount}";

    partial void OnFilteredCountChanged(int value) => OnPropertyChanged(nameof(ShowingSummaryText));
    partial void OnTotalCountChanged(int value) => OnPropertyChanged(nameof(ShowingSummaryText));

    // Layout & Responsive UI State
    [ObservableProperty]
    private bool _isAdvancedFiltersExpanded = false;

    [ObservableProperty]
    private bool _isDetailPaneVisible = true;

    public string AdvancedFiltersToggleText => IsAdvancedFiltersExpanded
        ? (ActiveAdvancedFilterCount > 0 ? $"▴ Less Filters ({ActiveAdvancedFilterCount})" : "▴ Less Filters")
        : (ActiveAdvancedFilterCount > 0 ? $"🎛️ Filters ({ActiveAdvancedFilterCount} active) ▾" : "🎛️ Advanced Filters ▾");

    public string DetailPaneToggleText => IsDetailPaneVisible ? "◨ Hide Details" : "◧ Show Details";

    public int ActiveAdvancedFilterCount
    {
        get
        {
            int count = 0;
            if (!string.IsNullOrWhiteSpace(SelectedState) && SelectedState != "All States/Provinces") count++;
            if (!string.IsNullOrWhiteSpace(SelectedCity) && SelectedCity != "All Cities") count++;
            if (SelectedScoreFilter != "All Scores (0–100)") count++;
            if (SelectedContactFilter != "All Contacts") count++;
            if (SelectedWebsiteFilter != "All Sites") count++;
            if (SelectedStage != null) count++;
            if (IsNearbyActive) count++;
            return count;
        }
    }

    public void NotifyAdvancedFilterPropertiesChanged()
    {
        OnPropertyChanged(nameof(ActiveAdvancedFilterCount));
        OnPropertyChanged(nameof(AdvancedFiltersToggleText));
    }

    [RelayCommand]
    public void ToggleAdvancedFilters()
    {
        IsAdvancedFiltersExpanded = !IsAdvancedFiltersExpanded;
        NotifyAdvancedFilterPropertiesChanged();
    }

    [RelayCommand]
    public void ToggleDetailPane()
    {
        IsDetailPaneVisible = !IsDetailPaneVisible;
        OnPropertyChanged(nameof(DetailPaneToggleText));
    }

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    // Modals
    [ObservableProperty]
    private bool _isImportModalOpen = false;

    [ObservableProperty]
    private bool _isAddBusinessModalOpen = false;

    [ObservableProperty]
    private bool _isAddContactModalOpen = false;

    [ObservableProperty]
    private bool _isDiscoveryModalOpen = false;

    [ObservableProperty]
    private bool _isEnrichConfirmModalOpen = false;

    [ObservableProperty]
    private string _enrichConfirmMessage = string.Empty;

    [ObservableProperty]
    private bool _isEnrichingWithGoogle = false;

    // Discovery Modal Cascading Hierarchies
    [ObservableProperty]
    private ObservableCollection<string> _discoveryCountries = ["Pakistan", "United States", "United Kingdom", "United Arab Emirates", "Canada", "Saudi Arabia", "Australia", "Germany"];

    [ObservableProperty]
    private string _selectedDiscoveryCountry = "Pakistan";

    [ObservableProperty]
    private ObservableCollection<string> _discoveryStates = [];

    [ObservableProperty]
    private string _selectedDiscoveryState = "Punjab";

    [ObservableProperty]
    private ObservableCollection<string> _discoveryCities = [];

    [ObservableProperty]
    private string _selectedDiscoveryCity = "Lahore";

    [ObservableProperty]
    private ObservableCollection<string> _discoveryEngines =
    [
        "🌐 All Sources (Multi-Engine Aggregator)",
        "🛰️ OpenStreetMap & Nominatim",
        "✨ Gemini AI Commercial Discovery (Free Tier)",
        "📍 Google My Business (Google Places)"
    ];

    [ObservableProperty]
    private string _selectedDiscoveryEngine = "🌐 All Sources (Multi-Engine Aggregator)";

    [ObservableProperty]
    private string _discoveryCustomKeyword = string.Empty;

    [ObservableProperty]
    private ObservableCollection<string> _discoveryCategoryPresets = [];

    [ObservableProperty]
    private string _selectedDiscoveryCategoryPreset = "Healthcare & Dental";

    [ObservableProperty]
    private ObservableCollection<string> _discoverySuggestedNiches = [];

    [ObservableProperty]
    private bool _isDiscoveryNearbyMode = false;

    [ObservableProperty]
    private double _discoveryRadiusKm = 15.0;

    [ObservableProperty]
    private ObservableCollection<DiscoveredLead> _discoveredLeads = [];

    [ObservableProperty]
    private bool _isDiscovering = false;

    [ObservableProperty]
    private string _discoveryStatus = "Ready to discover leads from OpenStreetMap.";

    [ObservableProperty]
    private int _discoveredTotal = 0;

    [ObservableProperty]
    private int _discoveredNew = 0;

    [ObservableProperty]
    private int _discoveredDuplicates = 0;

    [ObservableProperty]
    private DeduplicationStrategy _discoveryDedupStrategy = DeduplicationStrategy.Skip;

    // Advanced Discovery Scan Controls & Scope
    [ObservableProperty]
    private ObservableCollection<string> _discoveryBatchLimits =
    [
        "25 Leads (Fast)",
        "50 Leads (Standard)",
        "100 Leads (Comprehensive)",
        "200 Leads (Deep Extraction)"
    ];

    [ObservableProperty]
    private string _selectedDiscoveryBatchLimit = "50 Leads (Standard)";

    [ObservableProperty]
    private bool _discoveryRequirePhone = false;

    [ObservableProperty]
    private bool _discoveryTargetMissingWebsite = false;

    [ObservableProperty]
    private bool _discoveryTargetMissingGmb = false;

    // CSV Import State
    [ObservableProperty]
    private ImportPreviewResult? _importPreview;

    [ObservableProperty]
    private DeduplicationStrategy _selectedDedupStrategy = DeduplicationStrategy.Skip;

    [ObservableProperty]
    private bool _isImporting = false;

    private byte[]? _stagedCsvBytes;

    // Add Business Form Fields
    [ObservableProperty]
    private string _newBusinessName = string.Empty;

    [ObservableProperty]
    private string _newBusinessCategory = string.Empty;

    [ObservableProperty]
    private string _newBusinessCountry = "Pakistan";

    [ObservableProperty]
    private string _newBusinessState = "Punjab";

    [ObservableProperty]
    private string _newBusinessCity = "Lahore";

    [ObservableProperty]
    private string _newBusinessPhone = string.Empty;

    [ObservableProperty]
    private string _newBusinessEmail = string.Empty;

    [ObservableProperty]
    private string _newBusinessWebsite = string.Empty;

    [ObservableProperty]
    private string _newBusinessTags = string.Empty;

    // Add Contact Form Fields
    [ObservableProperty]
    private string _newContactFullName = string.Empty;

    [ObservableProperty]
    private string _newContactJobTitle = string.Empty;

    [ObservableProperty]
    private string _newContactEmail = string.Empty;

    [ObservableProperty]
    private string _newContactPhone = string.Empty;

    [ObservableProperty]
    private bool _newContactIsPrimary = false;

    public BusinessesViewModel(
        IBusinessRepository businessRepository,
        IPipelineStageRepository pipelineStageRepository,
        ICsvImporterService csvImporterService,
        IDeduplicationService deduplicationService,
        IDiscoveryService discoveryService,
        IGooglePlacesEnrichmentService placesService,
        IGeoLocationService geoLocationService,
        IServiceProvider serviceProvider)
    {
        _businessRepository = businessRepository;
        _pipelineStageRepository = pipelineStageRepository;
        _csvImporterService = csvImporterService;
        _deduplicationService = deduplicationService;
        _discoveryService = discoveryService;
        _placesService = placesService;
        _geoLocationService = geoLocationService;
        _serviceProvider = serviceProvider;

        _ = InitializeAsync();
    }

    public async Task InitializeAsync()
    {
        try
        {
            IsLoading = true;

            // Load Pipeline Stages
            var stages = await _pipelineStageRepository.GetAllAsync();
            PipelineStages = new ObservableCollection<PipelineStage>(stages);

            // Populate Countries
            var catalogCountries = _geoLocationService.GetAllCountries().Select(c => c.DisplayName).ToList();
            Countries = new ObservableCollection<string>(new[] { "All Countries" }.Concat(catalogCountries));

            // Populate Categories from presets + DB
            var catList = await _businessRepository.GetDistinctCategoriesAsync();
            var presetCategories = _discoveryService.GetPresetCategories().Keys;
            var mergedCategories = new HashSet<string>(catList.Concat(presetCategories), StringComparer.OrdinalIgnoreCase).OrderBy(c => c);
            Categories = new ObservableCollection<string>(new[] { "All Categories" }.Concat(mergedCategories));

            // Discovery presets & countries
            var allCountryNames = _geoLocationService.GetAllCountries().Select(c => c.Name).ToList();
            DiscoveryCountries = new ObservableCollection<string>(allCountryNames);
            if (!DiscoveryCountries.Contains(SelectedDiscoveryCountry))
            {
                SelectedDiscoveryCountry = DiscoveryCountries.FirstOrDefault() ?? "Pakistan";
            }

            DiscoveryCategoryPresets = new ObservableCollection<string>(_discoveryService.GetPresetCategories().Keys.Distinct(StringComparer.OrdinalIgnoreCase));
            SelectedDiscoveryCategoryPreset = DiscoveryCategoryPresets.FirstOrDefault() ?? "⭐ All Commercial Businesses & Enterprises";

            // Initialize Discovery Locations
            UpdateDiscoveryStates(SelectedDiscoveryCountry);
            UpdateDiscoveryCities(SelectedDiscoveryCountry, SelectedDiscoveryState);

            // Populate Initial Cascades
            await RefreshStatesForCountryAsync(SelectedCountry);
            await RefreshCitiesForStateAsync(SelectedCountry, SelectedState);
            RefreshSuggestedNiches();

            // Load Initial Businesses
            await ApplyFiltersAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Init Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
            NotifyAdvancedFilterPropertiesChanged();
        }
    }

    // --- Cascading Change Handlers for Main View ---
    private bool _isSuppressingLocationCascades = false;

    partial void OnSelectedCountryChanged(string value)
    {
        if (_isSuppressingLocationCascades) return;
        _ = Task.Run(async () =>
        {
            await RefreshStatesForCountryAsync(value);
            await RefreshCitiesForStateAsync(value, SelectedState);
            RefreshSuggestedNiches();
            await ApplyFiltersAsync();
        });
    }

    partial void OnSelectedStateChanged(string value)
    {
        if (_isSuppressingLocationCascades) return;
        _ = Task.Run(async () =>
        {
            await RefreshCitiesForStateAsync(SelectedCountry, value);
            RefreshSuggestedNiches();
            await ApplyFiltersAsync();
        });
    }

    partial void OnSelectedCityChanged(string value)
    {
        if (_isSuppressingLocationCascades) return;
        RefreshSuggestedNiches();
        _ = ApplyFiltersAsync();
    }

    partial void OnSelectedSortOptionChanged(string value)
    {
        _ = ApplyFiltersAsync();
    }

    partial void OnNearbyRadiusKmChanged(double value)
    {
        if (IsNearbyActive)
        {
            _ = DetectNearbyLocation();
        }
    }

    private async Task RefreshStatesForCountryAsync(string countryDisplay)
    {
        var rawCountry = ExtractCountryName(countryDisplay);
        var stateList = new List<string>();

        if (rawCountry != "All Countries")
        {
            var states = _geoLocationService.GetStatesForCountry(rawCountry);
            stateList = states.Select(s => s.Name).ToList();

            // Also check DB for any distinct states recorded
            var dbStates = await _businessRepository.GetDistinctStatesAsync(rawCountry);
            stateList = stateList.Union(dbStates).OrderBy(s => s).ToList();
        }

        States = new ObservableCollection<string>(new[] { "All States/Provinces" }.Concat(stateList));
        SelectedState = "All States/Provinces";
    }

    private async Task RefreshCitiesForStateAsync(string countryDisplay, string stateName)
    {
        var rawCountry = ExtractCountryName(countryDisplay);
        var cityList = new List<string>();

        if (rawCountry != "All Countries" && stateName != "All States/Provinces")
        {
            var cities = _geoLocationService.GetCitiesForState(rawCountry, stateName);
            cityList = cities.Select(c => c.Name).ToList();
        }
        else if (rawCountry != "All Countries")
        {
            var country = _geoLocationService.GetCountry(rawCountry);
            if (country != null)
            {
                cityList = country.States.SelectMany(s => s.Cities).Select(c => c.Name).ToList();
            }
        }
        else
        {
            // All cities from DB
            cityList = await _businessRepository.GetDistinctCitiesAsync();
        }

        Cities = new ObservableCollection<string>(new[] { "All Cities" }.Concat(cityList.Distinct().OrderBy(c => c)));
        SelectedCity = "All Cities";
    }

    private void RefreshSuggestedNiches()
    {
        var rawCountry = ExtractCountryName(SelectedCountry);
        var rawCity = SelectedCity != "All Cities" ? SelectedCity : null;
        var countryArg = rawCountry != "All Countries" ? rawCountry : null;

        var niches = _geoLocationService.GetSuggestedNiches(countryArg, rawCity);
        SuggestedNiches = new ObservableCollection<string>(niches);
    }

    private static string ExtractCountryName(string countryDisplay)
    {
        if (string.IsNullOrWhiteSpace(countryDisplay) || countryDisplay == "All Countries") return "All Countries";
        // Strips leading flag emojis if present (e.g. "🇵🇰 Pakistan" -> "Pakistan")
        var parts = countryDisplay.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 1 ? string.Join(' ', parts.Skip(1)) : parts[0];
    }

    // --- Quick Filter Command ---

    [RelayCommand]
    public async Task ApplyQuickFilter(string filterName)
    {
        ActiveQuickFilterName = filterName;
        SelectedQuickFilter = filterName switch
        {
            "🔥 Hot Leads" or "🔥 Hot Leads (70+)" or "Hot Leads" => QuickFilterType.HotLeads,
            "❌ No Website" or "No Website" => QuickFilterType.NoWebsite,
            "📍 Missing GMB" or "Missing GMB" => QuickFilterType.MissingGmb,
            "✓ Verified" or "✓ Verified Only" or "Verified" => QuickFilterType.GoogleVerified,
            "⭐ Top Rated" or "⭐ Top Rated (4.5+)" or "Top Rated" => QuickFilterType.TopRated,
            "🔬 Needs Audit" or "Needs Audit" => QuickFilterType.NeedsAudit,
            "⭐ Starred" or "⭐ Starred Leads" or "Starred" => QuickFilterType.StarredOnly,
            "📞 Has Phone" or "Has Phone" => QuickFilterType.HasPhone,
            "❌ Missing Contact" => QuickFilterType.MissingContact,
            _ => QuickFilterType.All
        };

        await ApplyFiltersAsync();
    }

    [RelayCommand]
    public async Task ClearQuickFilter()
    {
        ActiveQuickFilterName = "All";
        SelectedQuickFilter = QuickFilterType.All;
        await ApplyFiltersAsync();
    }

    [RelayCommand]
    public async Task SelectSuggestedNiche(string niche)
    {
        if (string.IsNullOrWhiteSpace(niche)) return;
        var preset = MapNicheToCategoryPreset(niche);

        var firstWord = preset.Split(' ')[0].TrimEnd(',');
        var nicheWord = niche.Split(' ')[0].TrimEnd(',');

        var matchedCat = Categories.FirstOrDefault(c => 
            c.Contains(firstWord, StringComparison.OrdinalIgnoreCase) || 
            c.Contains(nicheWord, StringComparison.OrdinalIgnoreCase));

        if (matchedCat != null && matchedCat != "All Categories")
        {
            SelectedCategory = matchedCat;
            SearchQuery = string.Empty;
            await ApplyFiltersAsync();

            if (FilteredCount > 0)
            {
                StatusMessage = $"Filtered by niche '{niche}' ({FilteredCount} leads in database)";
                return;
            }
        }

        // If no records in database for this niche yet, open the Discovery modal pre-set to this niche and scan!
        OpenDiscoveryModal();
        SelectedDiscoveryCategoryPreset = preset;
        DiscoveryCustomKeyword = niche;
        DiscoveryStatus = $"Searching OpenStreetMap & Google Places for '{niche}' in {SelectedDiscoveryCity}...";
        await StartDiscoveryScan();
    }

    // --- Nearby Me Proximity Detection ---

    [RelayCommand]
    public async Task DetectNearbyLocation()
    {
        try
        {
            IsDetectingLocation = true;
            StatusMessage = "📍 Detecting your live network location...";

            var nearby = await _geoLocationService.DetectNearbyLocationAsync(NearbyRadiusKm);
            IsNearbyActive = true;

            _isSuppressingLocationCascades = true;

            // 1. Match and set Country
            var matchedCountry = Countries.FirstOrDefault(c => c.Contains(nearby.Country, StringComparison.OrdinalIgnoreCase))
                                 ?? Countries.FirstOrDefault(c => nearby.Country.Contains(ExtractCountryName(c), StringComparison.OrdinalIgnoreCase));
            if (matchedCountry != null)
            {
                SelectedCountry = matchedCountry;
            }

            // 2. Populate States and match State
            await RefreshStatesForCountryAsync(nearby.Country);
            var matchedState = States.FirstOrDefault(s => s.Equals(nearby.StateProvince, StringComparison.OrdinalIgnoreCase))
                               ?? States.FirstOrDefault(s => s.Contains(nearby.StateProvince, StringComparison.OrdinalIgnoreCase));
            if (matchedState != null)
            {
                SelectedState = matchedState;
            }

            // 3. Populate Cities and match City
            await RefreshCitiesForStateAsync(nearby.Country, nearby.StateProvince);
            var matchedCity = Cities.FirstOrDefault(c => c.Equals(nearby.City, StringComparison.OrdinalIgnoreCase))
                              ?? Cities.FirstOrDefault(c => c.Contains(nearby.City, StringComparison.OrdinalIgnoreCase));
            if (matchedCity != null)
            {
                SelectedCity = matchedCity;
            }

            RefreshSuggestedNiches();
            _isSuppressingLocationCascades = false;

            StatusMessage = $"📍 Proximity Mode: within {nearby.RadiusKm} km of {nearby.City}, {nearby.StateProvince} ({nearby.Country})";
            await ApplyFiltersAsync();
        }
        catch (Exception ex)
        {
            _isSuppressingLocationCascades = false;
            StatusMessage = $"Nearby Error: {ex.Message}";
        }
        finally
        {
            IsDetectingLocation = false;
        }
    }

    [RelayCommand]
    public async Task ClearNearby()
    {
        IsNearbyActive = false;
        SelectedCountry = "All Countries";
        SelectedState = "All States/Provinces";
        SelectedCity = "All Cities";
        await ApplyFiltersAsync();
    }

    // --- Filter Application ---

    [RelayCommand]
    public async Task ApplyFilters()
    {
        await ApplyFiltersAsync();
    }

    public async Task ApplyFiltersAsync()
    {
        try
        {
            IsLoading = true;
            var rawCountry = ExtractCountryName(SelectedCountry);

            int? minScore = null;
            int? maxScore = null;
            if (SelectedScoreFilter.Contains("High Opportunity"))
            {
                minScore = 70;
            }
            else if (SelectedScoreFilter.Contains("Medium Opportunity"))
            {
                minScore = 40;
                maxScore = 69;
            }
            else if (SelectedScoreFilter.Contains("Low Opportunity"))
            {
                maxScore = 39;
            }

            bool? hasPhone = null;
            bool? hasEmail = null;
            if (SelectedContactFilter.Contains("Has Phone"))
            {
                hasPhone = true;
            }
            else if (SelectedContactFilter.Contains("Has Email"))
            {
                hasEmail = true;
            }
            else if (SelectedContactFilter.Contains("Missing Contact") || SelectedContactFilter.Contains("No Contact"))
            {
                hasPhone = false;
                hasEmail = false;
            }

            var criteria = new BusinessFilterCriteria
            {
                SearchQuery = string.IsNullOrWhiteSpace(SearchQuery) ? null : SearchQuery,
                Country = rawCountry != "All Countries" ? rawCountry : null,
                StateProvince = SelectedState != "All States/Provinces" ? SelectedState : null,
                City = SelectedCity != "All Cities" ? SelectedCity : null,
                Category = SelectedCategory != "All Categories" ? SelectedCategory : null,
                PipelineStageId = SelectedStage?.Id,
                HasWebsite = SelectedWebsiteFilter == "Has Website" ? true : SelectedWebsiteFilter == "No Website" ? false : null,
                HasPhone = hasPhone,
                HasEmail = hasEmail,
                MinScore = minScore,
                MaxScore = maxScore,
                QuickFilter = SelectedQuickFilter,
                SortBy = SelectedSortOption switch
                {
                    "Score: High to Low" => BusinessSortOption.ScoreDescending,
                    "Score: Low to High" => BusinessSortOption.ScoreAscending,
                    "Rating: 5★ Top Rated" or "Rating: Top Rated" => BusinessSortOption.ScoreDescending,
                    "Name: A to Z" => BusinessSortOption.NameAscending,
                    "Name: Z to A" => BusinessSortOption.NameDescending,
                    "Newest Discovered" => BusinessSortOption.CreatedDescending,
                    _ => BusinessSortOption.ScoreDescending
                },
                Take = 500
            };

            var list = await _businessRepository.GetFilteredAsync(criteria);
            Businesses = new ObservableCollection<Business>(list);
            FilteredCount = list.Count;
            TotalCount = await _businessRepository.GetTotalCountAsync();
            HotLeadsCount = await _businessRepository.GetHighOpportunityCountAsync(70);
            VerifiedCount = Businesses.Count(b => b.IsGoogleVerified);
            MissingGmbCount = Businesses.Count(b => !b.IsGoogleVerified && string.IsNullOrWhiteSpace(b.GooglePlaceId));
            NoWebsiteCount = Businesses.Count(b => string.IsNullOrWhiteSpace(b.WebsiteUrl));

            SelectedBusiness = Businesses.FirstOrDefault();

            if (SelectedQuickFilter != QuickFilterType.All)
            {
                StatusMessage = $"Active Filter: {ActiveQuickFilterName} ({FilteredCount} matches)";
            }
            else if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                StatusMessage = $"Search: \"{SearchQuery}\" ({FilteredCount} matches)";
            }
            else if (rawCountry != "All Countries" || SelectedCity != "All Cities" || SelectedCategory != "All Categories")
            {
                StatusMessage = $"Filtered by: {SelectedCountry} • {SelectedCity} • {SelectedCategory} ({FilteredCount} leads)";
            }
            else
            {
                StatusMessage = $"Database view active • {TotalCount} total registered businesses";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Filter Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
            NotifyAdvancedFilterPropertiesChanged();
        }
    }

    [RelayCommand]
    public async Task ResetFilters()
    {
        SearchQuery = string.Empty;
        SelectedCountry = "All Countries";
        SelectedState = "All States/Provinces";
        SelectedCity = "All Cities";
        SelectedCategory = "All Categories";
        SelectedScoreFilter = "All Scores (0–100)";
        SelectedContactFilter = "All Contacts";
        SelectedStage = null;
        SelectedWebsiteFilter = "All Sites";
        SelectedQuickFilter = QuickFilterType.All;
        ActiveQuickFilterName = "All";
        IsNearbyActive = false;
        await ApplyFiltersAsync();
    }

    [RelayCommand]
    public async Task ToggleStar(Business? business)
    {
        if (business == null) return;
        business.IsStarred = !business.IsStarred;
        await _businessRepository.UpdateAsync(business);
    }

    // --- CSV Import ---
    [RelayCommand]
    public void OpenImportModal()
    {
        ImportPreview = null;
        _stagedCsvBytes = null;
        IsImportModalOpen = true;
    }

    [RelayCommand]
    public void CloseImportModal()
    {
        IsImportModalOpen = false;
        ImportPreview = null;
        _stagedCsvBytes = null;
    }

    public async Task StageCsvForImportAsync(Stream csvStream)
    {
        try
        {
            IsLoading = true;
            using var ms = new MemoryStream();
            await csvStream.CopyToAsync(ms);
            _stagedCsvBytes = ms.ToArray();

            using var previewStream = new MemoryStream(_stagedCsvBytes);
            ImportPreview = await _csvImporterService.PreviewCsvAsync(previewStream);
            StatusMessage = $"CSV Parsed: {ImportPreview.TotalRows} rows found ({ImportPreview.ValidRows} valid, {ImportPreview.DuplicateRows} duplicates)";
        }
        catch (Exception ex)
        {
            StatusMessage = $"CSV Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
            NotifyAdvancedFilterPropertiesChanged();
        }
    }

    [RelayCommand]
    public async Task ExecuteImport()
    {
        if (_stagedCsvBytes == null || ImportPreview == null) return;

        try
        {
            IsImporting = true;
            using var stream = new MemoryStream(_stagedCsvBytes);
            var result = await _csvImporterService.ExecuteImportAsync(
                stream,
                ImportPreview.ColumnMapping,
                SelectedDedupStrategy);

            StatusMessage = $"Import finished: {result.CreatedCount} created, {result.UpdatedCount} updated, {result.SkippedCount} skipped";
            CloseImportModal();
            await RefreshStatesForCountryAsync(SelectedCountry);
            await RefreshCitiesForStateAsync(SelectedCountry, SelectedState);
            await ApplyFiltersAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Import Error: {ex.Message}";
        }
        finally
        {
            IsImporting = false;
        }
    }

    [RelayCommand]
    public async Task ExportCsv()
    {
        try
        {
            IsLoading = true;
            var bytes = await _csvImporterService.ExportToCsvAsync(Businesses);
            var exportDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            if (!Directory.Exists(exportDir)) exportDir = AppContext.BaseDirectory;

            var filename = $"TKS_LeadPilot_Export_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            var filePath = Path.Combine(exportDir, filename);
            await File.WriteAllBytesAsync(filePath, bytes);
            StatusMessage = $"Exported {Businesses.Count} leads to {filePath}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Export Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
            NotifyAdvancedFilterPropertiesChanged();
        }
    }

    [RelayCommand]
    public async Task ExportJson()
    {
        try
        {
            IsLoading = true;
            var exportDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            if (!Directory.Exists(exportDir)) exportDir = AppContext.BaseDirectory;

            var filename = $"TKS_LeadPilot_Export_{DateTime.Now:yyyyMMdd_HHmmss}.json";
            var filePath = Path.Combine(exportDir, filename);

            var options = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
            var json = System.Text.Json.JsonSerializer.Serialize(Businesses, options);
            await File.WriteAllTextAsync(filePath, json);
            StatusMessage = $"📄 Exported {Businesses.Count} leads (JSON) to {filePath}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Export Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
            NotifyAdvancedFilterPropertiesChanged();
        }
    }

    // --- Quick Actions on Selected Lead ---

    private static async Task CopyToClipboardAsync(string text)
    {
        try
        {
            if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow?.Clipboard != null)
            {
                await desktop.MainWindow.Clipboard.SetTextAsync(text);
            }
        }
        catch { }
    }

    public static string CleanPhoneNumberForWhatsApp(string? phone, string? country)
    {
        if (string.IsNullOrWhiteSpace(phone)) return string.Empty;
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (string.IsNullOrWhiteSpace(digits)) return string.Empty;

        if (digits.StartsWith("00")) digits = digits.Substring(2);

        if (digits.StartsWith("0"))
        {
            var c = country?.Trim().ToLowerInvariant() ?? "";
            string countryCode = "92"; // Default Pakistan
            if (c.Contains("united kingdom") || c == "uk") countryCode = "44";
            else if (c.Contains("united states") || c == "us" || c == "usa" || c.Contains("canada")) countryCode = "1";
            else if (c.Contains("australia")) countryCode = "61";
            else if (c.Contains("germany")) countryCode = "49";
            else if (c.Contains("france")) countryCode = "33";
            else if (c.Contains("uae") || c.Contains("emirates")) countryCode = "971";
            else if (c.Contains("saudi")) countryCode = "966";
            else if (c.Contains("india")) countryCode = "91";
            else if (c.Contains("turkey")) countryCode = "90";
            else if (c.Contains("china")) countryCode = "86";
            else if (c.Contains("japan")) countryCode = "81";

            digits = countryCode + digits.Substring(1);
        }

        return digits;
    }

    [RelayCommand]
    public async Task CopyPhone(Business? business = null)
    {
        var b = business ?? SelectedBusiness;
        if (b != null && !string.IsNullOrWhiteSpace(b.PhonePrimary))
        {
            await CopyToClipboardAsync(b.PhonePrimary);
            StatusMessage = $"📞 Phone copied ({b.PhonePrimary}) to clipboard!";
        }
    }

    [RelayCommand]
    public async Task CopyEmail(Business? business = null)
    {
        var b = business ?? SelectedBusiness;
        if (b != null && !string.IsNullOrWhiteSpace(b.EmailPrimary))
        {
            await CopyToClipboardAsync(b.EmailPrimary);
            StatusMessage = $"✉️ Email copied ({b.EmailPrimary}) to clipboard!";
        }
    }

    [RelayCommand]
    public void OpenWebsite(Business? business = null)
    {
        var b = business ?? SelectedBusiness;
        if (b != null && !string.IsNullOrWhiteSpace(b.WebsiteUrl))
        {
            try
            {
                var url = b.WebsiteUrl.Trim();
                var uri = url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                    ? url
                    : $"https://{url}";

                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = uri,
                    UseShellExecute = true
                });
                StatusMessage = $"🌐 Launching browser for {b.Name}...";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Browser Launch Error: {ex.Message}";
            }
        }
    }

    [RelayCommand]
    public void AuditWebsite(Business? business = null)
    {
        var b = business ?? SelectedBusiness;
        if (b == null) return;

        if (string.IsNullOrWhiteSpace(b.WebsiteUrl))
        {
            StatusMessage = $"⚠️ '{b.Name}' has no website to audit. Opening AI Landing Page Studio instead...";
            OpenLandingPageStudio(b);
            return;
        }

        AuditsViewModel.TargetBusinessIdForAudit = b.Id;
        GlobalNavigationRequested?.Invoke("Audits");
        StatusMessage = $"🔬 Opening Website Audit suite for '{b.Name}' ({b.WebsiteUrl})...";
    }

    [RelayCommand]
    public void OpenWhatsApp(Business? business = null)
    {
        var b = business ?? SelectedBusiness;
        if (b == null) return;

        if (string.IsNullOrWhiteSpace(b.PhonePrimary))
        {
            StatusMessage = $"⚠️ No phone number on record for '{b.Name}'.";
            return;
        }

        var digits = CleanPhoneNumberForWhatsApp(b.PhonePrimary, b.Country);
        if (string.IsNullOrWhiteSpace(digits))
        {
            StatusMessage = $"⚠️ Phone number '{b.PhonePrimary}' has no valid numeric digits for WhatsApp.";
            return;
        }

        try
        {
            var greeting = Uri.EscapeDataString($"Hi {b.Name}, we noticed your business in {b.City} and would like to share high-impact digital growth recommendations from TheKhanSoft.");
            var url = $"https://wa.me/{digits}?text={greeting}";
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
            StatusMessage = $"💬 WhatsApp chat initiated for '{b.Name}' (+{digits})";
        }
        catch (Exception ex)
        {
            StatusMessage = $"WhatsApp Error: {ex.Message}";
        }
    }

    [RelayCommand]
    public void OpenEmailClient(Business? business = null)
    {
        var b = business ?? SelectedBusiness;
        if (b == null) return;

        if (string.IsNullOrWhiteSpace(b.EmailPrimary))
        {
            StatusMessage = $"⚠️ No email address on record for '{b.Name}'.";
            return;
        }

        try
        {
            var subject = Uri.EscapeDataString($"Digital Growth Strategy for {b.Name}");
            var body = Uri.EscapeDataString($"Dear {b.Name} Team,\n\nWe analyzed local {b.Category} enterprises in {b.City} and identified strategic digital optimization opportunities for your organization.\n\nBest regards,\nTheKhanSoft Enterprise Suite\ninfo@thekhansoft.com");
            var url = $"mailto:{b.EmailPrimary}?subject={subject}&body={body}";
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
            StatusMessage = $"✉️ Email composer launched for '{b.Name}' ({b.EmailPrimary})";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Email Error: {ex.Message}";
        }
    }

    [RelayCommand]
    public void CallPhone(Business? business = null)
    {
        var b = business ?? SelectedBusiness;
        if (b == null) return;

        if (string.IsNullOrWhiteSpace(b.PhonePrimary))
        {
            StatusMessage = $"⚠️ No phone number on record for '{b.Name}'.";
            return;
        }

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = $"tel:{b.PhonePrimary.Trim()}",
                UseShellExecute = true
            });
            StatusMessage = $"📞 Dialing '{b.Name}' ({b.PhonePrimary})...";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Dialer Error: {ex.Message}";
        }
    }

    [RelayCommand]
    public void ViewOnGoogleMaps(Business? business = null)
    {
        var b = business ?? SelectedBusiness;
        if (b == null) return;

        try
        {
            var query = Uri.EscapeDataString($"{b.Name} {b.City} {b.Country}".Trim());
            var url = $"https://www.google.com/maps/search/?api=1&query={query}";
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
            StatusMessage = $"📍 Viewing '{b.Name}' on Google Maps...";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Maps Error: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task CopyAllContactInfo(Business? business = null)
    {
        var b = business ?? SelectedBusiness;
        if (b == null) return;

        var digits = CleanPhoneNumberForWhatsApp(b.PhonePrimary, b.Country);
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"🏢 Business: {b.Name}");
        sb.AppendLine($"📂 Category: {b.Category}");
        sb.AppendLine($"📍 Location: {b.City}, {b.StateProvince}, {b.Country}");
        if (!string.IsNullOrWhiteSpace(b.AddressLine1)) sb.AppendLine($"🏠 Address: {b.AddressLine1}");
        if (!string.IsNullOrWhiteSpace(b.PhonePrimary)) sb.AppendLine($"📞 Phone: {b.PhonePrimary}");
        if (!string.IsNullOrWhiteSpace(digits)) sb.AppendLine($"💬 WhatsApp: https://wa.me/{digits}");
        if (!string.IsNullOrWhiteSpace(b.EmailPrimary)) sb.AppendLine($"✉️ Email: {b.EmailPrimary}");
        if (!string.IsNullOrWhiteSpace(b.WebsiteUrl)) sb.AppendLine($"🌐 Website: {b.WebsiteUrl}");
        sb.AppendLine($"⭐ Opportunity Score: {b.LeadScore}/100 ({(b.LeadScore >= 70 ? "High Opportunity" : "Standard")})");
        if (b.PipelineStage != null) sb.AppendLine($"📊 Pipeline Stage: {b.PipelineStage.Name}");
        if (!string.IsNullOrWhiteSpace(b.Tags)) sb.AppendLine($"🏷️ Tags: {b.Tags}");

        await CopyToClipboardAsync(sb.ToString());
        StatusMessage = $"📋 Full contact summary for \"{b.Name}\" copied to clipboard!";
    }

    [RelayCommand]
    public async Task CopyWhatsAppLink(Business? business = null)
    {
        var b = business ?? SelectedBusiness;
        if (b == null || string.IsNullOrWhiteSpace(b.PhonePrimary)) return;

        var digits = CleanPhoneNumberForWhatsApp(b.PhonePrimary, b.Country);
        if (string.IsNullOrWhiteSpace(digits)) return;

        var link = $"https://wa.me/{digits}";
        await CopyToClipboardAsync(link);
        StatusMessage = $"💬 WhatsApp link ({link}) copied to clipboard!";
    }

    [RelayCommand]
    public async Task CopyWebsite(Business? business = null)
    {
        var b = business ?? SelectedBusiness;
        if (b == null || string.IsNullOrWhiteSpace(b.WebsiteUrl)) return;

        await CopyToClipboardAsync(b.WebsiteUrl);
        StatusMessage = $"🌐 Website URL ({b.WebsiteUrl}) copied to clipboard!";
    }

    [RelayCommand]
    public async Task UpdateLeadStage(string stageName)
    {
        if (SelectedBusiness == null || string.IsNullOrWhiteSpace(stageName)) return;

        var targetStage = PipelineStages.FirstOrDefault(s => s.Name.Equals(stageName, StringComparison.OrdinalIgnoreCase) || s.Code.Equals(stageName, StringComparison.OrdinalIgnoreCase));
        if (targetStage != null)
        {
            SelectedBusiness.PipelineStageId = targetStage.Id;
            SelectedBusiness.PipelineStage = targetStage;
            await _businessRepository.UpdateAsync(SelectedBusiness);
            StatusMessage = $"📊 Moved '{SelectedBusiness.Name}' to pipeline stage: {targetStage.Name}";
        }
    }

    [RelayCommand]
    public async Task CopyQuickPitch(Business? business = null)
    {
        var b = business ?? SelectedBusiness;
        if (b == null) return;

        var pitch = $"Subject: Digital Growth & Optimization for {b.Name}\n\n" +
                    $"Dear {b.Name} Team,\n\n" +
                    $"We analyzed local {b.Category} businesses in {b.City} and identified high-impact opportunities for your digital client acquisition.\n\n" +
                    (string.IsNullOrWhiteSpace(b.WebsiteUrl)
                        ? $"Currently, {b.Name} lacks an official, verified website. Prospective customers searching online for {b.Category} are discovering competitors instead. TheKhanSoft builds ultra-fast, mobile-responsive web platforms engineered to convert local traffic into paying clients.\n\n"
                        : $"Our technical audit of {b.WebsiteUrl} identified key optimization areas in load speeds, security hygiene, and search discoverability to drastically improve your conversion rates.\n\n") +
                    $"Would you be available for a brief 5-minute discovery consultation this week?\n\n" +
                    $"Sincerely,\nTheKhanSoft Enterprise Team\ninfo@thekhansoft.com";

        await CopyToClipboardAsync(pitch);
        StatusMessage = $"📋 Tailored outreach pitch for {b.Name} copied to clipboard!";
    }

    [RelayCommand]
    public void OpenLandingPageStudio(Business? business = null)
    {
        var b = business ?? SelectedBusiness;
        if (b == null)
        {
            StatusMessage = "⚠️ Select a business first to launch AI Landing Page Studio.";
            return;
        }

        LandingPageStudioViewModel.TargetBusinessForStudio = b;
        GlobalNavigationRequested?.Invoke("LandingStudio");
        StatusMessage = $"🎨 Pre-filled AI Landing Page Studio for '{b.Name}'.";
    }

    // --- Add Business ---
    [RelayCommand]
    public void OpenAddBusinessModal()
    {
        NewBusinessName = string.Empty;
        NewBusinessCategory = string.Empty;
        NewBusinessCountry = SelectedCountry != "All Countries" ? ExtractCountryName(SelectedCountry) : "Pakistan";
        NewBusinessState = SelectedState != "All States/Provinces" ? SelectedState : "Punjab";
        NewBusinessCity = SelectedCity != "All Cities" ? SelectedCity : "Lahore";
        NewBusinessPhone = string.Empty;
        NewBusinessEmail = string.Empty;
        NewBusinessWebsite = string.Empty;
        NewBusinessTags = string.Empty;
        IsAddBusinessModalOpen = true;
    }

    [RelayCommand]
    public void CloseAddBusinessModal()
    {
        IsAddBusinessModalOpen = false;
    }

    [RelayCommand]
    public async Task SaveNewBusiness()
    {
        if (string.IsNullOrWhiteSpace(NewBusinessName))
        {
            StatusMessage = "Business Name is required";
            return;
        }

        try
        {
            var defaultStage = PipelineStages.FirstOrDefault()?.Id ?? "stage-discovered";
            var newBiz = new Business
            {
                Name = NewBusinessName.Trim(),
                Category = string.IsNullOrWhiteSpace(NewBusinessCategory) ? "General" : NewBusinessCategory.Trim(),
                Country = string.IsNullOrWhiteSpace(NewBusinessCountry) ? "Pakistan" : NewBusinessCountry.Trim(),
                StateProvince = string.IsNullOrWhiteSpace(NewBusinessState) ? null : NewBusinessState.Trim(),
                City = string.IsNullOrWhiteSpace(NewBusinessCity) ? "Unspecified" : NewBusinessCity.Trim(),
                PhonePrimary = string.IsNullOrWhiteSpace(NewBusinessPhone) ? null : NewBusinessPhone.Trim(),
                EmailPrimary = string.IsNullOrWhiteSpace(NewBusinessEmail) ? null : NewBusinessEmail.Trim(),
                WebsiteUrl = string.IsNullOrWhiteSpace(NewBusinessWebsite) ? null : NewBusinessWebsite.Trim(),
                WebsiteStatus = !string.IsNullOrWhiteSpace(NewBusinessWebsite) ? WebsiteStatus.ActiveWebsite : WebsiteStatus.NoWebsite,
                PipelineStageId = defaultStage,
                Tags = string.IsNullOrWhiteSpace(NewBusinessTags) ? null : NewBusinessTags.Trim(),
                SourceProvider = "MANUAL_ENTRY"
            };

            await _businessRepository.AddAsync(newBiz);
            CloseAddBusinessModal();
            await RefreshStatesForCountryAsync(SelectedCountry);
            await RefreshCitiesForStateAsync(SelectedCountry, SelectedState);
            await ApplyFiltersAsync();
            SelectedBusiness = Businesses.FirstOrDefault(b => b.Id == newBiz.Id);
            StatusMessage = $"Added business '{newBiz.Name}'";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Save Error: {ex.Message}";
        }
    }

    // --- Contact Management ---
    [RelayCommand]
    public void OpenAddContactModal()
    {
        if (SelectedBusiness == null) return;
        NewContactFullName = string.Empty;
        NewContactJobTitle = string.Empty;
        NewContactEmail = string.Empty;
        NewContactPhone = string.Empty;
        NewContactIsPrimary = false;
        IsAddContactModalOpen = true;
    }

    [RelayCommand]
    public void CloseAddContactModal()
    {
        IsAddContactModalOpen = false;
    }

    [RelayCommand]
    public async Task SaveNewContact()
    {
        if (SelectedBusiness == null || string.IsNullOrWhiteSpace(NewContactFullName))
        {
            StatusMessage = "Contact Full Name is required";
            return;
        }

        try
        {
            var contact = new BusinessContact
            {
                BusinessId = SelectedBusiness.Id,
                FullName = NewContactFullName.Trim(),
                JobTitle = string.IsNullOrWhiteSpace(NewContactJobTitle) ? null : NewContactJobTitle.Trim(),
                Email = string.IsNullOrWhiteSpace(NewContactEmail) ? null : NewContactEmail.Trim(),
                Phone = string.IsNullOrWhiteSpace(NewContactPhone) ? null : NewContactPhone.Trim(),
                IsPrimary = NewContactIsPrimary
            };

            await _businessRepository.AddContactAsync(contact);
            SelectedBusiness.Contacts.Add(contact);
            CloseAddContactModal();
            StatusMessage = $"Added contact '{contact.FullName}'";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Contact Error: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task DeleteContact(string? contactId)
    {
        if (SelectedBusiness == null || string.IsNullOrWhiteSpace(contactId)) return;

        try
        {
            await _businessRepository.DeleteContactAsync(contactId);
            var item = SelectedBusiness.Contacts.FirstOrDefault(c => c.Id == contactId);
            if (item != null)
            {
                SelectedBusiness.Contacts.Remove(item);
            }
            StatusMessage = "Contact deleted";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Delete Contact Error: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task DeleteBusiness()
    {
        if (SelectedBusiness == null) return;

        try
        {
            var name = SelectedBusiness.Name;
            await _businessRepository.DeleteAsync(SelectedBusiness.Id);
            Businesses.Remove(SelectedBusiness);
            SelectedBusiness = Businesses.FirstOrDefault();
            TotalCount--;
            FilteredCount--;
            StatusMessage = $"Deleted business '{name}'";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Delete Business Error: {ex.Message}";
        }
    }

    // --- Discovery Modal Cascading & Commands (Stage 5) ---

    partial void OnSelectedDiscoveryCountryChanged(string value)
    {
        UpdateDiscoveryStates(value);
        UpdateDiscoveryCities(value, SelectedDiscoveryState);
        UpdateDiscoveryNiches();
    }

    partial void OnSelectedDiscoveryStateChanged(string value)
    {
        UpdateDiscoveryCities(SelectedDiscoveryCountry, value);
        UpdateDiscoveryNiches();
    }

    partial void OnSelectedDiscoveryCityChanged(string value)
    {
        UpdateDiscoveryNiches();
    }

    private void UpdateDiscoveryStates(string country)
    {
        var states = _geoLocationService.GetStatesForCountry(country).Select(s => s.Name).ToList();
        DiscoveryStates = new ObservableCollection<string>(states);
        SelectedDiscoveryState = DiscoveryStates.FirstOrDefault() ?? string.Empty;
    }

    private void UpdateDiscoveryCities(string country, string state)
    {
        var cities = _geoLocationService.GetCitiesForState(country, state).Select(c => c.Name).ToList();
        DiscoveryCities = new ObservableCollection<string>(cities);
        SelectedDiscoveryCity = DiscoveryCities.FirstOrDefault() ?? string.Empty;
    }

    private void UpdateDiscoveryNiches()
    {
        var niches = _geoLocationService.GetSuggestedNiches(SelectedDiscoveryCountry, SelectedDiscoveryCity);
        DiscoverySuggestedNiches = new ObservableCollection<string>(niches);
    }

    [RelayCommand]
    public async Task SelectDiscoveryNiche(string niche)
    {
        if (string.IsNullOrWhiteSpace(niche)) return;
        var preset = MapNicheToCategoryPreset(niche);
        SelectedDiscoveryCategoryPreset = preset;
        DiscoveryCustomKeyword = niche;
        DiscoveryStatus = $"Selected niche '{niche}' ({preset}). Scanning...";
        await StartDiscoveryScan();
    }

    [RelayCommand]
    public void SelectAllDiscoveredLeads()
    {
        foreach (var lead in DiscoveredLeads)
        {
            lead.IsSelectedForImport = !lead.IsDuplicate;
        }
    }

    [RelayCommand]
    public void DeselectAllDiscoveredLeads()
    {
        foreach (var lead in DiscoveredLeads)
        {
            lead.IsSelectedForImport = false;
        }
    }

    [RelayCommand]
    public void SelectDiscoveredWithPhoneOnly()
    {
        foreach (var lead in DiscoveredLeads)
        {
            lead.IsSelectedForImport = !lead.IsDuplicate && !string.IsNullOrWhiteSpace(lead.Business?.PhonePrimary);
        }
    }

    [RelayCommand]
    public void SelectDiscoveredNoWebsiteOnly()
    {
        foreach (var lead in DiscoveredLeads)
        {
            lead.IsSelectedForImport = !lead.IsDuplicate && string.IsNullOrWhiteSpace(lead.Business?.WebsiteUrl);
        }
    }

    public string MapNicheToCategoryPreset(string niche)
    {
        if (string.IsNullOrWhiteSpace(niche)) return "⭐ All Commercial Businesses";
        var n = niche.ToLowerInvariant();

        if (n.Contains("clinic") || n.Contains("health") || n.Contains("dent") || n.Contains("medic") || n.Contains("aesthetic") || n.Contains("hospital") || n.Contains("surgical"))
            return "Healthcare & Dental";
        if (n.Contains("it") || n.Contains("saas") || n.Contains("soft") || n.Contains("tech") || n.Contains("digital") || n.Contains("cloud") || n.Contains("cyber"))
            return "Software & IT Services";
        if (n.Contains("law") || n.Contains("legal") || n.Contains("attorney") || n.Contains("barrister") || n.Contains("consultant") || n.Contains("visa") || n.Contains("diplomatic"))
            return "Lawyers & Legal";
        if (n.Contains("real estate") || n.Contains("housing") || n.Contains("propert") || n.Contains("estate") || n.Contains("realt") || n.Contains("develop"))
            return "Real Estate & Property";
        if (n.Contains("hotel") || n.Contains("resort") || n.Contains("tourism") || n.Contains("hospitality") || n.Contains("lodg") || n.Contains("travel"))
            return "Hotels & Lodging";
        if (n.Contains("rest") || n.Contains("food") || n.Contains("cafe") || n.Contains("dining") || n.Contains("gourmet") || n.Contains("spices") || n.Contains("bakery"))
            return "Restaurants & Food";
        if (n.Contains("textile") || n.Contains("apparel") || n.Contains("garment") || n.Contains("boutique") || n.Contains("fashion") || n.Contains("fabric"))
            return "Textile, Apparel & Fashion";
        if (n.Contains("solar") || n.Contains("energy") || n.Contains("power") || n.Contains("renew"))
            return "Solar & Renewable Energy";
        if (n.Contains("car") || n.Contains("auto") || n.Contains("vehicle") || n.Contains("mechanic") || n.Contains("dealership") || n.Contains("spare"))
            return "Automotive & Mechanics";
        if (n.Contains("school") || n.Contains("college") || n.Contains("educat") || n.Contains("academ") || n.Contains("board"))
            return "Education & Schools";
        if (n.Contains("construct") || n.Contains("builder") || n.Contains("engineer") || n.Contains("contractor") || n.Contains("marble") || n.Contains("mining"))
            return "Construction & Contractors";
        if (n.Contains("agro") || n.Contains("farm") || n.Contains("agricultur") || n.Contains("seed") || n.Contains("crop") || n.Contains("fertilizer"))
            return "Agricultural & Agro-Chemical";
        if (n.Contains("bank") || n.Contains("financ") || n.Contains("invest") || n.Contains("insur") || n.Contains("islamic"))
            return "Financial & Insurance";
        if (n.Contains("account") || n.Contains("tax") || n.Contains("audit"))
            return "Accountants & Tax";
        if (n.Contains("salon") || n.Contains("spa") || n.Contains("beauty"))
            return "Beauty & Salons";
        if (n.Contains("event") || n.Contains("wedding") || n.Contains("marriage") || n.Contains("banquet"))
            return "Event Planners & Marriage Halls";
        if (n.Contains("print") || n.Contains("packag") || n.Contains("sign"))
            return "Printing, Packaging & Signage";
        if (n.Contains("pet") || n.Contains("vet"))
            return "Pet Care & Veterinary Clinics";
        if (n.Contains("sports") || n.Contains("fitness") || n.Contains("gym"))
            return "Fitness & Gyms";
        if (n.Contains("retail") || n.Contains("store") || n.Contains("shop") || n.Contains("trade") || n.Contains("market") || n.Contains("logistics") || n.Contains("cargo") || n.Contains("port"))
            return "Commercial & Retail";

        return DiscoveryCategoryPresets.FirstOrDefault(p => p.Contains(niche, StringComparison.OrdinalIgnoreCase)) ?? "⭐ All Commercial Businesses";
    }

    [RelayCommand]
    public void OpenDiscoveryModal()
    {
        DiscoveredLeads.Clear();
        DiscoveredTotal = 0;
        DiscoveredNew = 0;
        DiscoveredDuplicates = 0;
        IsDiscoveryNearbyMode = false;
        UpdateDiscoveryStates(SelectedDiscoveryCountry);
        UpdateDiscoveryCities(SelectedDiscoveryCountry, SelectedDiscoveryState);
        UpdateDiscoveryNiches();
        DiscoveryStatus = $"Ready to scan multi-source channels for '{SelectedDiscoveryCategoryPreset}' in {SelectedDiscoveryCity}, {SelectedDiscoveryCountry}.";
        IsDiscoveryModalOpen = true;
    }

    [RelayCommand]
    public void CloseDiscoveryModal()
    {
        IsDiscoveryModalOpen = false;
    }

    [RelayCommand]
    public async Task SetDiscoveryNearby()
    {
        try
        {
            IsDiscovering = true;
            DiscoveryStatus = "📍 Detecting proximity for Discovery...";
            var loc = await _geoLocationService.DetectNearbyLocationAsync(DiscoveryRadiusKm);
            IsDiscoveryNearbyMode = true;

            var matchCountry = DiscoveryCountries.FirstOrDefault(c => c.Equals(loc.Country, StringComparison.OrdinalIgnoreCase));
            if (matchCountry != null) SelectedDiscoveryCountry = matchCountry;

            UpdateDiscoveryStates(loc.Country);
            var matchState = DiscoveryStates.FirstOrDefault(s => s.Equals(loc.StateProvince, StringComparison.OrdinalIgnoreCase));
            if (matchState != null) SelectedDiscoveryState = matchState;

            UpdateDiscoveryCities(loc.Country, loc.StateProvince);
            var matchCity = DiscoveryCities.FirstOrDefault(c => c.Equals(loc.City, StringComparison.OrdinalIgnoreCase));
            if (matchCity != null) SelectedDiscoveryCity = matchCity;

            DiscoveryStatus = $"📍 Nearby Discovery active: {DiscoveryRadiusKm} km radius around {loc.City} ({loc.Latitude:F4}, {loc.Longitude:F4})";
        }
        catch (Exception ex)
        {
            DiscoveryStatus = $"Proximity Error: {ex.Message}";
        }
        finally
        {
            IsDiscovering = false;
        }
    }

    [RelayCommand]
    public async Task StartDiscoveryScan()
    {
        try
        {
            IsDiscovering = true;
            DiscoveryStatus = $"Scanning multi-source channels ({SelectedDiscoveryEngine}) for '{SelectedDiscoveryCategoryPreset}' in {SelectedDiscoveryCity}, {SelectedDiscoveryCountry}...";

            var presets = _discoveryService.GetPresetCategories();
            List<string> targetAmenities = ["clinic", "dentist", "pharmacy"];
            if (presets.TryGetValue(SelectedDiscoveryCategoryPreset, out var list))
            {
                targetAmenities = list;
            }

            // If All Commercial preset selected, pass all_commercial flag
            if (SelectedDiscoveryCategoryPreset.Contains("All Commercial", StringComparison.OrdinalIgnoreCase))
            {
                targetAmenities = ["all_commercial"];
            }

            BoundingBox? bbox = null;
            if (IsDiscoveryNearbyMode)
            {
                var cityInfo = _geoLocationService.FindCity(SelectedDiscoveryCity, SelectedDiscoveryCountry);
                if (cityInfo != null)
                {
                    bbox = _geoLocationService.CalculateProximityBoundingBox(cityInfo.Latitude, cityInfo.Longitude, DiscoveryRadiusKm);
                }
            }

            int batchLimit = 50;
            if (SelectedDiscoveryBatchLimit.StartsWith("25")) batchLimit = 25;
            else if (SelectedDiscoveryBatchLimit.StartsWith("50")) batchLimit = 50;
            else if (SelectedDiscoveryBatchLimit.StartsWith("100")) batchLimit = 100;
            else if (SelectedDiscoveryBatchLimit.StartsWith("200")) batchLimit = 200;

            var query = new DiscoveryQuery
            {
                Country = SelectedDiscoveryCountry,
                StateProvince = SelectedDiscoveryState,
                City = SelectedDiscoveryCity,
                TargetAmenities = targetAmenities,
                BBox = bbox,
                Limit = batchLimit,
                TimeoutSeconds = 45,
                SearchEngine = SelectedDiscoveryEngine,
                CategoryPreset = SelectedDiscoveryCategoryPreset,
                CustomSearchTerm = DiscoveryCustomKeyword,
                RequirePhone = DiscoveryRequirePhone,
                TargetMissingWebsiteOnly = DiscoveryTargetMissingWebsite,
                TargetMissingGmbOnly = DiscoveryTargetMissingGmb
            };

            var result = await _discoveryService.DiscoverLeadsAsync(query);

            if (result.HasError)
            {
                DiscoveryStatus = $"Discovery failed: {result.ErrorMessage}";
                return;
            }

            DiscoveredLeads = new ObservableCollection<DiscoveredLead>(result.Leads);
            DiscoveredTotal = result.TotalFetched;
            DiscoveredNew = result.NewCount;
            DiscoveredDuplicates = result.DuplicateCount;

            if (DiscoveredTotal > 0)
            {
                DiscoveryStatus = $"Scan complete in {result.ExecutionDuration.TotalSeconds:F1}s via {result.Provider}: {DiscoveredTotal} found ({DiscoveredNew} new, {DiscoveredDuplicates} duplicates).";
            }
            else
            {
                DiscoveryStatus = $"Scan complete in {result.ExecutionDuration.TotalSeconds:F1}s: No matching businesses found in {SelectedDiscoveryCity}. Try selecting '⭐ All Commercial Businesses' or clearing the custom keyword.";
            }
        }
        catch (Exception ex)
        {
            DiscoveryStatus = $"Error: {ex.Message}";
        }
        finally
        {
            IsDiscovering = false;
        }
    }

    [RelayCommand]
    public async Task ExecuteDiscoveryImport()
    {
        var toImport = DiscoveredLeads.Where(l => l.IsSelectedForImport && !l.IsDuplicate).ToList();
        if (toImport.Count == 0)
        {
            DiscoveryStatus = "⚠️ No new leads selected for import. (Duplicates are skipped automatically).";
            return;
        }

        try
        {
            IsDiscovering = true;
            DiscoveryStatus = $"Importing {toImport.Count} leads into local SQLite database...";

            var result = await _discoveryService.ImportDiscoveredLeadsAsync(
                DiscoveredLeads,
                DeduplicationStrategy.Skip);

            StatusMessage = $"Discovery Import: {result.CreatedCount} new leads added to database ({result.SkippedCount} duplicates skipped)";
            CloseDiscoveryModal();

            // Reset filters to "All" so the newly imported leads are immediately visible to the operator
            SelectedCountry = "All Countries";
            SelectedState = "All States/Provinces";
            SelectedCity = "All Cities";
            SelectedCategory = "All Categories";
            SearchQuery = string.Empty;

            await RefreshStatesForCountryAsync(SelectedCountry);
            await RefreshCitiesForStateAsync(SelectedCountry, SelectedState);
            await ApplyFiltersAsync();
        }
        catch (Exception ex)
        {
            DiscoveryStatus = $"Import Error: {ex.Message}";
        }
        finally
        {
            IsDiscovering = false;
        }
    }

    // --- Google Places Enrichment Commands (Stage 7) ---
    [RelayCommand]
    public async Task OpenEnrichConfirm()
    {
        if (SelectedBusiness == null) return;

        var config = await _placesService.GetConfigAsync();
        if (!config.IsEnabled)
        {
            EnrichConfirmMessage = "Google Places enrichment is currently disabled.\n\nPlease enable it in Settings & Vault and enter an API key to perform commercial lookups.";
        }
        else if (SelectedBusiness.IsGoogleVerified && SelectedBusiness.LastEnrichedAtUtc.HasValue && (DateTime.UtcNow - SelectedBusiness.LastEnrichedAtUtc.Value).TotalDays < 30)
        {
            EnrichConfirmMessage = $"'{SelectedBusiness.Name}' was already enriched on Google Places within the last 30 days.\n\n" +
                                   $"Current Rating: ★ {SelectedBusiness.GoogleRating:F1} ({SelectedBusiness.GoogleReviewCount} reviews)\n\n" +
                                   "Running enrichment now will retrieve cached data at $0 cost without using any API quota.";
        }
        else
        {
            EnrichConfirmMessage = $"Enrich '{SelectedBusiness.Name}' with verified Google Places data.\n\n" +
                                   $"• SKU Tier: {config.SkuTier}\n" +
                                   $"• Daily Ceiling: {config.DailyRequestsUsed} / {config.DailyRequestCeiling} ({config.DailyRequestsRemaining} remaining today)\n" +
                                   $"• Monthly Free Cap: {config.MonthlyRequestsUsed} / {config.MonthlyFreeAllowance:N0} ({config.MonthlyRequestsRemaining:N0} remaining this month)\n\n" +
                                   "Strict Zero-Cost Guard guarantees $0.00 spend on Google Cloud. Do you wish to continue?";
        }

        IsEnrichConfirmModalOpen = true;
    }

    [RelayCommand]
    public void CloseEnrichConfirm()
    {
        IsEnrichConfirmModalOpen = false;
    }

    [RelayCommand]
    public async Task ExecuteGooglePlacesEnrichment()
    {
        if (SelectedBusiness == null) return;

        try
        {
            IsEnrichingWithGoogle = true;
            var result = await _placesService.EnrichBusinessAsync(SelectedBusiness, forceRefresh: false);

            if (result.IsSuccess)
            {
                if (result.IsCached)
                {
                    StatusMessage = $"Enrichment retrieved from cache: {result.BusinessName} (★ {result.Rating:F1}, {result.UserRatingsTotal} reviews). Quota untouched.";
                }
                else
                {
                    StatusMessage = $"Enriched '{result.BusinessName}' via Google Places (New) (★ {result.Rating:F1}, {result.UserRatingsTotal} reviews). Today: {result.RequestsUsedToday}/{result.DailyLimit} | Month: {result.MonthlyRequestsUsed}/{result.MonthlyLimit:N0}.";
                }

                // Refresh business from repository to update all UI properties
                var updated = await _businessRepository.GetByIdAsync(SelectedBusiness.Id);
                if (updated != null)
                {
                    var index = Businesses.IndexOf(SelectedBusiness);
                    if (index >= 0)
                    {
                        Businesses[index] = updated;
                        SelectedBusiness = updated;
                    }
                }
                CloseEnrichConfirm();
            }
            else
            {
                EnrichConfirmMessage = $"Enrichment could not complete:\n\n{result.ErrorMessage}";
            }
        }
        catch (Exception ex)
        {
            EnrichConfirmMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsEnrichingWithGoogle = false;
        }
    }
}
