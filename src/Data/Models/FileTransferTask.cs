using SqlSugar;

namespace LpsGateway.Data.Models;

/// <summary>
/// 文件传输任务实体
/// </summary>
[SugarTable("file_transfer_tasks")]
public class FileTransferTask
{
    /// <summary>
    /// 任务ID
    /// </summary>
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    /// <summary>
    /// 文件记录ID
    /// </summary>
    [SugarColumn(IsNullable = false, ColumnName = "file_record_id")]
    public int FileRecordId { get; set; }

    /// <summary>
    /// 会话ID
    /// </summary>
    [SugarColumn(Length = 100, IsNullable = true, ColumnName = "session_id")]
    public string? SessionId { get; set; }

    /// <summary>
    /// 任务状态 (pending/in_progress/completed/failed/cancelled)
    /// </summary>
    [SugarColumn(Length = 20, IsNullable = false)]
    public string Status { get; set; } = "pending";

    /// <summary>
    /// 进度百分比
    /// </summary>
    [SugarColumn(IsNullable = false)]
    public int Progress { get; set; } = 0;

    /// <summary>
    /// 总段数
    /// </summary>
    [SugarColumn(IsNullable = true, ColumnName = "total_segments")]
    public int? TotalSegments { get; set; }

    /// <summary>
    /// 已发送段数
    /// </summary>
    [SugarColumn(IsNullable = false, ColumnName = "sent_segments")]
    public int SentSegments { get; set; } = 0;

    /// <summary>
    /// 创建时间
    /// </summary>
    [SugarColumn(IsNullable = false, ColumnName = "created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 开始时间
    /// </summary>
    [SugarColumn(IsNullable = true, ColumnName = "started_at")]
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// 完成时间
    /// </summary>
    [SugarColumn(IsNullable = true, ColumnName = "completed_at")]
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// 错误消息
    /// </summary>
    [SugarColumn(IsNullable = true, ColumnDataType = "text", ColumnName = "error_message")]
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 导航属性：文件记录
    /// </summary>
    [SugarColumn(IsIgnore = true)]
    public FileRecord? FileRecord { get; set; }
}
