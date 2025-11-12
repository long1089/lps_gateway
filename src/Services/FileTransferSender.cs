using System.Text;
using LpsGateway.Data.Models;
using LpsGateway.Lib60870;
using Microsoft.Extensions.Logging;
using SqlSugar;

namespace LpsGateway.Services;

/// <summary>
/// 文件传输发送管理器实现
/// </summary>
/// <remarks>
/// 实现基于会话的文件传输，支持多客户端并发，正确的COT码使用，和TI=144错误控制
/// </remarks>
public class FileTransferSender : IFileTransferSender
{
    private readonly ISqlSugarClient _db;
    private readonly IIec102Slave _slave;
    private readonly ILogger<FileTransferSender> _logger;
    private const int MaxFileSize = 20480; // 512 * 40
    private const int MaxSegmentSize = 512;
    private const int FileNameFieldSize = 64;

    public FileTransferSender(
        ISqlSugarClient db,
        IIec102Slave slave,
        ILogger<FileTransferSender> logger)
    {
        _db = db;
        _slave = slave;
        _logger = logger;
    }

    public async Task<int> PrepareFileTransferAsync(
        string sessionEndpoint,
        int fileRecordId,
        CancellationToken cancellationToken = default)
    {
        // 加载文件记录
        var fileRecord = await _db.Queryable<FileRecord>()
            .Includes(x => x.ReportType)
            .Where(x => x.Id == fileRecordId)
            .FirstAsync(cancellationToken);

        if (fileRecord == null)
        {
            throw new ArgumentException($"文件记录不存在: {fileRecordId}", nameof(fileRecordId));
        }

        // 创建传输任务
        var task = new FileTransferTask
        {
            FileRecordId = fileRecordId,
            SessionId = sessionEndpoint,
            Status = "pending",
            CreatedAt = DateTime.UtcNow
        };

        await _db.Insertable(task).ExecuteCommandAsync(cancellationToken);

        _logger.LogInformation(
            "已为会话 {SessionEndpoint} 创建文件传输任务 #{TaskId} (文件: {FileName})",
            sessionEndpoint, task.Id, fileRecord.OriginalFilename);

        return task.Id;
    }

    public async Task<List<int>> PrepareFileTransferForAllSessionsAsync(
        int fileRecordId,
        CancellationToken cancellationToken = default)
    {
        var sessions = _slave.GetActiveSessionEndpoints().ToList();
        var taskIds = new List<int>();

        foreach (var session in sessions)
        {
            var taskId = await PrepareFileTransferAsync(session, fileRecordId, cancellationToken);
            taskIds.Add(taskId);
        }

        _logger.LogInformation(
            "已为 {SessionCount} 个会话创建文件传输任务 (文件记录ID: {FileRecordId})",
            sessions.Count, fileRecordId);

        return taskIds;
    }

    public async Task StartFileTransferAsync(int taskId, CancellationToken cancellationToken = default)
    {
        // 加载任务和相关数据
        var task = await _db.Queryable<FileTransferTask>()
            .Where(x => x.Id == taskId)
            .FirstAsync(cancellationToken);

        if (task == null)
        {
            throw new ArgumentException($"传输任务不存在: {taskId}", nameof(taskId));
        }

        if (task.Status != "pending")
        {
            _logger.LogWarning("任务 #{TaskId} 状态不是 pending，当前状态: {Status}", taskId, task.Status);
            return;
        }

        var fileRecord = await _db.Queryable<FileRecord>()
            .Includes(x => x.ReportType)
            .Where(x => x.Id == task.FileRecordId)
            .FirstAsync(cancellationToken);

        if (fileRecord == null)
        {
            await UpdateTaskStatusAsync(taskId, "failed", "文件记录不存在", cancellationToken);
            return;
        }

        // 更新任务状态为 in_progress
        task.Status = "in_progress";
        task.StartedAt = DateTime.UtcNow;
        await _db.Updateable(task).ExecuteCommandAsync(cancellationToken);

        // 异步执行传输（非阻塞）
        _ = Task.Run(async () =>
        {
            try
            {
                await TransferFileAsync(task, fileRecord, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "文件传输失败: 任务 #{TaskId}", taskId);
                await UpdateTaskStatusAsync(taskId, "failed", ex.Message, CancellationToken.None);
            }
        }, cancellationToken);

        _logger.LogInformation("已启动文件传输任务 #{TaskId}", taskId);
    }

