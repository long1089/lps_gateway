using LpsGateway.Data;
using LpsGateway.Data.Models;
using LpsGateway.Models;
using SqlSugar;

namespace LpsGateway.Services;

/// <summary>
/// 仪表盘服务实现
/// </summary>
public class DashboardService : IDashboardService
{
    private readonly ISqlSugarClient _db;
    private readonly IFileRecordRepository _fileRecordRepository;
    private readonly ILogger<DashboardService> _logger;
    private readonly DateTime _startTime;

    public DashboardService(
        ISqlSugarClient db,
        IFileRecordRepository fileRecordRepository,
        ILogger<DashboardService> logger)
    {
        _db = db;
        _fileRecordRepository = fileRecordRepository;
        _logger = logger;
        _startTime = DateTime.UtcNow;
    }

    /// <inheritdoc/>
    public async Task<DashboardViewModel> GetDashboardDataAsync()
    {
        try
        {
            var viewModel = new DashboardViewModel
            {
                SystemStatus = await GetSystemStatusAsync(),
                RecentDownloads = await GetRecentDownloadsAsync(10),
                RecentTransfers = await GetRecentTransfersAsync(10),
                ErrorAlerts = await GetErrorAlertsAsync(),
                CommunicationStatus = await GetCommunicationStatusAsync(),
                DiskUsage = await GetDiskUsageAsync()
            };

            return viewModel;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取仪表盘数据失败");
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<SystemStatusModel> GetSystemStatusAsync()
    {
        try
        {
            var today = DateTime.UtcNow.Date;
            var tomorrow = today.AddDays(1);

            var totalFiles = await _db.Queryable<FileRecord>().CountAsync();
            var todayFiles = await _db.Queryable<FileRecord>()
                .Where(f => f.DownloadTime >= today && f.DownloadTime < tomorrow)
                .CountAsync();

            var totalTasks = await _db.Queryable<FileTransferTask>().CountAsync();
            var pendingTasks = await _db.Queryable<FileTransferTask>()
                .Where(t => t.Status == "pending")
                .CountAsync();
            var inProgressTasks = await _db.Queryable<FileTransferTask>()
                .Where(t => t.Status == "in_progress")
                .CountAsync();
            var failedTasks = await _db.Queryable<FileTransferTask>()
                .Where(t => t.Status == "failed")
                .CountAsync();

            return new SystemStatusModel
            {
                TotalDownloadedFiles = totalFiles,
                TodayDownloadedFiles = todayFiles,
                TotalTransferTasks = totalTasks,
                PendingTasks = pendingTasks,
                InProgressTasks = inProgressTasks,
                FailedTasks = failedTasks,
                UptimeSeconds = (long)(DateTime.UtcNow - _startTime).TotalSeconds
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取系统状态失败");
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<List<FileDownloadRecordModel>> GetRecentDownloadsAsync(int count = 10)
    {
        try
        {
            var records = await _db.Queryable<FileRecord, ReportType>(
                    (f, r) => f.ReportTypeId == r.Id)
                .OrderByDescending((f, r) => f.DownloadTime)
                .Take(count)
                .Select((f, r) => new FileDownloadRecordModel
                {
                    Id = f.Id,
                    FileName = f.OriginalFilename,
                    ReportTypeName = r.Name,
                    FileSizeBytes = f.FileSize,
                    Status = f.Status,
                    DownloadTime = f.DownloadTime,
                    ErrorMessage = f.ErrorMessage
                })
                .ToListAsync();

            return records;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取最新文件下载记录失败");
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<List<FileTransferRecordModel>> GetRecentTransfersAsync(int count = 10)
    {
        try
        {
            var records = await _db.Queryable<FileTransferTask, FileRecord>(
                    (t, f) => t.FileRecordId == f.Id)
                .OrderByDescending((t, f) => t.CreatedAt)
                .Take(count)
                .Select((t, f) => new FileTransferRecordModel
                {
                    Id = t.Id,
                    FileName = f.OriginalFilename,
                    Status = t.Status,
                    Progress = t.Progress,
                    TotalSegments = t.TotalSegments,
                    SentSegments = t.SentSegments,
                    CreatedAt = t.CreatedAt,
                    CompletedAt = t.CompletedAt,
                    ErrorMessage = t.ErrorMessage
                })
                .ToListAsync();

            return records;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取最新文件传输记录失败");
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<List<ErrorAlertModel>> GetErrorAlertsAsync()
    {
        var alerts = new List<ErrorAlertModel>();

        try
        {
            // 检查磁盘使用情况
            var diskUsage = await GetDiskUsageAsync();
            if (diskUsage.IsCritical)
            {
                alerts.Add(new ErrorAlertModel
                {
                    Type = "磁盘空间",
                    Message = $"磁盘使用率已达到 {diskUsage.UsagePercent:F1}%，请及时清理",
                    Timestamp = DateTime.UtcNow,
                    Severity = "critical"
                });
            }
            else if (diskUsage.IsWarning)
            {
                alerts.Add(new ErrorAlertModel
                {
                    Type = "磁盘空间",
                    Message = $"磁盘使用率已达到 {diskUsage.UsagePercent:F1}%",
                    Timestamp = DateTime.UtcNow,
                    Severity = "warning"
                });
            }

            // 检查失败的文件下载
            var failedDownloads = await _db.Queryable<FileRecord>()
                .Where(f => f.Status == "error")
                .OrderByDescending(f => f.DownloadTime)
                .Take(5)
                .ToListAsync();

            foreach (var file in failedDownloads)
            {
                alerts.Add(new ErrorAlertModel
                {
                    Type = "文件下载失败",
                    Message = $"文件 {file.OriginalFilename} 下载失败: {file.ErrorMessage ?? "未知错误"}",
                    Timestamp = file.DownloadTime,
                    Severity = "error"
                });
            }

            // 检查失败的传输任务
            var failedTasks = await _db.Queryable<FileTransferTask, FileRecord>(
                    (t, f) => t.FileRecordId == f.Id)
                .Where((t, f) => t.Status == "failed")
                .OrderByDescending((t, f) => t.CreatedAt)
                .Take(5)
                .Select((t, f) => new
                {
                    FileName = f.OriginalFilename,
                    ErrorMessage = t.ErrorMessage,
                    CreatedAt = t.CreatedAt
                })
                .ToListAsync();

            foreach (var task in failedTasks)
            {
                alerts.Add(new ErrorAlertModel
                {
                    Type = "文件传输失败",
                    Message = $"文件 {task.FileName} 传输失败: {task.ErrorMessage ?? "未知错误"}",
                    Timestamp = task.CreatedAt,
                    Severity = "error"
                });
            }

            return alerts.OrderByDescending(a => a.Timestamp).Take(10).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取错误告警失败");
            return alerts;
        }
    }

    /// <inheritdoc/>
    public async Task<CommunicationStatusModel> GetCommunicationStatusAsync()
    {
        try
        {
            var today = DateTime.UtcNow.Date;
            var tomorrow = today.AddDays(1);

            // 注意：这里使用简单的方法，实际应该从 Iec102Slave/Master 服务获取实时状态
            // 目前只统计任务数量作为活动指标
            var todayTasks = await _db.Queryable<FileTransferTask>()
                .Where(t => t.CreatedAt >= today && t.CreatedAt < tomorrow)
                .CountAsync();

            var lastActivity = await _db.Queryable<FileTransferTask>()
                .OrderByDescending(t => t.CreatedAt)
                .Select(t => t.CreatedAt)
                .FirstAsync();

            return new CommunicationStatusModel
            {
                SlaveIsRunning = true, // 需要从实际服务获取
                MasterIsRunning = true, // 需要从实际服务获取
                ActiveConnections = 0, // 需要从实际服务获取
                TodayReceivedFrames = 0, // 需要从实际服务获取
                TodaySentFrames = todayTasks, // 近似值
                LastActivityTime = lastActivity
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取通讯状态失败");
            return new CommunicationStatusModel();
        }
    }

    /// <inheritdoc/>
    public async Task<DiskUsageModel> GetDiskUsageAsync()
    {
        try
        {
            // 获取文件存储根目录
            var storageRoot = Path.GetTempPath(); // 实际应该从配置获取

            var driveInfo = new DriveInfo(Path.GetPathRoot(storageRoot) ?? "/");

            var totalSpace = driveInfo.TotalSize;
            var freeSpace = driveInfo.AvailableFreeSpace;
            var usedSpace = totalSpace - freeSpace;
            var usagePercent = totalSpace > 0 ? (double)usedSpace / totalSpace * 100 : 0;

            return await Task.FromResult(new DiskUsageModel
            {
                TotalSpaceBytes = totalSpace,
                UsedSpaceBytes = usedSpace,
                FreeSpaceBytes = freeSpace,
                UsagePercent = usagePercent
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取磁盘使用情况失败");
            return new DiskUsageModel();
        }
    }
}
