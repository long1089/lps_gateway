using SqlSugar;

namespace LpsGateway.Data.Models;

/// <summary>
/// 调度配置实体
/// </summary>
[SugarTable("schedules")]
public class Schedule
{
    /// <summary>
    /// 调度ID
    /// </summary>
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    /// <summary>
    /// 报表类型ID
    /// </summary>
    [SugarColumn(IsNullable = false, ColumnName = "report_type_id")]
    public int ReportTypeId { get; set; }

    /// <summary>
    /// 调度类型 (daily/monthly/cron)
    /// </summary>
    [SugarColumn(Length = 20, IsNullable = false, ColumnName = "schedule_type")]
    public string ScheduleType { get; set; } = string.Empty;

    /// <summary>
    /// 时间点列表 JSON，如 ["08:00","11:15"]
    /// </summary>
    [SugarColumn(IsNullable = true, IsJson = true)]
    public List<string>? Times { get; set; }

    /// <summary>
    /// 月份中的日期 JSON，如 [1,10,20]
    /// </summary>
    [SugarColumn(IsNullable = true, IsJson = true, ColumnName = "month_days")]
    public List<int>? MonthDays { get; set; }

    /// <summary>
    /// Cron表达式
    /// </summary>
    [SugarColumn(Length = 100, IsNullable = true, ColumnName = "cron_expression")]
    public string? CronExpression { get; set; }

    /// <summary>
    /// 时区
    /// </summary>
    [SugarColumn(Length = 50, IsNullable = false)]
    public string Timezone { get; set; } = "UTC";

    /// <summary>
    /// 是否启用
    /// </summary>
    [SugarColumn(IsNullable = false)]
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 创建时间
    /// </summary>
    [SugarColumn(IsNullable = false, ColumnName = "created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// 更新时间
    /// </summary>
    [SugarColumn(IsNullable = false, ColumnName = "updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// 导航属性：报表类型
    /// </summary>
    [SugarColumn(IsIgnore = true)]
    public ReportType? ReportType { get; set; }
}
