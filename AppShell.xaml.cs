using ReceiptVault.Views;

namespace ReceiptVault;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        Routing.RegisterRoute(nameof(CapturePage), typeof(CapturePage));
        Routing.RegisterRoute(nameof(ReceiptDetailPage), typeof(ReceiptDetailPage));
        Routing.RegisterRoute(nameof(ExportPage), typeof(ExportPage));
    }
}
