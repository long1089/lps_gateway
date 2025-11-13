using LpsGateway.Data;
using LpsGateway.Data.Models;
using SqlSugar;

namespace LpsGateway.Services;

/// <summary>
/// 认证服务实现
/// </summary>
public class AuthService : IAuthService
{
    private readonly ISqlSugarClient _db;
    private readonly ILogger<AuthService> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuditLogRepository _auditLogRepository;

    public AuthService(ISqlSugarClient db, ILogger<AuthService> logger,IHttpContextAccessor httpContextAccessor,IAuditLogRepository auditLogRepository)
    {
        _db = db;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
        _auditLogRepository = auditLogRepository;
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

            _logger.LogInformation("用户登录成功: {Username}", sanitizedUsername);

            await _auditLogRepository.AddLogAsync("LOGIN", user.Id);

            // MVC模式不需要返回token，返回null即可
            return (true, null, user);
        }
        catch (Exception ex)
        {
            var sanitizedUsername = LogHelper.SanitizeForLog(username, 50);
            _logger.LogError(ex, "登录失败: {Username}", sanitizedUsername);
            return (false, null, null);
        }
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
