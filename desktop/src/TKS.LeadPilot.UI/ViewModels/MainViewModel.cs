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
    }

    [RelayCommand]
    public void Navigate(string destination)
    {
        ActiveTab = destination;

        CurrentView = destination switch
        {
            "Dashboard" => _serviceProvider.GetRequiredService<DashboardViewModel>(),
            "Businesses" => _serviceProvider.GetRequiredService<BusinessesViewModel>(),
            "Audits" => _serviceProvider.GetRequiredService<AuditsViewModel>(),
            "Pipeline" => _serviceProvider.GetRequiredService<PipelineViewModel>(),
            "Messages" or "Outreach" => _serviceProvider.GetRequiredService<MessageStudioViewModel>(),
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
