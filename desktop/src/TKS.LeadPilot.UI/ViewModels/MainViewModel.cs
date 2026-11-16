using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TKS.LeadPilot.Core.Interfaces;

namespace TKS.LeadPilot.UI.ViewModels;

public partial class MainViewModel : ViewModelBase
{
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

    public MainViewModel(IBusinessRepository businessRepository, ISecretVault secretVault)
    {
        _businessRepository = businessRepository;
        _secretVault = secretVault;
        _currentView = new DashboardViewModel(_businessRepository);
    }

    [RelayCommand]
    public void Navigate(string destination)
    {
        ActiveTab = destination;

        CurrentView = destination switch
        {
            "Dashboard" => new DashboardViewModel(_businessRepository),
            "Businesses" => new BusinessesViewModel(_businessRepository),
            "Settings" => new SettingsViewModel(_secretVault),
            _ => new DashboardViewModel(_businessRepository)
        };
    }

    [RelayCommand]
    public void ToggleEmergencyAi()
    {
        IsAiEmergencyPaused = !IsAiEmergencyPaused;
    }
}
