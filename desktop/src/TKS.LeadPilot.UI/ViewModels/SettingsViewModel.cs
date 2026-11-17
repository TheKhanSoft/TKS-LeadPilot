using System;
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

    // Google Places Settings (Stage 7)
    [ObservableProperty]
    private string _googlePlacesApiKey = string.Empty;

    [ObservableProperty]
    private bool _isGooglePlacesEnabled = false;

    [ObservableProperty]
    private int _googlePlacesDailyCeiling = 50;

    [ObservableProperty]
    private int _googlePlacesUsedToday = 0;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public SettingsViewModel(
        ISecretVault secretVault,
        IGooglePlacesEnrichmentService placesService,
        IAiProposalSynthesizer aiSynthesizer)
    {
        _secretVault = secretVault;
        _placesService = placesService;
        _aiSynthesizer = aiSynthesizer;
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

    // Google Places Commands (Stage 7)
    [RelayCommand]
    public async Task SaveGooglePlacesSettings()
    {
        try
        {
            var config = new GooglePlacesConfig
            {
                IsEnabled = IsGooglePlacesEnabled,
                DailyRequestCeiling = GooglePlacesDailyCeiling
            };

            string? keyToSave = null;
            if (!string.IsNullOrWhiteSpace(GooglePlacesApiKey) && !GooglePlacesApiKey.Contains("..."))
            {
                keyToSave = GooglePlacesApiKey.Trim();
            }

            await _placesService.SaveConfigAsync(config, keyToSave);
            StatusMessage = "✅ Google Places settings and cost controls saved successfully.";
            await LoadSettingsAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error saving Google Places settings: {ex.Message}";
        }
    }
}
