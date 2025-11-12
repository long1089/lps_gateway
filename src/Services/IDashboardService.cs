using LpsGateway.Models;

namespace LpsGateway.Services;

/// <summary>
/// 仪表盘服务接口
/// </summary>
public interface IDashboardService
{
    /// <summary>
    /// 获取仪表盘数据
    /// </summary>
    Task<DashboardViewModel> GetDashboardDataAsync();

    /// <summary>
    /// 获取系统状态
    /// </summary>
    Task<SystemStatusModel> GetSystemStatusAsync();

    /// <summary>
    /// 获取最新文件下载记录
    /// </summary>
    Task<List<FileDownloadRecordModel>> GetRecentDownloadsAsync(int count = 10);

    /// <summary>
    /// 获取最新文件传输记录
    /// </summary>
    Task<List<FileTransferRecordModel>> GetRecentTransfersAsync(int count = 10);

    /// <summary>
    /// 获取错误告警
    /// </summary>
    Task<List<ErrorAlertModel>> GetErrorAlertsAsync();

    /// <summary>
    /// 获取通讯状态
    /// </summary>
    Task<CommunicationStatusModel> GetCommunicationStatusAsync();

    /// <summary>
    /// 获取磁盘使用情况
    /// </summary>
    Task<DiskUsageModel> GetDiskUsageAsync();
}
