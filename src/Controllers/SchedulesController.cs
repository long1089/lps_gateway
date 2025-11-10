using LpsGateway.Data;
using LpsGateway.Data.Models;
using LpsGateway.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace LpsGateway.Controllers;

/// <summary>
/// 调度配置控制器
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SchedulesController : ControllerBase
{
    private readonly IScheduleRepository _repository;
    private readonly ILogger<SchedulesController> _logger;

    public SchedulesController(IScheduleRepository repository, ILogger<SchedulesController> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <summary>
    /// 获取所有调度配置
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<Schedule>>>> GetAll([FromQuery] bool? enabled = null)
    {
        try
        {
            var items = await _repository.GetAllAsync(enabled);
            return Ok(new ApiResponse<List<Schedule>>
            {
                Success = true,
                Data = items
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取调度配置列表失败");
            return StatusCode(500, new ApiResponse<List<Schedule>>
            {
                Success = false,
                Message = "获取调度配置列表失败"
            });
        }
    }

    /// <summary>
    /// 根据报表类型获取调度配置
    /// </summary>
    [HttpGet("reporttype/{reportTypeId}")]
    public async Task<ActionResult<ApiResponse<List<Schedule>>>> GetByReportType(int reportTypeId)
    {
        try
        {
            var items = await _repository.GetByReportTypeAsync(reportTypeId);
            return Ok(new ApiResponse<List<Schedule>>
            {
                Success = true,
                Data = items
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取报表类型调度配置失败: {ReportTypeId}", reportTypeId);
            return StatusCode(500, new ApiResponse<List<Schedule>>
            {
                Success = false,
                Message = "获取调度配置失败"
            });
        }
    }

    /// <summary>
    /// 根据ID获取调度配置
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<Schedule>>> GetById(int id)
    {
        try
        {
            var item = await _repository.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound(new ApiResponse<Schedule>
                {
                    Success = false,
                    Message = "调度配置不存在"
                });
            }

            return Ok(new ApiResponse<Schedule>
            {
                Success = true,
                Data = item
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取调度配置失败: {Id}", id);
            return StatusCode(500, new ApiResponse<Schedule>
            {
                Success = false,
                Message = "获取调度配置失败"
            });
        }
    }

    /// <summary>
    /// 创建调度配置
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ApiResponse<Schedule>>> Create([FromBody] ScheduleDto dto)
    {
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

            var created = await _repository.CreateAsync(schedule);

            return CreatedAtAction(nameof(GetById), new { id = created.Id }, new ApiResponse<Schedule>
            {
                Success = true,
                Data = created
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建调度配置失败");
            return StatusCode(500, new ApiResponse<Schedule>
            {
                Success = false,
                Message = "创建调度配置失败"
            });
        }
    }

    /// <summary>
    /// 更新调度配置
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ApiResponse<Schedule>>> Update(int id, [FromBody] ScheduleDto dto)
    {
        try
        {
            var existing = await _repository.GetByIdAsync(id);
            if (existing == null)
            {
                return NotFound(new ApiResponse<Schedule>
                {
                    Success = false,
                    Message = "调度配置不存在"
                });
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

            return Ok(new ApiResponse<Schedule>
            {
                Success = true,
                Data = existing
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新调度配置失败: {Id}", id);
            return StatusCode(500, new ApiResponse<Schedule>
            {
                Success = false,
                Message = "更新调度配置失败"
            });
        }
    }

    /// <summary>
    /// 删除调度配置
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(int id)
    {
        try
        {
            var existing = await _repository.GetByIdAsync(id);
            if (existing == null)
            {
                return NotFound(new ApiResponse<object>
                {
                    Success = false,
                    Message = "调度配置不存在"
                });
            }

            await _repository.DeleteAsync(id);

            return Ok(new ApiResponse<object>
            {
                Success = true,
                Message = "删除成功"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除调度配置失败: {Id}", id);
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Message = "删除调度配置失败"
            });
        }
    }
}