    public async Task CancelFileTransferAsync(int taskId, CancellationToken cancellationToken = default)
    {
        await UpdateTaskStatusAsync(taskId, "cancelled", "用户取消", cancellationToken);
        _logger.LogInformation("已取消文件传输任务 #{TaskId}", taskId);
    }

    public async Task<FileTransferTaskStatus?> GetTaskStatusAsync(
        int taskId,
        CancellationToken cancellationToken = default)
    {
        var task = await _db.Queryable<FileTransferTask>()
            .Where(x => x.Id == taskId)
            .FirstAsync(cancellationToken);

        if (task == null)
            return null;

        return new FileTransferTaskStatus
        {
            TaskId = task.Id,
            FileRecordId = task.FileRecordId,
            SessionEndpoint = task.SessionId,
            Status = task.Status,
            Progress = task.Progress,
            TotalSegments = task.TotalSegments ?? 0,
            SentSegments = task.SentSegments,
            ErrorMessage = task.ErrorMessage,
            StartedAt = task.StartedAt,
            CompletedAt = task.CompletedAt
        };
    }

    public async Task HandleFileTransferErrorAsync(
        string sessionEndpoint,
        byte errorCot,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "收到主站文件传输错误通知: 会话={SessionEndpoint}, COT=0x{ErrorCot:X2}",
            sessionEndpoint, errorCot);

        // 查找该会话的进行中任务
        var task = await _db.Queryable<FileTransferTask>()
            .Where(x => x.SessionId == sessionEndpoint && x.Status == "in_progress")
            .OrderByDescending(x => x.StartedAt)
            .FirstAsync(cancellationToken);

        if (task == null)
        {
            _logger.LogWarning("未找到会话 {SessionEndpoint} 的进行中任务", sessionEndpoint);
            return;
        }

        // 根据错误类型处理
        string errorMessage = errorCot switch
        {
            CauseOfTransmission.FileTooLongError => "文件过长错误",
            CauseOfTransmission.InvalidFileNameFormat => "文件名格式无效",
            CauseOfTransmission.FrameTooLongError => "单帧数据过长",
            _ => $"未知错误 (COT=0x{errorCot:X2})"
        };

        await UpdateTaskStatusAsync(task.Id, "failed", errorMessage, cancellationToken);

        // 发送对应的错误确认给主站
        byte errorAckCot = errorCot switch
        {
            CauseOfTransmission.FileTooLongError => CauseOfTransmission.FileTooLongAck, // 0x10
            CauseOfTransmission.InvalidFileNameFormat => CauseOfTransmission.InvalidFileNameFormatAck, // 0x12
            CauseOfTransmission.FrameTooLongError => CauseOfTransmission.FrameTooLongAck, // 0x14
            _ => CauseOfTransmission.FileTooLongAck // 默认
        };
        
        bool hasMoreData = task.SentSegments < (task.TotalSegments ?? 0);
        byte[] ackData = BitConverter.GetBytes(task.FileRecordId);
        
        _slave.QueueClass1DataToSession(
            sessionEndpoint,
            0x90, // TI=144 (0x90) for file transfer control
            errorAckCot,
            ackData);

