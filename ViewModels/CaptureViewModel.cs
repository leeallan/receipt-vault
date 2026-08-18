using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReceiptVault.Models;
using ReceiptVault.Services;

namespace ReceiptVault.ViewModels;

public partial class CaptureViewModel(
    IOcrService ocrService,
    IReceiptService receiptService,
    ISubscriptionService subscriptionService) : BaseViewModel
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
    [ObservableProperty] private bool showUpgradePrompt;

    public ObservableCollection<LineItem> LineItems { get; } = [];

    public bool HasLineItems => LineItems.Count > 0;

    public List<string> Categories => Models.Category.All.Select(c => c.Name).ToList();

    [RelayCommand]
    private async Task TakePhotoAsync()
    {
        if (!MediaPicker.Default.IsCaptureSupported)
        {
            await Shell.Current.DisplayAlertAsync("Not supported", "Camera not available on this device.", "OK");
            return;
        }

        var photo = await MediaPicker.Default.CapturePhotoAsync();
        await ProcessPhotoAsync(photo);
    }

    [RelayCommand]
    private async Task PickPhotoAsync()
    {
        var photos = await MediaPicker.Default.PickPhotosAsync();
        var photo = photos?.FirstOrDefault();
        await ProcessPhotoAsync(photo);
    }

    private async Task ProcessPhotoAsync(FileResult? photo)
    {
        if (photo is null) return;

        // Save a local copy
        var imagesDir = Path.Combine(FileSystem.AppDataDirectory, "receipt_images");
        Directory.CreateDirectory(imagesDir);
        var localPath = Path.Combine(imagesDir, $"{Guid.NewGuid()}.jpg");

        using var sourceStream = await photo.OpenReadAsync();
        using var destStream = File.OpenWrite(localPath);
        await sourceStream.CopyToAsync(destStream);

        CapturedImagePath = localPath;
        HasImage = true;

        await RunOcrAsync(localPath);
    }

    private async Task RunOcrAsync(string imagePath)
    {
        IsProcessingOcr = true;
        try
        {
            var result = await ocrService.RecognizeReceiptAsync(imagePath);
            if (result.Success)
            {
                Merchant = result.Merchant;
                TotalText = result.Total?.ToString("F2") ?? string.Empty;
                Date = result.Date ?? DateTime.Today;

                LineItems.Clear();
                foreach (var item in result.Items)
                    LineItems.Add(item);
                OnPropertyChanged(nameof(HasLineItems));
            }
        }
        finally
        {
            IsProcessingOcr = false;
        }
    }

    [RelayCommand]
    private void AddLineItem()
    {
        LineItems.Add(new LineItem { Description = string.Empty, Quantity = 1 });
        OnPropertyChanged(nameof(HasLineItems));
    }

    [RelayCommand]
    private void RemoveLineItem(LineItem item)
    {
        LineItems.Remove(item);
        OnPropertyChanged(nameof(HasLineItems));
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Merchant))
        {
            await Shell.Current.DisplayAlertAsync("Missing info", "Please enter a merchant name.", "OK");
            return;
        }

        var all = await receiptService.GetAllAsync();
        var canAdd = await subscriptionService.CheckCanAddReceiptAsync(all.Count);
        if (!canAdd)
        {
            ShowUpgradePrompt = true;
            return;
        }

        _ = decimal.TryParse(TotalText, out var total);

        var receipt = new Receipt
        {
            Merchant = Merchant,
            Total = total,
            Date = Date,
            Category = SelectedCategory,
            IsBusinessExpense = IsBusinessExpense,
            ImagePath = CapturedImagePath,
            Notes = Notes,
        };

        await receiptService.SaveAsync(receipt);

        foreach (var item in LineItems)
            item.Currency = receipt.Currency;
        await receiptService.SaveLineItemsAsync(receipt.Id, LineItems);

        await Shell.Current.GoToAsync("..");
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
        Merchant = string.Empty;
        TotalText = string.Empty;
        Date = DateTime.Today;
        SelectedCategory = "Other";
        IsBusinessExpense = false;
        Notes = null;
        ShowUpgradePrompt = false;
        LineItems.Clear();
        OnPropertyChanged(nameof(HasLineItems));
    }
}
