using LpsGateway.Data;
using SqlSugar;

namespace LpsGateway.HostedServices;

/// <summary>
/// 文件保留策略清理服务
/// 定期清理过期的文件记录
/// </summary>
public class RetentionWorkerHostedService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RetentionWorkerHostedService> _logger;
    private readonly TimeSpan _checkInterval;

    public RetentionWorkerHostedService(
        IServiceProvider serviceProvider,
        ILogger<RetentionWorkerHostedService> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        
        // 默认每小时检查一次
        var intervalMinutes = configuration.GetValue<int?>("Retention:CheckIntervalMinutes") ?? 60;
        _checkInterval = TimeSpan.FromMinutes(intervalMinutes);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("文件保留策略清理服务启动，检查间隔: {Interval} 分钟", _checkInterval.TotalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupExpiredFilesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "清理过期文件时发生错误");
            }

            // 等待下次检查
            await Task.Delay(_checkInterval, stoppingToken);
        }

        _logger.LogInformation("文件保留策略清理服务停止");
    }

    /// <summary>
    /// 清理过期的文件
    /// </summary>
    private async Task CleanupExpiredFilesAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ISqlSugarClient>();
        var fileRecordRepository = scope.ServiceProvider.GetRequiredService<IFileRecordRepository>();

        try
        {
            var now = DateTime.UtcNow;

            // 查找所有过期的文件记录
            var expiredFiles = await db.Queryable<Data.Models.FileRecord>()
                .Where(f => f.RetentionExpiresAt != null && f.RetentionExpiresAt < now)
                .Where(f => f.Status != "processing" && f.Status != "in_progress")
                .ToListAsync(cancellationToken);

            if (expiredFiles.Count == 0)
            {
                _logger.LogDebug("没有需要清理的过期文件");
                return;
            }

            _logger.LogInformation("发现 {Count} 个过期文件需要清理", expiredFiles.Count);

            var deletedCount = 0;
            var failedCount = 0;

            foreach (var file in expiredFiles)
            {
                try
                {
                    // 删除物理文件
                    if (File.Exists(file.StoragePath))
                    {
                        File.Delete(file.StoragePath);
                        _logger.LogDebug("删除物理文件: {Path}", file.StoragePath);
                    }

                    // 删除数据库记录
                    await db.Deleteable<Data.Models.FileRecord>()
                        .Where(f => f.Id == file.Id)
                        .ExecuteCommandAsync(cancellationToken);

                    deletedCount++;
                    _logger.LogInformation("已清理过期文件: {FileName} (ID: {Id})", file.OriginalFilename, file.Id);
                }
                catch (Exception ex)
                {
                    failedCount++;
                    _logger.LogError(ex, "清理文件失败: {FileName} (ID: {Id})", file.OriginalFilename, file.Id);
                }
            }

            _logger.LogInformation("过期文件清理完成，成功: {Success}，失败: {Failed}", deletedCount, failedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "执行过期文件清理任务失败");
        }
    }
}

/// <summary>
/// 文件保留策略配置选项
/// </summary>
public class RetentionOptions
{
    /// <summary>
    /// 检查间隔（分钟）
    /// </summary>
    public int CheckIntervalMinutes { get; set; } = 60;

    /// <summary>
    /// 默认保留天数
    /// </summary>
    public int DefaultRetentionDays { get; set; } = 30;
}
