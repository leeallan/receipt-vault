namespace ReceiptVault.Shared;

// The app is free with a single one-time "Premium" unlock (a non-consumable purchase).
public enum ProductTier
{
    Free,
    Premium,
}

// Individual premium capabilities, gated independently so the free app stays fully
// usable (unlimited receipts) and premium sells on real value rather than a count wall.
[Flags]
public enum Entitlement
{
    None        = 0,
    CloudSync   = 1 << 0,  // iCloud (CloudKit) backup + sync — reserved for a future release, not yet built or sold
    PdfExport   = 1 << 1,  // PDF export (basic CSV stays free)
    TaxReports  = 1 << 2,  // business-expense / tax summary reports
    LineItemOcr = 1 << 3,  // automatic line-item extraction + itemised export

    // The full premium bundle unlocked by the one-time Premium purchase. All local —
    // CloudSync is deliberately excluded until iCloud sync actually ships.
    Premium = PdfExport | TaxReports | LineItemOcr,
}
