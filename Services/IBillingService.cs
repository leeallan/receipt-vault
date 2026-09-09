using ReceiptVault.Shared;

namespace ReceiptVault.Services;

// A purchasable plan as returned by the store, with its localized price.
public class PremiumProduct
{
    public string ProductId { get; set; } = string.Empty;
    public ProductTier Tier { get; set; }
    public string LocalizedPrice { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

// Outcome of a purchase attempt. Cancellation is distinguished from failure so the UI
// can stay silent when the user simply backs out of the store sheet.
public record PurchaseResult(bool Success, ProductTier Tier, bool Cancelled, string? Error)
{
    public static PurchaseResult Ok(ProductTier tier) => new(true, tier, false, null);
    public static PurchaseResult Cancelled_() => new(false, ProductTier.Free, true, null);
    public static PurchaseResult Fail(string error) => new(false, ProductTier.Free, false, error);
}

public interface IBillingService
{
    // Fetches the live, localized products from the store (empty on failure/unavailable).
    Task<IReadOnlyList<PremiumProduct>> GetProductsAsync();

    // Runs the purchase flow for a product and, on success, applies the tier.
    Task<PurchaseResult> PurchaseAsync(string productId);

    // Restores prior purchases and returns the highest owned tier.
    Task<ProductTier> RestoreAsync();

    // Debug-only: runs the same restore query but returns a human-readable report of what
    // the store returned (connection, purchase count, product ids/states, or the error) so
    // sandbox "no purchases found" issues can be diagnosed on-device.
    Task<string> DiagnoseRestoreAsync();
}
