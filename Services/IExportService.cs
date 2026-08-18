using ReceiptVault.Models;

namespace ReceiptVault.Services;

public interface IExportService
{
    Task<string> ExportToCsvAsync(List<Receipt> receipts, string fileName,
        Dictionary<int, List<LineItem>>? lineItems = null);
    Task ShareFileAsync(string filePath);
}
