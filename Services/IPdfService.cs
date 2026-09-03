namespace ReceiptVault.Services;

// Renders a platform-agnostic PdfReport into an actual PDF file. Implemented per
// platform (iOS uses UIKit's PDF renderer); Android is a stub for now.
public interface IPdfService
{
    // True when this platform can produce PDFs.
    bool IsSupported { get; }

    // Renders the report to a PDF in the exports cache directory and returns the file
    // path, or null if PDF export isn't available on this platform.
    Task<string?> RenderAsync(PdfReport report, string fileName);
}

// A simple, platform-neutral description of a printable report. Nothing here is
// iOS/Android specific — the platform renderer turns it into a real PDF.
public sealed class PdfReport
{
    public required string Title { get; init; }
    public string? Subtitle { get; init; }

    // "Label / value" lines shown under the title (totals, date range, etc.).
    public List<(string Label, string Value)> Summary { get; init; } = [];

    public List<PdfTable> Tables { get; init; } = [];
}

public sealed class PdfTable
{
    public string? Heading { get; init; }
    public required IReadOnlyList<string> Columns { get; init; }
    public required IReadOnlyList<IReadOnlyList<string>> Rows { get; init; }

    // Optional relative column widths (must match Columns count); equal widths if null.
    public IReadOnlyList<float>? ColumnWeights { get; init; }

    // 0-based indices of columns to right-align (typically money columns).
    public HashSet<int> RightAlign { get; init; } = [];
}
