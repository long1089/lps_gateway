using LpsGateway.Data.Models;

namespace LpsGateway.Services;

/// <summary>
/// 认证服务接口
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// 用户登录
    /// </summary>
    Task<(bool Success, string? Token, User? User)> LoginAsync(string username, string password);
    
    /// <summary>
    /// 生成JWT令牌
    /// </summary>
    string GenerateToken(User user);
    
    /// <summary>
    /// 验证密码
    /// </summary>
    bool VerifyPassword(string password, string passwordHash);
    
    /// <summary>
    /// 哈希密码
    /// </summary>
    string HashPassword(string password);
}
