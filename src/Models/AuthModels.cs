namespace LpsGateway.Models;

/// <summary>
/// 登录请求DTO
/// </summary>
public class LoginRequest
{
    /// <summary>
    /// 用户名
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// 密码
    /// </summary>
    public string Password { get; set; } = string.Empty;
}

/// <summary>
/// 登录响应DTO
/// </summary>
public class LoginResponse
{
    /// <summary>
    /// JWT令牌
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// 用户名
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// 角色
    /// </summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// 过期时间
    /// </summary>
    public DateTime ExpiresAt { get; set; }
}
