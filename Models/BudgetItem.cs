using SQLite;

namespace ReceiptVault.Models;

[Table("BudgetItems")]
public class BudgetItem
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public string Category { get; set; } = string.Empty;
    public decimal MonthlyLimit { get; set; }
    public bool IsBusinessBudget { get; set; }

    [Ignore]
    public decimal SpentAmount { get; set; }

    [Ignore]
    public double Progress => MonthlyLimit > 0
        ? Math.Min((double)(SpentAmount / MonthlyLimit), 1.0)
        : 0;

    [Ignore]
    public bool IsOverBudget => SpentAmount > MonthlyLimit;

    [Ignore]
    public string Remaining => MonthlyLimit > 0
        ? $"{Services.CurrencyFormat.Format(Math.Max(MonthlyLimit - SpentAmount, 0))} left"
        : "No limit";
}
