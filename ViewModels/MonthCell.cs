using CommunityToolkit.Mvvm.ComponentModel;

namespace ReceiptVault.ViewModels;

/// <summary>
/// One cell in the dashboard's month/year picker grid.
/// </summary>
public partial class MonthCell : ObservableObject
{
    public int Year { get; init; }
    public int Month { get; init; }

    public string Label => new DateTime(Year, Month, 1).ToString("MMM");

    [ObservableProperty]
    private bool isSelected;

    // Months that haven't happened yet can't be picked, mirroring the ‹ › arrows.
    [ObservableProperty]
    private bool isAvailable = true;
}
