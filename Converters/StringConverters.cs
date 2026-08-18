using System.Globalization;

namespace ReceiptVault.Converters;

/// <summary>Returns true when the string is not null/empty (use for IsVisible).</summary>
public class StringNotNullConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        !string.IsNullOrWhiteSpace(value as string);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

/// <summary>Returns true when the string IS null/empty (inverse of above).</summary>
public class StringNullConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        string.IsNullOrWhiteSpace(value as string);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

/// <summary>Converts bool IsEditing to button label text.</summary>
public class EditButtonConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? "Done" : "Edit";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

/// <summary>Converts bool IsBusinessExpense to display string.</summary>
public class ExpenseTypeConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? "Business" : "Personal";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}
