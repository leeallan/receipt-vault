using Plugin.InAppBilling;
using ReceiptVault.Models;

namespace ReceiptVault.Services;

// Cross-platform in-app purchase wrapper over Plugin.InAppBilling (StoreKit on iOS,
// Play Billing on Android). Connects on demand and always disconnects afterwards.
//
// NOTE (Phase 3): purchases are currently trusted locally via ISubscriptionService.SetTier.
// This is the seam where server-side receipt/JWS verification will be inserted so the
// entitlement can't be spoofed and survives reinstalls.
public class BillingService(ISubscriptionService subscriptions) : IBillingService
{
    private static IInAppBilling Billing => CrossInAppBilling.Current;

    public async Task<IReadOnlyList<PremiumProduct>> GetProductsAsync()
    {
        try
        {
            if (!await Billing.ConnectAsync()) return [];

            var subs = await Billing.GetProductInfoAsync(ItemType.Subscription, BillingProducts.SubscriptionIds);
            var oneTime = await Billing.GetProductInfoAsync(ItemType.InAppPurchase, BillingProducts.NonConsumableIds);

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
        var itemType = BillingProducts.IsSubscription(productId)
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
                    var tier = BillingProducts.TierFor(productId);
                    subscriptions.SetTier(tier);   // TODO Phase 3: verify server-side first
                    return PurchaseResult.Ok(tier);

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
                .Select(p => BillingProducts.TierFor(p.ProductId))
                .DefaultIfEmpty(ProductTier.Free)
                .Max();   // Lifetime > Annual > Monthly > Free by enum order

            subscriptions.SetTier(owned);   // TODO Phase 3: verify server-side first
            return owned;
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

    private static PremiumProduct ToPremiumProduct(InAppBillingProduct p) => new()
    {
        ProductId = p.ProductId,
        Tier = BillingProducts.TierFor(p.ProductId),
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
