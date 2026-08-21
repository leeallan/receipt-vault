using ReceiptVault.Models;

namespace ReceiptVault.Services;

public interface ISubscriptionService
{
    // The user's current plan. Free until a verified purchase upgrades it.
    ProductTier ActiveTier { get; }

    // Convenience flag: true for any paid tier.
    bool IsPremium { get; }

    // The feature set the active tier grants right now.
    Entitlement ActiveEntitlements { get; }

    // Whether the user is entitled to a specific premium capability.
    bool Has(Entitlement entitlement);

    // Feature-gated model: receipts are unlimited for everyone. Retained so callers have
    // a single place to ask, and so a future policy change has a seam to hook into.
    Task<bool> CheckCanAddReceiptAsync(int currentCount);

    // Re-reads entitlements from the store / server (Phase 2/3). No-op placeholder today.
    Task RestorePurchasesAsync();

    // Applies a tier after a verified purchase. In Phase 3 the server becomes the
    // authoritative source and this just updates the local cache.
    void SetTier(ProductTier tier);
}
