using ReceiptVault.ViewModels;

namespace ReceiptVault.Views;

public partial class ReceiptsListPage : ContentPage
{
    private readonly ReceiptsListViewModel _vm;

    public ReceiptsListPage(ReceiptsListViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.LoadAsync();
    }
}
