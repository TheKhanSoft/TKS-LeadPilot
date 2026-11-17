using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TKS.LeadPilot.Core.Interfaces;
using TKS.LeadPilot.Core.Models;

namespace TKS.LeadPilot.UI.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private const string GooglePlacesKeyName = "GOOGLE_PLACES_API_KEY";
    private readonly ISecretVault _secretVault;
    private readonly IGooglePlacesEnrichmentService _placesService;
    private readonly IAiProposalSynthesizer _aiSynthesizer;
    private readonly ISyncService _syncService;

    // Gemini Settings (Stage 8)
    [ObservableProperty]
    private string _geminiApiKey = string.Empty;

    [ObservableProperty]
    private bool _isAiDisabled = false;

    [ObservableProperty]
    private int _dailyAiLimit = 1200;

    [ObservableProperty]
    private int _dailyAiUsedToday = 0;

    [ObservableProperty]
    private string _selectedGeminiModel = "gemini-1.5-flash";

    // Google Places Settings (Stage 7 - Google Places API New)
    [ObservableProperty]
    private string _googlePlacesApiKey = string.Empty;

    [ObservableProperty]
    private bool _isGooglePlacesEnabled = false;

    [ObservableProperty]
    private int _googlePlacesDailyCeiling = 50;

    [ObservableProperty]
    private int _googlePlacesUsedToday = 0;

    [ObservableProperty]
    private int _googlePlacesMonthlyUsed = 0;

    [ObservableProperty]
    private int _googlePlacesMonthlyLimit = 1000;

    [ObservableProperty]
    private int _googlePlacesMonthlyRemaining = 1000;

    [ObservableProperty]
    private int _googlePlacesDailyRemaining = 50;

    [ObservableProperty]
    private string _selectedGooglePlacesSkuTier = "Enterprise + Atmosphere (1,000 Free/Mo)";

    [ObservableProperty]
    private ObservableCollection<string> _googlePlacesSkuTiers = new([
        "Enterprise + Atmosphere (1,000 Free/Mo)",
        "Essentials (10,000 Free/Mo)"
    ]);

    [ObservableProperty]
    private bool _isTestingPlacesConnection = false;

    // Groq Cloud Free AI Engine Settings (100% Free - Fast Llama 3)
    [ObservableProperty]
    private string _groqApiKey = string.Empty;

    [ObservableProperty]
    private string _selectedGroqModel = "llama-3.3-70b-versatile";

    [ObservableProperty]
    private bool _isGroqEnabled = true;

    // Serper.dev Google Places (Free Tier - 2,500 Queries)
    [ObservableProperty]
    private string _serperApiKey = string.Empty;

    [ObservableProperty]
    private bool _isSerperEnabled = false;

    // Central Laravel Sync Settings (Stage 11)
    [ObservableProperty]
    private string _syncApiBaseUrl = "http://127.0.0.1:8000/api/v1";

    [ObservableProperty]
    private string _syncApiToken = string.Empty;

    [ObservableProperty]
    private string _syncDeviceId = string.Empty;

    [ObservableProperty]
    private string _syncDeviceName = Environment.MachineName;

    [ObservableProperty]
    private string _syncLastSyncedAt = "Never";

    [ObservableProperty]
    private bool _isSyncOnline = false;

    [ObservableProperty]
    private bool _isSyncing = false;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public SettingsViewModel(
        ISecretVault secretVault,
        IGooglePlacesEnrichmentService placesService,
        IAiProposalSynthesizer aiSynthesizer,
        ISyncService syncService)
    {
        _secretVault = secretVault;
        _placesService = placesService;
        _aiSynthesizer = aiSynthesizer;
        _syncService = syncService;
        _ = LoadSettingsAsync();
    }

    private async Task LoadSettingsAsync()
    {
        var key = _secretVault.GetSecret("GeminiApiKey") ?? _secretVault.GetSecret("GEMINI_API_KEY");
        if (!string.IsNullOrEmpty(key))
        {
            GeminiApiKey = key.Length > 10 ? $"{key[..6]}...{key[^4..]}" : "********";
        }

        var aiConfig = await _aiSynthesizer.GetConfigAsync();
        IsAiDisabled = !aiConfig.IsEnabled;
        DailyAiLimit = aiConfig.DailyRequestCeiling;
        DailyAiUsedToday = aiConfig.DailyRequestsUsed;
        SelectedGeminiModel = aiConfig.ModelName;

        var placesKey = _secretVault.GetSecret(GooglePlacesKeyName);
        if (!string.IsNullOrEmpty(placesKey))
        {
            GooglePlacesApiKey = placesKey.Length > 10 ? $"{placesKey[..6]}...{placesKey[^4..]}" : "********";
        }

        var placesConfig = await _placesService.GetConfigAsync();
        IsGooglePlacesEnabled = placesConfig.IsEnabled;
        GooglePlacesDailyCeiling = placesConfig.DailyRequestCeiling;
        GooglePlacesUsedToday = placesConfig.DailyRequestsUsed;
        GooglePlacesMonthlyUsed = placesConfig.MonthlyRequestsUsed;
        GooglePlacesMonthlyLimit = placesConfig.MonthlyFreeAllowance;
        GooglePlacesMonthlyRemaining = placesConfig.MonthlyRequestsRemaining;
        GooglePlacesDailyRemaining = placesConfig.DailyRequestsRemaining;
        SelectedGooglePlacesSkuTier = placesConfig.SkuTier == PlacesSkuTier.Essentials
            ? "Essentials (10,000 Free/Mo)"
            : "Enterprise + Atmosphere (1,000 Free/Mo)";

        // Groq Cloud AI Settings
        var groqKey = _secretVault.GetSecret("GROQ_API_KEY");
        if (!string.IsNullOrEmpty(groqKey))
        {
            GroqApiKey = groqKey.Length > 10 ? $"{groqKey[..6]}...{groqKey[^4..]}" : "********";
        }
        var groqModel = _secretVault.GetSecret("GROQ_MODEL");
        if (!string.IsNullOrEmpty(groqModel))
        {
            SelectedGroqModel = groqModel;
        }

        // Serper.dev Settings
        var serperKey = _secretVault.GetSecret("SERPER_API_KEY");
        if (!string.IsNullOrEmpty(serperKey))
        {
            SerperApiKey = serperKey.Length > 10 ? $"{serperKey[..6]}...{serperKey[^4..]}" : "********";
            IsSerperEnabled = true;
        }

        // Load Sync Settings (Stage 11)
        var syncConfig = await _syncService.GetConfigAsync();
        SyncApiBaseUrl = syncConfig.ApiBaseUrl;
        SyncDeviceId = syncConfig.DeviceId;
        SyncDeviceName = syncConfig.DeviceName;
        SyncLastSyncedAt = syncConfig.LastSyncedAtUtc.HasValue 
            ? syncConfig.LastSyncedAtUtc.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")
            : "Never";

        if (!string.IsNullOrEmpty(syncConfig.ApiToken))
        {
            SyncApiToken = syncConfig.ApiToken.Length > 8 ? $"{syncConfig.ApiToken[..4]}...{syncConfig.ApiToken[^4..]}" : "********";
        }
    }

    [RelayCommand]
    public async Task SaveApiKey()
    {
        try
        {
            var config = new GeminiConfig
            {
                IsEnabled = !IsAiDisabled,
                ModelName = SelectedGeminiModel,
                DailyRequestCeiling = DailyAiLimit
            };

            string? keyToSave = null;
            if (!string.IsNullOrWhiteSpace(GeminiApiKey) && !GeminiApiKey.Contains("..."))
            {
                keyToSave = GeminiApiKey.Trim();
            }

            await _aiSynthesizer.SaveConfigAsync(config, keyToSave);
            StatusMessage = "✅ Gemini AI settings and safety limits saved successfully.";
            await LoadSettingsAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error saving Gemini settings: {ex.Message}";
        }
    }

    [RelayCommand]
    public void TestAiConnection()
    {
        if (!_secretVault.HasSecret("GeminiApiKey") && !_secretVault.HasSecret("GEMINI_API_KEY"))
        {
            StatusMessage = "❌ No Gemini API key found in vault. Please save a key first.";
            return;
        }

        StatusMessage = "✅ Gemini API Key is configured in vault. Ready for Free Tier calls.";
    }

    // Google Places Commands (Stage 7 - Places API New)
    [RelayCommand]
    public async Task SaveGooglePlacesSettings()
    {
        try
        {
            var tier = SelectedGooglePlacesSkuTier != null && SelectedGooglePlacesSkuTier.Contains("Essentials", StringComparison.OrdinalIgnoreCase)
                ? PlacesSkuTier.Essentials
                : PlacesSkuTier.EnterpriseWithAtmosphere;

            var config = new GooglePlacesConfig
            {
                IsEnabled = IsGooglePlacesEnabled,
                SkuTier = tier,
                DailyRequestCeiling = GooglePlacesDailyCeiling
            };

            string? keyToSave = null;
            if (!string.IsNullOrWhiteSpace(GooglePlacesApiKey) && !GooglePlacesApiKey.Contains("..."))
            {
                keyToSave = GooglePlacesApiKey.Trim();
            }

            await _placesService.SaveConfigAsync(config, keyToSave);
            StatusMessage = $"✅ Google Places (New) settings saved: {tier} tier ({config.MonthlyFreeAllowance:N0} free/month).";
            await LoadSettingsAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error saving Google Places settings: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task TestGooglePlacesConnection()
    {
        try
        {
            IsTestingPlacesConnection = true;
            StatusMessage = "Testing Google Places API (New) credentials and endpoint...";

            if (!string.IsNullOrWhiteSpace(GooglePlacesApiKey) && !GooglePlacesApiKey.Contains("..."))
            {
                _secretVault.SaveSecret(GooglePlacesKeyName, GooglePlacesApiKey.Trim());
            }

            var (success, msg) = await _placesService.TestApiConnectionAsync();
            StatusMessage = msg;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Connection test error: {ex.Message}";
        }
        finally
        {
            IsTestingPlacesConnection = false;
        }
    }

    [RelayCommand]
    public async Task ResetGooglePlacesCounters()
    {
        try
        {
            await _placesService.ResetUsageCountersAsync();
            StatusMessage = "✅ Google Places request counters successfully reset to 0.";
            await LoadSettingsAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error resetting counters: {ex.Message}";
        }
    }

    // Groq Cloud Free AI Commands
    [RelayCommand]
    public async Task SaveGroqSettings()
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(GroqApiKey) && !GroqApiKey.Contains("..."))
            {
                _secretVault.SaveSecret("GROQ_API_KEY", GroqApiKey.Trim());
            }
            if (!string.IsNullOrWhiteSpace(SelectedGroqModel))
            {
                _secretVault.SaveSecret("GROQ_MODEL", SelectedGroqModel.Trim());
            }

            StatusMessage = "✅ Groq Cloud Free AI settings saved to vault successfully.";
            await LoadSettingsAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error saving Groq settings: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task TestGroqConnection()
    {
        var key = _secretVault.GetSecret("GROQ_API_KEY");
        if (string.IsNullOrWhiteSpace(key))
        {
            StatusMessage = "❌ No Groq API key in vault. Please save a free key from console.groq.com first.";
            return;
        }

        StatusMessage = "Testing Groq Cloud connection...";
        try
        {
            using var client = new System.Net.Http.HttpClient();
            using var req = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Get, "https://api.groq.com/openai/v1/models");
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", key);
            var resp = await client.SendAsync(req);
            if (resp.IsSuccessStatusCode)
            {
                StatusMessage = "✅ Groq Cloud AI connection verified! 100% Free ultra-fast Llama-3 ready.";
            }
            else
            {
                StatusMessage = $"⚠️ Groq API responded with status: {resp.StatusCode}. Please verify your key.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Groq test failed: {ex.Message}";
        }
    }

    // Serper.dev Google Search Commands
    [RelayCommand]
    public async Task SaveSerperSettings()
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(SerperApiKey) && !SerperApiKey.Contains("..."))
            {
                _secretVault.SaveSecret("SERPER_API_KEY", SerperApiKey.Trim());
            }
            StatusMessage = "✅ Serper.dev Google Places settings saved to vault successfully.";
            await LoadSettingsAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error saving Serper settings: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task TestSerperConnection()
    {
        var key = _secretVault.GetSecret("SERPER_API_KEY");
        if (string.IsNullOrWhiteSpace(key))
        {
            StatusMessage = "❌ No Serper API key in vault. Please save a free key from serper.dev first.";
            return;
        }

        StatusMessage = "Testing Serper.dev connection...";
        try
        {
            using var client = new System.Net.Http.HttpClient();
            using var req = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Post, "https://google.serper.dev/places")
            {
                Content = new System.Net.Http.StringContent("{\"q\":\"test\"}", System.Text.Encoding.UTF8, "application/json")
            };
            req.Headers.Add("X-API-KEY", key);
            var resp = await client.SendAsync(req);
            if (resp.IsSuccessStatusCode)
            {
                StatusMessage = "✅ Serper.dev Google Places connection verified! Free queries active.";
            }
            else
            {
                StatusMessage = $"⚠️ Serper API responded with status: {resp.StatusCode}.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Serper test failed: {ex.Message}";
        }
    }

    // Central Laravel Sync Commands (Stage 11)
    [RelayCommand]
    public async Task SaveSyncSettings()
    {
        try
        {
            var config = await _syncService.GetConfigAsync();
            config.ApiBaseUrl = SyncApiBaseUrl.Trim();
            config.DeviceName = SyncDeviceName.Trim();

            if (!string.IsNullOrWhiteSpace(SyncApiToken) && !SyncApiToken.Contains("..."))
            {
                config.ApiToken = SyncApiToken.Trim();
            }

            await _syncService.SaveConfigAsync(config);
            StatusMessage = "✅ Central Laravel Sync settings saved successfully.";
            await LoadSettingsAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error saving Sync settings: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task CheckSyncHealth()
    {
        try
        {
            var status = await _syncService.GetSyncStatusAsync();
            IsSyncOnline = status.IsOnline;
            if (status.IsOnline)
            {
                StatusMessage = $"✅ Server is online ({status.ServerMessage}, v{status.ServerVersion}). {status.PendingLocalChangesCount} local changes pending.";
            }
            else
            {
                StatusMessage = $"⚠️ Server is currently unreachable at {SyncApiBaseUrl}. Offline mode active.";
            }
        }
        catch (Exception ex)
        {
            IsSyncOnline = false;
            StatusMessage = $"Sync health check failed: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task TriggerSyncNow()
    {
        if (IsSyncing) return;
        IsSyncing = true;
        StatusMessage = "🔄 Synchronizing with central Laravel API...";

        try
        {
            var result = await _syncService.SynchronizeAsync();
            if (result.Success)
            {
                StatusMessage = $"✅ Sync complete! Pushed: {result.PushedCount}, Pulled: {result.PulledCount}, Conflicts: {result.ConflictsDetected}.";
                await LoadSettingsAsync();
            }
            else
            {
                StatusMessage = $"❌ Sync failed: {result.ErrorMessage}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Sync failed with exception: {ex.Message}";
        }
        finally
        {
            IsSyncing = false;
        }
    }
}
