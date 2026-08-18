using ReceiptVault.ViewModels;

namespace ReceiptVault.Views;

public partial class CapturePage : ContentPage
{
    private readonly CaptureViewModel _vm;

    public CapturePage(CaptureViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.Reset();
    }
}
