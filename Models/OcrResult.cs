namespace ReceiptVault.Models;

public class OcrResult
{
    public string Merchant { get; set; } = string.Empty;
    public decimal? Total { get; set; }
    public decimal? Tax { get; set; }
    public DateTime? Date { get; set; }
    public string RawText { get; set; } = string.Empty;
    public List<LineItem> Items { get; set; } = [];
    public bool Success => !string.IsNullOrWhiteSpace(RawText);
}
