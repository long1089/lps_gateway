using Microsoft.AspNetCore.SignalR;
using LpsGateway.Services;

namespace LpsGateway.Hubs;

/// <summary>
/// SignalR Hub for broadcasting communication status updates
/// 通讯状态更新 SignalR Hub
/// </summary>
public class CommunicationStatusHub : Hub
{
    private readonly ILogger<CommunicationStatusHub> _logger;
    private readonly ICommunicationStatusBroadcaster _statusBroadcaster;

    public CommunicationStatusHub(
        ILogger<CommunicationStatusHub> logger,
        ICommunicationStatusBroadcaster statusBroadcaster)
    {
        _logger = logger;
        _statusBroadcaster = statusBroadcaster;
    }

    /// <summary>
    /// 客户端连接事件
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("SignalR 客户端已连接: {ConnectionId}", Context.ConnectionId);
        
        // 向新连接的客户端发送当前状态
        try
        {
            var status = await _statusBroadcaster.GetCurrentStatusAsync();
            await Clients.Caller.SendAsync("ReceiveStatusUpdate", status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "发送初始状态失败");
        }
        
        await base.OnConnectedAsync();
    }

    /// <summary>
    /// 客户端断开事件
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (exception != null)
        {
            _logger.LogWarning(exception, "SignalR 客户端断开连接（异常）: {ConnectionId}", Context.ConnectionId);
        }
        else
        {
            _logger.LogInformation("SignalR 客户端已断开: {ConnectionId}", Context.ConnectionId);
        }
        await base.OnDisconnectedAsync(exception);
    }
}
