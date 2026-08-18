using SQLite;

namespace ReceiptVault.Models;

// A single purchased line on a receipt — what was bought, how many, what it cost,
// and any discount/saving applied to that line.
[Table("LineItems")]
public class LineItem
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int ReceiptId { get; set; }

    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; } = 1;

    // What the line cost (the amount actually charged for this line).
    public decimal Price { get; set; }

    // Discount / saving applied to this line, stored as a positive amount.
    public decimal Savings { get; set; }

    public string Currency { get; set; } = "GBP";

    [Ignore]
    public string CurrencySymbol => Currency switch
    {
        "GBP" => "£",
        "USD" => "$",
        "EUR" => "€",
        _ => Currency + " "
    };

    [Ignore]
    public string FormattedPrice => $"{CurrencySymbol}{Price:F2}";

    [Ignore]
    public string FormattedSavings => Savings > 0 ? $"−{CurrencySymbol}{Savings:F2}" : string.Empty;

    [Ignore]
    public bool HasSavings => Savings > 0;

    // "2 × " prefix when more than a single unit, otherwise blank.
    [Ignore]
    public string QuantityPrefix => Quantity > 1 ? $"{Quantity:0.##} × " : string.Empty;
}
