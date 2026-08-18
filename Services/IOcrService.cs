using ReceiptVault.Models;

namespace ReceiptVault.Services;

public interface IOcrService
{
    Task<OcrResult> RecognizeReceiptAsync(string imagePath);
}