        _logger.LogInformation(
            "已发送文件传输错误确认: 任务 #{TaskId}, COT=0x{Cot:X2}, ACD={HasMoreData}",
            task.Id, errorAckCot, hasMoreData);
    }

    // 私有方法：核心传输逻辑
    private async Task TransferFileAsync(
        FileTransferTask task,
        FileRecord fileRecord,
        CancellationToken cancellationToken)
    {
        try
        {
            // 1. 验证文件
            if (!File.Exists(fileRecord.StoragePath))
            {
                throw new FileNotFoundException($"文件不存在: {fileRecord.StoragePath}");
            }

            if (fileRecord.FileSize > MaxFileSize)
            {
                throw new InvalidOperationException(
                    $"文件过大: {fileRecord.FileSize} 字节 (最大 {MaxFileSize} 字节)");
            }

            // 2. 获取TypeId
            var typeId = DataClassification.GetTypeIdByReportType(fileRecord.ReportType?.Code ?? "");
            if (!typeId.HasValue)
            {
                throw new InvalidOperationException(
                    $"未知的报告类型: {fileRecord.ReportType?.Code}");
            }

            // 3. 读取并分段文件
            var fileContent = await File.ReadAllBytesAsync(fileRecord.StoragePath, cancellationToken);
            var segments = CreateSegments(fileRecord.OriginalFilename, fileContent);

            // 更新总段数
            task.TotalSegments = segments.Count;
            await _db.Updateable(task).ExecuteCommandAsync(cancellationToken);

            _logger.LogInformation(
                "开始传输文件: {FileName}, 大小={FileSize}字节, 段数={SegmentCount}",
                fileRecord.OriginalFilename, fileRecord.FileSize, segments.Count);

            // 4. 判断是1级还是2级数据
            bool isClass1 = DataClassification.IsClass1Data(typeId.Value);

            // 5. 逐段发送
            for (int i = 0; i < segments.Count; i++)
            {
                var segment = segments[i];
                bool isLastSegment = (i == segments.Count - 1);

                // 确定COT
                byte cot = isLastSegment
                    ? CauseOfTransmission.FileTransferComplete  // 0x07
                    : CauseOfTransmission.FileTransferInProgress; // 0x09

                // 发送到指定会话的队列
                if (isClass1)
                {
                    _slave.QueueClass1DataToSession(
                        task.SessionId!,
                        typeId.Value,
                        cot,
                        segment);
                }
                else
                {
                    _slave.QueueClass2DataToSession(
                        task.SessionId!,
                        typeId.Value,
                        cot,
                        segment);
                }

                // 更新进度
                task.SentSegments = i + 1;
                task.Progress = (i + 1) * 100 / segments.Count;
                await _db.Updateable(task).ExecuteCommandAsync(cancellationToken);

                _logger.LogDebug(
                    "已发送段 {SegmentIndex}/{TotalSegments} (进度: {Progress}%)",
                    i + 1, segments.Count, task.Progress);

                // 流控：避免发送过快
                await Task.Delay(10, cancellationToken);
            }

            // 6. 发送对账帧（TI=0x90, COT=0x0A）
            byte[] reconciliationData = new byte[4];
            BitConverter.GetBytes((int)fileRecord.FileSize).CopyTo(reconciliationData, 0);

            if (isClass1)
            {
                _slave.QueueClass1DataToSession(
                    task.SessionId!,
                    0x90, // Reconciliation TypeId
                    CauseOfTransmission.ReconciliationFromMaster,
                    reconciliationData);
            }
            else
            {
                _slave.QueueClass2DataToSession(
                    task.SessionId!,
                    0x90,
                    CauseOfTransmission.ReconciliationFromMaster,
                    reconciliationData);
            }

            // 7. 标记完成
            task.Status = "completed";
            task.CompletedAt = DateTime.UtcNow;
            await _db.Updateable(task).ExecuteCommandAsync(cancellationToken);

            _logger.LogInformation(
                "文件传输完成: 任务 #{TaskId}, 文件={FileName}, 段数={SegmentCount}",
                task.Id, fileRecord.OriginalFilename, segments.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "文件传输异常: 任务 #{TaskId}", task.Id);
            throw;
        }
    }

    // 创建文件分段
    private List<byte[]> CreateSegments(string filename, byte[] fileContent)
    {
        var segments = new List<byte[]>();

        // 编码文件名为GBK
        Encoding gbk = Encoding.GetEncoding("GBK");
        byte[] filenameBytes = new byte[FileNameFieldSize];
        byte[] encodedName = gbk.GetBytes(filename);

        if (encodedName.Length > FileNameFieldSize)
        {
            throw new ArgumentException(
                $"文件名GBK编码后超过{FileNameFieldSize}字节: {encodedName.Length}字节");
        }

        Array.Copy(encodedName, filenameBytes, Math.Min(encodedName.Length, FileNameFieldSize));

        // 分段文件内容
        int offset = 0;
        while (offset < fileContent.Length)
        {
            int segmentDataSize = Math.Min(MaxSegmentSize, fileContent.Length - offset);
            byte[] segment = new byte[FileNameFieldSize + segmentDataSize];

            // 复制文件名字段
            Array.Copy(filenameBytes, 0, segment, 0, FileNameFieldSize);

            // 复制数据字段
            Array.Copy(fileContent, offset, segment, FileNameFieldSize, segmentDataSize);

            segments.Add(segment);
            offset += segmentDataSize;
        }

        return segments;
    }

    // 更新任务状态
    private async Task UpdateTaskStatusAsync(
        int taskId,
        string status,
        string? errorMessage = null,
        CancellationToken cancellationToken = default)
    {
        var task = await _db.Queryable<FileTransferTask>()
            .Where(x => x.Id == taskId)
            .FirstAsync(cancellationToken);

        if (task == null)
            return;

        task.Status = status;
        if (errorMessage != null)
            task.ErrorMessage = errorMessage;

        if (status == "completed")
            task.CompletedAt = DateTime.UtcNow;

        await _db.Updateable(task).ExecuteCommandAsync(cancellationToken);
    }
}
