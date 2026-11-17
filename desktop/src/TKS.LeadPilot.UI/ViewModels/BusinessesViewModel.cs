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

    // Collections
    [ObservableProperty]
    private ObservableCollection<Business> _businesses = [];

    [ObservableProperty]
    private Business? _selectedBusiness;

    [ObservableProperty]
    private ObservableCollection<string> _cities = ["All Cities"];

    [ObservableProperty]
    private string _selectedCity = "All Cities";

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

    // Discovery State (Stage 5)
    [ObservableProperty]
    private ObservableCollection<string> _discoveryCities = ["Karachi", "Lahore", "Islamabad", "Rawalpindi", "Faisalabad", "Peshawar", "Multan"];

    [ObservableProperty]
    private string _selectedDiscoveryCity = "Karachi";

    [ObservableProperty]
    private ObservableCollection<string> _discoveryCategoryPresets = ["Healthcare & Dental", "Restaurants & Food", "Hotels & Lodging", "Retail & Shopping", "Automotive & Repair", "Education & Training"];

    [ObservableProperty]
    private string _selectedDiscoveryCategoryPreset = "Healthcare & Dental";

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
    private string _newBusinessCity = string.Empty;

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
        IGooglePlacesEnrichmentService placesService)
    {
        _businessRepository = businessRepository;
        _pipelineStageRepository = pipelineStageRepository;
        _csvImporterService = csvImporterService;
        _deduplicationService = deduplicationService;
        _discoveryService = discoveryService;
        _placesService = placesService;

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

            // Load Distinct Cities & Categories
            await RefreshFilterOptionsAsync();

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

    private async Task RefreshFilterOptionsAsync()
    {
        var cityList = await _businessRepository.GetDistinctCitiesAsync();
        var catList = await _businessRepository.GetDistinctCategoriesAsync();

        Cities = new ObservableCollection<string>(new[] { "All Cities" }.Concat(cityList));
        Categories = new ObservableCollection<string>(new[] { "All Categories" }.Concat(catList));
    }

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
            var criteria = new BusinessFilterCriteria
            {
                SearchQuery = string.IsNullOrWhiteSpace(SearchQuery) ? null : SearchQuery,
                City = SelectedCity != "All Cities" ? SelectedCity : null,
                Category = SelectedCategory != "All Categories" ? SelectedCategory : null,
                PipelineStageId = SelectedStage?.Id,
                HasWebsite = SelectedWebsiteFilter == "Has Website" ? true : SelectedWebsiteFilter == "No Website" ? false : null,
                Take = 500
            };

            var list = await _businessRepository.GetFilteredAsync(criteria);
            Businesses = new ObservableCollection<Business>(list);
            FilteredCount = list.Count;
            TotalCount = await _businessRepository.GetTotalCountAsync();

            SelectedBusiness = Businesses.FirstOrDefault();
            StatusMessage = $"Displaying {FilteredCount} of {TotalCount} businesses";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
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
        SelectedCity = "All Cities";
        SelectedCategory = "All Categories";
        SelectedStage = null;
        SelectedWebsiteFilter = "All Sites";
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
            await RefreshFilterOptionsAsync();
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
        NewBusinessCity = string.Empty;
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
            await RefreshFilterOptionsAsync();
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

    // --- Discovery Commands (Stage 5) ---
    [RelayCommand]
    public void OpenDiscoveryModal()
    {
        DiscoveredLeads.Clear();
        DiscoveredTotal = 0;
        DiscoveredNew = 0;
        DiscoveredDuplicates = 0;
        DiscoveryStatus = "Select target city and category preset to begin scan.";
        IsDiscoveryModalOpen = true;
    }

    [RelayCommand]
    public void CloseDiscoveryModal()
    {
        IsDiscoveryModalOpen = false;
    }

    [RelayCommand]
    public async Task StartDiscoveryScan()
    {
        try
        {
            IsDiscovering = true;
            DiscoveryStatus = $"Querying OpenStreetMap Overpass servers for {SelectedDiscoveryCategoryPreset} in {SelectedDiscoveryCity}...";

            var presets = _discoveryService.GetPresetCategories();
            List<string> targetAmenities = ["clinic", "dentist", "pharmacy"];
            if (presets.TryGetValue(SelectedDiscoveryCategoryPreset, out var list))
            {
                targetAmenities = list;
            }

            var query = new DiscoveryQuery
            {
                City = SelectedDiscoveryCity,
                TargetAmenities = targetAmenities,
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

            DiscoveryStatus = $"Discovery scan complete in {result.ExecutionDuration.TotalSeconds:F1}s: {DiscoveredTotal} found ({DiscoveredNew} new, {DiscoveredDuplicates} duplicates).";
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
            await RefreshFilterOptionsAsync();
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
