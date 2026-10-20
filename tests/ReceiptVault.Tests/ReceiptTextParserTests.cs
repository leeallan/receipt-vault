using System.Text.Json;
using ReceiptVault.Services;
using Xunit;

namespace ReceiptVault.Tests;

// Fixtures are real Apple Vision output captured from photos (see Fixtures/README.md),
// so these tests exercise the parser exactly as the app runs it, minus the camera.
public class ReceiptTextParserTests
{
    // Asda receipt: 13 items summing to the printed £34.80, with a "2 for £3" multibuy
    // saving of £1.48 printed under the second ham, paid total £33.32.
    private static readonly string[] AsdaItems =
    [
        "AVOCADO 1.29", "GRAPEFRUIT 1.47", "GRAPEFRUIT 1.47", "LARGE CANDLE 3.50",
        "CANDLE 3.50", "CHEESE 1.43", "TOILET ROLL 8.32", "HEINZ MAYO 3.38", "HAM 2.24",
        "HAM 2.24 (-1.48)", "CHICKEN 1.25", "JE EGGS 2.87", "ENERGY DRINK 1.84",
    ];

    [Theory]
    [InlineData("asda-flat.json")]
    // Curled paper: down the page the price column sags below its description until
    // each price sits most of the way to the next row.
    [InlineData("asda-curled.json")]
    // "£3.50" read as two overlapping runs, "£3" and ". 50".
    [InlineData("asda-split-price.json")]
    public void Asda_ReadsEveryItem(string fixture)
    {
        var result = Parse(fixture);

        Assert.Equal(string.Join("\n", AsdaItems), Describe(result));
        Assert.Equal(33.32m, result.Total);
        Assert.Equal("ASDA", result.Merchant);
    }

    // Receipt tilted ~2.4° with the right side low, rendered for the promo videos
    // (marketing/promo-videos/capture/receipts). Vision reports the short price runs as
    // level boxes, which outnumber the descriptions; levelling by them left every price
    // half a row below its description and no items were read.
    [Fact]
    public void Freshway_LevelsTiltFromLongRuns()
    {
        var result = Parse("freshway-tilted.json");

        Assert.Equal(
            """
            SOURDOUGH LOAF 2.10
            FREE RANGE EGGS 12 3.45
            SEMI SKIMMED MILK 4PT 1.65
            MATURE CHEDDAR 400G 3.75 (-0.75)
            BANANAS LOOSE 0.98
            OLIVE OIL 500ML 5.20
            GREEK YOGURT 500G 2.40
            COFFEE BEANS 1KG 11.50 (-1.50)
            CHERRY TOMATOES 1.85
            PASTA FUSILLI 1KG 1.60
            """.ReplaceLineEndings("\n"),
            Describe(result));
        Assert.Equal(32.23m, result.Total);
        Assert.Equal("FRESHWAY", result.Merchant);
    }

    // A US address starts with a house number, which reads like a product code.
    [Fact]
    public void Freshway_IgnoresUsStreetAddress()
    {
        var plain = Parse("freshway-tilted.json");
        var result = Parse("freshway-tilted.json", t => t == "12 High Street, Riverside" ? "1420 Canal St, Riverside" : t);

        Assert.Equal(Describe(plain), Describe(result));
    }

    // US receipts print the month first. 10/15 can only be month-first, so it's read that
    // way whatever the preference; 10/05 is ambiguous and follows it.
    [Theory]
    [InlineData("10/15/2026", false, 10, 15)]
    [InlineData("10/05/2026", true, 10, 5)]
    [InlineData("10/05/2026", false, 5, 10)]
    public void Freshway_ReadsDayOrMonthFirstDates(string printed, bool monthFirst, int month, int day)
    {
        var result = Parse("freshway-tilted.json", t => t == "15/10/2026" ? printed : t, monthFirst);

        Assert.Equal(new DateTime(2026, month, day), result.Date);
    }

    // Two-line items: the description, then "code qty @ £unit  total" beneath it.
    [Fact]
    public void Wickes_JoinsTwoLineItems()
    {
        var result = Parse("wickes-two-line.json");

        Assert.Equal(
            """
            Wickes 3.6V Screwdriver 15.00
            UniBond Sealant Finishing Tool 4.05
            Wickes 1 -a ge Cartridge Gun C30 380ml 5.50
            EvoSt k Sticks Like Waterproof Clr 290ml 6.00
            """.ReplaceLineEndings("\n"),
            Describe(result));
        Assert.Equal(30.55m, result.Total);
        Assert.Equal("Wickes", result.Merchant);
    }

