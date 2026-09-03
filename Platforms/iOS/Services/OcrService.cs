using System.Text.RegularExpressions;
using CoreGraphics;
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

            var observations = textRequest.Results
                .OfType<VNRecognizedTextObservation>()
                .Select(obs => (
                    Text: obs.TopCandidates(1).FirstOrDefault()?.String ?? "",
                    Box: obs.BoundingBox))
                .Where(o => !string.IsNullOrWhiteSpace(o.Text))
                .ToList();

            // Vision returns one observation per text run, so a multi-column receipt
            // (description on the left, price on the right) arrives as separate runs.
            // Regroup them into visual rows by vertical position before parsing.
            var lines = GroupIntoRows(observations);

            var result = ParseReceipt(lines);

            var documentCoverage = docRequest.Results?
                .OfType<VNRectangleObservation>()
                .Select(r => (double)(r.BoundingBox.Width * r.BoundingBox.Height))
                .DefaultIfEmpty(0)
                .Max() ?? 0;

            result.Validation = ValidateReceipt(lines, observations, documentCoverage);
            return result;
        });
    }

    // Decides whether the captured image is genuinely a receipt: it must contain
    // receipt-like text (prices, optionally receipt keywords) AND the receipt must fill
    // a meaningful share of the frame (a detected document ≥ 50%, or the recognised text
    // spanning ≥ 50%). This keeps the app from being used to store arbitrary photos.
    private static ReceiptValidation ValidateReceipt(
        List<string> lines,
        List<(string Text, CGRect Box)> observations,
        double documentCoverage)
    {
        var text = string.Join("\n", lines);
        var priceCount = Regex.Matches(text, @"\d{1,4}[.,]\d{2}\b").Count;
        var hasKeywords = Regex.IsMatch(text,
            @"\b(total|sub[\s\-]?total|vat|tax|amount|balance|change|cash|card|receipt|invoice|to pay)\b",
            RegexOptions.IgnoreCase);

        // Extent of all recognised text as a fraction of the frame.
        double textCoverage = 0;
        if (observations.Count > 0)
        {
            var minX = observations.Min(o => (double)o.Box.X);
            var maxX = observations.Max(o => (double)(o.Box.X + o.Box.Width));
            var minY = observations.Min(o => (double)o.Box.Y);
            var maxY = observations.Max(o => (double)(o.Box.Y + o.Box.Height));
            textCoverage = Math.Clamp((maxX - minX) * (maxY - minY), 0, 1);
        }

        var looksLikeReceipt = priceCount >= 3 || (priceCount >= 2 && hasKeywords);
        var coverageOk = documentCoverage >= 0.5 || textCoverage >= 0.5;

        return new ReceiptValidation
        {
            IsLikelyReceipt = looksLikeReceipt && coverageOk,
            PriceCount = priceCount,
            DocumentCoverage = documentCoverage,
            TextCoverage = textCoverage,
        };
    }

    // Stitches text runs that share a horizontal band back into single logical rows,
    // ordered left-to-right, so column-split receipts read like "DESCRIPTION ... PRICE".
    private static List<string> GroupIntoRows(List<(string Text, CGRect Box)> observations)
    {
        if (observations.Count == 0) return [];

        // BoundingBox is normalised (0–1) with the origin at the bottom-left, so a
        // larger Y is nearer the top of the receipt.
        static double MidY((string Text, CGRect Box) o) => o.Box.Y + o.Box.Height / 2.0;

        var avgHeight = observations.Average(o => (double)o.Box.Height);
        var threshold = Math.Max(avgHeight * 0.6, 0.004);

        var rows = new List<List<(string Text, CGRect Box)>>();
        foreach (var token in observations.OrderByDescending(MidY))
        {
            var y = MidY(token);
            var row = rows.FirstOrDefault(r => Math.Abs(r.Average(MidY) - y) < threshold);
            if (row is null)
            {
                row = [];
                rows.Add(row);
            }
            row.Add(token);
        }

        return rows
            .Select(r => string.Join(" ", r
                .OrderBy(t => (double)t.Box.X)
                .Select(t => t.Text.Trim())))
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();
    }

    private static OcrResult ParseReceipt(List<string> lines)
    {
        var result = new OcrResult { RawText = string.Join("\n", lines) };

        // Merchant: first non-empty line that looks like a name
        result.Merchant = lines
            .FirstOrDefault(l => l.Length > 2 && !Regex.IsMatch(l, @"^\d") && !l.Contains("receipt", StringComparison.OrdinalIgnoreCase))
            ?? string.Empty;

        // Total: last/largest currency amount near "total" keyword
        for (int i = lines.Count - 1; i >= 0; i--)
        {
            var line = lines[i];
            if (Regex.IsMatch(line, @"\b(total|amount due|balance|sum|to pay)\b", RegexOptions.IgnoreCase))
            {
                var m = Regex.Match(line, @"[£$€]?\s*(\d{1,6}[.,]\d{2})");
                if (m.Success && decimal.TryParse(m.Groups[1].Value.Replace(",", "."),
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var total))
                {
                    result.Total = total;
                    break;
                }
            }
        }

        // Fallback: find the largest currency amount if no "total" line found
        if (result.Total is null)
        {
            var amounts = lines
                .SelectMany(l => Regex.Matches(l, @"[£$€]\s*(\d{1,6}[.,]\d{2})"))
                .Select(m => decimal.TryParse(m.Groups[1].Value.Replace(",", "."),
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0m)
                .Where(v => v > 0)
                .ToList();

            if (amounts.Count > 0)
                result.Total = amounts.Max();
        }

        // Tax: look for VAT/tax line
        foreach (var line in lines)
        {
            if (Regex.IsMatch(line, @"\b(vat|tax|gst)\b", RegexOptions.IgnoreCase))
            {
                var m = Regex.Match(line, @"[£$€]?\s*(\d{1,4}[.,]\d{2})");
                if (m.Success && decimal.TryParse(m.Groups[1].Value.Replace(",", "."),
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var tax))
                {
                    result.Tax = tax;
                    break;
                }
            }
        }

        // Date: look for common date formats
        foreach (var line in lines)
        {
            var m = Regex.Match(line, @"\b(\d{1,2}[\/\-\.]\d{1,2}[\/\-\.]\d{2,4})\b");
            if (m.Success && DateTime.TryParse(m.Value,
                System.Globalization.CultureInfo.GetCultureInfo("en-GB"),
                System.Globalization.DateTimeStyles.None, out var date))
            {
                result.Date = date;
                break;
            }
        }

        result.Items = ParseLineItems(lines);

        return result;
    }

    // Lines that describe totals, payment or store metadata rather than a purchased item.
    private const string MetaKeywords =
        @"\b(sub[\s\-]?total|total|amount|balance|to pay|change|cash|card|tender|visa|mastercard|" +
        @"contactless|debit|credit|vat|tax|gst|receipt|thank|www|http|tel|phone|till|cashier|" +
        @"store|points|nectar points|aid|auth|approved|reference|invoice|order|table|server|" +
        @"items|net|goods|source|sale|verification|authorisation|authorization|eft|terminal|" +
        @"merchant|gbp|usd|eur|change due)\b";

    // Lines that describe a discount / saving applied to the previous item or the basket.
    private const string SavingsKeywords =
        @"\b(saving|savings|discount|reduced|multibuy|multi[\s\-]?buy|meal deal|offer|promo|" +
        @"clubcard price|clubcard|nectar|member|% off|voucher|coupon|was)\b";

    private static List<LineItem> ParseLineItems(List<string> lines)
    {
        var items = new List<LineItem>();

        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length < 3) continue;

            // Skip obvious totals / payment / metadata rows.
            if (Regex.IsMatch(line, MetaKeywords, RegexOptions.IgnoreCase))
                continue;

            // Leading quantity marker ("2 x", "2 X"), which supermarkets print before the
            // product code / description.
            decimal quantity = 1;
            var qm = Regex.Match(line, @"^(?<qty>\d{1,3})\s*[xX@]\s+");
            if (qm.Success && decimal.TryParse(qm.Groups["qty"].Value, out var q) && q > 0)
            {
                quantity = q;
                line = line[qm.Length..].Trim();
            }

            // Strip a leading product/PLU code (e.g. Aldi's "286117 WHISKEY BOURBON").
            // Its presence is a strong signal the row is a purchased item.
            var hasCode = false;
            var codeMatch = Regex.Match(line, @"^(?<code>\d{4,7})\s+(?=\S*[A-Za-z])");
            if (codeMatch.Success)
            {
                hasCode = true;
                line = line[codeMatch.Length..].Trim();
            }

            // Trailing money amount, optionally negative / parenthesised, with an optional
            // trailing tax-code letter or asterisk (e.g. "1.99 A", "2.50*").
            var m = Regex.Match(line,
                @"^(?<desc>.*?)[\s]*(?<neg>[-(])?\s*[£$€]?\s*(?<amt>\d{1,4}[.,]\d{2})\s*(?<neg2>[-)])?\s*[A-Za-z*]?\s*$");

            decimal? amount = null;
            var desc = line;
            var isNegative = false;

            if (m.Success && decimal.TryParse(m.Groups["amt"].Value.Replace(",", "."),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var parsed))
            {
                amount = parsed;
                desc = m.Groups["desc"].Value;
                isNegative = m.Groups["neg"].Success || m.Groups["neg2"].Success;
            }

            desc = desc.Trim(' ', '-', '.', '·', '*', ':');
            if (!Regex.IsMatch(desc, "[A-Za-z]{2,}")) continue; // need a real description

            var isSaving = amount is not null &&
                (isNegative || Regex.IsMatch(line, SavingsKeywords, RegexOptions.IgnoreCase));

            if (isSaving)
            {
                // Attach the saving to the most recent item; otherwise skip a basket-level saving
                // that has no item to hang it on.
                if (items.Count > 0)
                    items[^1].Savings += Math.Abs(amount!.Value);
                continue;
            }

            // Rows with no price only count as items when a product code marks them as such —
            // the price likely landed in a separate column the user can fill in.
            if (amount is null && !hasCode) continue;

            items.Add(new LineItem
            {
                Description = desc,
                Quantity = quantity,
                Price = amount ?? 0m,
            });
        }

        return items;
    }
}
