using SqlSugar;

namespace LpsGateway.Data.Models;

[SugarTable("RECEIVED_EFILES")]
public class ReceivedEfile
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    [SugarColumn(Length = 100)]
    public string CommonAddr { get; set; } = string.Empty;

    [SugarColumn(Length = 50)]
    public string TypeId { get; set; } = string.Empty;

    [SugarColumn(Length = 100)]
    public string FileName { get; set; } = string.Empty;

    public DateTime ReceivedAt { get; set; }

    public int FileSize { get; set; }

    [SugarColumn(Length = 20)]
    public string Status { get; set; } = "SUCCESS";

    [SugarColumn(Length = 500, IsNullable = true)]
    public string? ErrorMessage { get; set; }
}
