using LpsGateway.Data;
using LpsGateway.Data.Models;
using LpsGateway.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace LpsGateway.Controllers;

/// <summary>
/// 调度配置控制器 - MVC模式
/// </summary>
[Authorize]
public class SchedulesController : Controller
{
    private readonly IScheduleRepository _repository;
    private readonly IReportTypeRepository _reportTypeRepository;
    private readonly ILogger<SchedulesController> _logger;

    public SchedulesController(
        IScheduleRepository repository,
        IReportTypeRepository reportTypeRepository,
        ILogger<SchedulesController> logger)
    {
        _repository = repository;
        _reportTypeRepository = reportTypeRepository;
        _logger = logger;
    }

    /// <summary>
    /// 列表页面
    /// </summary>
    public async Task<IActionResult> Index()
    {
        var items = await _repository.GetAllAsync();
        return View(items);
    }

    /// <summary>
    /// 创建页面
    /// </summary>
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create()
    {
        ViewBag.ReportTypes = await _reportTypeRepository.GetAllAsync(true);
        return View();
    }

    /// <summary>
    /// 创建提交
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create(ScheduleDto dto)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.ReportTypes = await _reportTypeRepository.GetAllAsync(true);
            return View(dto);
        }

        try
        {
            var schedule = new Schedule
            {
                ReportTypeId = dto.ReportTypeId,
                ScheduleType = dto.ScheduleType,
                Timezone = dto.Timezone,
                Enabled = dto.Enabled
            };

            // 序列化JSON字段
            if (dto.Times != null && dto.Times.Any())
            {
                schedule.Times = JsonSerializer.Serialize(dto.Times);
            }

            if (dto.MonthDays != null && dto.MonthDays.Any())
            {
                schedule.MonthDays = JsonSerializer.Serialize(dto.MonthDays);
            }

            if (!string.IsNullOrEmpty(dto.CronExpression))
            {
                schedule.CronExpression = dto.CronExpression;
            }

            await _repository.CreateAsync(schedule);
            TempData["SuccessMessage"] = "调度配置创建成功";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建调度配置失败");
            ModelState.AddModelError(string.Empty, "创建失败，请重试");
            ViewBag.ReportTypes = await _reportTypeRepository.GetAllAsync(true);
            return View(dto);
        }
    }

    /// <summary>
    /// 编辑页面
    /// </summary>
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Edit(int id)
    {
        var schedule = await _repository.GetByIdAsync(id);
        if (schedule == null)
        {
            return NotFound();
        }

        var dto = new ScheduleDto
        {
            Id = schedule.Id,
            ReportTypeId = schedule.ReportTypeId,
            ScheduleType = schedule.ScheduleType,
            Timezone = schedule.Timezone,
            Enabled = schedule.Enabled,
            CronExpression = schedule.CronExpression
        };

        // 反序列化JSON字段
        if (!string.IsNullOrEmpty(schedule.Times))
        {
            dto.Times = JsonSerializer.Deserialize<List<string>>(schedule.Times);
        }

        if (!string.IsNullOrEmpty(schedule.MonthDays))
        {
            dto.MonthDays = JsonSerializer.Deserialize<List<int>>(schedule.MonthDays);
        }

        ViewBag.ReportTypes = await _reportTypeRepository.GetAllAsync(true);
        return View(dto);
    }

    /// <summary>
    /// 编辑提交
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Edit(int id, ScheduleDto dto)
    {
        if (id != dto.Id)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            ViewBag.ReportTypes = await _reportTypeRepository.GetAllAsync(true);
            return View(dto);
        }

        try
        {
            var existing = await _repository.GetByIdAsync(id);
            if (existing == null)
            {
                return NotFound();
            }

            existing.ReportTypeId = dto.ReportTypeId;
            existing.ScheduleType = dto.ScheduleType;
            existing.Timezone = dto.Timezone;
            existing.Enabled = dto.Enabled;

            // 序列化JSON字段
            if (dto.Times != null && dto.Times.Any())
            {
                existing.Times = JsonSerializer.Serialize(dto.Times);
            }
            else
            {
                existing.Times = null;
            }

            if (dto.MonthDays != null && dto.MonthDays.Any())
            {
                existing.MonthDays = JsonSerializer.Serialize(dto.MonthDays);
            }
            else
            {
                existing.MonthDays = null;
            }

            existing.CronExpression = dto.CronExpression;

            await _repository.UpdateAsync(existing);
            TempData["SuccessMessage"] = "调度配置更新成功";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新调度配置失败: {Id}", id);
            ModelState.AddModelError(string.Empty, "更新失败，请重试");
            ViewBag.ReportTypes = await _reportTypeRepository.GetAllAsync(true);
            return View(dto);
        }
    }

    /// <summary>
    /// 删除
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            var existing = await _repository.GetByIdAsync(id);
            if (existing == null)
            {
                return NotFound();
            }

            await _repository.DeleteAsync(id);
            TempData["SuccessMessage"] = "调度配置删除成功";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除调度配置失败: {Id}", id);
            TempData["ErrorMessage"] = "删除失败，请重试";
        }

        return RedirectToAction(nameof(Index));
    }
}
