using LpsGateway.Data;
using LpsGateway.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LpsGateway.Controllers;

/// <summary>
/// 文件下载控制器 - MVC模式
/// </summary>
[Authorize]
public class DownloadController : Controller
{
    private readonly IScheduleManager _scheduleManager;
    private readonly IReportTypeRepository _reportTypeRepository;
    private readonly ILogger<DownloadController> _logger;

    public DownloadController(
        IScheduleManager scheduleManager,
        IReportTypeRepository reportTypeRepository,
        ILogger<DownloadController> logger)
    {
        _scheduleManager = scheduleManager;
        _reportTypeRepository = reportTypeRepository;
        _logger = logger;
    }

    /// <summary>
    /// 下载管理页面
    /// </summary>
    public async Task<IActionResult> Index()
    {
        var reportTypes = await _reportTypeRepository.GetAllAsync(true);
        return View(reportTypes);
    }

    /// <summary>
    /// 手动触发文件下载
    /// </summary>
    /// <param name="reportTypeId">报表类型ID</param>
    /// <returns>操作结果</returns>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Operator")]
    public async Task<IActionResult> TriggerDownload(int reportTypeId)
    {
        try
        {
            var reportType = await _reportTypeRepository.GetByIdAsync(reportTypeId);
            if (reportType == null)
            {
                TempData["ErrorMessage"] = "报表类型不存在";
                return RedirectToAction(nameof(Index));
            }

            if (!reportType.Enabled)
            {
                TempData["ErrorMessage"] = "报表类型已禁用";
                return RedirectToAction(nameof(Index));
            }

            await _scheduleManager.TriggerDownloadAsync(reportTypeId);

            _logger.LogInformation("手动触发下载: ReportTypeId={ReportTypeId}, User={User}", 
                reportTypeId, User.Identity?.Name);

            TempData["SuccessMessage"] = $"下载任务已触发: {reportType.Name}";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "触发下载失败: ReportTypeId={ReportTypeId}", reportTypeId);
            TempData["ErrorMessage"] = $"触发下载失败: {ex.Message}";
            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>
    /// 重新加载所有调度
    /// </summary>
    /// <returns>操作结果</returns>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ReloadSchedules()
    {
        try
        {
            await _scheduleManager.ReloadSchedulesAsync();

            _logger.LogInformation("重新加载调度: User={User}", User.Identity?.Name);

            TempData["SuccessMessage"] = "调度已重新加载";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "重新加载调度失败");
            TempData["ErrorMessage"] = $"重新加载调度失败: {ex.Message}";
            return RedirectToAction(nameof(Index));
        }
    }
}
