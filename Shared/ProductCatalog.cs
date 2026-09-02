namespace ReceiptVault.Shared;

// The store product identifiers and how they map to plans/entitlements. Shared by the
// client and the server so the two can never disagree on what a product grants. These
// IDs must match the products created in App Store Connect and Google Play exactly.
public static class ProductCatalog
{
    public const string Monthly = "com.farabove.receiptvault.premium.monthly";
    public const string Annual = "com.farabove.receiptvault.premium.annual";
    public const string Lifetime = "com.farabove.receiptvault.premium.lifetime";

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

    // Cloud sync carries a recurring storage cost, so it's funded by recurring revenue:
    // the subscriptions include it, while Lifetime (a one-time payment) unlocks every
    // on-device premium feature but NOT cloud sync.
    public static Entitlement EntitlementsFor(ProductTier tier) => tier switch
    {
        ProductTier.Monthly or ProductTier.Annual => Entitlement.Premium,
        ProductTier.Lifetime => Entitlement.Premium & ~Entitlement.CloudSync,
        _ => Entitlement.None,
    };
}
