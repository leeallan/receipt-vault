namespace ReceiptVault.Services;

public interface ISubscriptionService
{
    bool IsPremium { get; }
    int FreeReceiptLimit { get; }
    Task<bool> CheckCanAddReceiptAsync(int currentCount);
    Task RestorePurchasesAsync();
}
