using LpsGateway.Data;
using LpsGateway.Data.Models;
using LpsGateway.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LpsGateway.Controllers;

/// <summary>
/// 报表类型配置控制器 - MVC模式
/// </summary>
[Authorize]
public class ReportTypesController : Controller
{
    private readonly IReportTypeRepository _repository;
    private readonly ISftpConfigRepository _sftpConfigRepository;
    private readonly ILogger<ReportTypesController> _logger;

    public ReportTypesController(
        IReportTypeRepository repository, 
        ISftpConfigRepository sftpConfigRepository,
        ILogger<ReportTypesController> logger)
    {
        _repository = repository;
        _sftpConfigRepository = sftpConfigRepository;
        _logger = logger;
    }

    /// <summary>
    /// 列表页面
    /// </summary>
    public async Task<IActionResult> Index()
    {
        var items = await _repository.GetAllAsync();
        
        // 加载SFTP配置信息用于显示
        var sftpConfigs = await _sftpConfigRepository.GetAllAsync();
        var sftpConfigDict = sftpConfigs.ToDictionary(s => s.Id, s => s.Name);
        ViewBag.SftpConfigNames = sftpConfigDict;
        
        return View(items);
    }

    /// <summary>
    /// 创建页面
    /// </summary>
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create()
    {
        var sftpConfigs = await _sftpConfigRepository.GetAllAsync(enabled: true);
        ViewBag.SftpConfigs = sftpConfigs;
        return View();
    }

    /// <summary>
    /// 创建提交
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create(ReportTypeDto dto)
    {
        if (!ModelState.IsValid)
        {
            var sftpConfigs = await _sftpConfigRepository.GetAllAsync(enabled: true);
            ViewBag.SftpConfigs = sftpConfigs;
            return View(dto);
        }

        try
        {
            // 验证编码是否已存在
            if (await _repository.ExistsAsync(dto.Code))
            {
                ModelState.AddModelError("Code", $"报表类型编码 '{dto.Code}' 已存在");
                var sftpConfigs = await _sftpConfigRepository.GetAllAsync(enabled: true);
                ViewBag.SftpConfigs = sftpConfigs;
                return View(dto);
            }

            var reportType = new ReportType
            {
                Code = dto.Code,
                Name = dto.Name,
                Description = dto.Description,
                DefaultSftpConfigId = dto.DefaultSftpConfigId,
                PathTemplate = dto.PathTemplate,
                Enabled = dto.Enabled
            };

            await _repository.CreateAsync(reportType);
            TempData["SuccessMessage"] = "报表类型创建成功";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建报表类型失败");
            ModelState.AddModelError(string.Empty, "创建失败，请重试");
            var sftpConfigs = await _sftpConfigRepository.GetAllAsync(enabled: true);
            ViewBag.SftpConfigs = sftpConfigs;
            return View(dto);
        }
    }

    /// <summary>
    /// 编辑页面
    /// </summary>
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Edit(int id)
    {
        var reportType = await _repository.GetByIdAsync(id);
        if (reportType == null)
        {
            return NotFound();
        }

        var dto = new ReportTypeDto
        {
            Id = reportType.Id,
            Code = reportType.Code,
            Name = reportType.Name,
            Description = reportType.Description,
            DefaultSftpConfigId = reportType.DefaultSftpConfigId,
            PathTemplate = reportType.PathTemplate,
            Enabled = reportType.Enabled
        };

        var sftpConfigs = await _sftpConfigRepository.GetAllAsync(enabled: true);
        ViewBag.SftpConfigs = sftpConfigs;
        return View(dto);
    }

    /// <summary>
    /// 编辑提交
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Edit(int id, ReportTypeDto dto)
    {
        if (id != dto.Id)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            var sftpConfigs = await _sftpConfigRepository.GetAllAsync(enabled: true);
            ViewBag.SftpConfigs = sftpConfigs;
            return View(dto);
        }

        try
        {
            var existing = await _repository.GetByIdAsync(id);
            if (existing == null)
            {
                return NotFound();
            }

            // 验证编码是否已被其他记录使用
            if (await _repository.ExistsAsync(dto.Code, id))
            {
                ModelState.AddModelError("Code", $"报表类型编码 '{dto.Code}' 已被其他记录使用");
                var sftpConfigs = await _sftpConfigRepository.GetAllAsync(enabled: true);
                ViewBag.SftpConfigs = sftpConfigs;
                return View(dto);
            }

            existing.Code = dto.Code;
            existing.Name = dto.Name;
            existing.Description = dto.Description;
            existing.DefaultSftpConfigId = dto.DefaultSftpConfigId;
            existing.PathTemplate = dto.PathTemplate;
            existing.Enabled = dto.Enabled;

            await _repository.UpdateAsync(existing);
            TempData["SuccessMessage"] = "报表类型更新成功";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新报表类型失败: {Id}", id);
            ModelState.AddModelError(string.Empty, "更新失败，请重试");
            var sftpConfigs = await _sftpConfigRepository.GetAllAsync(enabled: true);
            ViewBag.SftpConfigs = sftpConfigs;
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
            TempData["SuccessMessage"] = "报表类型删除成功";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除报表类型失败: {Id}", id);
            TempData["ErrorMessage"] = "删除失败，请重试";
        }

        return RedirectToAction(nameof(Index));
    }
}
