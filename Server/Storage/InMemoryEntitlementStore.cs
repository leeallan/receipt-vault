using System.Collections.Concurrent;

namespace ReceiptVault.Server.Storage;

// Non-durable store for local development when no Table Storage connection string is set.
public class InMemoryEntitlementStore : IEntitlementStore
{
    private readonly ConcurrentDictionary<string, EntitlementRecord> _records = new();

    private static string Key(string platform, string id) => $"{platform}|{id}";

    public Task UpsertAsync(EntitlementRecord record, CancellationToken ct)
    {
        _records[Key(record.PartitionKey, record.RowKey)] = record;
        return Task.CompletedTask;
    }

    public Task<EntitlementRecord?> GetAsync(string platform, string id, CancellationToken ct) =>
        Task.FromResult(_records.TryGetValue(Key(platform, id), out var r) ? r : null);
}
