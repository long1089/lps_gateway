using System.Collections.Generic;
using System.Threading.Tasks;

namespace LPSGateway.Data
{
    /// <summary>
    /// Repository interface for E-file data operations
    /// </summary>
    public interface IEFileRepository
    {
        /// <summary>
        /// Check if a file has already been processed
        /// </summary>
        Task<bool> IsFileProcessedAsync(string sourceIdentifier);

        /// <summary>
        /// Mark a file as processed
        /// </summary>
        Task MarkFileProcessedAsync(string sourceIdentifier);

        /// <summary>
        /// Upsert info table (header data)
        /// </summary>
        Task UpsertInfoTableAsync(string tableName, Dictionary<string, string> headerData);

        /// <summary>
        /// Insert data records into a table
        /// </summary>
        Task InsertRecordsAsync(string tableName, List<Dictionary<string, object?>> records);
    }
}
