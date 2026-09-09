using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReceiptVault.Models;
using ReceiptVault.Services;

namespace ReceiptVault.ViewModels;

[QueryProperty(nameof(ReceiptId), "id")]
public partial class ReceiptDetailViewModel(
    IReceiptService receiptService,
    IExportService exportService,
    ISubscriptionService subscriptionService,
    IBillingService billingService) : BaseViewModel
{
    [ObservableProperty] private int receiptId;
    [ObservableProperty] private Receipt? receipt;
    [ObservableProperty] private string merchant = string.Empty;
    [ObservableProperty] private string totalText = string.Empty;
    [ObservableProperty] private DateTime date = DateTime.Today;
    [ObservableProperty] private string selectedCategory = "Other";
    [ObservableProperty] private bool isBusinessExpense;
    [ObservableProperty] private string? notes;
    [ObservableProperty] private bool isEditing;

    public ObservableCollection<LineItem> LineItems { get; } = [];

    public bool HasLineItems => LineItems.Count > 0;

    // Item scanning is a Premium feature. The items are stored regardless, but a free
    // user sees them locked behind an unlock prompt rather than the itemised breakdown.
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
    // an unlock (the items are already loaded, they were just hidden).
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

    public List<string> Categories => Category.All.Select(c => c.Name).ToList();

    partial void OnReceiptIdChanged(int value) => _ = LoadAsync(value);

    public async Task LoadAsync(int id)
    {
        await RunAsync(async () =>
        {
            Receipt = await receiptService.GetByIdAsync(id);
            if (Receipt is null) return;

            Merchant = Receipt.Merchant;
            TotalText = Receipt.Total.ToString("F2");
            Date = Receipt.Date;
            SelectedCategory = Receipt.Category;
            IsBusinessExpense = Receipt.IsBusinessExpense;
            Notes = Receipt.Notes;

            LineItems.Clear();
            foreach (var item in await receiptService.GetLineItemsAsync(id))
                LineItems.Add(item);
            OnPropertyChanged(nameof(IsPremium));
            NotifyItemsChanged();
        });
    }

    [RelayCommand]
    private void ToggleEdit() => IsEditing = !IsEditing;

    // Top "＋ Add item" button — inserts a new blank item at the top of the list.
    [RelayCommand]
    private void AddLineItem()
    {
        LineItems.Insert(0, NewLineItem());
        NotifyItemsChanged();
    }

    // Bottom "＋ Add item" button — appends a new blank item at the end of the list.
    [RelayCommand]
    private void AddLineItemBottom()
    {
        LineItems.Add(NewLineItem());
        NotifyItemsChanged();
    }

    private LineItem NewLineItem() => new()
    {
        Description = string.Empty,
        Quantity = 1,
        Currency = Receipt?.Currency ?? "GBP",
    };

    [RelayCommand]
    private void RemoveLineItem(LineItem item)
    {
        LineItems.Remove(item);
        NotifyItemsChanged();
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (Receipt is null) return;

        _ = decimal.TryParse(TotalText, out var total);

        Receipt.Merchant = Merchant;
        Receipt.Total = total;
        Receipt.Date = Date;
        Receipt.Category = SelectedCategory;
        Receipt.IsBusinessExpense = IsBusinessExpense;
        Receipt.Notes = Notes;

        await receiptService.SaveAsync(Receipt);

        foreach (var item in LineItems)
            item.Currency = Receipt.Currency;
        await receiptService.SaveLineItemsAsync(Receipt.Id, LineItems);

        IsEditing = false;
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (Receipt is null) return;

        var confirm = await Shell.Current.DisplayAlertAsync(
            "Delete receipt", "This cannot be undone.", "Delete", "Cancel");
        if (!confirm) return;

        await receiptService.DeleteAsync(Receipt);
        await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    private async Task ShareReceiptAsync()
    {
        if (Receipt is null) return;

        var safeMerchant = string.Concat($"{Receipt.Merchant}".Split(Path.GetInvalidFileNameChars()));
        var name = $"receipt_{Receipt.Date:yyyy-MM-dd}_{safeMerchant}";
        var date = Receipt.Date.Date;

        // Free tier shares a plain CSV; Premium gets the styled, itemised spreadsheet.
        string filePath;
        if (IsPremium)
        {
            var items = new Dictionary<int, List<LineItem>> { [Receipt.Id] = [.. LineItems] };
            filePath = await exportService.ExportToXlsxAsync([Receipt], name, items, date, date);
        }
        else
        {
            filePath = await exportService.ExportToCsvAsync([Receipt], name, date, date);
        }

        await exportService.ShareFileAsync(filePath);
    }
}
