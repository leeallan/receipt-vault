namespace ReceiptVault.Services;

public class SubscriptionService : ISubscriptionService
{
    private const string PremiumKey = "is_premium";
    public int FreeReceiptLimit => 20;

    public bool IsPremium
    {
        get => Preferences.Default.Get(PremiumKey, false);
        private set => Preferences.Default.Set(PremiumKey, value);
    }

    public async Task<bool> CheckCanAddReceiptAsync(int currentCount)
    {
        if (IsPremium) return true;
        return currentCount < FreeReceiptLimit;
    }

    public async Task RestorePurchasesAsync()
    {
        // TODO: integrate with Apple StoreKit 2 / Google Play Billing
        // For now, this is a placeholder
        await Task.CompletedTask;
    }

    // Call this after successful purchase confirmation
    public void ActivatePremium() => IsPremium = true;
}
