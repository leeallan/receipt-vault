using CommunityToolkit.Mvvm.ComponentModel;

namespace ReceiptVault.ViewModels;

/// <summary>
/// One category chip on the receipts list, carrying its own selected state so the
/// active filter is visible at a glance.
/// </summary>
public partial class FilterChip : ObservableObject
{
    public string Name { get; init; } = string.Empty;

    [ObservableProperty]
    private bool isSelected;
}
