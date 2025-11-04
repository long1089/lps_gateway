using System.Text;
using System.Globalization;
using LpsGateway.Data;
using Microsoft.Extensions.Logging;

namespace LpsGateway.Services;

/// <summary>
/// E 文件解析器，负责解析 E 文件并保存到数据库
/// </summary>
public class EFileParser : IEFileParser
{
    /// <summary>
    /// NULL 值标识符（E 文件中使用 "-99" 表示 NULL）
    /// </summary>
    public const string NULL_VALUE_MARKER = "-99";

    private readonly IEFileRepository _repository;
    private readonly ILogger<EFileParser> _logger;
    private readonly Dictionary<string, Dictionary<string, string>> _columnMappings;
    private readonly Dictionary<string, Dictionary<string, Type>> _columnTypes;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="repository">E 文件仓储</param>
    /// <param name="logger">日志记录器</param>
    public EFileParser(IEFileRepository repository, ILogger<EFileParser> logger)
    {
        _repository = repository;
        _logger = logger;
        _columnMappings = new Dictionary<string, Dictionary<string, string>>();
        _columnTypes = new Dictionary<string, Dictionary<string, Type>>();
        
        _logger.LogInformation("EFileParser 已创建");
    }

    /// <summary>
    /// 清理用户输入用于日志记录（防止日志注入攻击）
    /// </summary>
    /// <param name="input">用户输入</param>
    /// <returns>清理后的字符串</returns>
    private static string SanitizeForLog(string? input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;
        
        // 移除换行符和控制字符
        return System.Text.RegularExpressions.Regex.Replace(input, @"[\r\n\t\x00-\x1F]", "");
    }

    /// <summary>
    /// 配置列映射（从文件列名到数据库列名）
    /// </summary>
    /// <param name="tableName">表名</param>
    /// <param name="mappings">列映射字典</param>
    public void ConfigureColumnMapping(string tableName, Dictionary<string, string> mappings)
    {
        _columnMappings[tableName] = mappings;
        _logger.LogInformation("配置表 {TableName} 的列映射: {Count} 个列", tableName, mappings.Count);
    }

    /// <summary>
    /// 配置列类型（用于类型转换）
    /// </summary>
    /// <param name="tableName">表名</param>
    /// <param name="types">列类型字典</param>
    public void ConfigureColumnTypes(string tableName, Dictionary<string, Type> types)
    {
        _columnTypes[tableName] = types;
        _logger.LogInformation("配置表 {TableName} 的列类型: {Count} 个列", tableName, types.Count);
    }

    /// <summary>
    /// 解析并保存 E 文件
    /// </summary>
    /// <param name="fileStream">文件流</param>
    /// <param name="commonAddr">公共地址</param>
    /// <param name="typeId">类型标识</param>
    /// <param name="fileName">文件名</param>
    public async Task ParseAndSaveAsync(Stream fileStream, string commonAddr, string typeId, string fileName)
    {
        _logger.LogInformation("开始解析文件: {FileName}, CommonAddr={CommonAddr}, TypeId={TypeId}", 
            SanitizeForLog(fileName), SanitizeForLog(commonAddr), SanitizeForLog(typeId));
        
        try
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            var gbk = Encoding.GetEncoding("GBK");

            using var reader = new StreamReader(fileStream, gbk);
            var content = await reader.ReadToEndAsync();

            var fileSize = (int)fileStream.Length;
            _logger.LogDebug("文件内容读取完成，大小: {FileSize} 字节", fileSize);

            // 检查文件是否已处理
            if (await _repository.IsFileProcessedAsync(commonAddr, typeId, fileName))
            {
                _logger.LogWarning("文件已处理，跳过: {FileName}", fileName);
                return;
            }

            // 解析表格数据
            var tables = ParseTables(content);
            _logger.LogInformation("解析出 {TableCount} 个表格", tables.Count);

            // 处理每个表格
            foreach (var (tableName, records) in tables)
            {
                _logger.LogInformation("处理表 {TableName}，记录数: {RecordCount}", tableName, records.Count);

                // 应用列映射和类型转换
                var convertedRecords = ConvertRecordTypes(tableName, records);

                if (tableName.EndsWith("_INFO", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogDebug("表 {TableName} 使用 UPSERT 模式", tableName);
                    foreach (var record in convertedRecords)
                    {
                        try
                        {
                            await _repository.UpsertInfoTableAsync(tableName, record, "ID");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "UPSERT 记录失败: 表={TableName}", tableName);
                        }
                    }
                }
                else
                {
                    _logger.LogDebug("表 {TableName} 使用批量 INSERT 模式", tableName);
                    try
                    {
                        await _repository.InsertRecordsAsync(tableName, convertedRecords);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "批量插入记录失败: 表={TableName}", tableName);
                    }
                }
            }

