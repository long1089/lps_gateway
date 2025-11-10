using LpsGateway.Data;
using LpsGateway.Data.Models;
using LpsGateway.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace LpsGateway.Controllers;

/// <summary>
/// 报表类型配置控制器
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ReportTypesController : ControllerBase
{
    private readonly IReportTypeRepository _repository;
    private readonly ILogger<ReportTypesController> _logger;

    public ReportTypesController(IReportTypeRepository repository, ILogger<ReportTypesController> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <summary>
    /// 获取所有报表类型
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<ReportType>>>> GetAll([FromQuery] bool? enabled = null)
    {
        try
        {
            var items = await _repository.GetAllAsync(enabled);
            return Ok(new ApiResponse<List<ReportType>>
            {
                Success = true,
                Data = items
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取报表类型列表失败");
            return StatusCode(500, new ApiResponse<List<ReportType>>
            {
                Success = false,
                Message = "获取报表类型列表失败"
            });
        }
    }

    /// <summary>
    /// 根据ID获取报表类型
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<ReportType>>> GetById(int id)
    {
        try
        {
            var item = await _repository.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound(new ApiResponse<ReportType>
                {
                    Success = false,
                    Message = "报表类型不存在"
                });
            }

            return Ok(new ApiResponse<ReportType>
            {
                Success = true,
                Data = item
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取报表类型失败: {Id}", id);
            return StatusCode(500, new ApiResponse<ReportType>
            {
                Success = false,
                Message = "获取报表类型失败"
            });
        }
    }

    /// <summary>
    /// 创建报表类型
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ApiResponse<ReportType>>> Create([FromBody] ReportTypeDto dto)
    {
        try
        {
            // 验证编码是否已存在
            if (await _repository.ExistsAsync(dto.Code))
            {
                return BadRequest(new ApiResponse<ReportType>
                {
                    Success = false,
                    Message = $"报表类型编码 '{dto.Code}' 已存在"
                });
            }

            var reportType = new ReportType
            {
                Code = dto.Code,
                Name = dto.Name,
                Description = dto.Description,
                DefaultSftpConfigId = dto.DefaultSftpConfigId,
                Enabled = dto.Enabled
            };

            var created = await _repository.CreateAsync(reportType);

            return CreatedAtAction(nameof(GetById), new { id = created.Id }, new ApiResponse<ReportType>
            {
                Success = true,
                Data = created
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建报表类型失败");
            return StatusCode(500, new ApiResponse<ReportType>
            {
                Success = false,
                Message = "创建报表类型失败"
            });
        }
    }

    /// <summary>
    /// 更新报表类型
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ApiResponse<ReportType>>> Update(int id, [FromBody] ReportTypeDto dto)
    {
        try
        {
            var existing = await _repository.GetByIdAsync(id);
            if (existing == null)
            {
                return NotFound(new ApiResponse<ReportType>
                {
                    Success = false,
                    Message = "报表类型不存在"
                });
            }

            // 验证编码是否已被其他记录使用
            if (await _repository.ExistsAsync(dto.Code, id))
            {
                return BadRequest(new ApiResponse<ReportType>
                {
                    Success = false,
                    Message = $"报表类型编码 '{dto.Code}' 已被其他记录使用"
                });
            }

            existing.Code = dto.Code;
            existing.Name = dto.Name;
            existing.Description = dto.Description;
            existing.DefaultSftpConfigId = dto.DefaultSftpConfigId;
            existing.Enabled = dto.Enabled;

            await _repository.UpdateAsync(existing);

            return Ok(new ApiResponse<ReportType>
            {
                Success = true,
                Data = existing
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新报表类型失败: {Id}", id);
            return StatusCode(500, new ApiResponse<ReportType>
            {
                Success = false,
                Message = "更新报表类型失败"
            });
        }
    }

    /// <summary>
    /// 删除报表类型
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
                    Message = "报表类型不存在"
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
            _logger.LogError(ex, "删除报表类型失败: {Id}", id);
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Message = "删除报表类型失败"
            });
        }
    }
}
