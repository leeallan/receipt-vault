namespace ReceiptVault.Services;

// Android PDF stub. PDF export is an iOS-first premium feature; wire up
// Android.Graphics.Pdf.PdfDocument here when the Android build ships.
public class PdfService : IPdfService
{
    public bool IsSupported => false;

    public Task<string?> RenderAsync(PdfReport report, string fileName) =>
        Task.FromResult<string?>(null);
}
