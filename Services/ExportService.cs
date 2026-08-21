using System.Text;
using ReceiptVault.Models;

namespace ReceiptVault.Services;

public class ExportService : IExportService
{
    public async Task<string> ExportToCsvAsync(List<Receipt> receipts, string fileName,
        Dictionary<int, List<LineItem>>? lineItems = null)
    {
        var ordered = receipts.OrderByDescending(r => r.Date).ToList();

        // Single table: each receipt is a "Receipt" row immediately followed by its "Item"
        // rows. ReceiptId links them so the file stays correct if re-sorted in a spreadsheet;
        // item rows repeat Date/Merchant/Category/Expense for filtering and pivots, but leave
        // Total/Tax blank so summing the Total column never double-counts.
        var sb = new StringBuilder();
        sb.AppendLine("ReceiptId,RowType,Date,Merchant,Category,Expense,Item,Qty,Price,Saving,Total,Tax,Currency,Tags,Notes");

        foreach (var r in ordered)
        {
            var items = lineItems is not null && lineItems.TryGetValue(r.Id, out var found)
                ? found
                : [];

            var expense = r.IsBusinessExpense ? "Business" : "Personal";
            var receiptSavings = items.Sum(i => i.Savings);

            // Receipt summary row.
            sb.AppendLine(string.Join(",",
                r.Id,
                "Receipt",
                r.Date.ToString("yyyy-MM-dd"),
                QuoteCsv(r.Merchant),
                QuoteCsv(r.Category),
                expense,
                "",                                             // Item
                "",                                             // Qty
                "",                                             // Price
                receiptSavings > 0 ? receiptSavings.ToString("F2") : "", // Saving (receipt total)
                r.Total.ToString("F2"),
                r.Tax?.ToString("F2") ?? "",
                r.Currency,
                QuoteCsv(r.Tags ?? ""),
                QuoteCsv(r.Notes ?? "")
            ));

            // One row per purchased sub-item.
            foreach (var item in items)
            {
                sb.AppendLine(string.Join(",",
                    r.Id,
                    "Item",
                    r.Date.ToString("yyyy-MM-dd"),
                    QuoteCsv(r.Merchant),
                    QuoteCsv(r.Category),
                    expense,
                    QuoteCsv(item.Description),
                    item.Quantity.ToString("0.##"),
                    item.Price.ToString("F2"),
                    item.Savings.ToString("F2"),
                    "",                                         // Total (receipt-level only)
                    "",                                         // Tax (receipt-level only)
                    r.Currency,
                    "",                                         // Tags
                    ""                                          // Notes
                ));
            }
        }

        var exportDir = Path.Combine(FileSystem.CacheDirectory, "exports");
        Directory.CreateDirectory(exportDir);
        var filePath = Path.Combine(exportDir, fileName.EndsWith(".csv") ? fileName : fileName + ".csv");

        await File.WriteAllTextAsync(filePath, sb.ToString(), Encoding.UTF8);
        return filePath;
    }

    public async Task ShareFileAsync(string filePath)
    {
        await Share.Default.RequestAsync(new ShareFileRequest
        {
            Title = "Export Receipts",
            File = new ShareFile(filePath)
        });
    }

    private static string QuoteCsv(string value) =>
        value.Contains(',') || value.Contains('"') || value.Contains('\n')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
}
