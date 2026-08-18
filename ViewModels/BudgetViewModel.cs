using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReceiptVault.Models;
using ReceiptVault.Services;

namespace ReceiptVault.ViewModels;

public partial class BudgetViewModel(
    DatabaseService db,
    IReceiptService receiptService) : BaseViewModel
{
    [ObservableProperty] private ObservableCollection<BudgetItem> budgetItems = [];
    [ObservableProperty] private decimal totalSpent;
    [ObservableProperty] private decimal totalBudget;
    [ObservableProperty] private double totalProgress;
    [ObservableProperty] private string currentMonthLabel = DateTime.Today.ToString("MMMM yyyy");

    public async Task LoadAsync()
    {
        await RunAsync(async () =>
        {
            var now = DateTime.Today;
            var items = await db.GetAllAsync<BudgetItem>();
            var spending = await receiptService.GetSpendingByCategoryAsync(now.Year, now.Month);

            foreach (var item in items)
                item.SpentAmount = spending.TryGetValue(item.Category, out var s) ? s : 0;

            BudgetItems = new ObservableCollection<BudgetItem>(items.OrderByDescending(i => i.SpentAmount));
            TotalSpent = items.Sum(i => i.SpentAmount);
            TotalBudget = items.Sum(i => i.MonthlyLimit);
            TotalProgress = TotalBudget > 0 ? Math.Min((double)(TotalSpent / TotalBudget), 1.0) : 0;
        });
    }

    [RelayCommand]
    private async Task AddBudgetAsync()
    {
        var categoryResult = await Shell.Current.DisplayActionSheetAsync(
            "Select category", "Cancel", null,
            Category.All.Select(c => c.Name).ToArray());

        if (categoryResult is null or "Cancel") return;

        var limitInput = await Shell.Current.DisplayPromptAsync(
            "Monthly limit", $"Budget for {categoryResult} (£):",
            keyboard: Keyboard.Numeric);

        if (!decimal.TryParse(limitInput, out var limit) || limit <= 0) return;

        var existing = BudgetItems.FirstOrDefault(b => b.Category == categoryResult);
        if (existing is not null)
        {
            existing.MonthlyLimit = limit;
            await db.InsertOrReplaceAsync(existing);
        }
        else
        {
            var newItem = new BudgetItem { Category = categoryResult, MonthlyLimit = limit };
            await db.InsertAsync(newItem);
        }

        await LoadAsync();
    }

    [RelayCommand]
    private async Task DeleteBudgetAsync(BudgetItem item)
    {
        await db.DeleteAsync(item);
        BudgetItems.Remove(item);
    }
}
