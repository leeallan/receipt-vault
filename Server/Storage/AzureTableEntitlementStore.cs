using Azure;
using Azure.Data.Tables;

namespace ReceiptVault.Server.Storage;

// Durable entitlement store backed by Azure Table Storage — cheap, serverless key→row
// lookups, ideal for the accountless "one row per store identity" model.
public class AzureTableEntitlementStore : IEntitlementStore
{
    private readonly TableClient _table;

    public AzureTableEntitlementStore(string connectionString, string tableName)
    {
        _table = new TableClient(connectionString, tableName);
        _table.CreateIfNotExists();
    }

    public async Task UpsertAsync(EntitlementRecord record, CancellationToken ct) =>
        await _table.UpsertEntityAsync(record, TableUpdateMode.Replace, ct);

    public async Task<EntitlementRecord?> GetAsync(string platform, string id, CancellationToken ct)
    {
        try
        {
            var response = await _table.GetEntityAsync<EntitlementRecord>(platform, id, cancellationToken: ct);
            return response.Value;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }
}
