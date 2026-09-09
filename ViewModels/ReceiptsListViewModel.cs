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

    public ObservableCollection<FilterChip> Filters { get; } =
    [
        new FilterChip { Name = "All", IsSelected = true },
        new FilterChip { Name = "Personal" },
        new FilterChip { Name = "Business" }
    ];

    // ---- Date filter ---------------------------------------------------------
    // Off by default; the From/To values are kept even when the filter is cleared so
    // reopening the panel shows the last range the user chose.

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DateFilterLabel))]
    [NotifyPropertyChangedFor(nameof(IsFiltered))]
    private bool hasDateFilter;

    [ObservableProperty]
    private bool isDatePanelOpen;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DateFilterLabel))]
    private DateTime fromDate = DateTime.Today.AddMonths(-1);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DateFilterLabel))]
    private DateTime toDate = DateTime.Today;

    public string DateFilterLabel => HasDateFilter
        ? $"{FromDate:d MMM} – {ToDate:d MMM yy}"
        : "Any date";

    // Drives the empty view: "nothing matches" reads very differently from "no receipts yet".
    public bool IsFiltered =>
        HasDateFilter || SelectedFilter != "All" || !string.IsNullOrWhiteSpace(SearchQuery);

    public async Task LoadAsync()
    {
        await RunAsync(async () =>
        {
            await ApplyFilterAsync();
        });
    }

    partial void OnSearchQueryChanged(string value)
    {
        OnPropertyChanged(nameof(IsFiltered));
        _ = ApplyFilterAsync();
    }

    partial void OnSelectedFilterChanged(string value)
    {
        foreach (var chip in Filters) chip.IsSelected = chip.Name == value;
        OnPropertyChanged(nameof(IsFiltered));
        _ = ApplyFilterAsync();
    }

    // Set while a preset writes both ends of the range, so the two setters below don't
    // each fire their own query on the way to the same final range.
    private bool _applyingRange;

    // Picking a date is itself the act of switching the filter on.
    partial void OnFromDateChanged(DateTime value)
    {
        if (value.Date > DateTime.Today) { FromDate = DateTime.Today; return; }
        if (ToDate.Date < value.Date) { ToDate = value.Date; return; }
        if (_applyingRange) return;
        HasDateFilter = true;
        _ = ApplyFilterAsync();
    }

    partial void OnToDateChanged(DateTime value)
    {
        if (value.Date < FromDate.Date) { ToDate = FromDate.Date; return; }
        if (_applyingRange) return;
        HasDateFilter = true;
        _ = ApplyFilterAsync();
    }

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

        if (HasDateFilter)
        {
            var from = FromDate.Date;
            var to = ToDate.Date;
            results = results.Where(r => r.Date.Date >= from && r.Date.Date <= to).ToList();
        }

        Receipts = new ObservableCollection<Receipt>(results);
        HasReceipts = Receipts.Count > 0;
    }

    [RelayCommand]
    private void ToggleDatePanel() => IsDatePanelOpen = !IsDatePanelOpen;

    [RelayCommand]
    private async Task ClearDateFilterAsync()
    {
        if (!HasDateFilter) return;
        HasDateFilter = false;
        await ApplyFilterAsync();
    }

    // Presets mirror the export page's quick ranges, plus the "last 90 days" span that
    // covers most "where's that receipt from a while back" hunts.
    [RelayCommand]
    private async Task DateQuickRangeAsync(string range)
    {
        var today = DateTime.Today;
        var (from, to) = range switch
        {
            "ThisMonth" => (new DateTime(today.Year, today.Month, 1), today),
            "LastMonth" => (new DateTime(today.Year, today.Month, 1).AddMonths(-1),
                            new DateTime(today.Year, today.Month, 1).AddDays(-1)),
            "Last90" => (today.AddDays(-90), today),
            "ThisYear" => (new DateTime(today.Year, 1, 1), today),
            _ => (FromDate, ToDate)
        };

        _applyingRange = true;
        // Widen before narrowing so the change handlers never clamp a valid range.
        if (from <= ToDate) { FromDate = from; ToDate = to; }
        else { ToDate = to; FromDate = from; }
        _applyingRange = false;

        HasDateFilter = true;
        await ApplyFilterAsync();
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
    private void SelectFilter(FilterChip chip) => SelectedFilter = chip.Name;

    [RelayCommand]
    private async Task NavigateToCaptureAsync() =>
        await Shell.Current.GoToAsync("CapturePage");

    [RelayCommand]
    private async Task OpenExportAsync() =>
        await Shell.Current.GoToAsync("ExportPage");
}
