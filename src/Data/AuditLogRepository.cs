using LpsGateway.Data.Models;
using LpsGateway.Extensions;
using SqlSugar;

namespace LpsGateway.Data;

/// <summary>
/// 审计日志仓储实现
/// </summary>
public class AuditLogRepository : IAuditLogRepository
{
    private readonly ISqlSugarClient _db;
    private readonly ILogger<AuditLogRepository> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuditLogRepository(ISqlSugarClient db, ILogger<AuditLogRepository> logger, IHttpContextAccessor httpContextAccessor)
    {
        _db = db;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
    }

    /// <inheritdoc/>
    public async Task<int> CreateAsync(AuditLog log)
    {
        try
        {
            return await _db.Insertable(log).ExecuteReturnIdentityAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建审计日志失败");
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<List<AuditLog>> GetRecentAsync(int count = 100, int? userId = null, string? action = null)
    {
        try
        {
            var query = _db.Queryable<AuditLog>();

            if (userId.HasValue)
            {
                query = query.Where(a => a.UserId == userId.Value);
            }

            if (!string.IsNullOrEmpty(action))
            {
                query = query.Where(a => a.Action == action);
            }

            return await query
                .OrderByDescending(a => a.CreatedAt)
                .Take(count)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取最近审计日志失败");
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<List<AuditLog>> GetByDateRangeAsync(DateTime startDate, DateTime endDate, int? userId = null)
    {
        try
        {
            var query = _db.Queryable<AuditLog>()
                .Where(a => a.CreatedAt >= startDate && a.CreatedAt <= endDate);

            if (userId.HasValue)
            {
                query = query.Where(a => a.UserId == userId.Value);
            }

            return await query
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取时间范围内审计日志失败");
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<Dictionary<string, int>> GetActionCountsAsync(DateTime startDate, DateTime endDate)
    {
        try
        {
            var results = await _db.Queryable<AuditLog>()
                .Where(a => a.CreatedAt >= startDate && a.CreatedAt <= endDate)
                .GroupBy(a => a.Action)
                .Select(g => new { Action = g.Action, Count = SqlFunc.AggregateCount(g.Action) })
                .ToListAsync();

            return results.ToDictionary(r => r.Action, r => r.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "统计审计日志失败");
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task AddLogAsync(string action, int? userId = null, string? resource = null, AuditLog.AuditLogFieldChange? fieldChange = null)
    {
        _ = await _db.Insertable<AuditLog>(new AuditLog
        {
            Action = action,
            CreatedAt = DateTime.Now,
            UserId = userId ?? _httpContextAccessor.HttpContext?.GetUserId(),
            IpAddress = _httpContextAccessor.HttpContext?.GetUserIpAddress(),
        }).ExecuteCommandAsync();
    }
}
