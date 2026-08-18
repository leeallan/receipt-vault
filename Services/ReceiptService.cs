using ReceiptVault.Models;

namespace ReceiptVault.Services;

public class ReceiptService(DatabaseService db) : IReceiptService
{
    public async Task<List<Receipt>> GetAllAsync()
    {
        var all = await db.GetAllAsync<Receipt>();
        return [.. all.OrderByDescending(r => r.Date)];
    }

    public async Task<List<Receipt>> GetByMonthAsync(int year, int month)
    {
        var all = await db.GetAllAsync<Receipt>();
        return [.. all
            .Where(r => r.Date.Year == year && r.Date.Month == month)
            .OrderByDescending(r => r.Date)];
    }

    public async Task<List<Receipt>> SearchAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return await GetAllAsync();

        var all = await db.GetAllAsync<Receipt>();
        var q = query.Trim().ToLowerInvariant();
        return [.. all
            .Where(r =>
                r.Merchant.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                r.Category.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                (r.Notes?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (r.Tags?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false))
            .OrderByDescending(r => r.Date)];
    }

    public async Task<Receipt?> GetByIdAsync(int id) =>
        await db.GetByIdAsync<Receipt>(id);

    public async Task<int> SaveAsync(Receipt receipt)
    {
        receipt.UpdatedAt = DateTime.UtcNow;
        if (receipt.Id == 0)
        {
            receipt.CreatedAt = DateTime.UtcNow;
            return await db.InsertAsync(receipt);
        }
        return await db.InsertOrReplaceAsync(receipt);
    }

    public async Task DeleteAsync(Receipt receipt)
    {
        if (!string.IsNullOrEmpty(receipt.ImagePath) && File.Exists(receipt.ImagePath))
            File.Delete(receipt.ImagePath);

        await db.ExecuteAsync("DELETE FROM LineItems WHERE ReceiptId = ?", receipt.Id);
        await db.DeleteAsync(receipt);
    }

    public async Task<List<LineItem>> GetLineItemsAsync(int receiptId)
    {
        var items = await db.QueryAsync<LineItem>(
            "SELECT * FROM LineItems WHERE ReceiptId = ? ORDER BY Id", receiptId);
        return items;
    }

    public async Task<Dictionary<int, List<LineItem>>> GetAllLineItemsAsync()
    {
        var all = await db.GetAllAsync<LineItem>();
        return all
            .GroupBy(i => i.ReceiptId)
            .ToDictionary(g => g.Key, g => g.OrderBy(i => i.Id).ToList());
    }

    // Replaces the stored line items for a receipt with the supplied set.
    public async Task SaveLineItemsAsync(int receiptId, IEnumerable<LineItem> items)
    {
        await db.ExecuteAsync("DELETE FROM LineItems WHERE ReceiptId = ?", receiptId);

        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.Description) && item.Price == 0 && item.Savings == 0)
                continue;

            item.Id = 0;
            item.ReceiptId = receiptId;
            await db.InsertAsync(item);
        }
    }

    public async Task<decimal> GetMonthlyTotalAsync(int year, int month, bool? isBusinessExpense = null)
    {
        var receipts = await GetByMonthAsync(year, month);
        var filtered = isBusinessExpense.HasValue
            ? receipts.Where(r => r.IsBusinessExpense == isBusinessExpense.Value)
            : receipts;
        return filtered.Sum(r => r.Total);
    }

    public async Task<Dictionary<string, decimal>> GetSpendingByCategoryAsync(int year, int month)
    {
        var receipts = await GetByMonthAsync(year, month);
        return receipts
            .GroupBy(r => r.Category)
            .ToDictionary(g => g.Key, g => g.Sum(r => r.Total));
    }
}
