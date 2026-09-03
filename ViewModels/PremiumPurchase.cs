using ReceiptVault.Services;

namespace ReceiptVault.ViewModels;

// Shared one-time Premium unlock flow: load the product, confirm the price, purchase,
// and report the outcome. Returns true only if Premium is now unlocked. Used everywhere
// the unlock is offered so the copy and behaviour stay consistent.
public static class PremiumPurchase
{
    public static async Task<bool> RunAsync(IBillingService billing)
    {
        var products = await billing.GetProductsAsync();
        var premium = products.FirstOrDefault();
        if (premium is null)
        {
            await Shell.Current.DisplayAlertAsync(
                "Store unavailable",
                "Couldn't load Premium right now. Please try again later.",
                "OK");
            return false;
        }

        var confirm = await Shell.Current.DisplayAlertAsync(
            "Unlock Premium",
            $"Unlock automatic item scanning, PDF export, and tax & expense reports for a one-time {premium.LocalizedPrice}.",
            $"Unlock — {premium.LocalizedPrice}", "Not now");
        if (!confirm) return false;

        var result = await billing.PurchaseAsync(premium.ProductId);

        if (result.Success)
        {
            await Shell.Current.DisplayAlertAsync(
                "Welcome to Premium", "Thanks! Your premium features are now unlocked.", "OK");
            return true;
        }

        if (!result.Cancelled)
            await Shell.Current.DisplayAlertAsync(
                "Purchase failed", result.Error ?? "Please try again.", "OK");
        return false;
    }
}