            // 标记文件已处理
            await _repository.MarkFileProcessedAsync(commonAddr, typeId, fileName, fileSize);
            _logger.LogInformation("文件处理完成并标记: {FileName}", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "解析或保存文件失败: {FileName}", fileName);
            await _repository.MarkFileProcessedAsync(commonAddr, typeId, fileName, (int)fileStream.Length, "ERROR", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// 解析表格数据
    /// </summary>
    /// <param name="content">文件内容</param>
    /// <returns>表格数据字典</returns>
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
                    _logger.LogDebug("解析表格: {TableName}", currentTable);
                }
            }
            else if (trimmed.StartsWith("@") && currentTable != null)
            {
                headers = trimmed.Substring(1).Split('\t').Select(h => h.Trim()).ToList();
                _logger.LogDebug("解析列头: {Headers}", string.Join(", ", headers));
            }
            else if (trimmed.StartsWith("#") && currentTable != null && headers != null)
            {
                var values = trimmed.Substring(1).Split('\t');
                var record = new Dictionary<string, object?>();
                
                for (int i = 0; i < Math.Min(headers.Count, values.Length); i++)
                {
                    var value = values[i].Trim();
                    record[headers[i]] = value == NULL_VALUE_MARKER ? null : (object?)value;
                }

                result[currentTable].Add(record);
            }
        }

        return result;
    }

    /// <summary>
    /// 转换记录的数据类型
    /// </summary>
    /// <param name="tableName">表名</param>
    /// <param name="records">原始记录列表</param>
    /// <returns>转换后的记录列表</returns>
    private List<Dictionary<string, object?>> ConvertRecordTypes(string tableName, List<Dictionary<string, object?>> records)
    {
        var result = new List<Dictionary<string, object?>>();

        // 获取列映射和类型配置
        var hasMapping = _columnMappings.TryGetValue(tableName, out var mapping);
        var hasTypes = _columnTypes.TryGetValue(tableName, out var types);

        foreach (var record in records)
        {
            var convertedRecord = new Dictionary<string, object?>();

            foreach (var kvp in record)
            {
                var columnName = kvp.Key;
                var value = kvp.Value;

                // 应用列名映射
                if (hasMapping && mapping!.TryGetValue(columnName, out var mappedName))
                {
                    columnName = mappedName;
                }

                // 应用类型转换
                if (value != null && hasTypes && types!.TryGetValue(columnName, out var targetType))
                {
                    value = ConvertValue(value, targetType, tableName, columnName);
                }

                convertedRecord[columnName] = value;
            }

            result.Add(convertedRecord);
        }

        return result;
    }

    /// <summary>
    /// 转换单个值到目标类型
    /// </summary>
    /// <param name="value">原始值</param>
    /// <param name="targetType">目标类型</param>
    /// <param name="tableName">表名（用于日志）</param>
    /// <param name="columnName">列名（用于日志）</param>
    /// <returns>转换后的值</returns>
    private object? ConvertValue(object value, Type targetType, string tableName, string columnName)
    {
        try
        {
            var stringValue = value.ToString();
            if (string.IsNullOrWhiteSpace(stringValue))
            {
                return null;
            }

            // 处理可空类型
            var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

            if (underlyingType == typeof(int))
            {
                if (int.TryParse(stringValue, out var intValue))
                    return intValue;
            }
            else if (underlyingType == typeof(long))
            {
                if (long.TryParse(stringValue, out var longValue))
                    return longValue;
            }
            else if (underlyingType == typeof(double))
            {
                if (double.TryParse(stringValue, NumberStyles.Any, CultureInfo.InvariantCulture, out var doubleValue))
                    return doubleValue;
            }
            else if (underlyingType == typeof(decimal))
            {
                if (decimal.TryParse(stringValue, NumberStyles.Any, CultureInfo.InvariantCulture, out var decimalValue))
                    return decimalValue;
            }
            else if (underlyingType == typeof(DateTime))
            {
                if (DateTime.TryParse(stringValue, out var dateValue))
                    return dateValue;
            }
            else if (underlyingType == typeof(bool))
            {
                if (bool.TryParse(stringValue, out var boolValue))
                    return boolValue;
            }
            else if (underlyingType == typeof(string))
            {
                return stringValue;
            }

            _logger.LogWarning("无法转换值: 表={TableName}, 列={ColumnName}, 值={Value}, 目标类型={TargetType}",
                tableName, columnName, stringValue, targetType.Name);
            
            // 转换失败，返回 null 或默认值
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "类型转换异常: 表={TableName}, 列={ColumnName}, 值={Value}, 目标类型={TargetType}",
                tableName, columnName, value, targetType.Name);
            return null;
        }
    }
}
