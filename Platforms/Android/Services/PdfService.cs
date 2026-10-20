using Android.Graphics;
using Android.Graphics.Pdf;
using Android.Text;
using Microsoft.Maui.Storage;
using Color = Android.Graphics.Color;
using Paint = Android.Graphics.Paint;
using Path = System.IO.Path;
using TextAlign = Android.Text.Layout.Alignment;

namespace ReceiptVault.Services;

// Renders a PdfReport to a real PDF with Android's PdfDocument. Mirrors the iOS renderer:
// A4 portrait, simple top-to-bottom flow and automatic page breaks at the margin.
public class PdfService : IPdfService
{
    public bool IsSupported => true;

    // A4 at 72 dpi (PdfDocument page units are PostScript points).
    const int PageWidth = 595;
    const int PageHeight = 842;
    const float Margin = 40f;
    const float ContentWidth = PageWidth - 2 * Margin;

    public Task<string?> RenderAsync(PdfReport report, string fileName) => Task.Run(() =>
    {
        var exportDir = Path.Combine(FileSystem.CacheDirectory, "exports");
        Directory.CreateDirectory(exportDir);
        var path = Path.Combine(exportDir, fileName.EndsWith(".pdf") ? fileName : fileName + ".pdf");

        var ink = Color.Rgb(20, 20, 30);
        var muted = Color.Rgb(120, 120, 130);
        var accent = Color.Rgb(108, 99, 255);
        var rule = Color.Rgb(220, 220, 228);

        TextPaint Font(float size, bool bold)
        {
            var paint = new TextPaint(PaintFlags.AntiAlias) { TextSize = size };
            paint.SetTypeface(Typeface.Create(Typeface.Default, bold ? TypefaceStyle.Bold : TypefaceStyle.Normal));
            return paint;
        }

        var titleFont = Font(22, true);
        var subtitleFont = Font(12, false);
        var headingFont = Font(14, true);
        var cellFont = Font(10, false);
        var cellBoldFont = Font(10, true);
        using var rulePaint = new Paint { Color = rule };

        using var doc = new PdfDocument();
        PdfDocument.Page? page = null;
        int pageNumber = 0;
        float y = 0;

        void NewPage()
        {
            if (page is not null) doc.FinishPage(page);
            using var info = new PdfDocument.PageInfo.Builder(PageWidth, PageHeight, ++pageNumber).Create();
            page = doc.StartPage(info);
            y = Margin;
        }

        StaticLayout Layout(string? text, TextPaint font, float width, TextAlign align) =>
            StaticLayout.Builder.Obtain(text ?? "", 0, (text ?? "").Length, font, (int)Math.Max(1, width))
                .SetAlignment(align)
                .SetIncludePad(false)
                .Build();

        float Measure(string? text, TextPaint font, float width)
        {
            using var layout = Layout(text, font, width, TextAlign.AlignNormal!);
            return layout.Height;
        }

        void Draw(string text, float x, float top, float width, TextPaint font, Color color, TextAlign align)
        {
            font.Color = color;
            using var layout = Layout(text, font, width, align);
            var canvas = page!.Canvas!;
            canvas.Save();
            canvas.Translate(x, top);
            layout.Draw(canvas);
            canvas.Restore();
        }

        void EnsureSpace(float needed)
        {
            if (y + needed > PageHeight - Margin) NewPage();
        }

        void HorizontalRule()
        {
            page!.Canvas!.DrawRect(Margin, y, Margin + ContentWidth, y + 0.5f, rulePaint);
            y += 8;
        }

        var left = TextAlign.AlignNormal!;
        var right = TextAlign.AlignOpposite!;

        NewPage();

        // Title + subtitle.
        Draw(report.Title, Margin, y, ContentWidth, titleFont, ink, left);
        y += Measure(report.Title, titleFont, ContentWidth) + 4;
        if (!string.IsNullOrEmpty(report.Subtitle))
        {
            Draw(report.Subtitle, Margin, y, ContentWidth, subtitleFont, muted, left);
            y += Measure(report.Subtitle, subtitleFont, ContentWidth) + 8;
        }
        HorizontalRule();

        // Summary lines.
        foreach (var (label, value) in report.Summary)
        {
            EnsureSpace(18);
            Draw(label, Margin, y, ContentWidth * 0.6f, cellFont, muted, left);
            Draw(value, Margin + ContentWidth * 0.6f, y, ContentWidth * 0.4f, cellBoldFont, ink, right);
            y += 16;
        }
        if (report.Summary.Count > 0) { y += 4; HorizontalRule(); }

        // Tables.
        foreach (var table in report.Tables)
        {
            EnsureSpace(48);
            if (!string.IsNullOrEmpty(table.Heading))
            {
                Draw(table.Heading, Margin, y, ContentWidth, headingFont, accent, left);
                y += Measure(table.Heading, headingFont, ContentWidth) + 6;
            }

            int cols = table.Columns.Count;
            var weights = table.ColumnWeights is { Count: > 0 } w && w.Count == cols
                ? w.ToArray()
                : Enumerable.Repeat(1f, cols).ToArray();
            float weightSum = weights.Sum();
            var widths = weights.Select(v => ContentWidth * (v / weightSum)).ToArray();
            var xs = new float[cols];
            float acc = Margin;
            for (int i = 0; i < cols; i++) { xs[i] = acc; acc += widths[i]; }

            void DrawRow(IReadOnlyList<string> cells, TextPaint font, Color color)
            {
                float rowH = 0;
                for (int i = 0; i < cols; i++)
                    rowH = Math.Max(rowH, Measure(cells[i], font, widths[i] - 6));
                EnsureSpace(rowH + 6);
                for (int i = 0; i < cols; i++)
                    Draw(cells[i], xs[i] + 2, y, widths[i] - 6, font, color,
                        table.RightAlign.Contains(i) ? right : left);
                y += rowH + 6;
            }

            DrawRow(table.Columns, cellBoldFont, ink);
            HorizontalRule();
            foreach (var row in table.Rows)
                DrawRow(row, cellFont, ink);

            y += 12;
        }

        doc.FinishPage(page);
        using (var stream = File.Create(path))
            doc.WriteTo(stream);
        return (string?)path;
    });
}
