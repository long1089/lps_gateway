namespace LpsGateway.Lib60870;

/// <summary>
/// IEC-102 子站接口
/// </summary>
public interface IIec102Slave
{
    /// <summary>
    /// 获取所有活动会话的端点列表
    /// </summary>
    IEnumerable<string> GetActiveSessionEndpoints();

    /// <summary>
    /// 将1级数据排队到指定会话
    /// </summary>
    void QueueClass1DataToSession(string sessionEndpoint, byte typeId, byte cot, byte[] data);

    /// <summary>
    /// 将2级数据排队到指定会话
    /// </summary>
    void QueueClass2DataToSession(string sessionEndpoint, byte typeId, byte cot, byte[] data);

    /// <summary>
    /// 将1级数据广播到所有会话
    /// </summary>
    void QueueClass1DataToAll(byte typeId, byte cot, byte[] data);

    /// <summary>
    /// 将2级数据广播到所有会话
    /// </summary>
    void QueueClass2DataToAll(byte typeId, byte cot, byte[] data);

    /// <summary>
    /// 启动子站服务
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 停止子站服务
    /// </summary>
    Task StopAsync();
}
