using System.Text;
using ReceiptVault.Models;

namespace ReceiptVault.Services;

public class ExportService : IExportService
{
    public async Task<string> ExportToCsvAsync(List<Receipt> receipts, string fileName,
        Dictionary<int, List<LineItem>>? lineItems = null)
    {
        var ordered = receipts.OrderByDescending(r => r.Date).ToList();

        var sb = new StringBuilder();
        sb.AppendLine("Date,Merchant,Total,Currency,Category,Type,Tax,Tags,Notes");

        foreach (var r in ordered)
        {
            sb.AppendLine(string.Join(",",
                r.Date.ToString("yyyy-MM-dd"),
                QuoteCsv(r.Merchant),
                r.Total.ToString("F2"),
                r.Currency,
                QuoteCsv(r.Category),
                r.IsBusinessExpense ? "Business" : "Personal",
                r.Tax?.ToString("F2") ?? "",
                QuoteCsv(r.Tags ?? ""),
                QuoteCsv(r.Notes ?? "")
            ));
        }

        // Second section: itemised lines, one row per purchased item.
        if (lineItems is not null && lineItems.Values.Any(v => v.Count > 0))
        {
            sb.AppendLine();
            sb.AppendLine("Receipt Items");
            sb.AppendLine("Date,Merchant,Item,Quantity,Price,Saving,Currency");

            foreach (var r in ordered)
            {
                if (!lineItems.TryGetValue(r.Id, out var items) || items.Count == 0)
                    continue;

                foreach (var item in items)
                {
                    sb.AppendLine(string.Join(",",
                        r.Date.ToString("yyyy-MM-dd"),
                        QuoteCsv(r.Merchant),
                        QuoteCsv(item.Description),
                        item.Quantity.ToString("0.##"),
                        item.Price.ToString("F2"),
                        item.Savings.ToString("F2"),
                        r.Currency
                    ));
                }
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
