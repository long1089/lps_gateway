using SqlSugar;

namespace LpsGateway.Data.Models;

/// <summary>
/// 文件记录实体
/// </summary>
[SugarTable("file_records")]
public class FileRecord
{
    /// <summary>
    /// 文件记录ID
    /// </summary>
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    /// <summary>
    /// 报表类型ID
    /// </summary>
    [SugarColumn(IsNullable = false, ColumnName = "report_type_id")]
    public int ReportTypeId { get; set; }

    /// <summary>
    /// SFTP配置ID
    /// </summary>
    [SugarColumn(IsNullable = true, ColumnName = "sftp_config_id")]
    public int? SftpConfigId { get; set; }

    /// <summary>
    /// 原始文件名
    /// </summary>
    [SugarColumn(Length = 255, IsNullable = false, ColumnName = "original_filename")]
    public string OriginalFilename { get; set; } = string.Empty;

    /// <summary>
    /// 存储路径
    /// </summary>
    [SugarColumn(Length = 1000, IsNullable = false, ColumnName = "storage_path")]
    public string StoragePath { get; set; } = string.Empty;

    /// <summary>
    /// 文件大小（字节）
    /// </summary>
    [SugarColumn(IsNullable = false, ColumnName = "file_size")]
    public long FileSize { get; set; }

    /// <summary>
    /// MD5哈希值
    /// </summary>
    [SugarColumn(Length = 32, IsNullable = true, ColumnName = "md5_hash")]
    public string? Md5Hash { get; set; }

    /// <summary>
    /// 下载时间
    /// </summary>
    [SugarColumn(IsNullable = false, ColumnName = "download_time")]
    public DateTime DownloadTime { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 文件状态 (downloaded/processing/sent/error/expired)
    /// </summary>
    [SugarColumn(Length = 20, IsNullable = false)]
    public string Status { get; set; } = "downloaded";
    
    /// <summary>
    /// 处理会话ID（用于独占锁定）
    /// </summary>
    [SugarColumn(Length = 100, IsNullable = true, ColumnName = "process_session_id")]
    public string? ProcessSessionId { get; set; }

    /// <summary>
    /// 保留策略过期时间
    /// </summary>
    [SugarColumn(IsNullable = true, ColumnName = "retention_expires_at")]
    public DateTime? RetentionExpiresAt { get; set; }

    /// <summary>
    /// 错误消息
    /// </summary>
    [SugarColumn(IsNullable = true, ColumnDataType = "text", ColumnName = "error_message")]
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 元数据 (JSON)
    /// </summary>
    [SugarColumn(IsNullable = true, ColumnDataType = "jsonb")]
    public string? Metadata { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    [SugarColumn(IsNullable = false, ColumnName = "created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 更新时间
    /// </summary>
    [SugarColumn(IsNullable = false, ColumnName = "updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 导航属性：报表类型
    /// </summary>
    [SugarColumn(IsIgnore = true)]
    public ReportType? ReportType { get; set; }

    /// <summary>
    /// 导航属性：SFTP配置
    /// </summary>
    [SugarColumn(IsIgnore = true)]
    public SftpConfig? SftpConfig { get; set; }
}
