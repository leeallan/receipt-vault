using Azure;
using Azure.Data.Tables;

namespace ReceiptVault.Server.Storage;

// One row per store identity. PartitionKey = platform ("Apple"/"Google"),
// RowKey = the store's stable id (Apple originalTransactionId / Google purchase token).
// This is what makes entitlements survive reinstalls: the same id always maps to the row.
public class EntitlementRecord : ITableEntity
{
    public string PartitionKey { get; set; } = string.Empty;
    public string RowKey { get; set; } = string.Empty;

    public string ProductId { get; set; } = string.Empty;
    public string Tier { get; set; } = "Free";
    public bool IsActive { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ETag ETag { get; set; }
    public DateTimeOffset? Timestamp { get; set; }
}
