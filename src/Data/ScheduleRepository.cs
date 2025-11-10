using LpsGateway.Data.Models;
using SqlSugar;

namespace LpsGateway.Data;

/// <summary>
/// 调度配置仓储实现
/// </summary>
public class ScheduleRepository : IScheduleRepository
{
    private readonly ISqlSugarClient _db;

    public ScheduleRepository(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task<Schedule?> GetByIdAsync(int id)
    {
        return await _db.Queryable<Schedule>()
            .Where(s => s.Id == id)
            .FirstAsync();
    }

    public async Task<List<Schedule>> GetByReportTypeAsync(int reportTypeId)
    {
        return await _db.Queryable<Schedule>()
            .Where(s => s.ReportTypeId == reportTypeId)
            .OrderBy(s => s.ScheduleType)
            .ToListAsync();
    }

    public async Task<List<Schedule>> GetAllAsync(bool? enabled = null)
    {
        var query = _db.Queryable<Schedule>();
        
        if (enabled.HasValue)
        {
            query = query.Where(s => s.Enabled == enabled.Value);
        }
        
        return await query.OrderBy(s => s.ReportTypeId).ToListAsync();
    }

    public async Task<Schedule> CreateAsync(Schedule schedule)
    {
        schedule.CreatedAt = DateTime.UtcNow;
        schedule.UpdatedAt = DateTime.UtcNow;
        
        var id = await _db.Insertable(schedule).ExecuteReturnIdentityAsync();
        schedule.Id = id;
        return schedule;
    }

    public async Task<bool> UpdateAsync(Schedule schedule)
    {
        schedule.UpdatedAt = DateTime.UtcNow;
        
        var result = await _db.Updateable(schedule)
            .Where(s => s.Id == schedule.Id)
            .ExecuteCommandAsync();
        
        return result > 0;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var result = await _db.Deleteable<Schedule>()
            .Where(s => s.Id == id)
            .ExecuteCommandAsync();
        
        return result > 0;
    }
}
