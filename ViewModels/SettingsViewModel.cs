using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReceiptVault.Services;
using ReceiptVault.Shared;

namespace ReceiptVault.ViewModels;

public partial class SettingsViewModel(
    IReceiptService receiptService,
    IExportService exportService,
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
        : "Free — unlock item scanning, reports & iCloud sync";

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

    // One-time unlock: a single non-consumable purchase. Confirm the localized price, buy,
    // then reload. Phase 5 can dress this up as a proper unlock screen.
    [RelayCommand]
    private async Task UpgradeAsync()
    {
        await RunAsync(async () =>
        {
            var products = await billingService.GetProductsAsync();
            var premium = products.FirstOrDefault();
            if (premium is null)
            {
                await Shell.Current.DisplayAlertAsync(
                    "Store unavailable",
                    "Couldn't load Premium right now. Please try again later.",
                    "OK");
                return;
            }

            var confirm = await Shell.Current.DisplayAlertAsync(
                "Unlock Premium",
                $"Unlock automatic item scanning, PDF & tax reports, and iCloud backup & sync for a one-time {premium.LocalizedPrice}.",
                $"Unlock — {premium.LocalizedPrice}", "Not now");
            if (!confirm) return;

            var result = await billingService.PurchaseAsync(premium.ProductId);
            await LoadAsync();

            if (result.Success)
                await Shell.Current.DisplayAlertAsync(
                    "Welcome to Premium", "Thanks! Your premium features are now unlocked.", "OK");
            else if (!result.Cancelled)
                await Shell.Current.DisplayAlertAsync(
                    "Purchase failed", result.Error ?? "Please try again.", "OK");
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
