using LpsGateway.Data.Models;

namespace LpsGateway.Data;

public interface IEFileRepository
{
    Task<bool> IsFileProcessedAsync(string commonAddr, string typeId, string fileName);
    Task MarkFileProcessedAsync(string commonAddr, string typeId, string fileName, int fileSize, string status = "SUCCESS", string? errorMessage = null);
    Task UpsertInfoTableAsync(string tableName, Dictionary<string, object?> record, string keyField);
    Task InsertRecordsAsync(string tableName, List<Dictionary<string, object?>> records);
}
