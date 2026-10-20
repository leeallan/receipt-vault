namespace ReceiptVault.Services;

// The currency chosen in Settings and how amounts are written in it. Each receipt keeps
// the currency it was saved in; totals across receipts are shown in the chosen one.
public static class CurrencyFormat
{
    public const string PreferenceKey = "default_currency";

    public static string Code => Preferences.Default.Get(PreferenceKey, "GBP");

    public static string Symbol => SymbolFor(Code);

    public static string SymbolFor(string? code) => code switch
    {
        "GBP" or null or "" => "£",
        "USD" => "$",
        "EUR" => "€",
        _ => code + " ",
    };

    public static string Format(decimal amount) => $"{Symbol}{amount:F2}";

    // UK and EU receipts show VAT; US receipts show sales tax.
    public static string TaxName(string? code) => code == "USD" ? "sales tax" : "VAT";
}
