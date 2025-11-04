using LpsGateway.Data.Models;
using Microsoft.Extensions.Logging;
using SqlSugar;

namespace LpsGateway.Data;

/// <summary>
/// E 文件仓储实现，负责数据库操作
/// </summary>
public class EFileRepository : IEFileRepository
{
    private readonly ISqlSugarClient _db;
    private readonly ILogger<EFileRepository> _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="db">SqlSugar 数据库客户端</param>
    /// <param name="logger">日志记录器</param>
    public EFileRepository(ISqlSugarClient db, ILogger<EFileRepository> logger)
    {
        _db = db;
        _logger = logger;
        _logger.LogInformation("EFileRepository 已创建");
    }

    /// <summary>
    /// 检查文件是否已处理
    /// </summary>
    /// <param name="commonAddr">公共地址</param>
    /// <param name="typeId">类型标识</param>
    /// <param name="fileName">文件名</param>
    /// <returns>如果文件已处理返回 true，否则返回 false</returns>
    public async Task<bool> IsFileProcessedAsync(string commonAddr, string typeId, string fileName)
    {
        _logger.LogDebug("检查文件是否已处理: {FileName}, CommonAddr={CommonAddr}, TypeId={TypeId}", 
            fileName, commonAddr, typeId);
        
        var count = await _db.Queryable<ReceivedEfile>()
            .Where(x => x.CommonAddr == commonAddr && x.TypeId == typeId && x.FileName == fileName)
            .CountAsync();
        
        var processed = count > 0;
        _logger.LogDebug("文件处理状态: {FileName} -> {Processed}", fileName, processed);
        
        return processed;
    }

    /// <summary>
    /// 标记文件已处理
    /// </summary>
    /// <param name="commonAddr">公共地址</param>
    /// <param name="typeId">类型标识</param>
    /// <param name="fileName">文件名</param>
    /// <param name="fileSize">文件大小（字节）</param>
    /// <param name="status">处理状态</param>
    /// <param name="errorMessage">错误消息（可选）</param>
    public async Task MarkFileProcessedAsync(string commonAddr, string typeId, string fileName, int fileSize, string status = "SUCCESS", string? errorMessage = null)
    {
        _logger.LogInformation("标记文件已处理: {FileName}, Status={Status}", fileName, status);
        
        try
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
            _logger.LogDebug("文件记录已插入数据库: {FileName}", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "标记文件失败: {FileName}", fileName);
            throw;
        }
    }

    /// <summary>
    /// Upsert 信息表记录（存在则更新，不存在则插入）
    /// </summary>
    /// <param name="tableName">表名</param>
    /// <param name="record">记录字典</param>
    /// <param name="keyField">主键字段名</param>
    public async Task UpsertInfoTableAsync(string tableName, Dictionary<string, object?> record, string keyField)
    {
        if (!record.ContainsKey(keyField))
        {
            _logger.LogError("记录缺少主键字段: {KeyField}", keyField);
            throw new ArgumentException($"记录必须包含主键字段 '{keyField}'");
        }

        var keyValue = record[keyField];
        _logger.LogDebug("Upsert 记录: 表={TableName}, 主键={KeyField}, 值={KeyValue}", 
            tableName, keyField, keyValue);

        try
        {
            // 开始事务
            _db.Ado.BeginTran();

            var sql = $"SELECT COUNT(*) FROM {tableName} WHERE {keyField} = @keyValue";
            var exists = await _db.Ado.GetIntAsync(sql, new { keyValue });

            if (exists > 0)
            {
                _logger.LogDebug("记录已存在，执行更新: 表={TableName}, 主键值={KeyValue}", tableName, keyValue);
                
                var setClauses = record.Where(kv => kv.Key != keyField)
                    .Select(kv => $"{kv.Key} = @{kv.Key}");
                var updateSql = $"UPDATE {tableName} SET {string.Join(", ", setClauses)} WHERE {keyField} = @{keyField}";
                await _db.Ado.ExecuteCommandAsync(updateSql, record);
            }
            else
            {
                _logger.LogDebug("记录不存在，执行插入: 表={TableName}, 主键值={KeyValue}", tableName, keyValue);
                
                var columns = string.Join(", ", record.Keys);
                var parameters = string.Join(", ", record.Keys.Select(k => $"@{k}"));
                var insertSql = $"INSERT INTO {tableName} ({columns}) VALUES ({parameters})";
                await _db.Ado.ExecuteCommandAsync(insertSql, record);
            }

            // 提交事务
            _db.Ado.CommitTran();
            _logger.LogInformation("Upsert 成功: 表={TableName}, 主键值={KeyValue}", tableName, keyValue);
        }
        catch (Exception ex)
        {
            _db.Ado.RollbackTran();
            _logger.LogError(ex, "Upsert 失败: 表={TableName}, 主键值={KeyValue}", tableName, keyValue);
            throw;
        }
    }

    /// <summary>
    /// 批量插入记录
    /// </summary>
    /// <param name="tableName">表名</param>
    /// <param name="records">记录列表</param>
    public async Task InsertRecordsAsync(string tableName, List<Dictionary<string, object?>> records)
    {
        if (records.Count == 0)
        {
            _logger.LogDebug("无记录需要插入: 表={TableName}", tableName);
            return;
        }

        _logger.LogInformation("批量插入记录: 表={TableName}, 记录数={Count}", tableName, records.Count);

        try
        {
            // 开始事务
            _db.Ado.BeginTran();

            var columns = string.Join(", ", records[0].Keys);
            var parameters = string.Join(", ", records[0].Keys.Select(k => $"@{k}"));
            var insertSql = $"INSERT INTO {tableName} ({columns}) VALUES ({parameters})";

            int insertedCount = 0;
            foreach (var record in records)
            {
                await _db.Ado.ExecuteCommandAsync(insertSql, record);
                insertedCount++;
            }

            // 提交事务
            _db.Ado.CommitTran();
            _logger.LogInformation("批量插入成功: 表={TableName}, 插入数={Count}", tableName, insertedCount);
        }
        catch (Exception ex)
        {
            _db.Ado.RollbackTran();
            _logger.LogError(ex, "批量插入失败: 表={TableName}", tableName);
            throw;
        }
    }
}
