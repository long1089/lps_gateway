using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using LpsGateway.Data;

namespace LpsGateway.Controllers;

/// <summary>
/// 审计日志控制器
/// </summary>
[Authorize(Roles = "Admin")]
public class AuditLogsController : Controller
{
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly ILogger<AuditLogsController> _logger;

    public AuditLogsController(
        IAuditLogRepository auditLogRepository,
        ILogger<AuditLogsController> logger)
    {
        _auditLogRepository = auditLogRepository;
        _logger = logger;
    }

    /// <summary>
    /// 审计日志列表页
    /// </summary>
    public async Task<IActionResult> Index(string? act = null, int count = 100)
    {
        try
        {
            var logs = await _auditLogRepository.GetRecentAsync(count, null, act);
            ViewBag.Act = act;
            ViewBag.Count = count;
            return View(logs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取审计日志列表失败");
            TempData["Error"] = "获取审计日志失败";
            return View(new List<Data.Models.AuditLog>());
        }
    }

    /// <summary>
    /// 获取操作统计
    /// </summary>
    public async Task<IActionResult> Statistics()
    {
        try
        {
            var endDate = DateTime.Now;
            var startDate = endDate.AddDays(-30);
            
            var actionCounts = await _auditLogRepository.GetActionCountsAsync(startDate, endDate);
            
            return View(actionCounts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取审计统计失败");
            TempData["Error"] = "获取审计统计失败";
            return View(new Dictionary<string, int>());
        }
    }
}
