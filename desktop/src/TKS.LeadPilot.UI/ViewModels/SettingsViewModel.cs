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

    // Gemini Settings
    [ObservableProperty]
    private string _geminiApiKey = string.Empty;

    [ObservableProperty]
    private bool _isAiDisabled = false;

    [ObservableProperty]
    private int _dailyAiLimit = 1200;

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

    public SettingsViewModel(ISecretVault secretVault, IGooglePlacesEnrichmentService placesService)
    {
        _secretVault = secretVault;
        _placesService = placesService;
        _ = LoadSettingsAsync();
    }

    private async Task LoadSettingsAsync()
    {
        var key = _secretVault.GetSecret("GeminiApiKey");
        if (!string.IsNullOrEmpty(key))
        {
            GeminiApiKey = key.Length > 10 ? $"{key[..6]}...{key[^4..]}" : "********";
        }

        var placesKey = _secretVault.GetSecret(GooglePlacesKeyName);
        if (!string.IsNullOrEmpty(placesKey))
        {
            GooglePlacesApiKey = placesKey.Length > 10 ? $"{placesKey[..6]}...{placesKey[^4..]}" : "********";
        }

        var config = await _placesService.GetConfigAsync();
        IsGooglePlacesEnabled = config.IsEnabled;
        GooglePlacesDailyCeiling = config.DailyRequestCeiling;
        GooglePlacesUsedToday = config.DailyRequestsUsed;
    }

    [RelayCommand]
    public void SaveApiKey()
    {
        if (string.IsNullOrWhiteSpace(GeminiApiKey) || GeminiApiKey.Contains("..."))
        {
            StatusMessage = "Please enter a valid Gemini API key.";
            return;
        }

        _secretVault.SaveSecret("GeminiApiKey", GeminiApiKey.Trim());
        StatusMessage = "✅ Gemini API key securely saved in Windows DPAPI vault.";
        _ = LoadSettingsAsync();
    }

    [RelayCommand]
    public void TestAiConnection()
    {
        if (!_secretVault.HasSecret("GeminiApiKey"))
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
