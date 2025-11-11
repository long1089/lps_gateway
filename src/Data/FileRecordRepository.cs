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
            fileRecord.CreatedAt = DateTime.UtcNow;
            fileRecord.UpdatedAt = DateTime.UtcNow;
            
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
            fileRecord.UpdatedAt = DateTime.UtcNow;
            
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
}
