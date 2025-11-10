using SqlSugar;

namespace LpsGateway.Data.Models;

/// <summary>
/// 报表类型配置实体
/// </summary>
[SugarTable("report_types")]
public class ReportType
{
    /// <summary>
    /// 报表类型ID
    /// </summary>
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    /// <summary>
    /// 报表类型编码
    /// </summary>
    [SugarColumn(Length = 20, IsNullable = false)]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// 报表类型名称
    /// </summary>
    [SugarColumn(Length = 100, IsNullable = false)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 描述
    /// </summary>
    [SugarColumn(IsNullable = true, ColumnDataType = "text")]
    public string? Description { get; set; }

    /// <summary>
    /// 默认SFTP配置ID
    /// </summary>
    [SugarColumn(IsNullable = true, ColumnName = "default_sftp_config_id")]
    public int? DefaultSftpConfigId { get; set; }

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

    /// <summary>
    /// 导航属性：默认SFTP配置
    /// </summary>
    [SugarColumn(IsIgnore = true)]
    public SftpConfig? DefaultSftpConfig { get; set; }

    /// <summary>
    /// 导航属性：调度配置列表
    /// </summary>
    [SugarColumn(IsIgnore = true)]
    public List<Schedule>? Schedules { get; set; }
}
