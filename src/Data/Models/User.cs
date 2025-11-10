using SqlSugar;

namespace LpsGateway.Data.Models;

/// <summary>
/// 用户实体
/// </summary>
[SugarTable("users")]
public class User
{
    /// <summary>
    /// 用户ID
    /// </summary>
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    /// <summary>
    /// 用户名
    /// </summary>
    [SugarColumn(Length = 50, IsNullable = false)]
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// 密码哈希
    /// </summary>
    [SugarColumn(Length = 255, IsNullable = false, ColumnName = "password_hash")]
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>
    /// 角色 (Admin/Operator)
    /// </summary>
    [SugarColumn(Length = 20, IsNullable = false)]
    public string Role { get; set; } = "Operator";

    /// <summary>
    /// 账户是否启用
    /// </summary>
    [SugarColumn(IsNullable = false)]
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 创建时间
    /// </summary>
    [SugarColumn(IsNullable = false, ColumnName = "created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 更新时间
    /// </summary>
    [SugarColumn(IsNullable = false, ColumnName = "updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
