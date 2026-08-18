using SQLite;
using ReceiptVault.Models;

namespace ReceiptVault.Services;

public class DatabaseService
{
    private SQLiteAsyncConnection? _db;

    private async Task<SQLiteAsyncConnection> GetDbAsync()
    {
        if (_db is not null) return _db;

        var dbPath = Path.Combine(FileSystem.AppDataDirectory, "receiptvault.db3");
        _db = new SQLiteAsyncConnection(dbPath, SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.SharedCache);

        await _db.CreateTableAsync<Receipt>();
        await _db.CreateTableAsync<BudgetItem>();
        await _db.CreateTableAsync<LineItem>();

        return _db;
    }

    public async Task<List<T>> GetAllAsync<T>() where T : new()
    {
        var db = await GetDbAsync();
        return await db.Table<T>().ToListAsync();
    }

    public async Task<T?> GetByIdAsync<T>(int id) where T : new()
    {
        var db = await GetDbAsync();
        return await db.FindAsync<T>(id);
    }

    // Inserts a new row and lets SQLite assign the AutoIncrement primary key
    // (back-filled onto the item). Use this for new records — InsertOrReplace
    // writes the explicit PK, so a default Id of 0 would overwrite the prior row.
    public async Task<int> InsertAsync<T>(T item)
    {
        var db = await GetDbAsync();
        return await db.InsertAsync(item);
    }

    public async Task<int> InsertOrReplaceAsync<T>(T item)
    {
        var db = await GetDbAsync();
        return await db.InsertOrReplaceAsync(item);
    }

    public async Task<int> DeleteAsync<T>(T item)
    {
        var db = await GetDbAsync();
        return await db.DeleteAsync(item);
    }

    public async Task<List<T>> QueryAsync<T>(string sql, params object[] args) where T : new()
    {
        var db = await GetDbAsync();
        return await db.QueryAsync<T>(sql, args);
    }

    public async Task<T> ExecuteScalarAsync<T>(string sql, params object[] args)
    {
        var db = await GetDbAsync();
        return await db.ExecuteScalarAsync<T>(sql, args);
    }

    public async Task<int> ExecuteAsync(string sql, params object[] args)
    {
        var db = await GetDbAsync();
        return await db.ExecuteAsync(sql, args);
    }
}
