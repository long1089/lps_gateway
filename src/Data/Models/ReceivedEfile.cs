using SqlSugar;

namespace LpsGateway.Data.Models;

/// <summary>
/// 接收的 E 文件记录模型
/// </summary>
[SugarTable("RECEIVED_EFILES")]
public class ReceivedEfile
{
    /// <summary>
    /// 主键标识
    /// </summary>
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    /// <summary>
    /// 公共地址
    /// </summary>
    [SugarColumn(Length = 100)]
    public string CommonAddr { get; set; } = string.Empty;

    /// <summary>
    /// 类型标识
    /// </summary>
    [SugarColumn(Length = 50)]
    public string TypeId { get; set; } = string.Empty;

    /// <summary>
    /// 文件名
    /// </summary>
    [SugarColumn(Length = 100)]
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// 接收时间
    /// </summary>
    public DateTime ReceivedAt { get; set; }

    /// <summary>
    /// 文件大小（字节）
    /// </summary>
    public int FileSize { get; set; }

    /// <summary>
    /// 处理状态（SUCCESS/ERROR）
    /// </summary>
    [SugarColumn(Length = 20)]
    public string Status { get; set; } = "SUCCESS";

    /// <summary>
    /// 错误消息（如果有）
    /// </summary>
    [SugarColumn(Length = 500, IsNullable = true)]
    public string? ErrorMessage { get; set; }
}
