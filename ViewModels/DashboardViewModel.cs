using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReceiptVault.Models;
using ReceiptVault.Services;

namespace ReceiptVault.ViewModels;

public partial class DashboardViewModel(IReceiptService receiptService) : BaseViewModel
{
    [ObservableProperty]
    private decimal monthlyTotal;

    [ObservableProperty]
    private decimal businessTotal;

    [ObservableProperty]
    private decimal personalTotal;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentMonthLabel))]
    [NotifyPropertyChangedFor(nameof(CanGoNext))]
    private DateTime selectedMonth = new(DateTime.Today.Year, DateTime.Today.Month, 1);

    public string CurrentMonthLabel => SelectedMonth.ToString("MMMM yyyy");

    // Don't let the user page into months that haven't happened yet.
    public bool CanGoNext => SelectedMonth < new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);

    [ObservableProperty]
    private ObservableCollection<Receipt> recentReceipts = [];

    [ObservableProperty]
    private int receiptCount;

    public async Task LoadAsync()
    {
        await RunAsync(async () =>
        {
            var year = SelectedMonth.Year;
            var month = SelectedMonth.Month;
            MonthlyTotal = await receiptService.GetMonthlyTotalAsync(year, month);
            BusinessTotal = await receiptService.GetMonthlyTotalAsync(year, month, isBusinessExpense: true);
            PersonalTotal = await receiptService.GetMonthlyTotalAsync(year, month, isBusinessExpense: false);

            // "Recent" = most recently captured, regardless of the receipt's own date,
            // so a receipt filed against an earlier month still surfaces here immediately.
            var all = await receiptService.GetAllAsync();
            ReceiptCount = all.Count;
            RecentReceipts = new ObservableCollection<Receipt>(
                all.OrderByDescending(r => r.CreatedAt).Take(5));
        });
    }

    [RelayCommand]
    private async Task PreviousMonthAsync()
    {
        SelectedMonth = SelectedMonth.AddMonths(-1);
        await LoadAsync();
    }

    [RelayCommand]
    private async Task NextMonthAsync()
    {
        if (!CanGoNext) return;
        SelectedMonth = SelectedMonth.AddMonths(1);
        await LoadAsync();
    }

    [RelayCommand]
    private async Task NavigateToCaptureAsync()
    {
        await Shell.Current.GoToAsync("CapturePage");
    }

    [RelayCommand]
    private async Task OpenReceiptAsync(Receipt receipt)
    {
        await Shell.Current.GoToAsync($"ReceiptDetailPage?id={receipt.Id}");
    }
}
