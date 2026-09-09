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
    public bool CanGoNext => SelectedMonth < ThisMonth;

    private static DateTime ThisMonth => new(DateTime.Today.Year, DateTime.Today.Month, 1);

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

    // ---- Month / year picker -------------------------------------------------
    // The ‹ › arrows only step one month at a time, which gets painful once there's
    // a year or two of history. Tapping the month label opens a jump-anywhere grid.

    [ObservableProperty]
    private bool isMonthPickerOpen;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PickerYearLabel))]
    [NotifyPropertyChangedFor(nameof(CanPickerGoNextYear))]
    private int pickerYear = DateTime.Today.Year;

    public string PickerYearLabel => PickerYear.ToString();

    public bool CanPickerGoNextYear => PickerYear < DateTime.Today.Year;

    [ObservableProperty]
    private ObservableCollection<MonthCell> monthCells = [];

    partial void OnPickerYearChanged(int value) => BuildMonthCells();

    private void BuildMonthCells()
    {
        var thisMonth = ThisMonth;
        MonthCells = new ObservableCollection<MonthCell>(
            Enumerable.Range(1, 12).Select(m => new MonthCell
            {
                Year = PickerYear,
                Month = m,
                IsAvailable = new DateTime(PickerYear, m, 1) <= thisMonth,
                IsSelected = PickerYear == SelectedMonth.Year && m == SelectedMonth.Month
            }));
    }

    [RelayCommand]
    private void OpenMonthPicker()
    {
        PickerYear = SelectedMonth.Year;
        // Setting PickerYear only rebuilds the grid when the value actually changes,
        // so build unconditionally to pick up a new selection within the same year.
        BuildMonthCells();
        IsMonthPickerOpen = true;
    }

    [RelayCommand]
    private void CloseMonthPicker() => IsMonthPickerOpen = false;

    [RelayCommand]
    private void PickerPreviousYear() => PickerYear--;

    [RelayCommand]
    private void PickerNextYear()
    {
        if (CanPickerGoNextYear) PickerYear++;
    }

    [RelayCommand]
    private async Task SelectMonthAsync(MonthCell cell)
    {
        if (!cell.IsAvailable) return;
        SelectedMonth = new DateTime(cell.Year, cell.Month, 1);
        IsMonthPickerOpen = false;
        await LoadAsync();
    }

    // ---- Navigation ----------------------------------------------------------

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
