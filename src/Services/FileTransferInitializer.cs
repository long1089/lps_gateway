using LpsGateway.Data;
using LpsGateway.Data.Models;
using SqlSugar;

namespace LpsGateway.Services;

/// <summary>
/// 文件传输初始化服务实现
/// </summary>
/// <remarks>
/// 当客户端连接时，自动为已下载的文件创建传输任务
/// 根据报表类型的数据分类（1级/2级）确定传输优先级
/// </remarks>
public class FileTransferInitializer : IFileTransferInitializer
{
    private readonly IFileRecordRepository _fileRecordRepository;
    private readonly IReportTypeRepository _reportTypeRepository;
    private readonly ISqlSugarClient _db;
    private readonly ILogger<FileTransferInitializer> _logger;
    
    // 1级数据类型代码（优先数据）
    private static readonly HashSet<string> Class1DataTypes = new()
    {
        "EFJ_FIVE_WIND_TOWER",      // 0x9A: 测风塔采集数据
        "EFJ_DQ_RESULT_UP",          // 0x9B: 短期预测
        "EFJ_CDQ_RESULT_UP",         // 0x9C: 超短期预测
        "EFJ_NWP_UP",                // 0x9D: 天气预报
        "EGF_FIVE_GF_QXZ"            // 0xA1: 气象站采集数据
    };
    
    // 1级数据Type ID（优先数据）
    private static readonly HashSet<byte> Class1TypeIds = new()
    {
        0x9A, // EFJ_FIVE_WIND_TOWER
        0x9B, // EFJ_DQ_RESULT_UP
        0x9C, // EFJ_CDQ_RESULT_UP
        0x9D, // EFJ_NWP_UP
        0xA1  // EGF_FIVE_GF_QXZ
    };

    public FileTransferInitializer(
        IFileRecordRepository fileRecordRepository,
        IReportTypeRepository reportTypeRepository,
        ISqlSugarClient db,
        ILogger<FileTransferInitializer> logger)
    {
        _fileRecordRepository = fileRecordRepository;
        _reportTypeRepository = reportTypeRepository;
        _db = db;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<int> InitializeTransfersForSessionAsync(string sessionId, string endpoint, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("开始为会话初始化文件传输任务: SessionId={SessionId}, Endpoint={Endpoint}", 
            sessionId, endpoint);
        
        try
        {
            // 获取所有已下载状态的文件记录
            var downloadedFiles = await _fileRecordRepository.GetDownloadedFilesForTransferAsync();
            
            if (downloadedFiles.Count == 0)
            {
                _logger.LogInformation("没有待传输的文件记录");
                return 0;
            }
            
            _logger.LogInformation("发现 {Count} 个待传输的文件记录", downloadedFiles.Count);
            
            var initializedCount = 0;
            
            foreach (var fileRecord in downloadedFiles)
            {
                // 检查是否已经存在该文件的待处理或进行中的任务
                var existingTask = await _db.Queryable<FileTransferTask>()
                    .Where(t => t.FileRecordId == fileRecord.Id)
                    .Where(t => t.Status == "pending" || t.Status == "in_progress")
                    .AnyAsync(cancellationToken);
                
                if (existingTask)
                {
                    _logger.LogDebug("文件记录已有待处理任务，跳过: FileRecordId={FileRecordId}", fileRecord.Id);
                    continue;
                }
                
                // 创建新的传输任务
                var transferTask = new FileTransferTask
                {
                    FileRecordId = fileRecord.Id,
                    SessionId = sessionId,
                    Status = "pending",
                    Progress = 0,
                    SentSegments = 0,
                    CreatedAt = DateTime.UtcNow
                };
                
                try
                {
                    await _db.Insertable(transferTask).ExecuteCommandAsync(cancellationToken);
                    initializedCount++;
                    
                    _logger.LogInformation("创建文件传输任务: FileRecordId={FileRecordId}, TaskId={TaskId}, FileName={FileName}", 
                        fileRecord.Id, transferTask.Id, fileRecord.OriginalFilename);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "创建文件传输任务失败: FileRecordId={FileRecordId}", fileRecord.Id);
                }
            }
            
            _logger.LogInformation("文件传输任务初始化完成: 创建了 {Count} 个任务", initializedCount);
            return initializedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "初始化文件传输任务时发生异常");
            return 0;
        }
    }

    /// <inheritdoc />
    public bool IsClass1Data(string reportTypeCode)
    {
        return Class1DataTypes.Contains(reportTypeCode);
    }

    /// <inheritdoc />
    public bool IsClass1DataByTypeId(byte typeId)
    {
        return Class1TypeIds.Contains(typeId);
    }
}
