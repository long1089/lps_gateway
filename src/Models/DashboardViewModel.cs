namespace LpsGateway.Models;

/// <summary>
/// 仪表盘视图模型
/// </summary>
public class DashboardViewModel
{
    /// <summary>
    /// 系统状态
    /// </summary>
    public SystemStatusModel SystemStatus { get; set; } = new();

    /// <summary>
    /// 最新文件下载记录
    /// </summary>
    public List<FileDownloadRecordModel> RecentDownloads { get; set; } = new();

    /// <summary>
    /// 最新文件传输记录
    /// </summary>
    public List<FileTransferRecordModel> RecentTransfers { get; set; } = new();

    /// <summary>
    /// 错误告警
    /// </summary>
    public List<ErrorAlertModel> ErrorAlerts { get; set; } = new();

    /// <summary>
    /// 通讯状态
    /// </summary>
    public CommunicationStatusModel CommunicationStatus { get; set; } = new();

    /// <summary>
    /// 磁盘使用情况
    /// </summary>
    public DiskUsageModel DiskUsage { get; set; } = new();
}

/// <summary>
/// 系统状态模型
/// </summary>
public class SystemStatusModel
{
    /// <summary>
    /// 总下载文件数
    /// </summary>
    public int TotalDownloadedFiles { get; set; }

    /// <summary>
    /// 今日下载文件数
    /// </summary>
    public int TodayDownloadedFiles { get; set; }

    /// <summary>
    /// 总传输任务数
    /// </summary>
    public int TotalTransferTasks { get; set; }

    /// <summary>
    /// 待处理任务数
    /// </summary>
    public int PendingTasks { get; set; }

    /// <summary>
    /// 进行中任务数
    /// </summary>
    public int InProgressTasks { get; set; }

    /// <summary>
    /// 失败任务数
    /// </summary>
    public int FailedTasks { get; set; }

    /// <summary>
    /// 系统运行时间（秒）
    /// </summary>
    public long UptimeSeconds { get; set; }
}

/// <summary>
/// 文件下载记录模型
/// </summary>
public class FileDownloadRecordModel
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ReportTypeName { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime DownloadTime { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// 文件传输记录模型
/// </summary>
public class FileTransferRecordModel
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int Progress { get; set; }
    public int? TotalSegments { get; set; }
    public int SentSegments { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// 错误告警模型
/// </summary>
public class ErrorAlertModel
{
    public string Type { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string Severity { get; set; } = "warning"; // warning, error, critical
}

/// <summary>
/// 通讯状态模型
/// </summary>
public class CommunicationStatusModel
{
    /// <summary>
    /// IEC-102 主站状态
    /// </summary>
    public bool MasterIsRunning { get; set; }

    /// <summary>
    /// 活跃连接数
    /// </summary>
    public int ActiveConnections { get; set; }

    /// <summary>
    /// 今日发送帧数
    /// </summary>
    public int TodaySentFrames { get; set; }

    /// <summary>
    /// 最后活动时间
    /// </summary>
    public DateTime? LastActivityTime { get; set; }
}

/// <summary>
/// 磁盘使用情况模型
/// </summary>
public class DiskUsageModel
{
    /// <summary>
    /// 总空间（字节）
    /// </summary>
    public long TotalSpaceBytes { get; set; }

    /// <summary>
    /// 已使用空间（字节）
    /// </summary>
    public long UsedSpaceBytes { get; set; }

    /// <summary>
    /// 可用空间（字节）
    /// </summary>
    public long FreeSpaceBytes { get; set; }

    /// <summary>
    /// 使用率百分比
    /// </summary>
    public double UsagePercent { get; set; }

    /// <summary>
    /// 是否超过警告阈值（80%）
    /// </summary>
    public bool IsWarning => UsagePercent >= 80.0;

    /// <summary>
    /// 是否超过严重阈值（90%）
    /// </summary>
    public bool IsCritical => UsagePercent >= 90.0;
}
