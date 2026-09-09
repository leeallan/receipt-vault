using ReceiptVault.ViewModels;

namespace ReceiptVault.Views;

public partial class ExportPage : ContentPage
{
    private readonly ExportViewModel _vm;

    public ExportPage(ExportViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
    }

    // Re-check entitlement on return (e.g. after unlocking Premium elsewhere).
    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.RefreshPremiumState();
    }

    private async void OnBackClicked(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync("..");
}
