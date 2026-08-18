using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReceiptVault.Models;
using ReceiptVault.Services;

namespace ReceiptVault.ViewModels;

[QueryProperty(nameof(ReceiptId), "id")]
public partial class ReceiptDetailViewModel(
    IReceiptService receiptService,
    IExportService exportService) : BaseViewModel
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
            OnPropertyChanged(nameof(HasLineItems));
        });
    }

    [RelayCommand]
    private void ToggleEdit() => IsEditing = !IsEditing;

    [RelayCommand]
    private void AddLineItem()
    {
        LineItems.Add(new LineItem
        {
            Description = string.Empty,
            Quantity = 1,
            Currency = Receipt?.Currency ?? "GBP",
        });
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

        var filePath = await exportService.ExportToCsvAsync(
            [Receipt], $"receipt_{Receipt.Date:yyyy-MM-dd}_{Receipt.Merchant}");
        await exportService.ShareFileAsync(filePath);
    }
}
