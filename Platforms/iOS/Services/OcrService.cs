using Foundation;
using UIKit;
using Vision;
using ReceiptVault.Models;
using ReceiptVault.Services;

namespace ReceiptVault.Services;

public class OcrService : IOcrService
{
    public Task<OcrResult> RecognizeReceiptAsync(string imagePath)
    {
        return Task.Run(() =>
        {
            var image = UIImage.FromFile(imagePath);
            // A photo we can't even decode isn't a receipt we can store.
            if (image?.CGImage is null)
                return new OcrResult { Validation = new ReceiptValidation { IsLikelyReceipt = false } };

            var textRequest = new VNRecognizeTextRequest(completionHandler: null);
            textRequest.RecognitionLevel = VNRequestTextRecognitionLevel.Accurate;
            textRequest.UsesLanguageCorrection = true;

            // Detects the document (receipt) region so we can measure how much of the
            // frame it fills — an image where the "receipt" is a small part of a larger
            // scene is rejected.
            var docRequest = new VNDetectDocumentSegmentationRequest(completionHandler: null);

            var handler = new VNImageRequestHandler(image.CGImage, new NSDictionary());
            handler.Perform([textRequest, docRequest], out var error);

            if (error is not null || textRequest.Results is null)
                return new OcrResult { Validation = new ReceiptValidation { IsLikelyReceipt = false } };

            var tokens = textRequest.Results
                .OfType<VNRecognizedTextObservation>()
                .Select(obs => (Obs: obs, Text: obs.TopCandidates(1).FirstOrDefault()?.String ?? ""))
                .Where(o => !string.IsNullOrWhiteSpace(o.Text))
                .Select(o => new OcrToken(
                    o.Text,
                    o.Obs.BoundingBox.X, o.Obs.BoundingBox.Y, o.Obs.BoundingBox.Width, o.Obs.BoundingBox.Height,
                    o.Obs.TopLeft.X, o.Obs.TopLeft.Y, o.Obs.TopRight.X, o.Obs.TopRight.Y))
                .ToList();

            var documentCoverage = docRequest.Results?
                .OfType<VNRectangleObservation>()
                .Select(r => (double)(r.BoundingBox.Width * r.BoundingBox.Height))
                .DefaultIfEmpty(0)
                .Max() ?? 0;

            var result = ReceiptTextParser.Parse(tokens, image.CGImage.Width, image.CGImage.Height, documentCoverage,
                monthFirst: CurrencyFormat.Code == "USD");
            return result;
        });
    }
}
