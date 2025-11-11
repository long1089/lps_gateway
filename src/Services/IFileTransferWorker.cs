using LpsGateway.Data.Models;

namespace LpsGateway.Services;

/// <summary>
/// 文件传输工作器接口
/// </summary>
/// <remarks>
/// 负责将文件分段并通过IEC-102协议传输
/// </remarks>
public interface IFileTransferWorker
{
    /// <summary>
    /// 传输文件
    /// </summary>
    /// <param name="task">文件传输任务</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功</returns>
    Task<bool> TransferFileAsync(FileTransferTask task, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 取消文件传输
    /// </summary>
    /// <param name="taskId">任务ID</param>
    void CancelTransfer(int taskId);
    
    /// <summary>
    /// 获取正在传输的任务数量
    /// </summary>
    int GetActiveTransferCount();
}
