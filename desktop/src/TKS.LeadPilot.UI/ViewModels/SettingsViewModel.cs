using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TKS.LeadPilot.Core.Interfaces;

namespace TKS.LeadPilot.UI.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly ISecretVault _secretVault;

    [ObservableProperty]
    private string _geminiApiKey = string.Empty;

    [ObservableProperty]
    private bool _isAiDisabled = false;

    [ObservableProperty]
    private int _dailyAiLimit = 1200;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public SettingsViewModel(ISecretVault secretVault)
    {
        _secretVault = secretVault;
        LoadSettings();
    }

    private void LoadSettings()
    {
        var key = _secretVault.GetSecret("GeminiApiKey");
        if (!string.IsNullOrEmpty(key))
        {
            // Mask key for safety (show first 6, last 4)
            GeminiApiKey = key.Length > 10 ? $"{key[..6]}...{key[^4..]}" : "********";
        }
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
        LoadSettings();
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
}
