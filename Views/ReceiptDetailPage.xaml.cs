using ReceiptVault.ViewModels;

namespace ReceiptVault.Views;

public partial class ReceiptDetailPage : ContentPage
{
    public ReceiptDetailPage(ReceiptDetailViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    private async void OnBackClicked(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync("..");
}
