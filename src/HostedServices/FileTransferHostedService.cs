using LpsGateway.Data;
using LpsGateway.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SqlSugar;

namespace LpsGateway.HostedServices;

/// <summary>
/// 后台服务：定期扫描并处理待传输的文件任务
/// Background service that periodically scans and processes pending file transfer tasks
/// </summary>
public class FileTransferHostedService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<FileTransferHostedService> _logger;
    private readonly TimeSpan _scanInterval = TimeSpan.FromSeconds(5);
    private readonly int _maxConcurrentTransfers = 3;

    public FileTransferHostedService(
        IServiceProvider serviceProvider,
        ILogger<FileTransferHostedService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("FileTransferHostedService 已启动");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingTasksAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理待传输任务时发生错误");
            }

            await Task.Delay(_scanInterval, stoppingToken);
        }

        _logger.LogInformation("FileTransferHostedService 已停止");
    }

    private async Task ProcessPendingTasksAsync(CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<IFileTransferSender>();
        var db = scope.ServiceProvider.GetRequiredService<ISqlSugarClient>();

        // 查询所有状态为 pending 的任务
        var pendingTasks = await db.Queryable<Data.Models.FileTransferTask>()
            .Where(t => t.Status == "pending")
            .OrderBy(t => t.CreatedAt)
            .ToListAsync(stoppingToken);

        if (pendingTasks.Count == 0)
        {
            return;
        }

        // 查询当前正在进行的任务数量
        var inProgressCount = await db.Queryable<Data.Models.FileTransferTask>()
            .Where(t => t.Status == "in_progress")
            .CountAsync(stoppingToken);

        // 计算可以启动的任务数量
        var availableSlots = _maxConcurrentTransfers - inProgressCount;
        if (availableSlots <= 0)
        {
            _logger.LogDebug("当前有 {Count} 个任务正在传输，已达到并发上限 {Max}",
                inProgressCount, _maxConcurrentTransfers);
            return;
        }

        // 启动可用数量的任务
        var tasksToStart = pendingTasks.Take(availableSlots).ToList();
        _logger.LogInformation("发现 {Total} 个待传输任务，将启动 {Count} 个任务",
            pendingTasks.Count, tasksToStart.Count);

        foreach (var task in tasksToStart)
        {
            try
            {
                _logger.LogInformation("启动文件传输任务 ID={TaskId}, FileRecordId={FileRecordId}",
                    task.Id, task.FileRecordId);

                // 异步启动传输（非阻塞）
                _ = sender.StartFileTransferAsync(task.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "启动文件传输任务 ID={TaskId} 时发生错误", task.Id);
            }
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("FileTransferHostedService 正在停止...");
        return base.StopAsync(cancellationToken);
    }
}
