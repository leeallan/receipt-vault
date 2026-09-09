using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReceiptVault.Models;
using ReceiptVault.Services;

namespace ReceiptVault.ViewModels;

public partial class ExportViewModel(
    IReceiptService receiptService,
    IExportService exportService,
    IReportService reportService,
    ISubscriptionService subscriptionService,
    IBillingService billingService) : BaseViewModel
{
    [ObservableProperty] private DateTime fromDate = DateTime.Today.AddMonths(-1);
    [ObservableProperty] private DateTime toDate = DateTime.Today;
    [ObservableProperty] private bool itemised;

    public bool IsPremium => subscriptionService.IsPremium;
    public bool IsFree => !IsPremium;

    // Free users export a plain CSV; Premium unlocks the styled spreadsheet.
    public string SpreadsheetButtonText => IsPremium ? "Export spreadsheet (XLSX)" : "Export CSV";

    public void RefreshPremiumState()
    {
        OnPropertyChanged(nameof(IsPremium));
        OnPropertyChanged(nameof(IsFree));
        OnPropertyChanged(nameof(SpreadsheetButtonText));
        if (!IsPremium && Itemised) Itemised = false;
    }

    // Quick date-range presets. Set From first, then To (the change handlers keep the
    // range valid as each is assigned).
    [RelayCommand]
    private void QuickRange(string range)
    {
        var today = DateTime.Today;
        switch (range)
        {
            case "ThisMonth":
                FromDate = new DateTime(today.Year, today.Month, 1);
                ToDate = today;
                break;
            case "LastMonth":
                var start = new DateTime(today.Year, today.Month, 1).AddMonths(-1);
                FromDate = start;
                ToDate = start.AddMonths(1).AddDays(-1);
                break;
            case "ThisYear":
                FromDate = new DateTime(today.Year, 1, 1);
                ToDate = today;
                break;
            case "All":
                FromDate = new DateTime(2000, 1, 1);
                ToDate = today;
                break;
        }
    }

    // Keep the range sane: From never in the future, To never before From.
    partial void OnFromDateChanged(DateTime value)
    {
        if (value.Date > DateTime.Today) FromDate = DateTime.Today;
        else if (ToDate.Date < value.Date) ToDate = value.Date;
    }

    partial void OnToDateChanged(DateTime value)
    {
        if (value.Date < FromDate.Date) ToDate = FromDate.Date;
    }

    partial void OnItemisedChanged(bool value)
    {
        // Itemised export is a Premium feature; free users stay receipt-level.
        if (value && !IsPremium) Itemised = false;
    }

    [RelayCommand]
    private Task ExportSpreadsheetAsync()
    {
        var name = $"receipts_{FromDate:yyyyMMdd}_{ToDate:yyyyMMdd}";

        // Free tier is limited to a plain, non-itemised CSV. Premium gets the styled
        // spreadsheet, itemised when the toggle is on.
        if (!IsPremium)
            return GenerateAsync(
                async (receipts, _) =>
                    await exportService.ExportToCsvAsync(receipts, name, FromDate.Date, ToDate.Date),
                needItems: false);

        return GenerateAsync(
            async (receipts, items) =>
                await exportService.ExportToXlsxAsync(receipts, name, items, FromDate.Date, ToDate.Date),
            needItems: Itemised);
    }

    [RelayCommand]
    private async Task ExportPdfAsync()
    {
        if (!await EnsurePremiumAsync("PDF export")) return;
        var name = $"receipts_{FromDate:yyyyMMdd}_{ToDate:yyyyMMdd}";
        var subtitle = $"{FromDate:d MMM yyyy} – {ToDate:d MMM yyyy}";
        await GenerateAsync(
            (receipts, _) => reportService.BuildReceiptsPdfAsync(receipts, "Receipts", subtitle, name),
            needItems: false);
    }

    [RelayCommand]
    private async Task TaxReportAsync()
    {
        if (!await EnsurePremiumAsync("Tax & expense reports")) return;
        var name = $"tax_report_{FromDate:yyyyMMdd}_{ToDate:yyyyMMdd}";
        var period = $"{FromDate:d MMM yyyy} – {ToDate:d MMM yyyy}";
        await GenerateAsync(
            (receipts, _) => reportService.BuildTaxReportPdfAsync(receipts, period, name),
            needItems: false);
    }

    // Offer the unlock so a free user can enable the itemised toggle.
    [RelayCommand]
    private async Task UnlockAsync()
    {
        if (await PremiumPurchase.RunAsync(billingService)) RefreshPremiumState();
    }

    private async Task GenerateAsync(
        Func<List<Receipt>, Dictionary<int, List<LineItem>>?, Task<string?>> build,
        bool needItems)
    {
        await RunAsync(async () =>
        {
            var from = FromDate.Date;
            var to = ToDate.Date;
            var receipts = (await receiptService.GetAllAsync())
                .Where(r => r.Date.Date >= from && r.Date.Date <= to)
                .OrderByDescending(r => r.Date)
                .ToList();

            if (receipts.Count == 0)
            {
                await Shell.Current.DisplayAlertAsync(
                    "Nothing to export", "No receipts in the selected date range.", "OK");
                return;
            }

            Dictionary<int, List<LineItem>>? items = null;
            if (needItems)
            {
                var allItems = await receiptService.GetAllLineItemsAsync();
                items = allItems
                    .Where(kv => receipts.Any(r => r.Id == kv.Key))
                    .ToDictionary(kv => kv.Key, kv => kv.Value);
            }

            var path = await build(receipts, items);
            if (path is null)
            {
                await Shell.Current.DisplayAlertAsync(
                    "Not available", "That export isn't available on this device.", "OK");
                return;
            }

            await exportService.ShareFileAsync(path);
        });
    }

    private async Task<bool> EnsurePremiumAsync(string feature)
    {
        if (IsPremium) return true;

        var unlock = await Shell.Current.DisplayAlertAsync(
            "Premium feature",
            $"{feature} is part of Receipt Vault Premium — a one-time unlock.",
            "Unlock Premium", "Not now");
        if (unlock && await PremiumPurchase.RunAsync(billingService))
        {
            RefreshPremiumState();
            return true;
        }
        return false;
    }
}
