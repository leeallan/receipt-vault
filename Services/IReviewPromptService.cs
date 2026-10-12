namespace ReceiptVault.Services;

public interface IReviewPromptService
{
    Task OnReceiptSavedAsync();
    Task OnExportCompletedAsync();
}
