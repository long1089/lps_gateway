using LpsGateway.Models;
using LpsGateway.Hubs;
using Microsoft.AspNetCore.SignalR;
using SqlSugar;
using LpsGateway.Data.Models;

namespace LpsGateway.Services;

/// <summary>
/// 通讯状态广播服务实现
/// </summary>
public class CommunicationStatusBroadcaster : ICommunicationStatusBroadcaster
{
    private readonly IHubContext<CommunicationStatusHub> _hubContext;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<CommunicationStatusBroadcaster> _logger;
    
    // 内存中的连接状态
    private readonly HashSet<string> _activeConnections = new();
    private readonly object _lock = new();
    private DateTime? _lastActivityTime;
    private bool _masterIsRunning;

    public CommunicationStatusBroadcaster(
        IHubContext<CommunicationStatusHub> hubContext,
        IServiceProvider serviceProvider,
        ILogger<CommunicationStatusBroadcaster> logger)
    {
        _hubContext = hubContext;
        _serviceProvider = serviceProvider;
        _logger = logger;
        _masterIsRunning = false;
    }

    /// <summary>
    /// 记录主站连接事件
    /// </summary>
    public void RecordMasterConnection(string endpoint)
    {
        lock (_lock)
        {
            _activeConnections.Add(endpoint);
            _lastActivityTime = DateTime.UtcNow;
            _logger.LogInformation("主站已连接: {Endpoint}, 活跃连接数: {Count}", endpoint, _activeConnections.Count);
        }

        // 异步广播状态更新（不阻塞调用线程）
        _ = BroadcastCurrentStatusAsync();
    }

    /// <summary>
    /// 记录主站断开事件
    /// </summary>
    public void RecordMasterDisconnection(string endpoint)
    {
        lock (_lock)
        {
            _activeConnections.Remove(endpoint);
            _lastActivityTime = DateTime.UtcNow;
            _logger.LogInformation("主站已断开: {Endpoint}, 活跃连接数: {Count}", endpoint, _activeConnections.Count);
        }

        // 异步广播状态更新（不阻塞调用线程）
        _ = BroadcastCurrentStatusAsync();
    }

    /// <summary>
    /// 设置主站运行状态
    /// </summary>
    public void SetMasterRunningStatus(bool isRunning)
    {
        lock (_lock)
        {
            _masterIsRunning = isRunning;
        }

        // 异步广播状态更新（不阻塞调用线程）
        _ = BroadcastCurrentStatusAsync();
    }

    /// <summary>
    /// 广播通讯状态更新
    /// </summary>
    public async Task BroadcastStatusUpdateAsync(CommunicationStatusModel status)
    {
        try
        {
            await _hubContext.Clients.All.SendAsync("ReceiveStatusUpdate", status);
            _logger.LogDebug("已广播通讯状态更新");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "广播通讯状态更新失败");
        }
    }

    /// <summary>
    /// 获取当前通讯状态
    /// </summary>
    public async Task<CommunicationStatusModel> GetCurrentStatusAsync()
    {
        try
        {
            var today = DateTime.UtcNow.Date;
            var tomorrow = today.AddDays(1);

            int todayTasks = 0;
            DateTime? lastActivity = null;

            // 使用服务定位器模式创建作用域来访问数据库
            using (var scope = _serviceProvider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ISqlSugarClient>();
                
                // 统计今日发送的任务数（作为今日发送帧数的近似）
                todayTasks = await db.Queryable<FileTransferTask>()
                    .Where(t => t.CreatedAt >= today && t.CreatedAt < tomorrow)
                    .CountAsync();

                // 获取最后活动时间
                try
                {
                    lastActivity = await db.Queryable<FileTransferTask>()
                        .OrderByDescending(t => t.CreatedAt)
                        .Select(t => t.CreatedAt)
                        .FirstAsync();
                }
                catch
                {
                    // 如果没有记录，使用内存中的值
                }
            }

            int activeConnections;
            DateTime? lastActivityFromMemory;
            bool masterRunning;
            
            lock (_lock)
            {
                activeConnections = _activeConnections.Count;
                lastActivityFromMemory = _lastActivityTime;
                masterRunning = _masterIsRunning;
            }

            return new CommunicationStatusModel
            {
                MasterIsRunning = masterRunning,
                ActiveConnections = activeConnections,
                TodaySentFrames = todayTasks,
                LastActivityTime = lastActivity ?? lastActivityFromMemory
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取通讯状态失败");
            return new CommunicationStatusModel();
        }
    }

    /// <summary>
    /// 广播当前状态
    /// </summary>
    private async Task BroadcastCurrentStatusAsync()
    {
        try
        {
            var status = await GetCurrentStatusAsync();
            await BroadcastStatusUpdateAsync(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "广播当前状态失败");
        }
    }
}