    // The decimal point lost entirely ("£3 50") or spaced out ("€8 .32").
    [Theory]
    [InlineData("asda-flat.json", "£3.50", "£3 50")]
    [InlineData("asda-flat.json", "£3.50", "£3 . 50")]
    [InlineData("asda-curled.json", "€8.32", "€8 .32")]
    public void Asda_RepairsMissingDecimalPoint(string fixture, string printed, string misread)
    {
        Assert.Contains(printed, File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", fixture)));
        var result = Parse(fixture, text => text == printed ? misread : text);

        Assert.Equal(string.Join("\n", AsdaItems), Describe(result));
    }

    // Same receipt, but Vision dropped the "@" on two of the price rows
    // ("136260 £4.05 4.05").
    [Fact]
    public void Wickes_JoinsTwoLineItemsWithoutAtSign()
    {
        var result = Parse("wickes-missing-at.json");

        Assert.Equal([15.00m, 4.05m, 5.50m, 6.00m], result.Items.Select(i => i.Price));
        Assert.Equal(30.55m, result.Total);
    }

    // Document-scanner capture that includes the payment section: "SALE Total 30.55" read
    // as "›ALE 30.55", and the card auth time below the payment line read as "ch 09.50".
    [Fact]
    public void Wickes_IgnoresMisreadTotalAndPaymentDetails()
    {
        var result = Parse("wickes-scanned.json");

        Assert.Equal([15.00m, 4.05m, 5.50m, 6.00m], result.Items.Select(i => i.Price));
        Assert.Equal(30.55m, result.Total);
    }

    // A faint "£" read as "6": "TOTAL: £33.32" came out as "TOTAL: 633.32" on the phone.
    // The items still add up to £33.32, which identifies the stray leading digit.
    [Fact]
    public void Total_CorrectsPoundSignReadAsDigit()
    {
        var result = Parse("asda-flat.json", text => text == "£33.32" ? "633.32" : text);

        Assert.Equal(33.32m, result.Total);
    }

    // The final "TOTAL: £33.32" was cut off (read as "TATAI." with no amount), leaving only
    // the pre-savings "TOTAL £34.80". The items' £1.48 saving gives the amount paid.
    [Fact]
    public void Total_SubtractsSavingsWhenAmountPaidIsMissing()
    {
        var result = Parse("asda-flat.json", text => text switch
        {
            "TOTAL:" => "TATAI.",
            "£33.32" => null,
            _ => text,
        });

        Assert.Equal(34.80m - 1.48m, result.Total);
    }

    // Two-line item with a quantity: "2 * 45.00  90.00".
    [Fact]
    public void Ikea_ReadsQuantityFromTwoLineItem()
    {
        var result = Parse("ikea-two-line.json");

        var item = Assert.Single(result.Items);
        Assert.StartsWith("MALM", item.Description);
        Assert.Equal(2, item.Quantity);
        Assert.Equal(90.00m, item.Price);
        Assert.Equal(90.00m, result.Total);
        Assert.Equal("IKEA", result.Merchant);
    }

    // Single-line item ending in IKEA's VAT-code digit: "MALM N chest/6 drw 1  119.00 0".
    [Fact]
    public void Ikea_IgnoresTrailingTaxCodeDigit()
    {
        var result = Parse("ikea-tax-code.json");

        var item = Assert.Single(result.Items);
        Assert.StartsWith("MALM", item.Description);
        Assert.Equal(119.00m, item.Price);
        Assert.Equal(119.00m, result.Total);
    }

    // Android (ML Kit) capture of the same IKEA receipt, photographed with the left edge
    // and logo cut off. ML Kit glues the VAT-code digit onto the price ("119.000"), and the
    // only header left is "elcome to IKEA Warrington".
    [Fact]
    public void Ikea_MlKit_SplitsGluedTaxCodeAndDropsWelcome()
    {
        var result = Parse("ikea-mlkit-cropped.json");

        Assert.Equal("MALM N chest/6 drw 1 119.00", Describe(result));
        Assert.Equal(119.00m, result.Total);
        Assert.Equal("IKEA Warrington", result.Merchant);
    }

    // The same receipt photographed sideways. ML Kit reads rotated text (Vision only reads
    // upright text), so the runs arrive at 90° and must be turned upright before rows are
    // stitched; otherwise whole columns merge into one "row".
    [Fact]
    public void Ikea_MlKit_ReadsSidewaysPhoto()
    {
        var result = Parse("ikea-mlkit-sideways.json");

        Assert.StartsWith("MALM", Describe(result));
        Assert.Single(result.Items);
        Assert.Equal(119.00m, result.Items[0].Price);
        Assert.Equal(119.00m, result.Total);
        // ML Kit misses the logo here and reads "elcome to IKEA arrington".
        Assert.Equal("IKEA arrington", result.Merchant);
    }

    // "was ... NOW" prices with per-item savings, and "Total Saving Today" below the
    // amount paid that must not be taken as the total.
    [Fact]
    public void Boots_ReadsSavingsAndTotal()
    {
        var result = Parse("boots-savings.json");

        Assert.Equal(3, result.Items.Count);
        Assert.Equal([8.05m, 6.04m, 4.49m], result.Items.Select(i => i.Price));
        Assert.Equal([0.90m, 0.68m, 0.50m], result.Items.Select(i => i.Savings));
        Assert.Equal(18.58m, result.Total);
        Assert.Equal("Boots UK Limited", result.Merchant);
    }

    // One line per item, so a failure shows the whole list rather than a truncated diff.
    private static string Describe(Models.OcrResult result) =>
        string.Join("\n", result.Items.Select(i =>
            $"{i.Description} {i.Price:F2}" + (i.Savings > 0 ? $" (-{i.Savings:F2})" : "")));

    // rewrite lets a test simulate a different mis-read of the same photo; returning null
    // drops that text run entirely.
    private static Models.OcrResult Parse(string fixture, Func<string, string?>? rewrite = null, bool monthFirst = false)
    {
        var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", fixture));
        var capture = JsonSerializer.Deserialize<Capture>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        var tokens = rewrite is null
            ? capture.Tokens
            : capture.Tokens
                .Select(t => rewrite(t.Text) is { } text ? t with { Text = text } : (OcrToken?)null)
                .OfType<OcrToken>()
                .ToList();
        return ReceiptTextParser.Parse(tokens, capture.PixelWidth, capture.PixelHeight, documentCoverage: 1, monthFirst);
    }

    private sealed record Capture(double PixelWidth, double PixelHeight, List<OcrToken> Tokens);
}
