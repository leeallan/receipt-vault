using CoreGraphics;
using Foundation;
using Microsoft.Maui.Storage;
using UIKit;

namespace ReceiptVault.Services;

// Renders a PdfReport to a real PDF with UIKit's PDF renderer. A4 portrait, with
// simple top-to-bottom flow and automatic page breaks when content reaches the margin.
public class PdfService : IPdfService
{
    public bool IsSupported => true;

    // A4 at 72 dpi.
    const float PageWidth = 595f;
    const float PageHeight = 842f;
    const float Margin = 40f;
    const float ContentWidth = PageWidth - 2 * Margin;

    public Task<string?> RenderAsync(PdfReport report, string fileName)
    {
        var exportDir = Path.Combine(FileSystem.CacheDirectory, "exports");
        Directory.CreateDirectory(exportDir);
        var path = Path.Combine(exportDir, fileName.EndsWith(".pdf") ? fileName : fileName + ".pdf");

        var titleFont = UIFont.BoldSystemFontOfSize(22);
        var subtitleFont = UIFont.SystemFontOfSize(12);
        var headingFont = UIFont.BoldSystemFontOfSize(14);
        var cellFont = UIFont.SystemFontOfSize(10);
        var cellBoldFont = UIFont.BoldSystemFontOfSize(10);

        var ink = UIColor.FromRGB(20, 20, 30);
        var muted = UIColor.FromRGB(120, 120, 130);
        var accent = UIColor.FromRGB(108, 99, 255);
        var rule = UIColor.FromRGB(220, 220, 228);

        var renderer = new UIGraphicsPdfRenderer(
            new CGRect(0, 0, PageWidth, PageHeight), new UIGraphicsPdfRendererFormat());

        var data = renderer.CreatePdf(ctx =>
        {
            float y = 0;

            void NewPage()
            {
                ctx.BeginPage();
                y = Margin;
            }

            float Measure(string text, UIFont font, float width)
            {
                var attrs = new UIStringAttributes { Font = font };
                var size = new NSString(text ?? "").GetBoundingRect(
                    new CGSize(width, 10000),
                    NSStringDrawingOptions.UsesLineFragmentOrigin, attrs, null);
                return (float)Math.Ceiling(size.Height);
            }

            void Draw(string text, float x, float top, float width, UIFont font, UIColor color, UITextAlignment align)
            {
                var para = new NSMutableParagraphStyle
                {
                    Alignment = align,
                    LineBreakMode = UILineBreakMode.TailTruncation,
                };
                var attrs = new UIStringAttributes { Font = font, ForegroundColor = color, ParagraphStyle = para };
                new NSString(text ?? "").DrawString(
                    new CGRect(x, top, width, Measure(text, font, width) + 2), attrs);
            }

            void EnsureSpace(float needed)
            {
                if (y + needed > PageHeight - Margin) NewPage();
            }

            void HorizontalRule()
            {
                rule.SetFill();
                UIBezierPath.FromRect(new CGRect(Margin, y, ContentWidth, 0.5f)).Fill();
                y += 8;
            }

            NewPage();

            // Title + subtitle.
            Draw(report.Title, Margin, y, ContentWidth, titleFont, ink, UITextAlignment.Left);
            y += Measure(report.Title, titleFont, ContentWidth) + 4;
            if (!string.IsNullOrEmpty(report.Subtitle))
            {
                Draw(report.Subtitle, Margin, y, ContentWidth, subtitleFont, muted, UITextAlignment.Left);
                y += Measure(report.Subtitle, subtitleFont, ContentWidth) + 8;
            }
            HorizontalRule();

            // Summary lines.
            foreach (var (label, value) in report.Summary)
            {
                EnsureSpace(18);
                Draw(label, Margin, y, ContentWidth * 0.6f, cellFont, muted, UITextAlignment.Left);
                Draw(value, Margin + ContentWidth * 0.6f, y, ContentWidth * 0.4f, cellBoldFont, ink, UITextAlignment.Right);
                y += 16;
            }
            if (report.Summary.Count > 0) { y += 4; HorizontalRule(); }

            // Tables.
            foreach (var table in report.Tables)
            {
                EnsureSpace(48);
                if (!string.IsNullOrEmpty(table.Heading))
                {
                    Draw(table.Heading, Margin, y, ContentWidth, headingFont, accent, UITextAlignment.Left);
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

                void DrawRow(IReadOnlyList<string> cells, UIFont font, UIColor color)
                {
                    float rowH = 0;
                    for (int i = 0; i < cols; i++)
                        rowH = Math.Max(rowH, Measure(cells[i], font, widths[i] - 6));
                    EnsureSpace(rowH + 6);
                    for (int i = 0; i < cols; i++)
                    {
                        var align = table.RightAlign.Contains(i) ? UITextAlignment.Right : UITextAlignment.Left;
                        Draw(cells[i], xs[i] + 2, y, widths[i] - 6, font, color, align);
                    }
                    y += rowH + 6;
                }

                DrawRow(table.Columns, cellBoldFont, ink);
                HorizontalRule();
                foreach (var row in table.Rows)
                    DrawRow(row, cellFont, ink);

                y += 12;
            }
        });

        File.WriteAllBytes(path, data.ToArray());
        return Task.FromResult<string?>(path);
    }
}
