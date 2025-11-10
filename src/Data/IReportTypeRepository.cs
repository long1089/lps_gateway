using LpsGateway.Data.Models;

namespace LpsGateway.Data;

/// <summary>
/// 报表类型仓储接口
/// </summary>
public interface IReportTypeRepository
{
    Task<ReportType?> GetByIdAsync(int id);
    Task<ReportType?> GetByCodeAsync(string code);
    Task<List<ReportType>> GetAllAsync(bool? enabled = null);
    Task<ReportType> CreateAsync(ReportType reportType);
    Task<bool> UpdateAsync(ReportType reportType);
    Task<bool> DeleteAsync(int id);
    Task<bool> ExistsAsync(string code, int? excludeId = null);
}
