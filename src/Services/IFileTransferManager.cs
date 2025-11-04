namespace LpsGateway.Services;

/// <summary>
/// 文件传输管理器接口
/// </summary>
public interface IFileTransferManager
{
    /// <summary>
    /// 处理接收到的 ASDU 数据
    /// </summary>
    /// <param name="asduData">ASDU 字节数组</param>
    /// <returns>异步任务</returns>
    Task ProcessAsduAsync(byte[] asduData);
}
