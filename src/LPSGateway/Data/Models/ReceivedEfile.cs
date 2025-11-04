using System;
using SqlSugar;

namespace LPSGateway.Data.Models
{
    /// <summary>
    /// Model for tracking received E-files
    /// </summary>
    [SugarTable("received_efiles")]
    public class ReceivedEfile
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public long Id { get; set; }

        [SugarColumn(Length = 255, IsNullable = false)]
        public string SourceIdentifier { get; set; } = string.Empty;

        [SugarColumn(IsNullable = false)]
        public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;

        [SugarColumn(IsNullable = false)]
        public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;

        [SugarColumn(Length = 50)]
        public string? Status { get; set; } = "processed";
    }
}
