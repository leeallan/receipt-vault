using ReceiptVault.Models;

namespace ReceiptVault.Services;

public class SubscriptionService : ISubscriptionService
{
    private const string TierKey = "active_tier";

    // Local cache of the entitlement state. Authoritative today; becomes a cache in
    // front of server-verified entitlements in Phase 3.
    public ProductTier ActiveTier
    {
        get => (ProductTier)Preferences.Default.Get(TierKey, (int)ProductTier.Free);
        private set => Preferences.Default.Set(TierKey, (int)value);
    }

    public bool IsPremium => ActiveTier != ProductTier.Free;

    // Every paid tier currently unlocks the whole premium bundle.
    public Entitlement ActiveEntitlements =>
        IsPremium ? Entitlement.Premium : Entitlement.None;

    public bool Has(Entitlement entitlement) =>
        (ActiveEntitlements & entitlement) == entitlement;

    // Unlimited receipts for everyone under the feature-gated model.
    public Task<bool> CheckCanAddReceiptAsync(int currentCount) => Task.FromResult(true);

    public async Task RestorePurchasesAsync()
    {
        // TODO (Phase 2/3): restore via StoreKit / Play Billing, then reconcile against
        // the entitlement server.
        await Task.CompletedTask;
    }

    public void SetTier(ProductTier tier) => ActiveTier = tier;
}
