using LpsGateway.Models;
using LpsGateway.Services;
using Microsoft.AspNetCore.Mvc;

namespace LpsGateway.Controllers;

/// <summary>
/// 认证控制器
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthService authService, ILogger<AuthController> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    /// <summary>
    /// 用户登录
    /// </summary>
    [HttpPost("login")]
    public async Task<ActionResult<ApiResponse<LoginResponse>>> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new ApiResponse<LoginResponse>
            {
                Success = false,
                Message = "用户名和密码不能为空"
            });
        }

        var (success, token, user) = await _authService.LoginAsync(request.Username, request.Password);

        if (!success || user == null || token == null)
        {
            return Unauthorized(new ApiResponse<LoginResponse>
            {
                Success = false,
                Message = "用户名或密码错误"
            });
        }

        var response = new LoginResponse
        {
            Token = token,
            Username = user.Username,
            Role = user.Role,
            ExpiresAt = DateTime.UtcNow.AddMinutes(480)
        };

        return Ok(new ApiResponse<LoginResponse>
        {
            Success = true,
            Data = response
        });
    }
}
