using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LPSGateway.Data.Models;
using Microsoft.Extensions.Logging;
using SqlSugar;

namespace LPSGateway.Data
{
    /// <summary>
    /// Repository implementation using SqlSugar for OpenGauss/PostgreSQL
    /// </summary>
    public class EFileRepository : IEFileRepository
    {
        private readonly ISqlSugarClient _db;
        private readonly ILogger<EFileRepository> _logger;

        public EFileRepository(ISqlSugarClient db, ILogger<EFileRepository> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<bool> IsFileProcessedAsync(string sourceIdentifier)
        {
            var count = await _db.Queryable<ReceivedEfile>()
                .Where(f => f.SourceIdentifier == sourceIdentifier)
                .CountAsync();

            return count > 0;
        }

        public async Task MarkFileProcessedAsync(string sourceIdentifier)
        {
            var record = new ReceivedEfile
            {
                SourceIdentifier = sourceIdentifier,
                ReceivedAt = DateTime.UtcNow,
                ProcessedAt = DateTime.UtcNow,
                Status = "processed"
            };

            await _db.Insertable(record).ExecuteCommandAsync();
        }

        public async Task UpsertInfoTableAsync(string tableName, Dictionary<string, string> headerData)
        {
            if (headerData == null || headerData.Count == 0)
                return;

            try
            {
                var infoTableName = $"{tableName}_info";

                // Check if table exists, create if not
                if (!_db.DbMaintenance.IsAnyTable(infoTableName))
                {
                    _logger.LogInformation($"Creating info table: {infoTableName}");
                    
                    // Create a simple key-value table for header info
                    var sql = $@"
                        CREATE TABLE IF NOT EXISTS {infoTableName} (
                            id SERIAL PRIMARY KEY,
                            key VARCHAR(255) NOT NULL,
                            value TEXT,
                            updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                        )";
                    await _db.Ado.ExecuteCommandAsync(sql);
                }

                // Upsert header data
                foreach (var kvp in headerData)
                {
                    var existingSql = $"SELECT COUNT(*) FROM {infoTableName} WHERE key = @key";
                    var exists = await _db.Ado.GetIntAsync(existingSql, new { key = kvp.Key });

                    if (exists > 0)
                    {
                        var updateSql = $"UPDATE {infoTableName} SET value = @value, updated_at = @updated WHERE key = @key";
                        await _db.Ado.ExecuteCommandAsync(updateSql, new { key = kvp.Key, value = kvp.Value, updated = DateTime.UtcNow });
                    }
                    else
                    {
                        var insertSql = $"INSERT INTO {infoTableName} (key, value, updated_at) VALUES (@key, @value, @updated)";
                        await _db.Ado.ExecuteCommandAsync(insertSql, new { key = kvp.Key, value = kvp.Value, updated = DateTime.UtcNow });
                    }
                }

                _logger.LogInformation($"Upserted {headerData.Count} header records to {infoTableName}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error upserting info table {tableName}");
                throw;
            }
        }

        public async Task InsertRecordsAsync(string tableName, List<Dictionary<string, object?>> records)
        {
            if (records == null || records.Count == 0)
                return;

            try
            {
                var dataTableName = $"{tableName}_data";

                // Check if table exists, create if not
                if (!_db.DbMaintenance.IsAnyTable(dataTableName))
                {
                    _logger.LogInformation($"Creating data table: {dataTableName}");

                    // Get column names from first record
                    var firstRecord = records.First();
                    var columns = string.Join(", ", firstRecord.Keys.Select(k => $"{k} TEXT"));

                    var sql = $@"
                        CREATE TABLE IF NOT EXISTS {dataTableName} (
                            id SERIAL PRIMARY KEY,
                            {columns},
                            created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                        )";
                    await _db.Ado.ExecuteCommandAsync(sql);
                }

                // Insert records
                foreach (var record in records)
                {
                    var columns = string.Join(", ", record.Keys);
                    var parameters = string.Join(", ", record.Keys.Select(k => $"@{k}"));
                    var insertSql = $"INSERT INTO {dataTableName} ({columns}, created_at) VALUES ({parameters}, @created)";

                    var paramDict = new Dictionary<string, object?>(record);
                    paramDict["created"] = DateTime.UtcNow;

                    await _db.Ado.ExecuteCommandAsync(insertSql, paramDict);
                }

                _logger.LogInformation($"Inserted {records.Count} records to {dataTableName}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error inserting records to {tableName}");
                throw;
            }
        }
    }
}
