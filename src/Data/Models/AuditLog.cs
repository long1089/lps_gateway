using SqlSugar;

namespace LpsGateway.Data.Models;

/// <summary>
/// 操作审计日志实体
/// </summary>
[SugarTable("audit_logs")]
public class AuditLog
{
    /// <summary>
    /// 日志ID
    /// </summary>
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    /// <summary>
    /// 用户ID
    /// </summary>
    [SugarColumn(IsNullable = true, ColumnName = "user_id")]
    public int? UserId { get; set; }

    /// <summary>
    /// 操作动作
    /// </summary>
    [SugarColumn(Length = 50, IsNullable = false)]
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// 资源标识
    /// </summary>
    [SugarColumn(Length = 100, IsNullable = false)]
    public string Resource { get; set; } = string.Empty;

    /// <summary>
    /// 详细信息 (JSON)
    /// </summary>
    [SugarColumn(IsNullable = true, IsJson = true)]
    public AuditLogFieldChange? Details { get; set; }

    /// <summary>
    /// IP地址
    /// </summary>
    [SugarColumn(Length = 50, IsNullable = true, ColumnName = "ip_address")]
    public string? IpAddress { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    [SugarColumn(IsNullable = false, ColumnName = "created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;


    public class AuditLogFieldChange
    {
        public Dictionary<string, object>? OldValue { get; set; }
        public Dictionary<string, object>? NewValue { get; set; }
    }
}