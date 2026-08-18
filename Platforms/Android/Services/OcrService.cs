using ReceiptVault.Models;
using ReceiptVault.Services;

namespace ReceiptVault.Services;

// Android OCR stub — wire up Google ML Kit Text Recognition here.
// Add NuGet: Xamarin.Google.MLKit.TextRecognition
// See: https://developers.google.com/ml-kit/vision/text-recognition/android
public class OcrService : IOcrService
{
    public Task<OcrResult> RecognizeReceiptAsync(string imagePath)
    {
        // TODO: implement with ML Kit
        // var image = InputImage.FromFilePath(imagePath);
        // var recognizer = TextRecognition.GetClient(new TextRecognizerOptions.Builder().Build());
        // var result = await recognizer.Process(image);
        // Parse result.Text ...

        return Task.FromResult(new OcrResult
        {
            RawText = string.Empty,
            Merchant = string.Empty
        });
    }
}
