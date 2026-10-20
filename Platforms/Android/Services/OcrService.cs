using Android.Gms.Extensions;
using Android.Graphics;
using Path = System.IO.Path;
using AndroidX.ExifInterface.Media;
using Xamarin.Google.MLKit.Vision.Common;
using Xamarin.Google.MLKit.Vision.Text;
using Xamarin.Google.MLKit.Vision.Text.Latin;
using ReceiptVault.Models;
using ReceiptVault.Services;
using MlText = Xamarin.Google.MLKit.Vision.Text.Text;

namespace ReceiptVault.Services;

// Android OCR via Google ML Kit (on-device, bundled Latin model). ML Kit reports pixel
// corner points with a top-left origin; they are converted to Vision's normalised,
// bottom-left-origin layout so the shared ReceiptTextParser sees the same geometry as iOS.
public class OcrService : IOcrService
{
    // Receipt text is small, so keep plenty of resolution, but cap huge camera photos
    // to keep recognition fast and memory sane.
    private const int MaxSide = 3000;

    public async Task<OcrResult> RecognizeReceiptAsync(string imagePath)
    {
        using var bitmap = await System.Threading.Tasks.Task.Run(() => LoadUpright(imagePath));
        // A photo we can't even decode isn't a receipt we can store.
        if (bitmap is null)
            return new OcrResult { Validation = new ReceiptValidation { IsLikelyReceipt = false } };

        MlText text;
        try
        {
            using var recognizer = TextRecognition.GetClient(TextRecognizerOptions.DefaultOptions);
            text = (MlText)await recognizer.Process(InputImage.FromBitmap(bitmap, 0));
        }
        catch (Exception)
        {
            return new OcrResult { Validation = new ReceiptValidation { IsLikelyReceipt = false } };
        }

        double w = bitmap.Width, h = bitmap.Height;
        var tokens = text.TextBlocks
            .SelectMany(b => b.Lines)
            .Where(l => !string.IsNullOrWhiteSpace(l.Text) && l.GetCornerPoints() is { Length: 4 })
            .Select(l => ToToken(l.Text, l.GetCornerPoints()!, w, h))
            .ToList();

#if DEBUG
        DumpFixture(imagePath, w, h, tokens);
#endif

        // ML Kit has no document-segmentation pass, so validation falls back to how much
        // of the frame the recognised text spans.
        return ReceiptTextParser.Parse(tokens, w, h, documentCoverage: 0,
            monthFirst: CurrencyFormat.Code == "USD");
    }

#if DEBUG
    // Writes the recognised tokens in the parser-test fixture format to
    // cache/ocr-dumps/<photo>.json, so a misread receipt can be pulled off a device with
    // `adb exec-out run-as <package> cat cache/ocr-dumps/...` and replayed as a test.
    private static void DumpFixture(string imagePath, double w, double h, List<OcrToken> tokens)
    {
        try
        {
            var dir = Path.Combine(FileSystem.CacheDirectory, "ocr-dumps");
            Directory.CreateDirectory(dir);
            var json = System.Text.Json.JsonSerializer.Serialize(
                new { pixelWidth = w, pixelHeight = h, tokens },
                new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
                });
            File.WriteAllText(Path.Combine(dir, Path.GetFileNameWithoutExtension(imagePath) + ".json"), json);
        }
        catch (Exception) { /* diagnostics only */ }
    }
#endif

    // Corner points run clockwise from the text's top-left: TL, TR, BR, BL.
    private static OcrToken ToToken(string value, Android.Graphics.Point[] c, double w, double h)
    {
        var minX = c.Min(p => p.X) / w;
        var maxX = c.Max(p => p.X) / w;
        var minY = c.Min(p => p.Y) / h;
        var maxY = c.Max(p => p.Y) / h;
        return new OcrToken(
            value,
            minX, 1 - maxY, maxX - minX, maxY - minY,
            c[0].X / w, 1 - c[0].Y / h,
            c[1].X / w, 1 - c[1].Y / h);
    }

    // Decodes the photo downsampled to at most MaxSide and rotated per its EXIF tag, so
    // camera shots stored sideways are read the right way up.
    private static Bitmap? LoadUpright(string path)
    {
        var bounds = new BitmapFactory.Options { InJustDecodeBounds = true };
        BitmapFactory.DecodeFile(path, bounds);
        if (bounds.OutWidth <= 0 || bounds.OutHeight <= 0) return null;

        var sample = 1;
        while (Math.Max(bounds.OutWidth, bounds.OutHeight) / (sample * 2) >= MaxSide)
            sample *= 2;

        var bitmap = BitmapFactory.DecodeFile(path, new BitmapFactory.Options { InSampleSize = sample });
        if (bitmap is null) return null;

        var degrees = new ExifInterface(path).GetAttributeInt(
            ExifInterface.TagOrientation, ExifInterface.OrientationNormal) switch
        {
            ExifInterface.OrientationRotate90 => 90,
            ExifInterface.OrientationRotate180 => 180,
            ExifInterface.OrientationRotate270 => 270,
            _ => 0
        };
        if (degrees == 0) return bitmap;

        using var matrix = new Matrix();
        matrix.PostRotate(degrees);
        var rotated = Bitmap.CreateBitmap(bitmap, 0, 0, bitmap.Width, bitmap.Height, matrix, true);
        bitmap.Dispose();
        return rotated;
    }
}
