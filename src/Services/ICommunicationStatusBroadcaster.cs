using LpsGateway.Models;

namespace LpsGateway.Services;

/// <summary>
/// 通讯状态广播服务接口
/// </summary>
public interface ICommunicationStatusBroadcaster
{
    /// <summary>
    /// 广播通讯状态更新
    /// </summary>
    Task BroadcastStatusUpdateAsync(CommunicationStatusModel status);

    /// <summary>
    /// 记录主站连接事件
    /// </summary>
    void RecordMasterConnection(string endpoint);

    /// <summary>
    /// 记录主站断开事件
    /// </summary>
    void RecordMasterDisconnection(string endpoint);

    /// <summary>
    /// 设置主站运行状态
    /// </summary>
    void SetMasterRunningStatus(bool isRunning);

    /// <summary>
    /// 获取当前通讯状态
    /// </summary>
    Task<CommunicationStatusModel> GetCurrentStatusAsync();
}
