namespace LpsGateway.Services;

/// <summary>
/// 文件传输发送管理器接口
/// </summary>
/// <remarks>
/// 基于会话的文件传输管理，支持多客户端并发传输
/// </remarks>
public interface IFileTransferSender
{
    /// <summary>
    /// 为指定会话准备文件传输
    /// </summary>
    /// <param name="sessionEndpoint">会话端点（客户端标识）</param>
    /// <param name="fileRecordId">文件记录ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>传输任务ID</returns>
    Task<int> PrepareFileTransferAsync(string sessionEndpoint, int fileRecordId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 为所有活动会话准备文件传输（广播）
    /// </summary>
    /// <param name="fileRecordId">文件记录ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>创建的传输任务ID列表</returns>
    Task<List<int>> PrepareFileTransferForAllSessionsAsync(int fileRecordId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 开始文件传输（异步，非阻塞）
    /// </summary>
    /// <param name="taskId">传输任务ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task StartFileTransferAsync(int taskId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取消文件传输
    /// </summary>
    /// <param name="taskId">传输任务ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task CancelFileTransferAsync(int taskId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取传输任务状态
    /// </summary>
    /// <param name="taskId">传输任务ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务状态信息</returns>
    Task<FileTransferTaskStatus?> GetTaskStatusAsync(int taskId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 处理主站的文件传输错误通知（TI=144）
    /// </summary>
    /// <param name="sessionEndpoint">会话端点</param>
    /// <param name="errorCot">错误COT码</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task HandleFileTransferErrorAsync(string sessionEndpoint, byte errorCot, CancellationToken cancellationToken = default);
}

/// <summary>
/// 文件传输任务状态
/// </summary>
public class FileTransferTaskStatus
{
    public int TaskId { get; set; }
    public int FileRecordId { get; set; }
    public string? SessionEndpoint { get; set; }
    public string Status { get; set; } = "pending";
    public int Progress { get; set; }
    public int TotalSegments { get; set; }
    public int SentSegments { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
