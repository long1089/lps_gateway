using System.Collections.Concurrent;
using System.Text;
using LpsGateway.Data.Models;
using LpsGateway.Lib60870;
using Microsoft.Extensions.Logging;
using SqlSugar;

namespace LpsGateway.Services;

/// <summary>
/// 文件传输工作器实现
/// </summary>
/// <remarks>
/// 负责将文件分段并通过IEC-102协议传输，支持背压控制
/// </remarks>
public class FileTransferWorker : IFileTransferWorker
{
    private readonly ILogger<FileTransferWorker> _logger;
    private readonly ISqlSugarClient _db;
    private readonly Iec102Slave? _slave;
    private readonly ConcurrentDictionary<int, CancellationTokenSource> _activeTransfers = new();
    
    /// <summary>
    /// 文件名长度（64字节）
    /// </summary>
    private const int FileNameLength = 64;
    
    /// <summary>
    /// 每个片段最大数据长度（512字节）
    /// </summary>
    private const int MaxSegmentSize = 512;
    
    /// <summary>
    /// 文件最大长度 (512 * 40 = 20480字节)
    /// </summary>
    private const int MaxFileSize = 20480;
    
    /// <summary>
    /// 单帧最大长度（512字节）
    /// </summary>
    private const int MaxFrameSize = 512;
    
    public FileTransferWorker(
        ILogger<FileTransferWorker> logger,
        ISqlSugarClient db,
        Iec102Slave? slave = null)
    {
        _logger = logger;
        _db = db;
        _slave = slave;
    }
    
