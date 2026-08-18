using ReceiptVault.Models;

namespace ReceiptVault.Services;

public interface IReceiptService
{
    Task<List<Receipt>> GetAllAsync();
    Task<List<Receipt>> GetByMonthAsync(int year, int month);
    Task<List<Receipt>> SearchAsync(string query);
    Task<Receipt?> GetByIdAsync(int id);
    Task<int> SaveAsync(Receipt receipt);
    Task DeleteAsync(Receipt receipt);
    Task<List<LineItem>> GetLineItemsAsync(int receiptId);
    Task<Dictionary<int, List<LineItem>>> GetAllLineItemsAsync();
    Task SaveLineItemsAsync(int receiptId, IEnumerable<LineItem> items);
    Task<decimal> GetMonthlyTotalAsync(int year, int month, bool? isBusinessExpense = null);
    Task<Dictionary<string, decimal>> GetSpendingByCategoryAsync(int year, int month);
}
