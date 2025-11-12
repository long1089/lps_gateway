namespace LpsGateway.Data;

/// <summary>
/// 审计日志仓储接口
/// </summary>
public interface IAuditLogRepository
{
    /// <summary>
    /// 创建审计日志
    /// </summary>
    Task<int> CreateAsync(Models.AuditLog log);

    /// <summary>
    /// 获取最近的审计日志
    /// </summary>
    /// <param name="count">返回数量</param>
    /// <param name="userId">用户ID过滤（可选）</param>
    /// <param name="action">操作过滤（可选）</param>
    Task<List<Models.AuditLog>> GetRecentAsync(int count = 100, int? userId = null, string? action = null);

    /// <summary>
    /// 获取指定时间范围内的审计日志
    /// </summary>
    Task<List<Models.AuditLog>> GetByDateRangeAsync(DateTime startDate, DateTime endDate, int? userId = null);

    /// <summary>
    /// 统计指定时间范围内的审计日志数量（按操作分组）
    /// </summary>
    Task<Dictionary<string, int>> GetActionCountsAsync(DateTime startDate, DateTime endDate);
}
