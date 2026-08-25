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

    // Interim purchase flow: pick a plan from an action sheet, then buy. Phase 5 replaces
    // this with a dedicated tiered paywall screen; the billing plumbing stays the same.
    [RelayCommand]
    private async Task UpgradeAsync()
    {
        await RunAsync(async () =>
        {
            var products = await billingService.GetProductsAsync();
            if (products.Count == 0)
            {
                await Shell.Current.DisplayAlertAsync(
                    "Store unavailable",
                    "Couldn't load the plans right now. Please try again later.",
                    "OK");
                return;
            }

            var buttons = products.Select(p => $"{p.Title} — {p.LocalizedPrice}").ToArray();
            var choice = await Shell.Current.DisplayActionSheetAsync(
                "Choose your plan", "Cancel", null, buttons);
            if (string.IsNullOrEmpty(choice) || choice == "Cancel") return;

            var selected = products.FirstOrDefault(p => $"{p.Title} — {p.LocalizedPrice}" == choice);
            if (selected is null) return;

            var result = await billingService.PurchaseAsync(selected.ProductId);
            await LoadAsync();

            if (result.Success)
                await Shell.Current.DisplayAlertAsync(
                    "Welcome to Premium", "Thanks! Your premium features are now unlocked.", "OK");
            else if (!result.Cancelled)
                await Shell.Current.DisplayAlertAsync(
                    "Purchase failed", result.Error ?? "Please try again.", "OK");
        });
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
                    : $"Restored your {tier} plan.",
                "OK");
        });
    }
}
