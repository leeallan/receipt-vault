using ReceiptVault.Models;

namespace ReceiptVault.Services;

// The store product identifiers and how they map to plans. These IDs must match the
// products created in App Store Connect and Google Play Console exactly.
public static class BillingProducts
{
    public const string Monthly = "com.receiptvault.premium.monthly";
    public const string Annual = "com.receiptvault.premium.annual";
    public const string Lifetime = "com.receiptvault.premium.lifetime";

    // Auto-renewing subscriptions.
    public static readonly string[] SubscriptionIds = [Monthly, Annual];

    // One-time non-consumable purchase.
    public static readonly string[] NonConsumableIds = [Lifetime];

    public static bool IsSubscription(string productId) => SubscriptionIds.Contains(productId);

    public static ProductTier TierFor(string productId) => productId switch
    {
        Monthly => ProductTier.Monthly,
        Annual => ProductTier.Annual,
        Lifetime => ProductTier.Lifetime,
        _ => ProductTier.Free,
    };
}
