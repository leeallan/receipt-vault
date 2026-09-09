using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReceiptVault.Services;
using ReceiptVault.Shared;

namespace ReceiptVault.ViewModels;

public partial class SettingsViewModel(
    IReceiptService receiptService,
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

    // Opens the Export screen (date-range + itemised filters, spreadsheet/PDF/tax report).
    [RelayCommand]
    private async Task OpenExportAsync() => await Shell.Current.GoToAsync(nameof(Views.ExportPage));

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

    // Debug-only: surfaces exactly what the store returns for a restore query, so we can see
    // whether StoreKit reports the sandbox purchase (and under which product id/state).
    [RelayCommand]
    private async Task DiagnoseRestoreAsync()
    {
        await RunAsync(async () =>
        {
            var report = await billingService.DiagnoseRestoreAsync();
            await Shell.Current.DisplayAlertAsync("Restore diagnostics", report, "OK");
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
                    : "Your Premium unlock has been restored.",
                "OK");
        });
    }
}
