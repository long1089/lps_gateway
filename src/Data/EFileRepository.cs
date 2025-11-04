using LpsGateway.Data.Models;
using SqlSugar;

namespace LpsGateway.Data;

public class EFileRepository : IEFileRepository
{
    private readonly ISqlSugarClient _db;

    public EFileRepository(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task<bool> IsFileProcessedAsync(string commonAddr, string typeId, string fileName)
    {
        var count = await _db.Queryable<ReceivedEfile>()
            .Where(x => x.CommonAddr == commonAddr && x.TypeId == typeId && x.FileName == fileName)
            .CountAsync();
        return count > 0;
    }

    public async Task MarkFileProcessedAsync(string commonAddr, string typeId, string fileName, int fileSize, string status = "SUCCESS", string? errorMessage = null)
    {
        var record = new ReceivedEfile
        {
            CommonAddr = commonAddr,
            TypeId = typeId,
            FileName = fileName,
            ReceivedAt = DateTime.UtcNow,
            FileSize = fileSize,
            Status = status,
            ErrorMessage = errorMessage
        };
        await _db.Insertable(record).ExecuteCommandAsync();
    }

    public async Task UpsertInfoTableAsync(string tableName, Dictionary<string, object?> record, string keyField)
    {
        if (!record.ContainsKey(keyField))
        {
            throw new ArgumentException($"Record must contain key field '{keyField}'");
        }

        var keyValue = record[keyField];
        var sql = $"SELECT COUNT(*) FROM {tableName} WHERE {keyField} = @keyValue";
        var exists = await _db.Ado.GetIntAsync(sql, new { keyValue });

        if (exists > 0)
        {
            var setClauses = record.Where(kv => kv.Key != keyField)
                .Select(kv => $"{kv.Key} = @{kv.Key}");
            var updateSql = $"UPDATE {tableName} SET {string.Join(", ", setClauses)} WHERE {keyField} = @{keyField}";
            await _db.Ado.ExecuteCommandAsync(updateSql, record);
        }
        else
        {
            var columns = string.Join(", ", record.Keys);
            var parameters = string.Join(", ", record.Keys.Select(k => $"@{k}"));
            var insertSql = $"INSERT INTO {tableName} ({columns}) VALUES ({parameters})";
            await _db.Ado.ExecuteCommandAsync(insertSql, record);
        }
    }

    public async Task InsertRecordsAsync(string tableName, List<Dictionary<string, object?>> records)
    {
        if (records.Count == 0) return;

        var columns = string.Join(", ", records[0].Keys);
        var parameters = string.Join(", ", records[0].Keys.Select(k => $"@{k}"));
        var insertSql = $"INSERT INTO {tableName} ({columns}) VALUES ({parameters})";

        foreach (var record in records)
        {
            await _db.Ado.ExecuteCommandAsync(insertSql, record);
        }
    }
}
