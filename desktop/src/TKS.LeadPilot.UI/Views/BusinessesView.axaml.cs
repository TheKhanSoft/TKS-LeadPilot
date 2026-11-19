using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using TKS.LeadPilot.Core.Models;
using TKS.LeadPilot.UI.ViewModels;

namespace TKS.LeadPilot.UI.Views;

public partial class BusinessesView : UserControl
{
    public BusinessesView()
    {
        InitializeComponent();
    }

    private void OnSelectAllHeaderClick(object? sender, RoutedEventArgs e)
    {
        if (sender is CheckBox cb && DataContext is BusinessesViewModel vm)
        {
            var isChecked = cb.IsChecked == true;
            foreach (var b in vm.Businesses)
            {
                b.IsSelected = isChecked;
            }
            vm.UpdateSelectedCount();
        }
    }

    private async void OnStarButtonClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: Business business } && DataContext is BusinessesViewModel vm)
        {
            await vm.ToggleStar(business);
        }
    }

    private async void OnPickCsvFileClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not BusinessesViewModel vm) return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select Business CSV File to Import",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("CSV / Text Files")
                {
                    Patterns = ["*.csv", "*.tsv", "*.txt"]
                }
            ]
        });

        if (files.Count > 0)
        {
            await using var stream = await files[0].OpenReadAsync();
            vm.OpenImportModal();
            await vm.StageCsvForImportAsync(stream);
        }
    }
}
