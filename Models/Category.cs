namespace ReceiptVault.Models;

public class Category
{
    public string Name { get; init; } = string.Empty;
    public string Icon { get; init; } = "🏷️";
    public string ColorHex { get; init; } = "#9090B0";
    public bool IsBusinessCategory { get; init; }

    public static readonly List<Category> All =
    [
        // Personal
        new() { Name = "Groceries",      Icon = "🛒", ColorHex = "#FF7B7B" },
        new() { Name = "Dining",         Icon = "🍽️", ColorHex = "#FF9F43" },
        new() { Name = "Transport",      Icon = "🚌", ColorHex = "#54A0FF" },
        new() { Name = "Entertainment",  Icon = "🎬", ColorHex = "#5F27CD" },
        new() { Name = "Health",         Icon = "💊", ColorHex = "#01CBC6" },
        new() { Name = "Shopping",       Icon = "🛍️", ColorHex = "#FF6CAE" },
        new() { Name = "Utilities",      Icon = "💡", ColorHex = "#FFB347" },

        // Business
        new() { Name = "Meals & Ents",   Icon = "🍷", ColorHex = "#6C63FF", IsBusinessCategory = true },
        new() { Name = "Travel",         Icon = "✈️", ColorHex = "#3AE4C0", IsBusinessCategory = true },
        new() { Name = "Office",         Icon = "🖊️", ColorHex = "#54A0FF", IsBusinessCategory = true },
        new() { Name = "Software",       Icon = "💻", ColorHex = "#8B85FF", IsBusinessCategory = true },
        new() { Name = "Marketing",      Icon = "📣", ColorHex = "#FF9F43", IsBusinessCategory = true },
        new() { Name = "Professional",   Icon = "💼", ColorHex = "#01CBC6", IsBusinessCategory = true },

        // Catch-all
        new() { Name = "Other",          Icon = "🏷️", ColorHex = "#9090B0" },
    ];

    public static Category? Find(string name) =>
        All.FirstOrDefault(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    public static string ColorForName(string name) =>
        Find(name)?.ColorHex ?? "#9090B0";
}
