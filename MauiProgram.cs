using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using ReceiptVault.Services;
using ReceiptVault.ViewModels;
using ReceiptVault.Views;

namespace ReceiptVault;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // Services
        builder.Services.AddSingleton<DatabaseService>();
        builder.Services.AddSingleton<IReceiptService, ReceiptService>();
        builder.Services.AddSingleton<IExportService, ExportService>();
        builder.Services.AddSingleton<ISubscriptionService, SubscriptionService>();
        builder.Services.AddSingleton<IBillingService, BillingService>();
        builder.Services.AddSingleton<IOcrService, OcrService>();

        // ViewModels
        builder.Services.AddTransient<DashboardViewModel>();
        builder.Services.AddTransient<CaptureViewModel>();
        builder.Services.AddTransient<ReceiptsListViewModel>();
        builder.Services.AddTransient<ReceiptDetailViewModel>();
        builder.Services.AddTransient<BudgetViewModel>();
        builder.Services.AddTransient<SettingsViewModel>();

        // Pages
        builder.Services.AddTransient<DashboardPage>();
        builder.Services.AddTransient<CapturePage>();
        builder.Services.AddTransient<ReceiptsListPage>();
        builder.Services.AddTransient<ReceiptDetailPage>();
        builder.Services.AddTransient<BudgetPage>();
        builder.Services.AddTransient<SettingsPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
