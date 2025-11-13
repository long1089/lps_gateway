using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using LpsGateway.Services;

namespace LpsGateway.Controllers;

/// <summary>
/// 首页控制器
/// </summary>
[Authorize]
public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly IDashboardService _dashboardService;

    public HomeController(
        ILogger<HomeController> logger,
        IDashboardService dashboardService)
    {
        _logger = logger;
        _dashboardService = dashboardService;
    }

    /// <summary>
    /// 首页 - 显示仪表盘
    /// </summary>
    public async Task<IActionResult> Index()
    {
        try
        {
            var dashboardData = await _dashboardService.GetDashboardDataAsync();
            return View(dashboardData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取仪表盘数据失败");
            return View(new Models.DashboardViewModel());
        }
    }

    /// <summary>
    /// 错误页面
    /// </summary>
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View();
    }
}
