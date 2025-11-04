namespace LpsGateway.Lib60870;

/// <summary>
/// 链路层接口，定义通信链路的基本操作
/// </summary>
public interface ILinkLayer
{
    /// <summary>
    /// 启动链路层
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步任务</returns>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 停止链路层
    /// </summary>
    /// <returns>异步任务</returns>
    Task StopAsync();

    /// <summary>
    /// 发送数据
    /// </summary>
    /// <param name="data">要发送的字节数据</param>
    /// <returns>异步任务</returns>
    Task SendAsync(byte[] data);

    /// <summary>
    /// 数据接收事件
    /// </summary>
    event EventHandler<byte[]>? DataReceived;
}
