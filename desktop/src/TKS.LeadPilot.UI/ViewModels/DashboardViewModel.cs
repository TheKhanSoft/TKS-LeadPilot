using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TKS.LeadPilot.Core.Enums;
using TKS.LeadPilot.Core.Interfaces;
using TKS.LeadPilot.Core.Models;

namespace TKS.LeadPilot.UI.ViewModels;

public partial class DashboardViewModel : ViewModelBase
{
    private readonly IBusinessRepository _businessRepository;
    private readonly IGooglePlacesEnrichmentService? _placesService;
    private readonly IAiProposalSynthesizer? _aiSynthesizer;

    [ObservableProperty]
    private int _totalBusinesses;

    [ObservableProperty]
    private int _withWebsiteCount;

    [ObservableProperty]
    private int _noWebsiteCount;

    [ObservableProperty]
    private int _highOpportunityCount;

    [ObservableProperty]
    private int _averageLeadScore = 74;

    [ObservableProperty]
    private int _pendingApprovalsCount = 2;

    [ObservableProperty]
    private bool _isLoading = false;

    // Live Quota & Cost Telemetry
    [ObservableProperty]
    private int _geminiUsedToday = 0;

    [ObservableProperty]
    private int _geminiDailyLimit = 1200;

    [ObservableProperty]
    private int _placesUsedToday = 0;

    [ObservableProperty]
    private int _placesDailyLimit = 50;

    [ObservableProperty]
    private int _placesMonthlyUsed = 0;

    [ObservableProperty]
    private int _placesMonthlyLimit = 1000;

    [ObservableProperty]
    private string _placesSkuTierName = "Enterprise (1k/mo)";

    [ObservableProperty]
    private bool _isPlacesEnabled = false;

    public DashboardViewModel(
        IBusinessRepository businessRepository,
        IGooglePlacesEnrichmentService? placesService = null,
        IAiProposalSynthesizer? aiSynthesizer = null)
    {
        _businessRepository = businessRepository;
        _placesService = placesService;
        _aiSynthesizer = aiSynthesizer;
        _ = LoadStatisticsAsync();
    }

    [RelayCommand]
    public async Task LoadStatisticsAsync()
    {
        try
        {
            IsLoading = true;
            TotalBusinesses = await _businessRepository.GetTotalCountAsync();
            WithWebsiteCount = await _businessRepository.GetWithWebsiteCountAsync();
            NoWebsiteCount = Math.Max(0, TotalBusinesses - WithWebsiteCount);
            HighOpportunityCount = await _businessRepository.GetHighOpportunityCountAsync(70);

            if (_placesService != null)
            {
                var placesCfg = await _placesService.GetConfigAsync();
                PlacesUsedToday = placesCfg.DailyRequestsUsed;
                PlacesDailyLimit = placesCfg.DailyRequestCeiling;
                PlacesMonthlyUsed = placesCfg.MonthlyRequestsUsed;
                PlacesMonthlyLimit = placesCfg.MonthlyFreeAllowance;
                PlacesSkuTierName = placesCfg.SkuTier == PlacesSkuTier.Essentials ? "Essentials (10k/mo)" : "Enterprise (1k/mo)";
                IsPlacesEnabled = placesCfg.IsEnabled;
            }

            if (_aiSynthesizer != null)
            {
                var aiCfg = await _aiSynthesizer.GetConfigAsync();
                GeminiUsedToday = aiCfg.DailyRequestsUsed;
                GeminiDailyLimit = aiCfg.DailyRequestCeiling;
            }
        }
        catch
        {
            // Fallback for design-time / initial startup
            TotalBusinesses = 3;
            WithWebsiteCount = 2;
            NoWebsiteCount = 1;
            HighOpportunityCount = 2;
        }
        finally
        {
            IsLoading = false;
        }
    }
}
