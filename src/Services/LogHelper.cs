namespace LpsGateway.Services;

/// <summary>
/// 日志辅助工具类
/// </summary>
public static class LogHelper
{
    /// <summary>
    /// 清理用户输入以防止日志伪造攻击
    /// 移除控制字符和换行符
    /// </summary>
    public static string SanitizeForLog(string? input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }

        // 移除所有控制字符，包括换行符、回车符等
        return new string(input.Where(c => !char.IsControl(c)).ToArray());
    }

    /// <summary>
    /// 清理用户输入以防止日志伪造，并限制长度
    /// </summary>
    public static string SanitizeForLog(string? input, int maxLength)
    {
        var sanitized = SanitizeForLog(input);
        
        if (sanitized.Length > maxLength)
        {
            return sanitized.Substring(0, maxLength) + "...";
        }
        
        return sanitized;
    }
}
