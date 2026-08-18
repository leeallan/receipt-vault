using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReceiptVault.Models;
using ReceiptVault.Services;

namespace ReceiptVault.ViewModels;

public partial class ReceiptsListViewModel(IReceiptService receiptService) : BaseViewModel
{
    [ObservableProperty] private ObservableCollection<Receipt> receipts = [];
    [ObservableProperty] private string searchQuery = string.Empty;
    [ObservableProperty] private string selectedFilter = "All";
    [ObservableProperty] private bool hasReceipts;

    public string[] Filters => ["All", "Personal", "Business"];

    public async Task LoadAsync()
    {
        await RunAsync(async () =>
        {
            await ApplyFilterAsync();
        });
    }

    partial void OnSearchQueryChanged(string value) =>
        _ = ApplyFilterAsync();

    partial void OnSelectedFilterChanged(string value) =>
        _ = ApplyFilterAsync();

    private async Task ApplyFilterAsync()
    {
        var results = string.IsNullOrWhiteSpace(SearchQuery)
            ? await receiptService.GetAllAsync()
            : await receiptService.SearchAsync(SearchQuery);

        results = SelectedFilter switch
        {
            "Personal" => results.Where(r => !r.IsBusinessExpense).ToList(),
            "Business" => results.Where(r => r.IsBusinessExpense).ToList(),
            _ => results
        };

        Receipts = new ObservableCollection<Receipt>(results);
        HasReceipts = Receipts.Count > 0;
    }

    [RelayCommand]
    private async Task OpenReceiptAsync(Receipt receipt)
    {
        await Shell.Current.GoToAsync($"ReceiptDetailPage?id={receipt.Id}");
    }

    [RelayCommand]
    private async Task DeleteReceiptAsync(Receipt receipt)
    {
        var confirm = await Shell.Current.DisplayAlertAsync(
            "Delete receipt", $"Delete receipt from {receipt.Merchant}?", "Delete", "Cancel");
        if (!confirm) return;

        await receiptService.DeleteAsync(receipt);
        Receipts.Remove(receipt);
        HasReceipts = Receipts.Count > 0;
    }

    [RelayCommand]
    private void SelectFilter(string filter) => SelectedFilter = filter;

    [RelayCommand]
    private async Task NavigateToCaptureAsync() =>
        await Shell.Current.GoToAsync("CapturePage");
}
