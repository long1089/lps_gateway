using LpsGateway.Data.Models;
using LpsGateway.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SqlSugar;

namespace LpsGateway.HostedServices;

/// <summary>
/// 文件传输后台服务
/// </summary>
/// <remarks>
/// 定期检查待传输的文件任务并执行传输
/// </remarks>
public class FileTransferHostedService : BackgroundService
{
    private readonly ILogger<FileTransferHostedService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly TimeSpan _checkInterval = TimeSpan.FromSeconds(5);
    private readonly int _maxConcurrentTransfers = 3;
    
    public FileTransferHostedService(
        ILogger<FileTransferHostedService> logger,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("文件传输后台服务已启动");
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingTasksAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理文件传输任务时发生错误");
            }
            
            await Task.Delay(_checkInterval, stoppingToken);
        }
        
        _logger.LogInformation("文件传输后台服务已停止");
    }
    
    /// <summary>
    /// 处理待传输的任务
    /// </summary>
    private async Task ProcessPendingTasksAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ISqlSugarClient>();
        var worker = scope.ServiceProvider.GetRequiredService<IFileTransferWorker>();
        
        // 检查当前正在传输的任务数量
        int activeCount = worker.GetActiveTransferCount();
        if (activeCount >= _maxConcurrentTransfers)
        {
            _logger.LogDebug("达到最大并发传输数: {ActiveCount}/{MaxCount}", 
                activeCount, _maxConcurrentTransfers);
            return;
        }
        
        // 获取待传输的任务
        var availableSlots = _maxConcurrentTransfers - activeCount;
        var pendingTasks = await db.Queryable<FileTransferTask>()
            .Where(t => t.Status == "pending")
            .OrderBy(t => t.CreatedAt)
            .Take(availableSlots)
            .ToListAsync(cancellationToken);
        
        if (pendingTasks.Count == 0)
        {
            return;
        }
        
        _logger.LogInformation("找到 {Count} 个待传输任务", pendingTasks.Count);
        
        // 启动传输任务
        foreach (var task in pendingTasks)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await worker.TransferFileAsync(task, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "传输任务失败: TaskId={TaskId}", task.Id);
                }
            }, cancellationToken);
        }
    }
}
