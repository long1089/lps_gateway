using SqlSugar;

namespace LpsGateway.Data.Models;

/// <summary>
/// SFTP配置实体
/// </summary>
[SugarTable("sftp_configs")]
public class SftpConfig
{
    /// <summary>
    /// 配置ID
    /// </summary>
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    /// <summary>
    /// 配置名称
    /// </summary>
    [SugarColumn(Length = 100, IsNullable = false)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// SFTP主机地址
    /// </summary>
    [SugarColumn(Length = 255, IsNullable = false)]
    public string Host { get; set; } = string.Empty;

    /// <summary>
    /// SFTP端口
    /// </summary>
    [SugarColumn(IsNullable = false)]
    public int Port { get; set; } = 22;

    /// <summary>
    /// 用户名
    /// </summary>
    [SugarColumn(Length = 100, IsNullable = false)]
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// 认证类型 (password/key)
    /// </summary>
    [SugarColumn(Length = 20, IsNullable = false, ColumnName = "auth_type")]
    public string AuthType { get; set; } = "password";

    /// <summary>
    /// 加密的密码
    /// </summary>
    [SugarColumn(IsNullable = true, ColumnDataType = "text", ColumnName = "password_encrypted")]
    public string? PasswordEncrypted { get; set; }

    /// <summary>
    /// 私钥文件路径
    /// </summary>
    [SugarColumn(Length = 500, IsNullable = true, ColumnName = "key_path")]
    public string? KeyPath { get; set; }

    /// <summary>
    /// 加密的私钥密码短语
    /// </summary>
    [SugarColumn(IsNullable = true, ColumnDataType = "text", ColumnName = "key_passphrase_encrypted")]
    public string? KeyPassphraseEncrypted { get; set; }

    /// <summary>
    /// 路径模板，支持 {yyyy}/{MM}/{dd}/{HH}/{mm}
    /// </summary>
    [SugarColumn(Length = 500, IsNullable = false, ColumnName = "base_path_template")]
    public string BasePathTemplate { get; set; } = string.Empty;

    /// <summary>
    /// 并发限制
    /// </summary>
    [SugarColumn(IsNullable = false, ColumnName = "concurrency_limit")]
    public int ConcurrencyLimit { get; set; } = 5;

    /// <summary>
    /// 超时时间（秒）
    /// </summary>
    [SugarColumn(IsNullable = false, ColumnName = "timeout_sec")]
    public int TimeoutSec { get; set; } = 30;

    /// <summary>
    /// 是否启用
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
