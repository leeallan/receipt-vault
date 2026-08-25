namespace ReceiptVault.Server.Storage;

public interface IEntitlementStore
{
    Task UpsertAsync(EntitlementRecord record, CancellationToken ct);
    Task<EntitlementRecord?> GetAsync(string platform, string id, CancellationToken ct);
}
