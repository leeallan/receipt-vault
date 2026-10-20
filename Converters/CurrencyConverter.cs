using System.Globalization;
using ReceiptVault.Services;

namespace ReceiptVault.Converters;

public class CurrencyConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // ConverterParameter is an optional prefix, e.g. "of ".
        var amount = value switch { decimal d => d, double dbl => (decimal)dbl, _ => 0m };
        return $"{parameter}{CurrencyFormat.Format(amount)}";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}
