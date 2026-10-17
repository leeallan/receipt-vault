using System.Text.RegularExpressions;
using ReceiptVault.Models;

namespace ReceiptVault.Services;

// One run of recognised text. Coordinates are normalised (0–1) with the origin at the
// bottom-left, matching Apple Vision's bounding boxes; TopLeft/TopRight are the corners
// of the run's (possibly tilted) quadrilateral.
public readonly record struct OcrToken(
    string Text, double X, double Y, double Width, double Height,
    double TopLeftX, double TopLeftY, double TopRightX, double TopRightY);

// Turns recognised text runs into a parsed receipt. Platform-neutral (no Vision/UIKit
// types) so it can be unit-tested on any machine against captured OCR output.
public static class ReceiptTextParser
{
    public static OcrResult Parse(
        IReadOnlyList<OcrToken> tokens, double pxWidth, double pxHeight, double documentCoverage)
    {
        var lines = GroupIntoRows(tokens, MeasureSkew(tokens, pxWidth, pxHeight), pxWidth, pxHeight);
        var result = ParseReceipt(lines);
        if (FindLogo(tokens) is { } logo)
            result.Merchant = logo;
        result.Validation = ValidateReceipt(lines, tokens, documentCoverage);
        return result;
    }

    // Stores print their name as a big logo near the top, so the tallest text in the
    // upper part of the receipt is a better merchant than the first line, which may be a
    // promo banner or stray text from whatever is behind the receipt. Only used when it
    // clearly stands out from the body text.
    private static string? FindLogo(IReadOnlyList<OcrToken> tokens)
    {
        if (tokens.Count < 5) return null;

        var top = tokens.Max(t => t.Y + t.Height);
        var bottom = tokens.Min(t => t.Y);
        var upper = top - (top - bottom) * 0.4;
        var medianHeight = tokens.Select(t => t.Height).OrderBy(h => h).ElementAt(tokens.Count / 2);

        var logo = tokens
            .Where(t => t.Y >= upper
                && Regex.Matches(t.Text, "[A-Za-z]").Count >= 3
                && !Regex.IsMatch(t.Text, PromoKeywords, RegexOptions.IgnoreCase))
            .OrderByDescending(t => t.Height)
            .FirstOrDefault();

        return logo.Text is not null && logo.Height >= medianHeight * 1.8 ? logo.Text.Trim() : null;
    }

    // A hand-held photo is rarely square-on. Measure the tilt from each text run's
    // baseline (in pixels, since the normalised axes have different scales) and take
    // the median of the longer runs, which give the steadiest reading. Vision reports
    // short runs such as a "2.10" price as level boxes whatever the tilt, so on a receipt
    // with many items they would outvote the descriptions.
    private static double MeasureSkew(IReadOnlyList<OcrToken> tokens, double pxWidth, double pxHeight)
    {
        var longRuns = tokens.Where(t => t.Text.Length >= 8).ToList();
        var angles = (longRuns.Count >= 3 ? longRuns : tokens.Where(t => t.Text.Length >= 4))
            .Select(t => Math.Atan2(
                (t.TopRightY - t.TopLeftY) * pxHeight,
                (t.TopRightX - t.TopLeftX) * pxWidth))
            .OrderBy(a => a)
            .ToList();
        return angles.Count > 0 ? angles[angles.Count / 2] : 0;
    }

