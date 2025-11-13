using LpsGateway.Data.Models;
using SqlSugar;

namespace LpsGateway.Data;

/// <summary>
/// 文件记录仓储实现
/// </summary>
public class FileRecordRepository : IFileRecordRepository
{
    private readonly ISqlSugarClient _db;
    private readonly ILogger<FileRecordRepository> _logger;

    public FileRecordRepository(ISqlSugarClient db, ILogger<FileRecordRepository> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<FileRecord?> GetByIdAsync(int id)
    {
        try
        {
            return await _db.Queryable<FileRecord>()
                .Where(f => f.Id == id)
                .FirstAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取文件记录失败: FileRecordId={Id}", id);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<List<FileRecord>> GetAllAsync()
    {
        try
        {
            return await _db.Queryable<FileRecord>()
                .OrderBy(f => f.CreatedAt, OrderByType.Desc)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取所有文件记录失败");
            return new List<FileRecord>();
        }
    }

    /// <inheritdoc />
    public async Task<List<FileRecord>> GetByStatusAsync(string status)
    {
        try
        {
            return await _db.Queryable<FileRecord>()
                .Where(f => f.Status == status)
                .OrderBy(f => f.DownloadTime, OrderByType.Desc)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "根据状态获取文件记录失败: Status={Status}", status);
            return new List<FileRecord>();
        }
    }

    /// <inheritdoc />
    public async Task<List<FileRecord>> GetByReportTypeIdAsync(int reportTypeId)
    {
        try
        {
            return await _db.Queryable<FileRecord>()
                .Where(f => f.ReportTypeId == reportTypeId)
                .OrderBy(f => f.DownloadTime, OrderByType.Desc)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "根据报表类型获取文件记录失败: ReportTypeId={ReportTypeId}", reportTypeId);
            return new List<FileRecord>();
        }
    }

    /// <inheritdoc />
    public async Task<List<FileRecord>> GetByStatusAndReportTypeAsync(string status, int reportTypeId)
    {
        try
        {
            return await _db.Queryable<FileRecord>()
                .Where(f => f.Status == status && f.ReportTypeId == reportTypeId)
                .OrderBy(f => f.DownloadTime, OrderByType.Desc)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "根据状态和报表类型获取文件记录失败: Status={Status}, ReportTypeId={ReportTypeId}", 
                status, reportTypeId);
            return new List<FileRecord>();
        }
    }

    /// <inheritdoc />
    public async Task<int> CreateAsync(FileRecord fileRecord)
    {
        try
        {
            fileRecord.CreatedAt = DateTime.Now;
            fileRecord.UpdatedAt = DateTime.Now;
            
            return await _db.Insertable(fileRecord)
                .ExecuteReturnIdentityAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建文件记录失败: FileName={FileName}", fileRecord.OriginalFilename);
            return 0;
        }
    }

    /// <inheritdoc />
    public async Task<bool> UpdateAsync(FileRecord fileRecord)
    {
        try
        {
            fileRecord.UpdatedAt = DateTime.Now;
            
            var result = await _db.Updateable(fileRecord)
                .ExecuteCommandAsync();
            
            return result > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新文件记录失败: FileRecordId={Id}", fileRecord.Id);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(int id)
    {
        try
        {
            var result = await _db.Deleteable<FileRecord>()
                .Where(f => f.Id == id)
                .ExecuteCommandAsync();
            
            return result > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除文件记录失败: FileRecordId={Id}", id);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<List<FileRecord>> GetDownloadedFilesForTransferAsync(int? reportTypeId = null)
    {
        try
        {
            var query = _db.Queryable<FileRecord>()
                .Where(f => f.Status == "downloaded");
            
            if (reportTypeId.HasValue)
            {
                query = query.Where(f => f.ReportTypeId == reportTypeId.Value);
            }
            
            return await query
                .OrderBy(f => f.DownloadTime, OrderByType.Asc) // 先下载的先传输
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取待传输文件记录失败");
            return new List<FileRecord>();
        }
    }
    
    /// <summary>
    /// 尝试独占获取文件记录（原子更新processSessionId）
    /// </summary>
    public async Task<FileRecord?> TryAcquireFileForSessionAsync(int fileRecordId, string sessionId, IEnumerable<string> activeSessionIds)
    {
        try
        {
            // 使用WHERE条件确保原子性：只有当processSessionId为空或不在活跃会话列表中时才更新
            var activeSessionList = activeSessionIds.ToList();
            
            // 先获取文件记录
            var fileRecord = await _db.Queryable<FileRecord>()
                .Where(f => f.Id == fileRecordId && f.Status == "downloaded")
                .FirstAsync();
            
            if (fileRecord == null)
            {
                return null;
            }
            
            // 检查processSessionId是否可以独占
            if (!string.IsNullOrEmpty(fileRecord.ProcessSessionId) && activeSessionList.Contains(fileRecord.ProcessSessionId))
            {
                _logger.LogDebug("文件记录已被其他会话独占: FileRecordId={FileRecordId}, SessionId={SessionId}", 
                    fileRecordId, fileRecord.ProcessSessionId);
                return null;
            }
            
            // 尝试更新为当前会话ID
            fileRecord.ProcessSessionId = sessionId;
            fileRecord.UpdatedAt = DateTime.Now;
            
            var updated = await _db.Updateable(fileRecord)
                .Where(f => f.Id == fileRecordId)
                .Where(f => f.ProcessSessionId == null || !activeSessionList.Contains(f.ProcessSessionId))
                .ExecuteCommandAsync();
            
            if (updated > 0)
            {
                _logger.LogInformation("成功独占文件记录: FileRecordId={FileRecordId}, SessionId={SessionId}", 
                    fileRecordId, sessionId);
                return fileRecord;
            }
            
            _logger.LogDebug("文件记录独占失败（并发竞争）: FileRecordId={FileRecordId}", fileRecordId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "尝试独占文件记录失败: FileRecordId={FileRecordId}", fileRecordId);
            return null;
        }
    }
}
