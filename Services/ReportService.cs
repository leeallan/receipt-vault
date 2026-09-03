using ReceiptVault.Models;

namespace ReceiptVault.Services;

// Composes the premium PDF reports and delegates the actual drawing to IPdfService.
public class ReportService(IPdfService pdf) : IReportService
{
    public Task<string?> BuildReceiptsPdfAsync(List<Receipt> receipts, string title, string subtitle, string fileName)
    {
        var ordered = receipts.OrderByDescending(r => r.Date).ToList();
        var symbol = ordered.FirstOrDefault()?.CurrencySymbol ?? "£";

        var rows = ordered
            .Select(r => (IReadOnlyList<string>)new[]
            {
                r.Date.ToString("yyyy-MM-dd"),
                r.Merchant,
                r.Category,
                r.IsBusinessExpense ? "Business" : "Personal",
                r.Tax is > 0 ? $"{r.CurrencySymbol}{r.Tax:F2}" : "",
                $"{r.CurrencySymbol}{r.Total:F2}",
            })
            .ToList();

        var report = new PdfReport
        {
            Title = title,
            Subtitle = subtitle,
            Summary =
            {
                ("Receipts", ordered.Count.ToString()),
                ("Total tax / VAT", $"{symbol}{ordered.Sum(r => r.Tax ?? 0):F2}"),
                ("Total spend", $"{symbol}{ordered.Sum(r => r.Total):F2}"),
            },
            Tables =
            {
                new PdfTable
                {
                    Columns = ["Date", "Merchant", "Category", "Type", "Tax", "Total"],
                    ColumnWeights = [1.4f, 2.4f, 1.6f, 1.2f, 1f, 1.1f],
                    RightAlign = [4, 5],
                    Rows = rows,
                },
            },
        };

        return pdf.RenderAsync(report, fileName);
    }

    public Task<string?> BuildTaxReportPdfAsync(List<Receipt> receipts, string periodLabel, string fileName)
    {
        var symbol = receipts.FirstOrDefault()?.CurrencySymbol ?? "£";
        var business = receipts.Where(r => r.IsBusinessExpense).OrderByDescending(r => r.Date).ToList();
        var personal = receipts.Where(r => !r.IsBusinessExpense).ToList();

        var byCategory = receipts
            .GroupBy(r => string.IsNullOrWhiteSpace(r.Category) ? "Other" : r.Category)
            .Select(g => new { Category = g.Key, Count = g.Count(), Total = g.Sum(r => r.Total), Tax = g.Sum(r => r.Tax ?? 0) })
            .OrderByDescending(g => g.Total)
            .ToList();

        var report = new PdfReport
        {
            Title = "Tax & expense report",
            Subtitle = periodLabel,
            Summary =
            {
                ("Total receipts", receipts.Count.ToString()),
                ("Business spend", $"{symbol}{business.Sum(r => r.Total):F2}"),
                ("Personal spend", $"{symbol}{personal.Sum(r => r.Total):F2}"),
                ("Total tax / VAT", $"{symbol}{receipts.Sum(r => r.Tax ?? 0):F2}"),
                ("Total spend", $"{symbol}{receipts.Sum(r => r.Total):F2}"),
            },
            Tables =
            {
                new PdfTable
                {
                    Heading = "Spending by category",
                    Columns = ["Category", "Receipts", "Tax", "Total"],
                    ColumnWeights = [3f, 1.2f, 1.3f, 1.5f],
                    RightAlign = [1, 2, 3],
                    Rows = byCategory
                        .Select(c => (IReadOnlyList<string>)
                            [c.Category, c.Count.ToString(), $"{symbol}{c.Tax:F2}", $"{symbol}{c.Total:F2}"])
                        .ToList(),
                },
                new PdfTable
                {
                    Heading = "Business expenses",
                    Columns = ["Date", "Merchant", "Category", "Tax", "Total"],
                    ColumnWeights = [1.4f, 2.6f, 1.8f, 1.1f, 1.2f],
                    RightAlign = [3, 4],
                    Rows = business
                        .Select(r => (IReadOnlyList<string>)new[]
                        {
                            r.Date.ToString("yyyy-MM-dd"),
                            r.Merchant,
                            r.Category,
                            r.Tax is > 0 ? $"{r.CurrencySymbol}{r.Tax:F2}" : "",
                            $"{r.CurrencySymbol}{r.Total:F2}",
                        })
                        .ToList(),
                },
            },
        };

        return pdf.RenderAsync(report, fileName);
    }
}
