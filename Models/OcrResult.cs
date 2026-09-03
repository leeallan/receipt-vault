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

    // Guardrail so the app only stores actual receipts (not arbitrary photos).
    public ReceiptValidation Validation { get; set; } = new();
}

// Signals used to decide whether a captured image is really a receipt. Defaults to
// "likely" so platforms without OCR (the Android stub) don't block; the iOS OCR service
// always sets this explicitly.
public class ReceiptValidation
{
    public bool IsLikelyReceipt { get; set; } = true;

    // Number of price-shaped tokens found (e.g. 1.99, 12,50).
    public int PriceCount { get; set; }

    // Fraction of the frame (0–1) filled by the detected document, if one was found.
    public double DocumentCoverage { get; set; }

    // Fraction of the frame (0–1) spanned by the recognised text.
    public double TextCoverage { get; set; }
}
