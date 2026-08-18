using CommunityToolkit.Mvvm.ComponentModel;

namespace ReceiptVault.ViewModels;

public partial class BaseViewModel : ObservableObject
{
    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string? errorMessage;

    [ObservableProperty]
    private string title = string.Empty;

    public bool IsNotBusy => !IsBusy;

    protected async Task RunAsync(Func<Task> action)
    {
        if (IsBusy) return;
        try
        {
            IsBusy = true;
            ErrorMessage = null;
            await action();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
