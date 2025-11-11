using LpsGateway.Services;

namespace LpsGateway.HostedServices;

/// <summary>
/// 调度管理器后台服务
/// </summary>
public class ScheduleManagerHostedService : IHostedService
{
    private readonly IScheduleManager _scheduleManager;
    private readonly ILogger<ScheduleManagerHostedService> _logger;

    public ScheduleManagerHostedService(
        IScheduleManager scheduleManager,
        ILogger<ScheduleManagerHostedService> logger)
    {
        _scheduleManager = scheduleManager;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("启动调度管理器后台服务");
        
        try
        {
            await _scheduleManager.InitializeAsync();
            await _scheduleManager.StartAsync();
            
            _logger.LogInformation("调度管理器后台服务已启动");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "启动调度管理器失败");
            throw;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("停止调度管理器后台服务");
        
        try
        {
            await _scheduleManager.StopAsync();
            _logger.LogInformation("调度管理器后台服务已停止");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "停止调度管理器失败");
        }
    }
}
