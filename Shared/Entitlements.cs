namespace ReceiptVault.Shared;

// A purchasable plan. All paid tiers currently unlock the same feature set — they
// differ only in billing (price / cadence). Kept as distinct values so the paywall,
// analytics, and any future tier-specific perks have something to key off.
public enum ProductTier
{
    Free,
    Monthly,
    Annual,
    Lifetime,
}

// Individual premium capabilities, gated independently so the free app stays fully
// usable (unlimited receipts) and premium sells on real value rather than a count wall.
[Flags]
public enum Entitlement
{
    None        = 0,
    CloudSync   = 1 << 0,  // cloud backup + multi-device sync
    PdfExport   = 1 << 1,  // PDF export (basic CSV stays free)
    TaxReports  = 1 << 2,  // business-expense / tax summary reports
    LineItemOcr = 1 << 3,  // automatic line-item extraction + itemised export

    // The full premium bundle granted by every paid tier today.
    Premium = CloudSync | PdfExport | TaxReports | LineItemOcr,
}
