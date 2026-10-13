using SQLite;

namespace ReceiptVault.Models;

[Table("Receipts")]
public class Receipt
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public string Merchant { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public decimal? Tax { get; set; }
    public string Currency { get; set; } = "GBP";
    public DateTime Date { get; set; } = DateTime.Today;
    public string Category { get; set; } = "Other";
    public bool IsBusinessExpense { get; set; }

    // Stores just the image's file name. The app's sandbox path isn't stable on iOS (it
    // changes on restore to a new device, and Apple doesn't guarantee it across updates),
    // so absolute paths go stale. Older rows hold an absolute path; ImageFullPath copes.
    public string? ImagePath { get; set; }
    public string? Notes { get; set; }
    public string? Tags { get; set; }
    public string? RawOcrText { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public static string ImagesDirectory => Path.Combine(FileSystem.AppDataDirectory, "receipt_images");

    // Resolves ImagePath against the current sandbox. Taking only the file name also
    // re-homes legacy absolute paths that point into an old container.
    [Ignore]
    public string? ImageFullPath => string.IsNullOrEmpty(ImagePath)
        ? null
        : Path.Combine(ImagesDirectory, Path.GetFileName(ImagePath));

    [Ignore]
    public string FormattedTotal => $"{CurrencySymbol}{Total:F2}";

    [Ignore]
    public string CurrencySymbol => Currency switch
    {
        "GBP" => "£",
        "USD" => "$",
        "EUR" => "€",
        _ => Currency + " "
    };

    [Ignore]
    public string[] TagList => string.IsNullOrWhiteSpace(Tags)
        ? []
        : Tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
