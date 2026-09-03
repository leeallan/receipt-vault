using ReceiptVault.Models;

namespace ReceiptVault.Services;

// Builds the premium PDF reports from receipt data. Cross-platform: it composes a
// PdfReport and hands it to IPdfService for rendering.
public interface IReportService
{
    // A polished receipt-level PDF listing (date, merchant, category, type, tax, total)
    // with spend/tax totals. Returns the file path, or null if PDF isn't supported.
    Task<string?> BuildReceiptsPdfAsync(List<Receipt> receipts, string title, string subtitle, string fileName);

    // A tax / expense summary: business vs personal totals, total tax, spend by category,
    // and an itemised business-expense table. Returns the file path, or null.
    Task<string?> BuildTaxReportPdfAsync(List<Receipt> receipts, string periodLabel, string fileName);
}
