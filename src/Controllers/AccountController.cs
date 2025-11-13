using System.Security.Claims;
using LpsGateway.Data;
using LpsGateway.Models;
using LpsGateway.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LpsGateway.Controllers;

/// <summary>
/// 账户控制器 - MVC模式
/// </summary>
public class AccountController : Controller
{
    private readonly IAuthService _authService;
    private readonly ILogger<AccountController> _logger;
    private readonly IAuditLogRepository _auditLogRepository;

    public AccountController(IAuthService authService, ILogger<AccountController> logger, IAuditLogRepository auditLogRepository)
    {
        _authService = authService;
        _logger = logger;
        _auditLogRepository = auditLogRepository;
    }

    /// <summary>
    /// 登录页面
    /// </summary>
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Home");
        }

        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    /// <summary>
    /// 处理登录提交
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginRequest model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var (success, _, user) = await _authService.LoginAsync(model.Username, model.Password);

        if (!success || user == null)
        {
            ModelState.AddModelError(string.Empty, "用户名或密码错误");
            return View(model);
        }

        // 创建认证票据
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Role, user.Role)
        };

        var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var authProperties = new AuthenticationProperties
        {
            IsPersistent = true,
            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
        };

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(claimsIdentity),
            authProperties);

        _logger.LogInformation("用户 {Username} 登录成功", user.Username);

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction("Index", "Home");
    }

    /// <summary>
    /// 退出登录
    /// </summary>
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await _auditLogRepository.AddLogAsync("LOGOUT");
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        _logger.LogInformation("用户退出登录");
        return RedirectToAction("Index", "Home");
    }

    /// <summary>
    /// 访问被拒绝页面
    /// </summary>
    [HttpGet]
    public IActionResult AccessDenied()
    {
        return View();
    }
}
