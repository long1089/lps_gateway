using LpsGateway.Data.Models;
using SqlSugar;

namespace LpsGateway.Data;

/// <summary>
/// 报表类型仓储实现
/// </summary>
public class ReportTypeRepository : IReportTypeRepository
{
    private readonly ISqlSugarClient _db;

    public ReportTypeRepository(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task<ReportType?> GetByIdAsync(int id)
    {
        return await _db.Queryable<ReportType>()
            .Where(r => r.Id == id)
            .FirstAsync();
    }

    public async Task<ReportType?> GetByCodeAsync(string code)
    {
        return await _db.Queryable<ReportType>()
            .Where(r => r.Code == code)
            .FirstAsync();
    }

    public async Task<List<ReportType>> GetAllAsync(bool? enabled = null)
    {
        var query = _db.Queryable<ReportType>();
        
        if (enabled.HasValue)
        {
            query = query.Where(r => r.Enabled == enabled.Value);
        }
        
        return await query.OrderBy(r => r.Code).ToListAsync();
    }

    public async Task<ReportType> CreateAsync(ReportType reportType)
    {
        reportType.CreatedAt = DateTime.UtcNow;
        reportType.UpdatedAt = DateTime.UtcNow;
        
        var id = await _db.Insertable(reportType).ExecuteReturnIdentityAsync();
        reportType.Id = id;
        return reportType;
    }

    public async Task<bool> UpdateAsync(ReportType reportType)
    {
        reportType.UpdatedAt = DateTime.UtcNow;
        
        var result = await _db.Updateable(reportType)
            .Where(r => r.Id == reportType.Id)
            .ExecuteCommandAsync();
        
        return result > 0;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var result = await _db.Deleteable<ReportType>()
            .Where(r => r.Id == id)
            .ExecuteCommandAsync();
        
        return result > 0;
    }

    public async Task<bool> ExistsAsync(string code, int? excludeId = null)
    {
        var query = _db.Queryable<ReportType>()
            .Where(r => r.Code == code);
        
        if (excludeId.HasValue)
        {
            query = query.Where(r => r.Id != excludeId.Value);
        }
        
        return await query.AnyAsync();
    }
}
