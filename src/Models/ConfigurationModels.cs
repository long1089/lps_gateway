namespace LpsGateway.Models;

/// <summary>
/// 报表类型创建/更新DTO
/// </summary>
public class ReportTypeDto
{
    public int? Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? DefaultSftpConfigId { get; set; }
    public string? PathTemplate { get; set; }
    public bool Enabled { get; set; } = true;
}

/// <summary>
/// SFTP配置创建/更新DTO
/// </summary>
public class SftpConfigDto
{
    public int? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 22;
    public string Username { get; set; } = string.Empty;
    public string AuthType { get; set; } = "password";
    public string? Password { get; set; }
    public string? KeyPath { get; set; }
    public string? KeyPassphrase { get; set; }
    public int ConcurrencyLimit { get; set; } = 5;
    public int TimeoutSec { get; set; } = 30;
    public bool Enabled { get; set; } = true;
}

/// <summary>
/// 调度配置创建/更新DTO
/// </summary>
public class ScheduleDto
{
    public int? Id { get; set; }
    public int ReportTypeId { get; set; }
    public string ScheduleType { get; set; } = string.Empty;
    public List<string>? Times { get; set; }
    public List<int>? MonthDays { get; set; }
    public string? CronExpression { get; set; }
    public string Timezone { get; set; } = "UTC";
    public bool Enabled { get; set; } = true;
}
