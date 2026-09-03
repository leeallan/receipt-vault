using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReceiptVault.Services;
using ReceiptVault.Shared;

namespace ReceiptVault.ViewModels;

public partial class SettingsViewModel(
    IReceiptService receiptService,
    IExportService exportService,
    IReportService reportService,
    ISubscriptionService subscriptionService,
    IBillingService billingService) : BaseViewModel
{
    [ObservableProperty] private bool isPremium;
    [ObservableProperty] private int receiptCount;
    [ObservableProperty] private string selectedCurrency = "GBP";

    public string[] Currencies => ["GBP", "USD", "EUR"];

    // Debug-only: exposes a "Reset to Free" control for sandbox purchase testing so we can
    // re-open the paywall without deleting/reinstalling. Compiled out of release builds.
    public bool IsDebugBuild =>
#if DEBUG
        true;
#else
        false;
#endif

    public string SubscriptionLabel => IsPremium
        ? "Premium — unlocked. Thank you!"
        : "Free — unlock item scanning, PDF & tax reports";

    public async Task LoadAsync()
    {
        IsPremium = subscriptionService.IsPremium;
        SelectedCurrency = Preferences.Default.Get("default_currency", "GBP");
        var all = await receiptService.GetAllAsync();
        ReceiptCount = all.Count;
        OnPropertyChanged(nameof(SubscriptionLabel));
    }

    partial void OnSelectedCurrencyChanged(string value) =>
        Preferences.Default.Set("default_currency", value);

    [RelayCommand]
    private async Task ExportAllAsync()
    {
        await RunAsync(async () =>
        {
            var all = await receiptService.GetAllAsync();
            if (all.Count == 0)
            {
                await Shell.Current.DisplayAlertAsync("Nothing to export", "You have no receipts yet.", "OK");
                return;
            }
            var fileName = $"receipts_export_{DateTime.Today:yyyy-MM-dd}";
            // Free export is receipt-level; the itemised sub-rows are a premium feature.
            var items = subscriptionService.Has(Entitlement.LineItemOcr)
                ? await receiptService.GetAllLineItemsAsync()
                : null;
            var path = await exportService.ExportToCsvAsync(all, fileName, items);
            await exportService.ShareFileAsync(path);
        });
    }

    [RelayCommand]
    private async Task ExportMonthAsync()
    {
        await RunAsync(async () =>
        {
            var now = DateTime.Today;
            var monthly = await receiptService.GetByMonthAsync(now.Year, now.Month);
            if (monthly.Count == 0)
            {
                await Shell.Current.DisplayAlertAsync("Nothing to export", "No receipts this month.", "OK");
                return;
            }
            var fileName = $"receipts_{now:yyyy-MM}";
            // Free export is receipt-level; the itemised sub-rows are a premium feature.
            Dictionary<int, List<Models.LineItem>>? items = null;
            if (subscriptionService.Has(Entitlement.LineItemOcr))
            {
                var allItems = await receiptService.GetAllLineItemsAsync();
                items = allItems
                    .Where(kv => monthly.Any(r => r.Id == kv.Key))
                    .ToDictionary(kv => kv.Key, kv => kv.Value);
            }
            var path = await exportService.ExportToCsvAsync(monthly, fileName, items);
            await exportService.ShareFileAsync(path);
        });
    }

    // PDF export (premium). A polished receipt-level listing with spend/tax totals.
    [RelayCommand]
    private async Task ExportPdfAsync()
    {
        if (!await EnsurePremiumAsync("PDF export")) return;

        await RunAsync(async () =>
        {
            var all = await receiptService.GetAllAsync();
            if (all.Count == 0)
            {
                await Shell.Current.DisplayAlertAsync("Nothing to export", "You have no receipts yet.", "OK");
                return;
            }
            var fileName = $"receipts_{DateTime.Today:yyyy-MM-dd}";
            var subtitle = $"All receipts · exported {DateTime.Today:d MMM yyyy}";
            var path = await reportService.BuildReceiptsPdfAsync(all, "Receipts", subtitle, fileName);
            if (path is null)
            {
                await Shell.Current.DisplayAlertAsync("Not available", "PDF export isn't available on this device.", "OK");
                return;
            }
            await exportService.ShareFileAsync(path);
        });
    }

    // Tax & expense report (premium). Business/personal totals, tax, and category breakdown.
    [RelayCommand]
    private async Task TaxReportAsync()
    {
        if (!await EnsurePremiumAsync("Tax & expense reports")) return;

        await RunAsync(async () =>
        {
            var all = await receiptService.GetAllAsync();
            if (all.Count == 0)
            {
                await Shell.Current.DisplayAlertAsync("Nothing to report", "You have no receipts yet.", "OK");
                return;
            }
            var fileName = $"tax_report_{DateTime.Today:yyyy-MM-dd}";
            var period = $"All receipts · generated {DateTime.Today:d MMM yyyy}";
            var path = await reportService.BuildTaxReportPdfAsync(all, period, fileName);
            if (path is null)
            {
                await Shell.Current.DisplayAlertAsync("Not available", "PDF reports aren't available on this device.", "OK");
                return;
            }
            await exportService.ShareFileAsync(path);
        });
    }

    // Premium gate for a specific feature: returns true if already unlocked, otherwise
    // offers the one-time unlock and returns false.
    private async Task<bool> EnsurePremiumAsync(string feature)
    {
        if (subscriptionService.IsPremium) return true;

        var unlock = await Shell.Current.DisplayAlertAsync(
            "Premium feature",
            $"{feature} is part of Receipt Vault Premium — a one-time unlock.",
            "Unlock Premium", "Not now");
        if (unlock) await UpgradeAsync();
        return false;
    }

    // One-time unlock: a single non-consumable purchase. Confirm the localized price, buy,
    // then reload. Phase 5 can dress this up as a proper unlock screen.
    [RelayCommand]
    private async Task UpgradeAsync()
    {
        await RunAsync(async () =>
        {
            var unlocked = await PremiumPurchase.RunAsync(billingService);
            if (unlocked) await LoadAsync();
        });
    }

    // Debug-only: clears the locally cached entitlement so the paywall reappears. Does NOT
    // affect the sandbox's record of what the tester owns — clear that in App Store Connect.
    [RelayCommand]
    private async Task ResetEntitlementAsync()
    {
        subscriptionService.SetTier(ProductTier.Free);
        await LoadAsync();
    }

    [RelayCommand]
    private async Task RestorePurchasesAsync()
    {
        await RunAsync(async () =>
        {
            var tier = await billingService.RestoreAsync();
            await LoadAsync();

            await Shell.Current.DisplayAlertAsync(
                "Restore purchases",
                tier == ProductTier.Free
                    ? "No previous purchases were found for this account."
                    : "Your Premium unlock has been restored.",
                "OK");
        });
    }
}
