using System.Text;
using ClosedXML.Excel;
using ReceiptVault.Models;

namespace ReceiptVault.Services;

// Produces a presentable Excel workbook (bold indigo header, business rows tinted,
// itemised sub-rows shaded, a totals row, and currency-formatted money columns), plus
// a plain receipt-level CSV for the free tier.
public class ExportService : IExportService
{
    // ── Free: plain receipt-level CSV (no line items) ──────────────────────────
    public Task<string> ExportToCsvAsync(List<Receipt> receipts, string fileName, DateTime from, DateTime to) =>
        Task.Run(() => BuildCsv(receipts, fileName));

    static string BuildCsv(List<Receipt> receipts, string fileName)
    {
        var ordered = receipts.OrderByDescending(r => r.Date).ToList();

        var sb = new StringBuilder();
        sb.AppendLine("Date,Merchant,Category,Type,Total,Tax,Currency,Tags,Notes");
        foreach (var r in ordered)
        {
            sb.AppendLine(string.Join(",",
                r.Date.ToString("yyyy-MM-dd"),
                QuoteCsv(r.Merchant),
                QuoteCsv(r.Category),
                r.IsBusinessExpense ? "Business" : "Personal",
                r.Total.ToString("F2"),
                r.Tax?.ToString("F2") ?? "",
                r.Currency,
                QuoteCsv(r.Tags ?? ""),
                QuoteCsv(r.Notes ?? "")));
        }

        var exportDir = Path.Combine(FileSystem.CacheDirectory, "exports");
        Directory.CreateDirectory(exportDir);
        var path = Path.Combine(exportDir, fileName.EndsWith(".csv") ? fileName : fileName + ".csv");
        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        return path;
    }

    static string QuoteCsv(string value) =>
        value.Contains(',') || value.Contains('"') || value.Contains('\n')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;

    // ── Premium: styled XLSX ───────────────────────────────────────────────────
    static readonly XLColor Indigo = XLColor.FromHtml("#6C63FF");
    static readonly XLColor BusinessTint = XLColor.FromHtml("#EEECFF");
    static readonly XLColor ItemShade = XLColor.FromHtml("#F4F4F7");
    static readonly XLColor TotalTint = XLColor.FromHtml("#D7F7EE");

    public Task<string> ExportToXlsxAsync(List<Receipt> receipts, string fileName,
        Dictionary<int, List<LineItem>>? lineItems, DateTime from, DateTime to)
    {
        // ClosedXML is synchronous; build off the UI thread.
        return Task.Run(() => Build(receipts, fileName, lineItems, from, to));
    }

    static string Build(List<Receipt> receipts, string fileName,
        Dictionary<int, List<LineItem>>? lineItems, DateTime from, DateTime to)
    {
        var ordered = receipts.OrderByDescending(r => r.Date).ToList();
        var itemised = lineItems is not null;
        var symbol = ordered.FirstOrDefault()?.CurrencySymbol ?? "£";

        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Receipts");

        // Title block.
        ws.Cell(1, 1).Value = "Receipt Vault — Receipts";
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 16;
        ws.Cell(2, 1).Value = $"{from:d MMM yyyy} – {to:d MMM yyyy}   ·   {ordered.Count} receipt(s)";
        ws.Cell(2, 1).Style.Font.FontColor = XLColor.Gray;

        string[] headers = itemised
            ? ["Date", "Merchant", "Category", "Type", "Item", "Qty", "Price", "Saving", "Tax", "Total"]
            : ["Date", "Merchant", "Category", "Type", "Tax", "Total"];
        int cols = headers.Length;
        int taxCol = cols - 1;
        int totalCol = cols;

        const int headerRow = 4;
        for (int c = 1; c <= cols; c++)
        {
            var cell = ws.Cell(headerRow, c);
            cell.Value = headers[c - 1];
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = Indigo;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        int row = headerRow + 1;
        foreach (var r in ordered)
        {
            var rowSymbol = r.CurrencySymbol;

            ws.Cell(row, 1).Value = r.Date.ToString("dd MMM yyyy");
            ws.Cell(row, 2).Value = r.Merchant;
            ws.Cell(row, 3).Value = r.Category;
            ws.Cell(row, 4).Value = r.IsBusinessExpense ? "Business" : "Personal";
            SetMoney(ws.Cell(row, taxCol), r.Tax is > 0 ? r.Tax : null, rowSymbol);
            SetMoney(ws.Cell(row, totalCol), r.Total, rowSymbol);

            var receiptRange = ws.Range(row, 1, row, cols);
            receiptRange.Style.Font.Bold = true;
            if (r.IsBusinessExpense)
                receiptRange.Style.Fill.BackgroundColor = BusinessTint;
            row++;

            if (itemised && lineItems!.TryGetValue(r.Id, out var items))
            {
                foreach (var it in items)
                {
                    ws.Cell(row, 5).Value = it.Description;
                    ws.Cell(row, 5).Style.Alignment.Indent = 1;
                    ws.Cell(row, 6).Value = it.Quantity;
                    SetMoney(ws.Cell(row, 7), it.Price, rowSymbol);
                    SetMoney(ws.Cell(row, 8), it.Savings > 0 ? it.Savings : null, rowSymbol);
                    ws.Range(row, 1, row, cols).Style.Fill.BackgroundColor = ItemShade;
                    row++;
                }
            }
        }

        // Totals row.
        int totalRow = row + 1;
        ws.Cell(totalRow, 1).Value = "TOTAL";
        SetMoney(ws.Cell(totalRow, taxCol), ordered.Sum(r => r.Tax ?? 0), symbol);
        SetMoney(ws.Cell(totalRow, totalCol), ordered.Sum(r => r.Total), symbol);
        var totalRange = ws.Range(totalRow, 1, totalRow, cols);
        totalRange.Style.Font.Bold = true;
        totalRange.Style.Fill.BackgroundColor = TotalTint;

        ws.SheetView.FreezeRows(headerRow);
        ws.Columns().AdjustToContents();

        var exportDir = Path.Combine(FileSystem.CacheDirectory, "exports");
        Directory.CreateDirectory(exportDir);
        var path = Path.Combine(exportDir, fileName.EndsWith(".xlsx") ? fileName : fileName + ".xlsx");
        wb.SaveAs(path);
        return path;
    }

    static void SetMoney(IXLCell cell, decimal? value, string symbol)
    {
        if (value is null) return;
        cell.Value = value.Value;
        cell.Style.NumberFormat.Format = $"\"{symbol}\"#,##0.00";
        cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
    }

    public async Task ShareFileAsync(string filePath)
    {
        await Share.Default.RequestAsync(new ShareFileRequest
        {
            Title = "Export Receipts",
            File = new ShareFile(filePath)
        });
    }
}
