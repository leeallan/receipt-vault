using System.Text.RegularExpressions;
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
            if (image?.CGImage is null) return new OcrResult();

            var request = new VNRecognizeTextRequest(completionHandler: null);
            request.RecognitionLevel = VNRequestTextRecognitionLevel.Accurate;
            request.UsesLanguageCorrection = true;

            var handler = new VNImageRequestHandler(image.CGImage, new NSDictionary());
            handler.Perform([request], out var error);

            if (error is not null || request.Results is null) return new OcrResult();

            var lines = request.Results
                .OfType<VNRecognizedTextObservation>()
                .Select(obs => obs.TopCandidates(1).FirstOrDefault()?.String ?? "")
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();

            return ParseReceipt(lines);
        });
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
        @"store|points|nectar points|aid|auth|approved|reference|invoice|order|table|server)\b";

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

            // Trailing money amount, optionally negative / parenthesised, with an optional
            // trailing tax-code letter or asterisk (e.g. "1.99 A", "2.50*").
            var m = Regex.Match(line,
                @"^(?<desc>.*?)[\s]*(?<neg>[-(])?\s*[£$€]?\s*(?<amt>\d{1,4}[.,]\d{2})\s*(?<neg2>[-)])?\s*[A-Za-z*]?\s*$");
            if (!m.Success) continue;

            var desc = m.Groups["desc"].Value.Trim(' ', '-', '.', '·', '*', ':');
            if (!Regex.IsMatch(desc, "[A-Za-z]{2,}")) continue; // need a real description

            if (!decimal.TryParse(m.Groups["amt"].Value.Replace(",", "."),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var amount))
                continue;

            var isNegative = m.Groups["neg"].Success || m.Groups["neg2"].Success;
            var isSaving = isNegative || Regex.IsMatch(line, SavingsKeywords, RegexOptions.IgnoreCase);

            if (isSaving)
            {
                // Attach the saving to the most recent item; otherwise skip a basket-level saving
                // that has no item to hang it on.
                if (items.Count > 0)
                    items[^1].Savings += Math.Abs(amount);
                continue;
            }

            // Quantity: "2 x", "2x", "3 @ 0.99" style prefixes.
            decimal quantity = 1;
            var qm = Regex.Match(desc, @"^(?<qty>\d{1,3})\s*[xX@]\s*");
            if (qm.Success && decimal.TryParse(qm.Groups["qty"].Value, out var q) && q > 0)
            {
                quantity = q;
                desc = desc[qm.Length..].Trim(' ', '-', '·', '@', 'x', 'X');
            }

            if (desc.Length < 2) continue;

            items.Add(new LineItem
            {
                Description = desc,
                Quantity = quantity,
                Price = amount,
            });
        }

        return items;
    }
}
