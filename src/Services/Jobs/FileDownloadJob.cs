using LpsGateway.Data;
using LpsGateway.Data.Models;
using Quartz;

namespace LpsGateway.Services.Jobs;

/// <summary>
/// 文件下载调度任务
/// </summary>
public class FileDownloadJob : IJob
{
    private readonly ISftpManager _sftpManager;
    private readonly IReportTypeRepository _reportTypeRepository;
    private readonly IFileRecordRepository _fileRecordRepository;
    private readonly ILogger<FileDownloadJob> _logger;

    public FileDownloadJob(
        ISftpManager sftpManager,
        IReportTypeRepository reportTypeRepository,
        IFileRecordRepository fileRecordRepository,
        ILogger<FileDownloadJob> logger)
    {
        _sftpManager = sftpManager;
        _reportTypeRepository = reportTypeRepository;
        _fileRecordRepository = fileRecordRepository;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var dataMap = context.MergedJobDataMap;
        var reportTypeId = dataMap.GetInt("ReportTypeId");
        var scheduleId = dataMap.GetInt("ScheduleId");

        _logger.LogInformation("开始执行文件下载任务: ReportTypeId={ReportTypeId}, ScheduleId={ScheduleId}", 
            reportTypeId, scheduleId);

        try
        {
            var reportType = await _reportTypeRepository.GetByIdAsync(reportTypeId);
            if (reportType == null)
            {
                _logger.LogError("报表类型不存在: {ReportTypeId}", reportTypeId);
                return;
            }

            if (!reportType.Enabled || reportType.DefaultSftpConfigId == null)
            {
                _logger.LogWarning("报表类型已禁用或未配置SFTP: {ReportTypeId}", reportTypeId);
                return;
            }

            var sftpConfigId = reportType.DefaultSftpConfigId.Value;
            var now = DateTime.Now;
            
            // 解析路径模板
            var remotePath = _sftpManager.ParsePathTemplate(reportType.Code, now);
            
            // 列出远程文件
            var files = await _sftpManager.ListFilesAsync(sftpConfigId, remotePath, context.CancellationToken);
            
            _logger.LogInformation("发现 {Count} 个待下载文件", files.Count);

            // 下载文件
            foreach (var remoteFile in files)
            {
                var fileName = Path.GetFileName(remoteFile);
                
                // 检查文件是否已下载（根据文件名和报表类型）
                var existingRecord = await _fileRecordRepository.GetByStatusAndReportTypeAsync("downloaded", reportTypeId);
                var alreadyDownloaded = existingRecord.Any(r => r.OriginalFilename == fileName);
                
                if (alreadyDownloaded)
                {
                    _logger.LogInformation("文件已下载，跳过: {FileName}", fileName);
                    continue;
                }
                
                var localPath = Path.Combine("downloads", reportType.Code, fileName);
                
                var success = await _sftpManager.DownloadFileAsync(sftpConfigId, remoteFile, localPath, context.CancellationToken);
                
                if (success)
                {
                    _logger.LogInformation("文件下载成功: {RemoteFile}", remoteFile);
                    
                    // 保存文件记录到数据库
                    try
                    {
                        var fileInfo = new FileInfo(localPath);
                        var fileRecord = new FileRecord
                        {
                            ReportTypeId = reportTypeId,
                            SftpConfigId = sftpConfigId,
                            OriginalFilename = fileName,
                            StoragePath = localPath,
                            FileSize = fileInfo.Exists ? fileInfo.Length : 0,
                            DownloadTime = DateTime.UtcNow,
                            Status = "downloaded",
                            ProcessSessionId = null, // 初始为空，等待会话处理
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };
                        
                        var fileRecordId = await _fileRecordRepository.CreateAsync(fileRecord);
                        if (fileRecordId > 0)
                        {
                            _logger.LogInformation("文件记录已保存: FileRecordId={FileRecordId}, FileName={FileName}", 
                                fileRecordId, fileName);
                        }
                        else
                        {
                            _logger.LogWarning("保存文件记录失败: FileName={FileName}", fileName);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "保存文件记录时发生异常: FileName={FileName}", fileName);
                    }
                }
                else
                {
                    _logger.LogError("文件下载失败: {RemoteFile}", remoteFile);
                }
            }

            _logger.LogInformation("文件下载任务完成: ReportTypeId={ReportTypeId}", reportTypeId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "文件下载任务执行失败: ReportTypeId={ReportTypeId}", reportTypeId);
            throw;
        }
    }
}
