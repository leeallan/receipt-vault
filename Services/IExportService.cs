using ReceiptVault.Models;

namespace ReceiptVault.Services;

public interface IExportService
{
    // Free tier: a plain, receipt-level CSV (no line items). from/to label the range.
    Task<string> ExportToCsvAsync(List<Receipt> receipts, string fileName, DateTime from, DateTime to);

    // Premium: a styled .xlsx spreadsheet. When lineItems is supplied each receipt's items
    // are written as shaded sub-rows beneath it (itemised); when null, receipt-level only.
    Task<string> ExportToXlsxAsync(List<Receipt> receipts, string fileName,
        Dictionary<int, List<LineItem>>? lineItems, DateTime from, DateTime to);

    Task ShareFileAsync(string filePath);
}
