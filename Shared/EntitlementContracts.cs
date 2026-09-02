namespace ReceiptVault.Shared;

public enum StorePlatform
{
    Apple,
    Google,
}

// Sent by the app to POST /entitlements/verify. The app forwards whatever proof the store
// gave it; the server verifies authenticity.
//  - Apple: TransactionId (from StoreKit) — the server fetches the signed transaction (JWS)
//    from Apple's App Store Server API. SignedTransaction may also be supplied directly
//    (webhooks / a future StoreKit 2 client).
//  - Google: ProductId + PurchaseToken are validated via the Play Developer API.
public record VerifyEntitlementRequest(
    StorePlatform Platform,
    string? SignedTransaction = null,
    string? ProductId = null,
    string? PurchaseToken = null,
    string? TransactionId = null);

// The authoritative entitlement state the server returns. The client caches this and
// treats it as the source of truth, falling back to the cache only when offline.
public record EntitlementResponse(
    ProductTier Tier,
    Entitlement Entitlements,
    bool IsActive,
    DateTimeOffset? ExpiresAt)
{
    public static EntitlementResponse Free { get; } =
        new(ProductTier.Free, Entitlement.None, false, null);
}
