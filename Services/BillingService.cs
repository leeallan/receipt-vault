using Plugin.InAppBilling;
using ReceiptVault.Shared;

namespace ReceiptVault.Services;

// Cross-platform in-app purchase wrapper over Plugin.InAppBilling (StoreKit on iOS,
// Play Billing on Android). Connects on demand and always disconnects afterwards.
//
// Premium is a single non-consumable purchase, so ownership is tracked by the store: a
// successful purchase or a Restore is trusted locally. (No entitlement server — for a
// one-time unlock, Apple's native Restore covers reinstalls and new devices.)
public class BillingService(ISubscriptionService subscriptions) : IBillingService
{
    private static IInAppBilling Billing => CrossInAppBilling.Current;

    public async Task<IReadOnlyList<PremiumProduct>> GetProductsAsync()
    {
        try
        {
            if (!await Billing.ConnectAsync()) return [];

            var products = await Billing.GetProductInfoAsync(ItemType.InAppPurchase, ProductCatalog.NonConsumableIds);

            return (products ?? [])
                .Select(ToPremiumProduct)
                .ToList();
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
        try
        {
            if (!await Billing.ConnectAsync())
                return PurchaseResult.Fail("Couldn't reach the store. Please try again.");

            var purchase = await Billing.PurchaseAsync(productId, ItemType.InAppPurchase);
            if (purchase is null)
                return PurchaseResult.Cancelled_();

            switch (purchase.State)
            {
                case PurchaseState.Purchased:
                case PurchaseState.Restored:
                    // Acknowledge/finalize so the store doesn't auto-refund after 3 days.
                    await Billing.FinalizePurchaseAsync([purchase.TransactionIdentifier]);
                    var tier = ProductCatalog.TierFor(productId);
                    subscriptions.SetTier(tier);
                    return PurchaseResult.Ok(tier);

                case PurchaseState.PaymentPending:
                case PurchaseState.Deferred:
                    // e.g. Ask-to-Buy / SCA: entitlement arrives later via restore.
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

            var owned = (await Billing.GetPurchasesAsync(ItemType.InAppPurchase) ?? [])
                .Where(p => p.State is PurchaseState.Purchased or PurchaseState.Restored)
                .ToList();

            // Premium is owned if ANY owned purchase maps to it — don't just inspect the
            // first transaction. The store can return purchases in any order, and sandbox
            // accounts accumulate legacy/unrelated product ids that map to Free.
            var tier = owned.Any(p => ProductCatalog.TierFor(p.ProductId) != ProductTier.Free)
                ? ProductTier.Premium
                : ProductTier.Free;

            subscriptions.SetTier(tier);
            return tier;
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

    public async Task<string> DiagnoseRestoreAsync()
    {
        try
        {
            var connected = await Billing.ConnectAsync();
            if (!connected) return "ConnectAsync returned FALSE — could not reach the store.";

            var all = (await Billing.GetPurchasesAsync(ItemType.InAppPurchase) ?? []).ToList();

            var lines = new List<string> { $"Connected: true", $"Purchases returned: {all.Count}" };
            foreach (var p in all)
                lines.Add($"• {p.ProductId} | {p.State} | txn={p.TransactionIdentifier}");

            var owned = all
                .Where(p => p.State is PurchaseState.Purchased or PurchaseState.Restored)
                .ToList();
            lines.Add($"Owned (Purchased/Restored): {owned.Count}");
            lines.Add($"Expecting id: {ProductCatalog.Premium}");

            return string.Join("\n", lines);
        }
        catch (Exception ex)
        {
            return $"EXCEPTION: {ex.GetType().Name}\n{ex.Message}";
        }
        finally
        {
            await SafeDisconnectAsync();
        }
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
        PurchaseError.ItemUnavailable or PurchaseError.InvalidProduct => "That purchase isn't available.",
        _ => "Something went wrong with the purchase. Please try again.",
    };

    private static async Task SafeDisconnectAsync()
    {
        try { await Billing.DisconnectAsync(); } catch { /* best effort */ }
    }
}
