using Plugin.InAppBilling;
using ReceiptVault.Shared;

namespace ReceiptVault.Services;

// Cross-platform in-app purchase wrapper over Plugin.InAppBilling (StoreKit on iOS,
// Play Billing on Android). Connects on demand and always disconnects afterwards.
//
// Entitlements are confirmed server-side when the entitlement server is configured: the
// signed transaction is sent to the server, which verifies it and returns the authoritative
// tier. When the server is unconfigured or unreachable, we fall back to the store's own
// answer so purchasing still works (offline grace / pre-deploy).
public class BillingService(ISubscriptionService subscriptions, IEntitlementApi entitlementApi)
    : IBillingService
{
    private static IInAppBilling Billing => CrossInAppBilling.Current;

    public async Task<IReadOnlyList<PremiumProduct>> GetProductsAsync()
    {
        try
        {
            if (!await Billing.ConnectAsync()) return [];

            var subs = await Billing.GetProductInfoAsync(ItemType.Subscription, ProductCatalog.SubscriptionIds);
            var oneTime = await Billing.GetProductInfoAsync(ItemType.InAppPurchase, ProductCatalog.NonConsumableIds);

            var products = (subs ?? []).Concat(oneTime ?? [])
                .Select(ToPremiumProduct)
                .OrderBy(p => (int)p.Tier)   // Monthly, Annual, Lifetime
                .ToList();

            return products;
        }
        catch
        {
            return [];
        }
        finally
        {
            await SafeDisconnectAsync();
        }
    }

    public async Task<PurchaseResult> PurchaseAsync(string productId)
    {
        var itemType = ProductCatalog.IsSubscription(productId)
            ? ItemType.Subscription
            : ItemType.InAppPurchase;

        try
        {
            if (!await Billing.ConnectAsync())
                return PurchaseResult.Fail("Couldn't reach the store. Please try again.");

            var purchase = await Billing.PurchaseAsync(productId, itemType);
            if (purchase is null)
                return PurchaseResult.Cancelled_();

            switch (purchase.State)
            {
                case PurchaseState.Purchased:
                case PurchaseState.Restored:
                    // Acknowledge/finalize so the store doesn't auto-refund after 3 days.
                    await Billing.FinalizePurchaseAsync([purchase.TransactionIdentifier]);
                    var diagnostics = DescribePurchase(purchase);
                    var tier = await ResolveTierAsync(purchase, productId);
                    var result = tier == ProductTier.Free
                        ? PurchaseResult.Fail("We couldn't confirm your purchase. If you were charged, tap Restore.")
                        : PurchaseResult.Ok(tier);
                    return result with { Diagnostics = diagnostics };

                case PurchaseState.PaymentPending:
                case PurchaseState.Deferred:
                    // e.g. Ask-to-Buy / SCA: entitlement arrives later via restore/notification.
                    return PurchaseResult.Fail("Your purchase is pending approval.");

                default:
                    return PurchaseResult.Fail($"Purchase didn't complete ({purchase.State}).");
            }
        }
        catch (InAppBillingPurchaseException ex) when (ex.PurchaseError == PurchaseError.UserCancelled)
        {
            return PurchaseResult.Cancelled_();
        }
        catch (InAppBillingPurchaseException ex) when (ex.PurchaseError == PurchaseError.AlreadyOwned)
        {
            // Already entitled — treat as success and reconcile.
            var tier = await RestoreAsync();
            return PurchaseResult.Ok(tier);
        }
        catch (InAppBillingPurchaseException ex)
        {
            return PurchaseResult.Fail(FriendlyError(ex.PurchaseError));
        }
        catch (Exception ex)
        {
            return PurchaseResult.Fail(ex.Message);
        }
        finally
        {
            await SafeDisconnectAsync();
        }
    }

    public async Task<ProductTier> RestoreAsync()
    {
        try
        {
            if (!await Billing.ConnectAsync()) return subscriptions.ActiveTier;

            var subs = await Billing.GetPurchasesAsync(ItemType.Subscription);
            var oneTime = await Billing.GetPurchasesAsync(ItemType.InAppPurchase);

            var owned = (subs ?? []).Concat(oneTime ?? [])
                .Where(p => p.State is PurchaseState.Purchased or PurchaseState.Restored)
                .ToList();

            if (owned.Count == 0)
            {
                subscriptions.SetTier(ProductTier.Free);
                return ProductTier.Free;
            }

            // Verify the highest-value entitlement the account owns.
            var best = owned
                .OrderByDescending(p => (int)ProductCatalog.TierFor(p.ProductId))
                .First();
            return await ResolveTierAsync(best, best.ProductId);
        }
        catch
        {
            return subscriptions.ActiveTier;
        }
        finally
        {
            await SafeDisconnectAsync();
        }
    }

    // Confirms a purchase with the entitlement server and applies the resulting tier.
    // Server (when configured) is authoritative — even a downgrade to Free is honoured, as
    // that means verification failed. When the server is unconfigured/unreachable we fall
    // back to the store's own product id so the user isn't blocked.
    private async Task<ProductTier> ResolveTierAsync(InAppBillingPurchase purchase, string productId)
    {
        var localTier = ProductCatalog.TierFor(productId);

        if (!entitlementApi.IsConfigured)
        {
            subscriptions.SetTier(localTier);
            return localTier;
        }

        var platform = DeviceInfo.Current.Platform == DevicePlatform.iOS
            ? StorePlatform.Apple
            : StorePlatform.Google;

        // Apple: send the transaction id — the server fetches the signed transaction (JWS)
        // from Apple's App Store Server API (Plugin.InAppBilling is StoreKit 1 and can't
        // hand us a JWS). Google: product id + purchase token.
        var request = platform == StorePlatform.Apple
            ? new VerifyEntitlementRequest(platform, TransactionId: purchase.TransactionIdentifier)
            : new VerifyEntitlementRequest(platform, ProductId: productId, PurchaseToken: purchase.PurchaseToken);

        var response = await entitlementApi.VerifyAsync(request);

        // Unreachable → offline grace: trust the store locally this session.
        var tier = response?.Tier ?? localTier;
        subscriptions.SetTier(tier);
        return tier;
    }

    // Diagnostic for sandbox testing: works out whether the store handed us Apple's
    // StoreKit 2 signed transaction (a JWS — three base64url parts, header starts "eyJ")
    // in PurchaseToken, which is what we forward to the server for verification.
    private static string DescribePurchase(InAppBillingPurchase p)
    {
        var token = p.PurchaseToken ?? string.Empty;
        var dots = token.Count(c => c == '.');
        var head = token.Length > 20 ? token[..20] : token;
        var looksJws = token.StartsWith("eyJ", StringComparison.Ordinal) && dots == 2;

#if DEBUG
        // Full token to the debug console for anyone able to read device logs.
        System.Diagnostics.Debug.WriteLine($"[IAP] PurchaseToken (len {token.Length}): {token}");
#endif

        return
            $"Product: {p.ProductId}\n" +
            $"State: {p.State}\n" +
            $"TxnId: {p.TransactionIdentifier}\n" +
            $"OrigTxnId: {p.OriginalTransactionIdentifier}\n" +
            $"PurchaseToken: len={token.Length}, dots={dots}\n" +
            $"head='{head}'\n" +
            $"Verdict: {(looksJws
                ? "PurchaseToken IS the StoreKit 2 JWS ✓"
                : "PurchaseToken is NOT a JWS (receipt/other) — JWS field TBD")}";
    }

    private static PremiumProduct ToPremiumProduct(InAppBillingProduct p) => new()
    {
        ProductId = p.ProductId,
        Tier = ProductCatalog.TierFor(p.ProductId),
        LocalizedPrice = p.LocalizedPrice,
        Title = p.Name,
        Description = p.Description,
    };

    private static string FriendlyError(PurchaseError error) => error switch
    {
        PurchaseError.PaymentNotAllowed => "Payments aren't allowed on this device.",
        PurchaseError.BillingUnavailable or PurchaseError.ServiceUnavailable
            or PurchaseError.AppStoreUnavailable => "The store is unavailable right now.",
        PurchaseError.ItemUnavailable or PurchaseError.InvalidProduct => "That plan isn't available.",
        _ => "Something went wrong with the purchase. Please try again.",
    };

    private static async Task SafeDisconnectAsync()
    {
        try { await Billing.DisconnectAsync(); } catch { /* best effort */ }
    }
}
