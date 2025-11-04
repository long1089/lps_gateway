using LpsGateway.Data.Models;

namespace LpsGateway.Data;

/// <summary>
/// E 文件仓储接口
/// </summary>
public interface IEFileRepository
{
    /// <summary>
    /// 检查文件是否已处理
    /// </summary>
    /// <param name="commonAddr">公共地址</param>
    /// <param name="typeId">类型标识</param>
    /// <param name="fileName">文件名</param>
    /// <returns>如果文件已处理返回 true，否则返回 false</returns>
    Task<bool> IsFileProcessedAsync(string commonAddr, string typeId, string fileName);

    /// <summary>
    /// 标记文件已处理
    /// </summary>
    /// <param name="commonAddr">公共地址</param>
    /// <param name="typeId">类型标识</param>
    /// <param name="fileName">文件名</param>
    /// <param name="fileSize">文件大小（字节）</param>
    /// <param name="status">处理状态</param>
    /// <param name="errorMessage">错误消息（可选）</param>
    /// <returns>异步任务</returns>
    Task MarkFileProcessedAsync(string commonAddr, string typeId, string fileName, int fileSize, string status = "SUCCESS", string? errorMessage = null);

    /// <summary>
    /// Upsert 信息表记录（存在则更新，不存在则插入）
    /// </summary>
    /// <param name="tableName">表名</param>
    /// <param name="record">记录字典</param>
    /// <param name="keyField">主键字段名</param>
    /// <returns>异步任务</returns>
    Task UpsertInfoTableAsync(string tableName, Dictionary<string, object?> record, string keyField);

    /// <summary>
    /// 批量插入记录
    /// </summary>
    /// <param name="tableName">表名</param>
    /// <param name="records">记录列表</param>
    /// <returns>异步任务</returns>
    Task InsertRecordsAsync(string tableName, List<Dictionary<string, object?>> records);
}
