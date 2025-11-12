namespace LpsGateway.Services;

/// <summary>
/// 文件传输初始化服务接口
/// </summary>
public interface IFileTransferInitializer
{
    /// <summary>
    /// 为指定会话初始化文件传输任务
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <param name="endpoint">客户端端点</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>初始化的任务数量</returns>
    Task<int> InitializeTransfersForSessionAsync(string sessionId, string endpoint, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 判断报表类型代码是否为1级数据
    /// </summary>
    /// <param name="reportTypeCode">报表类型编码</param>
    /// <returns>如果是1级数据返回true，否则返回false</returns>
    bool IsClass1Data(string reportTypeCode);
    
    /// <summary>
    /// 根据TypeId判断是否为1级数据
    /// </summary>
    /// <param name="typeId">类型ID</param>
    /// <returns>如果是1级数据返回true，否则返回false</returns>
    bool IsClass1DataByTypeId(byte typeId);
}
