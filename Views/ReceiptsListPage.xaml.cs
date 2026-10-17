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

    // iOS keeps the keyboard up after a search and brings it back when returning from a
    // receipt, where it covers the tab bar. Results filter as you type, so drop it.
    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        SearchInput.Unfocus();
    }

    private void OnSearchButtonPressed(object? sender, EventArgs e) => SearchInput.Unfocus();
}
