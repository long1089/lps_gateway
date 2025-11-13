using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using LpsGateway.Services;
using Microsoft.AspNetCore.SignalR;
using LpsGateway.Hubs;

namespace LpsGateway.Controllers;

/// <summary>
/// 首页控制器
/// </summary>
[Authorize]
public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly IDashboardService _dashboardService;
    private readonly IHubContext<CommunicationStatusHub> _hubContext;
    private readonly ICommunicationStatusBroadcaster _statusBroadcaster;

    public HomeController(
        ILogger<HomeController> logger,
        IDashboardService dashboardService,
        IHubContext<CommunicationStatusHub> hubContext,
        ICommunicationStatusBroadcaster statusBroadcaster)
    {
        _logger = logger;
        _dashboardService = dashboardService;
        _hubContext = hubContext;
        _statusBroadcaster = statusBroadcaster;
    }

    /// <summary>
    /// 首页 - 显示仪表盘
    /// </summary>
    public async Task<IActionResult> Index()
    {
        try
        {
            var dashboardData = await _dashboardService.GetDashboardDataAsync();
            
            // 当页面加载时，向所有客户端广播当前状态
            _ = Task.Run(async () =>
            {
                var status = await _statusBroadcaster.GetCurrentStatusAsync();
                await _statusBroadcaster.BroadcastStatusUpdateAsync(status);
            });
            
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
