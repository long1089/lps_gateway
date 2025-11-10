using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using LpsGateway.Data.Models;
using Microsoft.IdentityModel.Tokens;
using SqlSugar;

namespace LpsGateway.Services;

/// <summary>
/// 认证服务实现
/// </summary>
public class AuthService : IAuthService
{
    private readonly ISqlSugarClient _db;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthService> _logger;

    public AuthService(ISqlSugarClient db, IConfiguration configuration, ILogger<AuthService> logger)
    {
        _db = db;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<(bool Success, string? Token, User? User)> LoginAsync(string username, string password)
    {
        try
        {
            // 清理用户名以防止日志伪造
            var sanitizedUsername = LogHelper.SanitizeForLog(username, 50);
            
            // 查找用户
            var user = await _db.Queryable<User>()
                .Where(u => u.Username == username)
                .FirstAsync();

            if (user == null)
            {
                _logger.LogWarning("用户不存在: {Username}", sanitizedUsername);
                return (false, null, null);
            }

            if (!user.Enabled)
            {
                _logger.LogWarning("用户已禁用: {Username}", sanitizedUsername);
                return (false, null, null);
            }

            // 验证密码
            if (!VerifyPassword(password, user.PasswordHash))
            {
                _logger.LogWarning("密码错误: {Username}", sanitizedUsername);
                return (false, null, null);
            }

            // 生成令牌
            var token = GenerateToken(user);
            _logger.LogInformation("用户登录成功: {Username}", sanitizedUsername);
            
            return (true, token, user);
        }
        catch (Exception ex)
        {
            var sanitizedUsername = LogHelper.SanitizeForLog(username, 50);
            _logger.LogError(ex, "登录失败: {Username}", sanitizedUsername);
            return (false, null, null);
        }
    }

    public string GenerateToken(User user)
    {
        var jwtSettings = _configuration.GetSection("JwtSettings");
        var secretKey = jwtSettings["SecretKey"] ?? "LpsGateway-Default-Secret-Key-Change-In-Production-Min32Chars!";
        var issuer = jwtSettings["Issuer"] ?? "LpsGateway";
        var audience = jwtSettings["Audience"] ?? "LpsGatewayClients";
        var expirationMinutes = int.Parse(jwtSettings["ExpirationMinutes"] ?? "480");

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.UniqueName, user.Username),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expirationMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public bool VerifyPassword(string password, string passwordHash)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, passwordHash);
        }
        catch
        {
            return false;
        }
    }

    public string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password);
    }
}