    /// <summary>
    /// 传输文件
    /// </summary>
    public async Task<bool> TransferFileAsync(FileTransferTask task, CancellationToken cancellationToken = default)
    {
        if (_slave == null)
        {
            _logger.LogError("IEC-102从站未配置，无法传输文件");
            return false;
        }
        
        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _activeTransfers[task.Id] = cts;
        
        try
        {
            _logger.LogInformation("开始传输文件任务: TaskId={TaskId}, FileRecordId={FileRecordId}", 
                task.Id, task.FileRecordId);
            
            // 更新任务状态为进行中
            task.Status = "in_progress";
            task.StartedAt = DateTime.UtcNow;
            await _db.Updateable(task).ExecuteCommandAsync(cts.Token);
            
            // 获取文件记录
            var fileRecord = await _db.Queryable<FileRecord>()
                .Where(f => f.Id == task.FileRecordId)
                .Includes(f => f.ReportType)
                .FirstAsync(cts.Token);
            
            if (fileRecord == null)
            {
                _logger.LogError("文件记录不存在: FileRecordId={FileRecordId}", task.FileRecordId);
                await UpdateTaskStatusAsync(task, "failed", "文件记录不存在", cts.Token);
                return false;
            }
            
            // 检查文件大小
            if (fileRecord.FileSize > MaxFileSize)
            {
                _logger.LogWarning("文件过长: FileName={FileName}, Size={Size}, MaxSize={MaxSize}", 
                    fileRecord.OriginalFilename, fileRecord.FileSize, MaxFileSize);
                
                // 发送文件过长错误控制帧 (0x92)
                await SendFileTooLongAsync(fileRecord, cts.Token);
                await UpdateTaskStatusAsync(task, "failed", "文件过长", cts.Token);
                return false;
            }
            
            // 检查文件名格式
            if (!ValidateFileName(fileRecord.OriginalFilename))
            {
                _logger.LogWarning("文件名格式不正确: FileName={FileName}", fileRecord.OriginalFilename);
                
                // 发送文件名格式错误控制帧 (0x93)
                await SendInvalidFileNameAsync(fileRecord, cts.Token);
                await UpdateTaskStatusAsync(task, "failed", "文件名格式不正确", cts.Token);
                return false;
            }
            
            // 读取文件内容
            byte[] fileContent;
            if (!string.IsNullOrEmpty(fileRecord.StoragePath) && File.Exists(fileRecord.StoragePath))
            {
                fileContent = await File.ReadAllBytesAsync(fileRecord.StoragePath, cts.Token);
            }
            else
            {
                _logger.LogError("文件不存在: StoragePath={StoragePath}", fileRecord.StoragePath);
                await UpdateTaskStatusAsync(task, "failed", "文件不存在", cts.Token);
                return false;
            }
            
            // 获取TypeId
            byte typeId = GetTypeIdForReportType(fileRecord.ReportType?.Code);
            if (typeId == 0)
            {
                _logger.LogError("无效的报告类型: ReportType={ReportType}", fileRecord.ReportType?.Code);
                await UpdateTaskStatusAsync(task, "failed", "无效的报告类型", cts.Token);
                return false;
            }
            
            // 分段传输
            var segments = CreateSegments(fileRecord.OriginalFilename, fileContent);
            task.TotalSegments = segments.Count;
            await _db.Updateable(task).ExecuteCommandAsync(cts.Token);
            
            _logger.LogInformation("文件分段完成: FileName={FileName}, TotalSegments={TotalSegments}", 
                fileRecord.OriginalFilename, segments.Count);
            
            // 逐段发送
            for (int i = 0; i < segments.Count; i++)
            {
                if (cts.Token.IsCancellationRequested)
                {
                    _logger.LogInformation("文件传输被取消: TaskId={TaskId}", task.Id);
                    await UpdateTaskStatusAsync(task, "cancelled", "传输被取消", cts.Token);
                    return false;
                }
                
                var segment = segments[i];
                bool isLastSegment = (i == segments.Count - 1);
                byte cot = isLastSegment ? (byte)0x07 : (byte)0x08; // 0x07=最后一帧, 0x08=非最后一帧
                
                // 检查单帧长度
                if (segment.Length > MaxFrameSize)
                {
                    _logger.LogWarning("单帧报文过长: SegmentIndex={Index}, Size={Size}", i, segment.Length);
                    await SendFrameTooLongAsync(fileRecord, cts.Token);
                    await UpdateTaskStatusAsync(task, "failed", "单帧报文过长", cts.Token);
                    return false;
                }
                
                // 发送数据段
                _slave.QueueClass2Data(typeId, cot, segment);
                
                task.SentSegments = i + 1;
                task.Progress = (int)((task.SentSegments * 100.0) / task.TotalSegments);
                await _db.Updateable(task).ExecuteCommandAsync(cts.Token);
                
                _logger.LogDebug("已发送片段: TaskId={TaskId}, Segment={Index}/{Total}, COT=0x{Cot:X2}", 
                    task.Id, i + 1, segments.Count, cot);
                
                // 背压控制：等待一小段时间避免发送过快
                await Task.Delay(10, cts.Token);
            }
            
            // 发送对账帧 (0x90)
            await SendReconciliationAsync(typeId, fileContent.Length, cts.Token);
            
            // 更新任务状态为完成
            await UpdateTaskStatusAsync(task, "completed", null, cts.Token);
            
            _logger.LogInformation("文件传输完成: TaskId={TaskId}, FileName={FileName}, Size={Size}", 
                task.Id, fileRecord.OriginalFilename, fileContent.Length);
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "文件传输失败: TaskId={TaskId}", task.Id);
            await UpdateTaskStatusAsync(task, "failed", ex.Message, CancellationToken.None);
            return false;
        }
        finally
        {
            _activeTransfers.TryRemove(task.Id, out _);
            cts.Dispose();
        }
    }
    
    /// <summary>
    /// 取消文件传输
    /// </summary>
    public void CancelTransfer(int taskId)
    {
        if (_activeTransfers.TryGetValue(taskId, out var cts))
        {
            _logger.LogInformation("取消文件传输: TaskId={TaskId}", taskId);
            cts.Cancel();
        }
    }
    
    /// <summary>
    /// 获取正在传输的任务数量
    /// </summary>
    public int GetActiveTransferCount()
    {
        return _activeTransfers.Count;
    }
    
    /// <summary>
    /// 创建文件分段
    /// </summary>
    private List<byte[]> CreateSegments(string fileName, byte[] fileContent)
    {
        var segments = new List<byte[]>();
        
        // 准备文件名（64字节，0x00填充）
        var fileNameBytes = new byte[FileNameLength];
        var nameBytes = Encoding.GetEncoding("GBK").GetBytes(fileName);
        Array.Copy(nameBytes, 0, fileNameBytes, 0, Math.Min(nameBytes.Length, FileNameLength));
        
        // 分段
        int offset = 0;
        while (offset < fileContent.Length)
        {
            int segmentDataSize = Math.Min(MaxSegmentSize, fileContent.Length - offset);
            var segment = new byte[FileNameLength + segmentDataSize];
            
            // 文件名
            Array.Copy(fileNameBytes, 0, segment, 0, FileNameLength);
            
            // 数据片段
            Array.Copy(fileContent, offset, segment, FileNameLength, segmentDataSize);
            
            segments.Add(segment);
            offset += segmentDataSize;
        }
        
        return segments;
    }
    
    /// <summary>
    /// 验证文件名格式
    /// </summary>
    private bool ValidateFileName(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
            return false;
        
        // 检查文件名长度（GBK编码）
        var nameBytes = Encoding.GetEncoding("GBK").GetBytes(fileName);
        if (nameBytes.Length > FileNameLength)
            return false;
        
        return true;
    }
    
    /// <summary>
    /// 获取报告类型对应的TypeId
    /// </summary>
    private byte GetTypeIdForReportType(string? reportType)
    {
        if (string.IsNullOrEmpty(reportType))
            return 0;
            
        // 根据报告类型映射到TypeId (0x95-0xA8)
        return reportType switch
        {
            "EFJ_FARM_INFO" => 0x95,
            "EFJ_FARM_UNIT_INFO" => 0x96,
            "EFJ_FARM_UNIT_RUN_STATE" => 0x97,
            "EFJ_FARM_RUN_CAP" => 0x98,
            "EFJ_WIND_TOWER_INFO" => 0x99,
            "EFJ_FIVE_WIND_TOWER" => 0x9A,
            "EFJ_DQ_RESULT_UP" => 0x9B,
            "EFJ_CDQ_RESULT_UP" => 0x9C,
            "EFJ_DQ_PLAN_UP" => 0xA6,
            "EFJ_NWP_UP" => 0x9D,
            "EFJ_OTHER_UP" => 0x9E,
            "EFJ_FIF_THEORY_POWER" => 0x9F,
            "EGF_GF_INFO" => 0xA4,
            "EGF_GF_QXZ_INFO" => 0xA0,
            "EGF_GF_UNIT_INFO" => 0xA3,
            "EGF_GF_UNIT_RUN_STATE" => 0xA2,
            "EGF_FIVE_GF_QXZ" => 0xA1,
            "EFJ_REALTIME" => 0xA7,
            "EGF_REALTIME" => 0xA8,
            _ => 0
        };
    }
    
    /// <summary>
    /// 发送对账帧 (TYP=0x90)
    /// </summary>
    private async Task SendReconciliationAsync(byte typeId, int fileLength, CancellationToken cancellationToken)
    {
        // 对账帧数据: 4字节文件长度（小端序）
        var data = new byte[4];
        data[0] = (byte)(fileLength & 0xFF);
        data[1] = (byte)((fileLength >> 8) & 0xFF);
        data[2] = (byte)((fileLength >> 16) & 0xFF);
        data[3] = (byte)((fileLength >> 24) & 0xFF);
        
        _slave?.QueueClass2Data(0x90, 0x0A, data); // COT=0x0A: 主站确认文件接收结束
        
        _logger.LogDebug("发送对账帧: TypeId=0x90, FileLength={FileLength}", fileLength);
        
        await Task.CompletedTask;
    }
    
    /// <summary>
    /// 发送文件过长错误控制帧 (TYP=0x92)
    /// </summary>
    private async Task SendFileTooLongAsync(FileRecord fileRecord, CancellationToken cancellationToken)
    {
        _slave?.QueueClass2Data(0x92, 0x10, Array.Empty<byte>()); // COT=0x10: 子站确认文件过长
        _logger.LogDebug("发送文件过长控制帧: FileName={FileName}", fileRecord.OriginalFilename);
        await Task.CompletedTask;
    }
    
    /// <summary>
    /// 发送文件名格式错误控制帧 (TYP=0x93)
    /// </summary>
    private async Task SendInvalidFileNameAsync(FileRecord fileRecord, CancellationToken cancellationToken)
    {
        _slave?.QueueClass2Data(0x93, 0x12, Array.Empty<byte>()); // COT=0x12: 子站确认文件名格式不正确
        _logger.LogDebug("发送文件名格式错误控制帧: FileName={FileName}", fileRecord.OriginalFilename);
        await Task.CompletedTask;
    }
    
    /// <summary>
    /// 发送单帧报文过长错误控制帧 (TYP=0x94)
    /// </summary>
    private async Task SendFrameTooLongAsync(FileRecord fileRecord, CancellationToken cancellationToken)
    {
        _slave?.QueueClass2Data(0x94, 0x14, Array.Empty<byte>()); // COT=0x14: 子站确认单帧报文过长
        _logger.LogDebug("发送单帧报文过长控制帧: FileName={FileName}", fileRecord.OriginalFilename);
        await Task.CompletedTask;
    }
    
    /// <summary>
    /// 更新任务状态
    /// </summary>
    private async Task UpdateTaskStatusAsync(
        FileTransferTask task, 
        string status, 
        string? errorMessage, 
        CancellationToken cancellationToken)
    {
        task.Status = status;
        task.ErrorMessage = errorMessage;
        
        if (status == "completed" || status == "failed" || status == "cancelled")
        {
            task.CompletedAt = DateTime.UtcNow;
        }
        
        await _db.Updateable(task).ExecuteCommandAsync(cancellationToken);
    }
}