    // Decides whether the captured image is genuinely a receipt: it must contain
    // receipt-like text (prices, optionally receipt keywords) AND the receipt must fill
    // a meaningful share of the frame (a detected document ≥ 50%, or the recognised text
    // spanning ≥ 50%). This keeps the app from being used to store arbitrary photos.
    internal static ReceiptValidation ValidateReceipt(
        List<string> lines,
        IReadOnlyList<OcrToken> observations,
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
            var minX = observations.Min(o => o.X);
            var maxX = observations.Max(o => (o.X + o.Width));
            var minY = observations.Min(o => o.Y);
            var maxY = observations.Max(o => (o.Y + o.Height));
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
    // Positions are first rotated by -skew so a tilted receipt's rows run level again;
    // otherwise a price on the right drifts into the neighbouring row (e.g. a "SAVING 90p"
    // line merging with the next item, turning that item's price into a saving).
    internal static List<string> GroupIntoRows(
        IReadOnlyList<OcrToken> observations, double skew, double pxWidth, double pxHeight)
    {
        if (observations.Count == 0) return [];

        // BoundingBox is normalised (0–1) with the origin at the bottom-left, so a
        // larger Y is nearer the top of the receipt.
        var cos = Math.Cos(skew);
        var sin = Math.Sin(skew);
        var tokens = observations.Select(o =>
        {
            var cx = (o.X + o.Width / 2) * pxWidth;
            var cy = (o.Y + o.Height / 2) * pxHeight;
            return new RowToken(o.Text.Trim(),
                X: (cx * cos + cy * sin) / pxWidth,
                Y: (-cx * sin + cy * cos) / pxHeight,
                HalfWidth: o.Width / 2);
        }).ToList();

        var avgHeight = observations.Average(o => o.Height);
        var threshold = Math.Max(avgHeight * 0.5, 0.004);

        // Curled or bowed paper isn't fixed by levelling: the price column sags (or rises)
        // relative to the descriptions, gradually, down the page — on a crumpled receipt
        // until each price sits most of the way to the next row. So learn that residual
        // slope from the rows already matched (top-down) and predict where a token
        // should sit relative to its horizontally-nearest neighbour.
        var drift = 0.0;

        var rows = new List<List<RowToken>>();
        foreach (var token in tokens.OrderByDescending(t => t.Y))
        {
            List<RowToken>? best = null;
            RowToken bestNearest = default;
            var bestDistance = threshold;
            foreach (var row in rows)
            {
                var nearest = row.MinBy(t => Math.Abs(t.X - token.X));
                var predictedY = nearest.Y + drift * (token.X - nearest.X);
                var distance = Math.Abs(predictedY - token.Y);
                if (distance < bestDistance)
                {
                    best = row;
                    bestNearest = nearest;
                    bestDistance = distance;
                }
            }

            if (best is null)
            {
                best = [];
                rows.Add(best);
            }
            else
            {
                // Only well-separated pairs (description ↔ price) say much about the slope.
                var dx = token.X - bestNearest.X;
                if (Math.Abs(dx) > 0.25)
                    drift = drift * 0.6 + (token.Y - bestNearest.Y) / dx * 0.4;
            }
            best.Add(token);
        }

        return rows
            .Select(r => string.Join(" ", MergeSplitAmounts(r.OrderBy(t => t.X).ToList())))
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();
    }

    private readonly record struct RowToken(string Text, double X, double Y, double HalfWidth)
    {
        public double Left => X - HalfWidth;
        public double Right => X + HalfWidth;
    }

    // Vision sometimes splits one price into two overlapping runs: "€8" + ".32", or "£3" +
    // "1,50" (the overlapping "3" misread). The left run holds the pounds and the right run
    // the pence, so rebuild the amount from those two parts.
    private static IEnumerable<string> MergeSplitAmounts(List<RowToken> row)
    {
        for (var i = 0; i < row.Count; i++)
        {
            if (i + 1 < row.Count && row[i + 1].Left < row[i].Right)
            {
                var whole = Regex.Match(row[i].Text, @"^(?<lead>-?[£$€]?)(?<int>\d{1,4})$");
                var pence = Regex.Match(row[i + 1].Text, @"^\d{0,4}\s?[.,]\s?(?<dec>\d{2})$");
                if (whole.Success && pence.Success)
                {
                    yield return $"{whole.Groups["lead"].Value}{whole.Groups["int"].Value}.{pence.Groups["dec"].Value}";
                    i++;
                    continue;
                }
            }
            yield return row[i].Text;
        }
    }

    // A faint decimal point is often lost or spaced out: "£3 50", "€8 .32". With a currency
    // sign in front and two digits at the end of the line, it can only be a price, so put
    // the point back. Applied after RawText is captured, which keeps what was actually read.
    private static string RepairAmounts(string line) =>
        Regex.Replace(line,
            @"(?<lead>(?:[£$€]|\bE)\d{1,4})\s*(?:[.,]\s*|\s+)(?<dec>\d{2})(?<tail>\s*(?:[A-Za-z*]|\s\d)?\s*)$",
            "${lead}.${dec}${tail}");

    private static OcrResult ParseReceipt(List<string> lines)
    {
        var result = new OcrResult { RawText = string.Join("\n", lines) };
        lines = lines.Select(RepairAmounts).ToList();

        // Merchant: first line that looks like a name — skipping numbers / stray marks and
        // the promo banners some stores print above their logo ("For a chance to win...").
        result.Merchant = lines
            .FirstOrDefault(l => Regex.Matches(l, "[A-Za-z]").Count >= 3
                && !Regex.IsMatch(l, @"^\d")
                && !Regex.IsMatch(l, PromoKeywords, RegexOptions.IgnoreCase))
            ?? string.Empty;

        result.Total = FindTotal(lines);

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

        (result.Items, var deductions) = ParseLineItems(lines);
        DropTotalReadAsItem(result.Items, result.Total);
        result.Total = ReconcileTotal(result.Total, result.Items, deductions);

        return result;
    }

    // Cross-checks the total against the items, which are read independently:
    // A total line whose keyword was misread ("SALE Total 30.55" as "›ALE 30.55") looks
    // like an item. If an item costs exactly the total and the other items already add up
    // to it, it's the total.
    private static void DropTotalReadAsItem(List<LineItem> items, decimal? total)
    {
        if (total is not { } t || items.Count < 2) return;

        var echo = items.FindLastIndex(i => i.Price == t && i.Savings == 0);
        if (echo >= 0 && items.Where((_, index) => index != echo).Sum(i => i.Price - i.Savings) == t)
            items.RemoveAt(echo);
    }

    // deductions are the minus lines ("COOKEDM - 2For£3 -£1.48") taken off the item prices;
    // savings that are only informational (Boots' "SAVING 90p" under a price already
    // reduced) don't count.
    //  - The amount paid can be missed (cut off, or read as "TATAI."), leaving only a
    //    pre-savings "TOTAL £34.80". If that equals the item prices and there were
    //    deductions, what was paid is the total less the deductions.
    //  - Vision sometimes reads a faint "£" as a digit, turning "£33.32" into "633.32". If
    //    dropping the leading digit makes the total agree with the items, it was the "£".
    private static decimal? ReconcileTotal(decimal? total, List<LineItem> items, decimal deductions)
    {
        if (total is not { } t || items.Count < 2) return total;

        var itemsGross = items.Sum(i => i.Price);
        var itemsNet = itemsGross - deductions;
        if (t == itemsNet) return total;

        if (t == itemsGross && deductions > 0)
            return itemsNet;

        var digits = t.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
        if (digits.Length > 4
            && decimal.TryParse(digits[1..], System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var withoutLead)
            && withoutLead == itemsNet)
            return withoutLead;

        return total;
    }

    // Header lines that are marketing or boilerplate rather than the store's name.
    private const string PromoKeywords =
        @"(receipt|chance to win|tell us|survey|feedback|www\.|\.com\b|http)";

    // Phrases that unambiguously mark the amount paid, preferred over a bare "total".
    private const string StrongTotalKeywords =
        @"\b(to pay|amount due|balance due|total due|grand total|amount payable)\b";

    // "Total"-bearing lines that are NOT the amount paid, e.g. Boots' "Total Saving Today £2.08"
    // printed below "TOTAL TO PAY 18.58", or "Total VAT", "Sub Total", "Total Points".
    private const string NotTotalKeywords =
        @"\b(sub[\s\-]?total|saving|savings|saved|discount|points|vat|tax|gst)\b";

    private static decimal? FindTotal(List<string> lines)
    {
        decimal? strong = null, plain = null;

        // Bottom-up, so the last matching line on the receipt wins within each tier.
        for (int i = lines.Count - 1; i >= 0; i--)
        {
            var line = lines[i];
            if (!Regex.IsMatch(line, @"\b(total|amount due|balance|sum|to pay)\b", RegexOptions.IgnoreCase)
                || Regex.IsMatch(line, NotTotalKeywords, RegexOptions.IgnoreCase))
                continue;

            var m = Regex.Match(line, @"[£$€]?\s*(\d{1,6}[.,]\d{2})");
            if (!m.Success || !decimal.TryParse(m.Groups[1].Value.Replace(",", "."),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var amount))
                continue;

            if (Regex.IsMatch(line, StrongTotalKeywords, RegexOptions.IgnoreCase))
                strong ??= amount;
            else
                plain ??= amount;

            if (strong is not null) break;
        }

        return strong ?? plain;
    }

    // Lines that describe totals, payment or store metadata rather than a purchased item.
    private const string MetaKeywords =
        @"\b(sub[\s\-]?total|total|amount|balance|to pay|change|cash|card|tender|visa|mastercard|" +
        @"contactless|debit|credit|vat|tax|gst|receipt|thank|www|http|tel|phone|till|cashier|" +
        @"store|points|nectar points|aid|auth|approved|reference|invoice|order|table|server|" +
        @"items|net|goods|source|sale|verification|authorisation|authorization|eft|terminal|" +
        @"merchant|gbp|usd|eur|change due|debited|cardholder|retain)\b";

    // A line that starts with the payment method. Anchored to the start so items such as
    // "BIRTHDAY CARD 2.50" don't match.
    private const string PaymentLine =
        @"^(mastercard|visa|amex|american express|maestro|card|cash|contactless|debit card|credit card)\b";

    // Lines that describe a discount / saving applied to the previous item or the basket.
    private const string SavingsKeywords =
        @"\b(saving|savings|discount|reduced|multibuy|multi[\s\-]?buy|meal deal|offer|promo|" +
        @"clubcard price|clubcard|nectar|member|% off|voucher|coupon|was)\b";

    private static (List<LineItem> Items, decimal Deductions) ParseLineItems(List<string> lines)
    {
        var items = new List<LineItem>();
        var deductions = 0m;

        // The previous row, when it was a description with no price of its own — the first
        // half of a two-line item.
        string? pendingDescription = null;

        foreach (var raw in lines)
        {
            var line = raw.Trim();
            var previousDescription = pendingDescription;
            pendingDescription = null;
            if (line.Length < 3) continue;

            // The payment line ("Mastercard 30.55-", "CARD £33.32") ends the item list;
            // anything below is card / VAT / auth details that can misread as items.
            if (items.Count > 0 && Regex.IsMatch(line, PaymentLine, RegexOptions.IgnoreCase))
                break;

            // Skip obvious totals / payment / metadata rows.
            if (Regex.IsMatch(line, MetaKeywords, RegexOptions.IgnoreCase))
                continue;

            // Two-line layout (Wickes, IKEA): the description on one row, then
            // "code qty @ £unit  total" on the next with no description of its own. The "@"
            // is often lost, so a currency sign on the unit price will do instead.
            var unitRow = Regex.Match(line,
                @"(?:(?<=^|\s)(?<qty>\d{1,3})\s*)?(?:[@*]\s*(?:[£$€]|E(?=\d))?|(?<=\s)(?:[£$€]|E(?=\d)))\s*(?<unit>\d{1,5}[.,]\d{2})\s+[£$€]?(?<total>\d{1,5}[.,]\d{2})\b");
            if (unitRow.Success && previousDescription is not null
                && !Regex.IsMatch(line[..unitRow.Index], "[A-Za-z]{2,}"))
            {
                var unit = ParseAmount(unitRow.Groups["unit"].Value);
                var total = ParseAmount(unitRow.Groups["total"].Value);

                // The quantity digit is easily lost or misread ("136260 @ £4.05"), so trust
                // total ÷ unit when it's a whole number.
                var unitQuantity = unitRow.Groups["qty"].Success ? decimal.Parse(unitRow.Groups["qty"].Value) : 1m;
                if (unit > 0 && total % unit == 0 && total / unit >= 1)
                    unitQuantity = total / unit;

                items.Add(new LineItem
                {
                    Description = previousDescription,
                    Quantity = unitQuantity,
                    Price = total,
                });
                continue;
            }

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
            // trailing tax code: a letter or asterisk ("1.99 A", "2.50*") or, as IKEA prints,
            // a separate digit ("119.00 0"). Vision often reads a faint "£" as "E" ("E2.87"),
            // so accept that as a currency sign too.
            var m = Regex.Match(line,
                @"^(?<desc>.*?)[\s]*(?<neg>[-(])?\s*(?:[£$€]|E(?=\d))?\s*(?<amt>\d{1,4}[.,]\d{2})\s*(?<neg2>[-)])?(?:\s*[A-Za-z*]|\s+\d)?\s*$");

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

            // Drop a "was" price printed before the charged price, e.g. Boots' "Ug Gl Sham 250 8.95 NOW 8.05".
            desc = Regex.Replace(desc, @"\s+[£$€]?[\d.,]+\s*NOW$", "", RegexOptions.IgnoreCase);
            desc = desc.Trim(' ', '-', '.', '·', '*', ':');
            if (!Regex.IsMatch(desc, "[A-Za-z]{2,}")) continue; // need a real description

            // Savings are often printed in pence ("SAVING 90p"); only read pence on saving rows.
            if (amount is null && Regex.IsMatch(line, SavingsKeywords, RegexOptions.IgnoreCase))
            {
                var pm = Regex.Match(line, @"\b(?<p>\d{1,3})\s?p$", RegexOptions.IgnoreCase);
                if (pm.Success)
                    amount = int.Parse(pm.Groups["p"].Value) / 100m;
            }

            var isSaving = amount is not null &&
                (isNegative || Regex.IsMatch(line, SavingsKeywords, RegexOptions.IgnoreCase));

            if (isSaving)
            {
                // Attach the saving to the most recent item; otherwise skip a basket-level saving
                // that has no item to hang it on. A saving can't exceed the item's price — one
                // that does is something else with a minus sign, e.g. a misread payment line
                // "Mastercara 30.55-".
                if (items.Count > 0 && Math.Abs(amount!.Value) <= items[^1].Price)
                {
                    items[^1].Savings += Math.Abs(amount.Value);
                    if (isNegative)
                        deductions += Math.Abs(amount.Value);
                }
                continue;
            }

            // Rows with no price only count as items when a product code marks them as such —
            // the price likely landed in a separate column the user can fill in. Otherwise it
            // may be the description half of a two-line item.
            if (amount is null && !hasCode)
            {
                pendingDescription = desc;
                continue;
            }

            items.Add(new LineItem
            {
                Description = desc,
                Quantity = quantity,
                Price = amount ?? 0m,
            });
        }

        return (items, deductions);
    }

    private static decimal ParseAmount(string value) =>
        decimal.Parse(value.Replace(",", "."), System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture);
}
