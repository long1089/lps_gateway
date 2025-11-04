using System.Text;
using LpsGateway.Data;

namespace LpsGateway.Services;

public class EFileParser : IEFileParser
{
    private readonly IEFileRepository _repository;

    public EFileParser(IEFileRepository repository)
    {
        _repository = repository;
    }

    public async Task ParseAndSaveAsync(Stream fileStream, string commonAddr, string typeId, string fileName)
    {
        try
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            var gbk = Encoding.GetEncoding("GBK");

            using var reader = new StreamReader(fileStream, gbk);
            var content = await reader.ReadToEndAsync();

            var fileSize = (int)fileStream.Length;

            if (await _repository.IsFileProcessedAsync(commonAddr, typeId, fileName))
            {
                return;
            }

            var tables = ParseTables(content);

            foreach (var (tableName, records) in tables)
            {
                if (tableName.EndsWith("_INFO", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var record in records)
                    {
                        await _repository.UpsertInfoTableAsync(tableName, record, "ID");
                    }
                }
                else
                {
                    await _repository.InsertRecordsAsync(tableName, records);
                }
            }

            await _repository.MarkFileProcessedAsync(commonAddr, typeId, fileName, fileSize);
        }
        catch (Exception ex)
        {
            await _repository.MarkFileProcessedAsync(commonAddr, typeId, fileName, (int)fileStream.Length, "ERROR", ex.Message);
            throw;
        }
    }

    private Dictionary<string, List<Dictionary<string, object?>>> ParseTables(string content)
    {
        var result = new Dictionary<string, List<Dictionary<string, object?>>>();
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        string? currentTable = null;
        List<string>? headers = null;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            
            if (trimmed.StartsWith("<table>"))
            {
                var parts = trimmed.Split(new[] { ' ', '>' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 1)
                {
                    currentTable = parts[1];
                    headers = null;
                    result[currentTable] = new List<Dictionary<string, object?>>();
                }
            }
            else if (trimmed.StartsWith("@") && currentTable != null)
            {
                headers = trimmed.Substring(1).Split('\t').Select(h => h.Trim()).ToList();
            }
            else if (trimmed.StartsWith("#") && currentTable != null && headers != null)
            {
                var values = trimmed.Substring(1).Split('\t');
                var record = new Dictionary<string, object?>();
                
                for (int i = 0; i < Math.Min(headers.Count, values.Length); i++)
                {
                    var value = values[i].Trim();
                    record[headers[i]] = value == "-99" ? null : (object?)value;
                }

                result[currentTable].Add(record);
            }
        }

        return result;
    }
}
