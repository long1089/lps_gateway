using LpsGateway.Data;
using LpsGateway.Data.Models;
using LpsGateway.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LpsGateway.Controllers;

/// <summary>
/// SFTP配置控制器
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SftpConfigsController : ControllerBase
{
    private readonly ISftpConfigRepository _repository;
    private readonly ILogger<SftpConfigsController> _logger;

    public SftpConfigsController(ISftpConfigRepository repository, ILogger<SftpConfigsController> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <summary>
    /// 获取所有SFTP配置
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<SftpConfig>>>> GetAll([FromQuery] bool? enabled = null)
    {
        try
        {
            var items = await _repository.GetAllAsync(enabled);
            
            // 隐藏敏感信息
            foreach (var item in items)
            {
                item.PasswordEncrypted = null;
                item.KeyPassphraseEncrypted = null;
            }
            
            return Ok(new ApiResponse<List<SftpConfig>>
            {
                Success = true,
                Data = items
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取SFTP配置列表失败");
            return StatusCode(500, new ApiResponse<List<SftpConfig>>
            {
                Success = false,
                Message = "获取SFTP配置列表失败"
            });
        }
    }

    /// <summary>
    /// 根据ID获取SFTP配置
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<SftpConfig>>> GetById(int id)
    {
        try
        {
            var item = await _repository.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound(new ApiResponse<SftpConfig>
                {
                    Success = false,
                    Message = "SFTP配置不存在"
                });
            }

            // 隐藏敏感信息
            item.PasswordEncrypted = null;
            item.KeyPassphraseEncrypted = null;

            return Ok(new ApiResponse<SftpConfig>
            {
                Success = true,
                Data = item
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取SFTP配置失败: {Id}", id);
            return StatusCode(500, new ApiResponse<SftpConfig>
            {
                Success = false,
                Message = "获取SFTP配置失败"
            });
        }
    }

    /// <summary>
    /// 创建SFTP配置
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ApiResponse<SftpConfig>>> Create([FromBody] SftpConfigDto dto)
    {
        try
        {
            var config = new SftpConfig
            {
                Name = dto.Name,
                Host = dto.Host,
                Port = dto.Port,
                Username = dto.Username,
                AuthType = dto.AuthType,
                BasePathTemplate = dto.BasePathTemplate,
                ConcurrencyLimit = dto.ConcurrencyLimit,
                TimeoutSec = dto.TimeoutSec,
                Enabled = dto.Enabled
            };

            // 简单加密（生产环境应使用更强的加密方式）
            if (!string.IsNullOrEmpty(dto.Password))
            {
                config.PasswordEncrypted = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(dto.Password));
            }
            
            if (!string.IsNullOrEmpty(dto.KeyPath))
            {
                config.KeyPath = dto.KeyPath;
            }
            
            if (!string.IsNullOrEmpty(dto.KeyPassphrase))
            {
                config.KeyPassphraseEncrypted = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(dto.KeyPassphrase));
            }

            var created = await _repository.CreateAsync(config);
            
            // 隐藏敏感信息
            created.PasswordEncrypted = null;
            created.KeyPassphraseEncrypted = null;

            return CreatedAtAction(nameof(GetById), new { id = created.Id }, new ApiResponse<SftpConfig>
            {
                Success = true,
                Data = created
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建SFTP配置失败");
            return StatusCode(500, new ApiResponse<SftpConfig>
            {
                Success = false,
                Message = "创建SFTP配置失败"
            });
        }
    }

    /// <summary>
    /// 更新SFTP配置
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ApiResponse<SftpConfig>>> Update(int id, [FromBody] SftpConfigDto dto)
    {
        try
        {
            var existing = await _repository.GetByIdAsync(id);
            if (existing == null)
            {
                return NotFound(new ApiResponse<SftpConfig>
                {
                    Success = false,
                    Message = "SFTP配置不存在"
                });
            }

            existing.Name = dto.Name;
            existing.Host = dto.Host;
            existing.Port = dto.Port;
            existing.Username = dto.Username;
            existing.AuthType = dto.AuthType;
            existing.BasePathTemplate = dto.BasePathTemplate;
            existing.ConcurrencyLimit = dto.ConcurrencyLimit;
            existing.TimeoutSec = dto.TimeoutSec;
            existing.Enabled = dto.Enabled;

            // 如果提供了新密码，则更新
            if (!string.IsNullOrEmpty(dto.Password))
            {
                existing.PasswordEncrypted = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(dto.Password));
            }
            
            if (!string.IsNullOrEmpty(dto.KeyPath))
            {
                existing.KeyPath = dto.KeyPath;
            }
            
            if (!string.IsNullOrEmpty(dto.KeyPassphrase))
            {
                existing.KeyPassphraseEncrypted = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(dto.KeyPassphrase));
            }

            await _repository.UpdateAsync(existing);
            
            // 隐藏敏感信息
            existing.PasswordEncrypted = null;
            existing.KeyPassphraseEncrypted = null;

            return Ok(new ApiResponse<SftpConfig>
            {
                Success = true,
                Data = existing
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新SFTP配置失败: {Id}", id);
            return StatusCode(500, new ApiResponse<SftpConfig>
            {
                Success = false,
                Message = "更新SFTP配置失败"
            });
        }
    }

    /// <summary>
    /// 删除SFTP配置
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
                    Message = "SFTP配置不存在"
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
            _logger.LogError(ex, "删除SFTP配置失败: {Id}", id);
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Message = "删除SFTP配置失败"
            });
        }
    }
}
