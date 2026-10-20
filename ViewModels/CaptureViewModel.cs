using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReceiptVault.Models;
using ReceiptVault.Services;

namespace ReceiptVault.ViewModels;

public partial class CaptureViewModel(
    IOcrService ocrService,
    IReceiptService receiptService,
    ISubscriptionService subscriptionService,
    IBillingService billingService,
    IReviewPromptService reviewPromptService) : BaseViewModel
{
    [ObservableProperty] private string? capturedImagePath;
    [ObservableProperty] private string merchant = string.Empty;
    [ObservableProperty] private string totalText = string.Empty;
    [ObservableProperty] private DateTime date = DateTime.Today;
    [ObservableProperty] private string selectedCategory = "Other";
    [ObservableProperty] private bool isBusinessExpense;
    [ObservableProperty] private string? notes;
    [ObservableProperty] private bool isProcessingOcr;
    [ObservableProperty] private bool hasImage;
    // Fields were filled in from the photo, so remind the user to check them.
    [ObservableProperty] private bool wasScanned;

    public string CurrencySymbol => CurrencyFormat.Symbol;
    public string MerchantPlaceholder => CurrencyFormat.Code == "USD"
        ? "e.g. Walmart, Starbucks"
        : "e.g. Tesco, Costa Coffee";

    // Kept with the saved receipt so mis-reads can be diagnosed later.
    private string? rawOcrText;

    // Stored receipt photos are capped at this long edge (full-res camera images were ~24 MB).
    private const int MaxStoredImageEdge = 2560;

    public ObservableCollection<LineItem> LineItems { get; } = [];

    public bool HasLineItems => LineItems.Count > 0;

    // Item scanning is a Premium feature. Free users still get items scanned (so they
    // see the value and the data is there when they upgrade), but the list is locked.
    public bool IsPremium => subscriptionService.Has(Shared.Entitlement.LineItemOcr);
    public bool ShowLockedItems => !IsPremium && HasLineItems;
    public bool ShowBottomAddItem => IsPremium && HasLineItems;
    public int LineItemCount => LineItems.Count;

    private void NotifyItemsChanged()
    {
        OnPropertyChanged(nameof(HasLineItems));
        OnPropertyChanged(nameof(ShowLockedItems));
        OnPropertyChanged(nameof(ShowBottomAddItem));
        OnPropertyChanged(nameof(LineItemCount));
    }

    // Re-evaluate the premium-gated properties so the item list reveals immediately after
    // an unlock, without leaving the page (navigating away would reset the capture).
    public void RefreshPremiumState()
    {
        OnPropertyChanged(nameof(IsPremium));
        OnPropertyChanged(nameof(ShowLockedItems));
        OnPropertyChanged(nameof(ShowBottomAddItem));
    }

    [RelayCommand]
    private async Task UnlockAsync()
    {
        var unlocked = await PremiumPurchase.RunAsync(billingService);
        if (unlocked) RefreshPremiumState();
    }

    public List<string> Categories => Models.Category.All.Select(c => c.Name).ToList();

    [RelayCommand]
    private async Task TakePhotoAsync()
    {
        if (!MediaPicker.Default.IsCaptureSupported)
        {
            await Shell.Current.DisplayAlertAsync("Not supported", "Camera not available on this device.", "OK");
            return;
        }

        FileResult? photo;
        try
        {
            photo = await MediaPicker.Default.CapturePhotoAsync();
        }
        catch (PermissionException)
        {
            // Denied camera/storage access throws rather than returning null; an
            // unhandled throw from a relay command takes the whole app down.
            await ShowPermissionAlertAsync("camera");
            return;
        }
        await ProcessPhotoAsync(photo);
    }

    [RelayCommand]
    private async Task PickPhotoAsync()
    {
        IEnumerable<FileResult>? photos;
        try
        {
            photos = await MediaPicker.Default.PickPhotosAsync();
        }
        catch (PermissionException)
        {
            await ShowPermissionAlertAsync("photos");
            return;
        }
        await ProcessPhotoAsync(photos?.FirstOrDefault());
    }

    private static Task ShowPermissionAlertAsync(string what) =>
        Shell.Current.DisplayAlertAsync(
            "Permission needed",
            $"Receipt Vault needs access to your {what} to add a receipt. You can allow it in Settings.",
            "OK");

    private async Task ProcessPhotoAsync(FileResult? photo)
    {
        if (photo is null) return;

        // Write to a temporary file first — we only keep it if it validates as a receipt.
        var imagesDir = Receipt.ImagesDirectory;
        Directory.CreateDirectory(imagesDir);
        var localPath = Path.Combine(imagesDir, $"{Guid.NewGuid()}.jpg");

        using (var sourceStream = await photo.OpenReadAsync())
        using (var destStream = File.OpenWrite(localPath))
            await sourceStream.CopyToAsync(destStream);

        IsProcessingOcr = true;
        OcrResult result;
        try
        {
            result = await ocrService.RecognizeReceiptAsync(localPath);
        }
        finally
        {
            IsProcessingOcr = false;
        }

        // Guardrail: only genuine receipts are stored. Anything that doesn't read as a
        // receipt (or where the receipt doesn't fill the frame) is discarded, not saved.
        if (!result.Validation.IsLikelyReceipt)
        {
            TryDelete(localPath);
            await Shell.Current.DisplayAlertAsync(
                "That doesn't look like a receipt",
                "We couldn't find receipt details in this photo. Make sure the receipt is well-lit and fills most of the frame, then try again.\n\n" +
                "If you're still having problems, let us know at farabovestudios@gmail.com.",
                "OK");
            return;
        }

        // OCR has already read the full-resolution photo; only now shrink it for storage.
        await ShrinkForStorageAsync(localPath);

        CapturedImagePath = localPath;
        HasImage = true;

        if (result.Success)
        {
            WasScanned = true;
            Merchant = result.Merchant;
            rawOcrText = result.RawText;
            TotalText = result.Total?.ToString("F2") ?? string.Empty;
            Date = result.Date ?? DateTime.Today;

            LineItems.Clear();
            foreach (var item in result.Items)
                LineItems.Add(item);
            NotifyItemsChanged();
        }
    }

    // Downsizes and re-encodes the photo in place. Best effort: on any failure the
    // full-size original is kept. iOS only for now — Android's PlatformImage ignores the
    // EXIF orientation flag, so a re-encoded camera photo could come out sideways.
    private static async Task ShrinkForStorageAsync(string path)
    {
#if IOS
        var tempPath = path + ".tmp";
        try
        {
            using (var source = File.OpenRead(path))
            {
                using var image = Microsoft.Maui.Graphics.Platform.PlatformImage.FromStream(source);
                if (Math.Max(image.Width, image.Height) > MaxStoredImageEdge)
                {
                    using var shrunk = image.Downsize(MaxStoredImageEdge, disposeOriginal: false);
                    using var dest = File.Create(tempPath);
                    await shrunk.SaveAsync(dest, ImageFormat.Jpeg, quality: 0.8f);
                }
                else
                {
                    using var dest = File.Create(tempPath);
                    await image.SaveAsync(dest, ImageFormat.Jpeg, quality: 0.8f);
                }
            }
            File.Move(tempPath, path, overwrite: true);
        }
        catch
        {
            TryDelete(tempPath);
        }
#else
        await Task.CompletedTask;
#endif
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { /* best effort */ }
    }

    // Top "＋ Add item" button — inserts a new blank item at the top of the list.
    [RelayCommand]
    private void AddLineItem()
    {
        LineItems.Insert(0, new LineItem { Description = string.Empty, Quantity = 1 });
        NotifyItemsChanged();
    }

    // Bottom "＋ Add item" button — appends a new blank item at the end of the list.
    [RelayCommand]
    private void AddLineItemBottom()
    {
        LineItems.Add(new LineItem { Description = string.Empty, Quantity = 1 });
        NotifyItemsChanged();
    }

    [RelayCommand]
    private void RemoveLineItem(LineItem item)
    {
        LineItems.Remove(item);
        NotifyItemsChanged();
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Merchant))
        {
            await Shell.Current.DisplayAlertAsync("Missing info", "Please enter a merchant name.", "OK");
            return;
        }

        _ = decimal.TryParse(TotalText, out var total);

        var receipt = new Receipt
        {
            Merchant = Merchant,
            Total = total,
            Currency = CurrencyFormat.Code,
            Date = Date,
            Category = SelectedCategory,
            IsBusinessExpense = IsBusinessExpense,
            ImagePath = CapturedImagePath is null ? null : Path.GetFileName(CapturedImagePath),
            Notes = Notes,
            RawOcrText = rawOcrText,
        };

        await receiptService.SaveAsync(receipt);

        foreach (var item in LineItems)
            item.Currency = receipt.Currency;
        await receiptService.SaveLineItemsAsync(receipt.Id, LineItems);

        await Shell.Current.GoToAsync("..");
        _ = reviewPromptService.OnReceiptSavedAsync();
    }

    [RelayCommand]
    private async Task CancelAsync()
    {
        if (CapturedImagePath is not null && File.Exists(CapturedImagePath))
            File.Delete(CapturedImagePath);

        await Shell.Current.GoToAsync("..");
    }

    public void Reset()
    {
        CapturedImagePath = null;
        HasImage = false;
        WasScanned = false;
        rawOcrText = null;
        Merchant = string.Empty;
        TotalText = string.Empty;
        Date = DateTime.Today;
        SelectedCategory = "Other";
        IsBusinessExpense = false;
        Notes = null;
        LineItems.Clear();
        NotifyItemsChanged();
        RefreshPremiumState();
    }
}
