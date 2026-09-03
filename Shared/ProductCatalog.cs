namespace ReceiptVault.Shared;

// The single store product and how it maps to a plan/entitlements. Shared by the client
// and the server so the two can never disagree. The ID must match the non-consumable
// in-app purchase created in App Store Connect exactly.
public static class ProductCatalog
{
    // One-time, non-consumable "Premium" unlock.
    public const string Premium = "com.farabove.receiptvault.premium";

    public static readonly string[] NonConsumableIds = [Premium];

    // No subscriptions in the one-time-purchase model.
    public static bool IsSubscription(string productId) => false;

    public static ProductTier TierFor(string productId) =>
        productId == Premium ? ProductTier.Premium : ProductTier.Free;

    // The one-time purchase unlocks the whole premium bundle.
    public static Entitlement EntitlementsFor(ProductTier tier) =>
        tier == ProductTier.Free ? Entitlement.None : Entitlement.Premium;
}
