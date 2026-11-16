using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TKS.LeadPilot.Core.Interfaces;
using TKS.LeadPilot.Core.Models;

namespace TKS.LeadPilot.UI.ViewModels;

public partial class BusinessesViewModel : ViewModelBase
{
    private readonly IBusinessRepository _businessRepository;

    [ObservableProperty]
    private ObservableCollection<Business> _businesses = [];

    [ObservableProperty]
    private Business? _selectedBusiness;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private bool _isLoading = false;

    public BusinessesViewModel(IBusinessRepository businessRepository)
    {
        _businessRepository = businessRepository;
        _ = LoadBusinessesAsync();
    }

    [RelayCommand]
    private async Task Refresh()
    {
        await LoadBusinessesAsync();
    }

    [RelayCommand]
    private async Task Search()
    {
        await LoadBusinessesAsync(SearchQuery);
    }

    public async Task LoadBusinessesAsync(string? query = null)
    {
        try
        {
            IsLoading = true;
            var list = await _businessRepository.GetAllAsync(query);
            Businesses = new ObservableCollection<Business>(list);
            SelectedBusiness = Businesses.FirstOrDefault();
        }
        catch
        {
            // Fail gracefully
        }
        finally
        {
            IsLoading = false;
        }
    }
}
