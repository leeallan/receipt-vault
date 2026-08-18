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
        ? "Premium — unlimited receipts"
        : $"Free — {ReceiptCount}/{subscriptionService.FreeReceiptLimit} receipts used";

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
            var path = await exportService.ExportToCsvAsync(all, fileName);
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
            var path = await exportService.ExportToCsvAsync(monthly, fileName);
            await exportService.ShareFileAsync(path);
        });
    }

    [RelayCommand]
    private async Task UpgradeAsync()
    {
        // TODO: StoreKit 2 / Google Play integration
        await Shell.Current.DisplayAlertAsync(
            "Receipt Vault Premium",
            "Unlimited receipts for £2.99/month.\n\nIn-app purchase coming soon.",
            "OK");
    }

    [RelayCommand]
    private async Task RestorePurchasesAsync()
    {
        await subscriptionService.RestorePurchasesAsync();
        await LoadAsync();
    }
}
