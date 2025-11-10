using LpsGateway.Data.Models;

namespace LpsGateway.Data;

/// <summary>
/// 调度配置仓储接口
/// </summary>
public interface IScheduleRepository
{
    Task<Schedule?> GetByIdAsync(int id);
    Task<List<Schedule>> GetByReportTypeAsync(int reportTypeId);
    Task<List<Schedule>> GetAllAsync(bool? enabled = null);
    Task<Schedule> CreateAsync(Schedule schedule);
    Task<bool> UpdateAsync(Schedule schedule);
    Task<bool> DeleteAsync(int id);
}
