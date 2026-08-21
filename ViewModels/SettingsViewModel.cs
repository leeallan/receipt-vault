using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReceiptVault.Services;

namespace ReceiptVault.ViewModels;

public partial class SettingsViewModel(
    IReceiptService receiptService,
    IExportService exportService,
    ISubscriptionService subscriptionService) : BaseViewModel
{
    [ObservableProperty] private bool isPremium;
    [ObservableProperty] private int receiptCount;
    [ObservableProperty] private string selectedCurrency = "GBP";

    public string[] Currencies => ["GBP", "USD", "EUR"];

    public string SubscriptionLabel => IsPremium
        ? $"Premium — {subscriptionService.ActiveTier} plan"
        : "Free — unlock cloud sync, PDF & tax reports";

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
            var items = await receiptService.GetAllLineItemsAsync();
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
            var allItems = await receiptService.GetAllLineItemsAsync();
            var items = allItems
                .Where(kv => monthly.Any(r => r.Id == kv.Key))
                .ToDictionary(kv => kv.Key, kv => kv.Value);
            var path = await exportService.ExportToCsvAsync(monthly, fileName, items);
            await exportService.ShareFileAsync(path);
        });
    }

    [RelayCommand]
    private async Task UpgradeAsync()
    {
        // TODO (Phase 2/5): open the tiered paywall + StoreKit / Play Billing purchase.
        await Shell.Current.DisplayAlertAsync(
            "Receipt Vault Premium",
            "Cloud sync, PDF & tax reports, and automatic line-item scanning.\n\n" +
            "Monthly £2.99 · Annual £19.99 · Lifetime £39.99\n\nIn-app purchase coming soon.",
            "OK");
    }

    [RelayCommand]
    private async Task RestorePurchasesAsync()
    {
        await subscriptionService.RestorePurchasesAsync();
        await LoadAsync();
    }
}
