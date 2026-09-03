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
    CloudSync   = 1 << 0,  // iCloud (CloudKit) backup + multi-device sync
    PdfExport   = 1 << 1,  // PDF export (basic CSV stays free)
    TaxReports  = 1 << 2,  // business-expense / tax summary reports
    LineItemOcr = 1 << 3,  // automatic line-item extraction + itemised export

    // The full premium bundle unlocked by the one-time Premium purchase.
    Premium = CloudSync | PdfExport | TaxReports | LineItemOcr,
}
