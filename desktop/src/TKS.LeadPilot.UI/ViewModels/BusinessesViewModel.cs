using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TKS.LeadPilot.Core.Enums;
using TKS.LeadPilot.Core.Interfaces;
using TKS.LeadPilot.Core.Models;

namespace TKS.LeadPilot.UI.ViewModels;

public partial class BusinessesViewModel : ViewModelBase
{
    private readonly IBusinessRepository _businessRepository;
    private readonly IPipelineStageRepository _pipelineStageRepository;
    private readonly ICsvImporterService _csvImporterService;
    private readonly IDeduplicationService _deduplicationService;
    private readonly IDiscoveryService _discoveryService;
    private readonly IGooglePlacesEnrichmentService _placesService;
    private readonly IGeoLocationService _geoLocationService;

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
        IGeoLocationService geoLocationService)
    {
        _businessRepository = businessRepository;
        _pipelineStageRepository = pipelineStageRepository;
        _csvImporterService = csvImporterService;
        _deduplicationService = deduplicationService;
        _discoveryService = discoveryService;
        _placesService = placesService;
        _geoLocationService = geoLocationService;

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

            // Discovery presets
            DiscoveryCategoryPresets = new ObservableCollection<string>(_discoveryService.GetPresetCategories().Keys);
            SelectedDiscoveryCategoryPreset = DiscoveryCategoryPresets.FirstOrDefault() ?? "Healthcare & Dental";

            // Initialize Discovery Locations
            UpdateDiscoveryStates("Pakistan");
            UpdateDiscoveryCities("Pakistan", "Punjab");

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
        }
    }

    // --- Cascading Change Handlers for Main View ---

    partial void OnSelectedCountryChanged(string value)
    {
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
        _ = Task.Run(async () =>
        {
            await RefreshCitiesForStateAsync(SelectedCountry, value);
            RefreshSuggestedNiches();
            await ApplyFiltersAsync();
        });
    }

    partial void OnSelectedCityChanged(string value)
    {
        RefreshSuggestedNiches();
        _ = ApplyFiltersAsync();
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
            "🔥 Hot Leads" => QuickFilterType.HotLeads,
            "❌ No Website" => QuickFilterType.NoWebsite,
            "📍 Missing GMB" or "Missing GMB" => QuickFilterType.MissingGmb,
            "✓ Verified" => QuickFilterType.GoogleVerified,
            "⭐ Top Rated" => QuickFilterType.TopRated,
            "Needs Audit" => QuickFilterType.NeedsAudit,
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
        SearchQuery = niche;
        await ApplyFiltersAsync();
    }

    // --- Nearby Me Proximity Detection ---

    [RelayCommand]
    public async Task DetectNearbyLocation()
    {
        try
        {
            IsDetectingLocation = true;
            StatusMessage = "📍 Detecting your approximate location...";

            var nearby = await _geoLocationService.DetectNearbyLocationAsync(NearbyRadiusKm);
            IsNearbyActive = true;

            // Select Country, State, City
            var matchedCountry = Countries.FirstOrDefault(c => c.Contains(nearby.Country, StringComparison.OrdinalIgnoreCase));
            if (matchedCountry != null)
            {
                SelectedCountry = matchedCountry;
            }

            await RefreshStatesForCountryAsync(nearby.Country);
            var matchedState = States.FirstOrDefault(s => s.Equals(nearby.StateProvince, StringComparison.OrdinalIgnoreCase));
            if (matchedState != null)
            {
                SelectedState = matchedState;
            }

            await RefreshCitiesForStateAsync(nearby.Country, nearby.StateProvince);
            var matchedCity = Cities.FirstOrDefault(c => c.Equals(nearby.City, StringComparison.OrdinalIgnoreCase));
            if (matchedCity != null)
            {
                SelectedCity = matchedCity;
            }

            StatusMessage = $"📍 Proximity Mode: within {nearby.RadiusKm} km of {nearby.City}, {nearby.StateProvince} ({nearby.Country})";
            await ApplyFiltersAsync();
        }
        catch (Exception ex)
        {
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

            var criteria = new BusinessFilterCriteria
            {
                SearchQuery = string.IsNullOrWhiteSpace(SearchQuery) ? null : SearchQuery,
                Country = rawCountry != "All Countries" ? rawCountry : null,
                StateProvince = SelectedState != "All States/Provinces" ? SelectedState : null,
                City = SelectedCity != "All Cities" ? SelectedCity : null,
                Category = SelectedCategory != "All Categories" ? SelectedCategory : null,
                PipelineStageId = SelectedStage?.Id,
                HasWebsite = SelectedWebsiteFilter == "Has Website" ? true : SelectedWebsiteFilter == "No Website" ? false : null,
                QuickFilter = SelectedQuickFilter,
                Take = 500
            };

            var list = await _businessRepository.GetFilteredAsync(criteria);
            Businesses = new ObservableCollection<Business>(list);
            FilteredCount = list.Count;
            TotalCount = await _businessRepository.GetTotalCountAsync();
            HotLeadsCount = await _businessRepository.GetHighOpportunityCountAsync(70);
            VerifiedCount = Businesses.Count(b => b.IsGoogleVerified);

            SelectedBusiness = Businesses.FirstOrDefault();
            StatusMessage = $"Showing {FilteredCount} of {TotalCount} leads • {HotLeadsCount} High Opportunity • {VerifiedCount} Google Verified";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Filter Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
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
        }
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
    public void SelectDiscoveryNiche(string niche)
    {
        if (string.IsNullOrWhiteSpace(niche)) return;
        var preset = DiscoveryCategoryPresets.FirstOrDefault(p =>
            niche.Contains(p.Split(' ')[0], StringComparison.OrdinalIgnoreCase));
        if (preset != null)
        {
            SelectedDiscoveryCategoryPreset = preset;
        }
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
        DiscoveryStatus = $"Ready to scan for {SelectedDiscoveryCategoryPreset} in {SelectedDiscoveryCity}, {SelectedDiscoveryCountry}.";
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
            DiscoveryStatus = $"Querying OpenStreetMap Overpass servers for {SelectedDiscoveryCategoryPreset} in {SelectedDiscoveryCity}, {SelectedDiscoveryCountry}...";

            var presets = _discoveryService.GetPresetCategories();
            List<string> targetAmenities = ["clinic", "dentist", "pharmacy"];
            if (presets.TryGetValue(SelectedDiscoveryCategoryPreset, out var list))
            {
                targetAmenities = list;
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

            var query = new DiscoveryQuery
            {
                Country = SelectedDiscoveryCountry,
                StateProvince = SelectedDiscoveryState,
                City = SelectedDiscoveryCity,
                TargetAmenities = targetAmenities,
                BBox = bbox,
                Limit = 150,
                TimeoutSeconds = 30
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
                DiscoveryStatus = $"Scan complete in {result.ExecutionDuration.TotalSeconds:F1}s: No matching businesses found in {SelectedDiscoveryCity}.";
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
        if (DiscoveredLeads.Count == 0) return;

        try
        {
            IsDiscovering = true;
            DiscoveryStatus = "Importing selected leads into local SQLite database...";

            var result = await _discoveryService.ImportDiscoveredLeadsAsync(
                DiscoveredLeads,
                DiscoveryDedupStrategy);

            StatusMessage = $"Discovery Import: {result.CreatedCount} created, {result.UpdatedCount} merged, {result.SkippedCount} skipped";
            CloseDiscoveryModal();
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
            var remaining = Math.Max(0, config.DailyRequestCeiling - config.DailyRequestsUsed);
            EnrichConfirmMessage = $"Enrich '{SelectedBusiness.Name}' with verified Google Places data (Rating, Reviews, Address, Hours).\n\n" +
                                   $"• Daily Ceiling: {config.DailyRequestCeiling} requests/day\n" +
                                   $"• Used Today: {config.DailyRequestsUsed}\n" +
                                   $"• Remaining: {remaining}\n\n" +
                                   "Strict Cost Guard is active to prevent unexpected billing. Do you wish to continue?";
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
                    StatusMessage = $"Enriched '{result.BusinessName}' via Google Places (★ {result.Rating:F1}, {result.UserRatingsTotal} reviews). Daily quota: {result.RequestsUsedToday}/{result.DailyLimit}.";
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
