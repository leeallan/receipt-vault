using ReceiptVault.ViewModels;

namespace ReceiptVault.Views;

public partial class ReceiptDetailPage : ContentPage
{
    private readonly ReceiptDetailViewModel _vm;

    public ReceiptDetailPage(ReceiptDetailViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
    }

    // Re-check entitlement on return (e.g. after unlocking Premium elsewhere) so the
    // item list appears without needing to re-open the receipt.
    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.RefreshPremiumState();
    }

    private async void OnBackClicked(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync("..");
}
