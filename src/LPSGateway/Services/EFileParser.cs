using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LPSGateway.Data;
using LPSGateway.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace LPSGateway.Services
{
    /// <summary>
    /// Parser for GBK-encoded E-files with table blocks
    /// </summary>
    public class EFileParser : IEFileParser
    {
        private readonly IEFileRepository _repository;
        private readonly ILogger<EFileParser> _logger;

        public EFileParser(IEFileRepository repository, ILogger<EFileParser> logger)
        {
            _repository = repository;
            _logger = logger;
        }

        public async Task ParseAsync(byte[] data, string sourceIdentifier)
        {
            using var stream = new MemoryStream(data);
            await ParseAsync(stream, sourceIdentifier);
        }

        public async Task ParseAsync(Stream stream, string sourceIdentifier)
        {
            try
            {
                // Check if file already processed
                if (await _repository.IsFileProcessedAsync(sourceIdentifier))
                {
                    _logger.LogInformation($"File {sourceIdentifier} already processed, skipping");
                    return;
                }

                // Read GBK-encoded content
                var gbk = Encoding.GetEncoding("GBK");
                string content;
                using (var reader = new StreamReader(stream, gbk))
                {
                    content = await reader.ReadToEndAsync();
                }

                _logger.LogDebug($"Parsing E-file from {sourceIdentifier}, length={content.Length}");

                // Parse table blocks
                var tables = ParseTableBlocks(content);

                foreach (var table in tables)
                {
                    _logger.LogInformation($"Processing table: {table.TableName} with {table.Rows.Count} rows");

                    // Upsert info table (header)
                    await _repository.UpsertInfoTableAsync(table.TableName, table.Header);

                    // Insert data records
                    await _repository.InsertRecordsAsync(table.TableName, table.Rows);
                }

                // Mark file as processed
                await _repository.MarkFileProcessedAsync(sourceIdentifier);

                _logger.LogInformation($"Successfully parsed E-file {sourceIdentifier} with {tables.Count} tables");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error parsing E-file {sourceIdentifier}");
                throw;
            }
        }

        private List<TableBlock> ParseTableBlocks(string content)
        {
            var tables = new List<TableBlock>();
            var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            TableBlock? currentTable = null;
            Dictionary<string, string>? currentHeader = null;

            foreach (var line in lines)
            {
                var trimmed = line.Trim();

                if (trimmed.StartsWith("<") && trimmed.EndsWith(">"))
                {
                    // Table block start
                    var tableName = trimmed.Substring(1, trimmed.Length - 2).Trim();
                    currentTable = new TableBlock { TableName = tableName };
                    currentHeader = new Dictionary<string, string>();
                    tables.Add(currentTable);
                }
                else if (trimmed.StartsWith("@"))
                {
                    // Header line
                    var headerLine = trimmed.Substring(1).Trim();
                    var parts = headerLine.Split('\t');

                    if (parts.Length == 2)
                    {
                        currentHeader?.Add(parts[0].Trim(), parts[1].Trim());
                    }
                }
                else if (trimmed.StartsWith("#"))
                {
                    // Data row
                    var dataLine = trimmed.Substring(1).Trim();
                    var values = dataLine.Split('\t');

                    // Convert to dictionary (assuming first header defines column order)
                    if (currentTable != null && currentHeader != null)
                    {
                        currentTable.Header = currentHeader;
                        
                        var row = new Dictionary<string, object?>();
                        var columnNames = currentHeader.Keys.ToArray();

                        for (int i = 0; i < values.Length && i < columnNames.Length; i++)
                        {
                            var value = values[i].Trim();
                            
                            // Map -99 to NULL
                            if (value == "-99" || string.IsNullOrEmpty(value))
                            {
                                row[columnNames[i]] = null;
                            }
                            else
                            {
                                row[columnNames[i]] = value;
                            }
                        }

                        currentTable.Rows.Add(row);
                    }
                }
            }

            return tables;
        }

        private class TableBlock
        {
            public string TableName { get; set; } = string.Empty;
            public Dictionary<string, string> Header { get; set; } = new();
            public List<Dictionary<string, object?>> Rows { get; set; } = new();
        }
    }
}
