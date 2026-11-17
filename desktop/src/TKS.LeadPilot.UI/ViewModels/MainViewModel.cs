using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using TKS.LeadPilot.Core.Interfaces;

namespace TKS.LeadPilot.UI.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IBusinessRepository _businessRepository;
    private readonly ISecretVault _secretVault;

    [ObservableProperty]
    private ViewModelBase _currentView;

    [ObservableProperty]
    private string _activeTab = "Dashboard";

    public bool IsDashboardActive => ActiveTab == "Dashboard";
    public bool IsBusinessesActive => ActiveTab == "Businesses";
    public bool IsDiscoveryActive => ActiveTab == "Discovery";
    public bool IsAuditsActive => ActiveTab == "Audits";
    public bool IsPipelineActive => ActiveTab == "Pipeline";
    public bool IsMessagesActive => ActiveTab == "Messages" || ActiveTab == "Outreach";
    public bool IsLandingStudioActive => ActiveTab == "LandingStudio" || ActiveTab == "Studio";
    public bool IsSettingsActive => ActiveTab == "Settings";

    partial void OnActiveTabChanged(string value)
    {
        OnPropertyChanged(nameof(IsDashboardActive));
        OnPropertyChanged(nameof(IsBusinessesActive));
        OnPropertyChanged(nameof(IsDiscoveryActive));
        OnPropertyChanged(nameof(IsAuditsActive));
        OnPropertyChanged(nameof(IsPipelineActive));
        OnPropertyChanged(nameof(IsMessagesActive));
        OnPropertyChanged(nameof(IsLandingStudioActive));
        OnPropertyChanged(nameof(IsSettingsActive));
    }

    [ObservableProperty]
    private bool _isAiEmergencyPaused = false;

    [ObservableProperty]
    private int _aiRequestsUsed = 12;

    [ObservableProperty]
    private int _aiRequestsLimit = 1200;

    [ObservableProperty]
    private string _syncStatusText = "Local-Only (SQLite)";

    public MainViewModel(IServiceProvider serviceProvider, IBusinessRepository businessRepository, ISecretVault secretVault)
    {
        _serviceProvider = serviceProvider;
        _businessRepository = businessRepository;
        _secretVault = secretVault;
        _currentView = _serviceProvider.GetRequiredService<DashboardViewModel>();

        BusinessesViewModel.GlobalNavigationRequested += dest => Navigate(dest);
    }

    [RelayCommand]
    public void Navigate(string destination)
    {
        ActiveTab = destination;

        CurrentView = destination switch
        {
            "Dashboard" => _serviceProvider.GetRequiredService<DashboardViewModel>(),
            "Businesses" or "Discovery" => _serviceProvider.GetRequiredService<BusinessesViewModel>(),
            "Audits" => _serviceProvider.GetRequiredService<AuditsViewModel>(),
            "Pipeline" => _serviceProvider.GetRequiredService<PipelineViewModel>(),
            "Messages" or "Outreach" => _serviceProvider.GetRequiredService<MessageStudioViewModel>(),
            "LandingStudio" or "Studio" => _serviceProvider.GetRequiredService<LandingPageStudioViewModel>(),
            "Settings" => _serviceProvider.GetRequiredService<SettingsViewModel>(),
            _ => _serviceProvider.GetRequiredService<DashboardViewModel>()
        };
    }

    [RelayCommand]
    public void ToggleEmergencyAi()
    {
        IsAiEmergencyPaused = !IsAiEmergencyPaused;
    }
}
