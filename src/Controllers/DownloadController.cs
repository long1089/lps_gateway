using LpsGateway.Data;
using LpsGateway.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LpsGateway.Controllers;

/// <summary>
/// 文件下载控制器
/// </summary>
[Authorize]
[Route("api/[controller]")]
[ApiController]
public class DownloadController : ControllerBase
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
    /// 手动触发文件下载
    /// </summary>
    /// <param name="reportTypeId">报表类型ID</param>
    /// <returns>操作结果</returns>
    [HttpPost("trigger/{reportTypeId}")]
    [Authorize(Roles = "Admin,Operator")]
    public async Task<IActionResult> TriggerDownload(int reportTypeId)
    {
        try
        {
            var reportType = await _reportTypeRepository.GetByIdAsync(reportTypeId);
            if (reportType == null)
            {
                return NotFound(new { success = false, message = "报表类型不存在" });
            }

            if (!reportType.Enabled)
            {
                return BadRequest(new { success = false, message = "报表类型已禁用" });
            }

            await _scheduleManager.TriggerDownloadAsync(reportTypeId);

            _logger.LogInformation("手动触发下载: ReportTypeId={ReportTypeId}, User={User}", 
                reportTypeId, User.Identity?.Name);

            return Ok(new 
            { 
                success = true, 
                message = "下载任务已触发",
                reportTypeId = reportTypeId,
                reportTypeName = reportType.Name
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "触发下载失败: ReportTypeId={ReportTypeId}", reportTypeId);
            return StatusCode(500, new { success = false, message = "触发下载失败", error = ex.Message });
        }
    }

    /// <summary>
    /// 重新加载所有调度
    /// </summary>
    /// <returns>操作结果</returns>
    [HttpPost("reload-schedules")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ReloadSchedules()
    {
        try
        {
            await _scheduleManager.ReloadSchedulesAsync();

            _logger.LogInformation("重新加载调度: User={User}", User.Identity?.Name);

            return Ok(new { success = true, message = "调度已重新加载" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "重新加载调度失败");
            return StatusCode(500, new { success = false, message = "重新加载调度失败", error = ex.Message });
        }
    }
}
