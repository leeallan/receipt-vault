namespace ReceiptVault.Shared;

// The store product identifiers and how they map to plans/entitlements. Shared by the
// client and the server so the two can never disagree on what a product grants. These
// IDs must match the products created in App Store Connect and Google Play exactly.
public static class ProductCatalog
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

    // Every paid tier unlocks the whole premium bundle today.
    public static Entitlement EntitlementsFor(ProductTier tier) =>
        tier == ProductTier.Free ? Entitlement.None : Entitlement.Premium;
}
