using LpsGateway.Data;
using LpsGateway.Data.Models;
using LpsGateway.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LpsGateway.Controllers;

/// <summary>
/// SFTP配置控制器 - MVC模式
/// </summary>
[Authorize]
public class SftpConfigsController : Controller
{
    private readonly ISftpConfigRepository _repository;
    private readonly ILogger<SftpConfigsController> _logger;

    public SftpConfigsController(ISftpConfigRepository repository, ILogger<SftpConfigsController> logger)
    {
        _repository = repository;
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
    public IActionResult Create()
    {
        return View();
    }

    /// <summary>
    /// 创建提交
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create(SftpConfigDto dto)
    {
        if (!ModelState.IsValid)
        {
            return View(dto);
        }

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

            // 简单加密
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

            await _repository.CreateAsync(config);
            TempData["SuccessMessage"] = "SFTP配置创建成功";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建SFTP配置失败");
            ModelState.AddModelError(string.Empty, "创建失败，请重试");
            return View(dto);
        }
    }

    /// <summary>
    /// 编辑页面
    /// </summary>
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Edit(int id)
    {
        var config = await _repository.GetByIdAsync(id);
        if (config == null)
        {
            return NotFound();
        }

        var dto = new SftpConfigDto
        {
            Id = config.Id,
            Name = config.Name,
            Host = config.Host,
            Port = config.Port,
            Username = config.Username,
            AuthType = config.AuthType,
            BasePathTemplate = config.BasePathTemplate,
            ConcurrencyLimit = config.ConcurrencyLimit,
            TimeoutSec = config.TimeoutSec,
            Enabled = config.Enabled,
            KeyPath = config.KeyPath
        };

        return View(dto);
    }

    /// <summary>
    /// 编辑提交
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Edit(int id, SftpConfigDto dto)
    {
        if (id != dto.Id)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return View(dto);
        }

        try
        {
            var existing = await _repository.GetByIdAsync(id);
            if (existing == null)
            {
                return NotFound();
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
            TempData["SuccessMessage"] = "SFTP配置更新成功";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新SFTP配置失败: {Id}", id);
            ModelState.AddModelError(string.Empty, "更新失败，请重试");
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
            TempData["SuccessMessage"] = "SFTP配置删除成功";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除SFTP配置失败: {Id}", id);
            TempData["ErrorMessage"] = "删除失败，请重试";
        }

        return RedirectToAction(nameof(Index));
    }
}
