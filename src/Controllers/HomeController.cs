using Microsoft.AspNetCore.Mvc;

namespace LpsGateway.Controllers;

/// <summary>
/// 首页控制器
/// </summary>
public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;

    public HomeController(ILogger<HomeController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 首页
    /// </summary>
    public IActionResult Index()
    {
        return View();
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
