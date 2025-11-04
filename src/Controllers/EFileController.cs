using Microsoft.AspNetCore.Mvc;
using LpsGateway.Services;
using Microsoft.Extensions.Logging;

namespace LpsGateway.Controllers;

/// <summary>
/// E 文件控制器，提供文件上传和报文触发接口
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class EFileController : ControllerBase
{
    private readonly IEFileParser _parser;
    private readonly IFileTransferManager _transferManager;
    private readonly ILogger<EFileController> _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="parser">E 文件解析器</param>
    /// <param name="transferManager">文件传输管理器</param>
    /// <param name="logger">日志记录器</param>
    public EFileController(IEFileParser parser, IFileTransferManager transferManager, ILogger<EFileController> logger)
    {
        _parser = parser;
        _transferManager = transferManager;
        _logger = logger;
    }

    /// <summary>
    /// 上传 E 文件
    /// </summary>
    /// <param name="file">要上传的文件</param>
    /// <param name="commonAddr">公共地址</param>
    /// <param name="typeId">类型标识</param>
    /// <returns>处理结果</returns>
    [HttpPost("upload")]
    [ProducesResponseType(typeof(UploadResponse), 200)]
    [ProducesResponseType(typeof(ErrorResponse), 400)]
    [ProducesResponseType(typeof(ErrorResponse), 500)]
    public async Task<IActionResult> Upload([FromForm] IFormFile file, [FromForm] string commonAddr, [FromForm] string typeId)
    {
        _logger.LogInformation("接收到文件上传请求: FileName={FileName}, CommonAddr={CommonAddr}, TypeId={TypeId}, Size={Size}",
            file?.FileName, commonAddr, typeId, file?.Length ?? 0);

        if (file == null || file.Length == 0)
        {
            _logger.LogWarning("文件上传失败：文件为空");
            return BadRequest(new ErrorResponse { Error = "未上传文件或文件为空", Details = "file 参数为空或长度为 0" });
        }

        if (string.IsNullOrWhiteSpace(commonAddr))
        {
            _logger.LogWarning("文件上传失败：公共地址为空");
            return BadRequest(new ErrorResponse { Error = "公共地址不能为空", Details = "commonAddr 参数为空" });
        }

        if (string.IsNullOrWhiteSpace(typeId))
        {
            _logger.LogWarning("文件上传失败：类型标识为空");
            return BadRequest(new ErrorResponse { Error = "类型标识不能为空", Details = "typeId 参数为空" });
        }

        try
        {
            var startTime = DateTime.UtcNow;
            using var stream = file.OpenReadStream();
            await _parser.ParseAndSaveAsync(stream, commonAddr, typeId, file.FileName);
            var processingTime = (DateTime.UtcNow - startTime).TotalMilliseconds;

            _logger.LogInformation("文件上传并处理成功: {FileName}, 耗时: {ProcessingTime}ms", file.FileName, processingTime);

            return Ok(new UploadResponse
            {
                Message = "文件上传并处理成功",
                FileName = file.FileName,
                FileSize = file.Length,
                CommonAddr = commonAddr,
                TypeId = typeId,
                ProcessingTimeMs = processingTime
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "文件上传处理失败: {FileName}", file.FileName);
            return StatusCode(500, new ErrorResponse
            {
                Error = "文件处理失败",
                Details = ex.Message,
                StackTrace = ex.StackTrace
            });
        }
    }

    /// <summary>
    /// 触发报文处理
    /// </summary>
    /// <param name="request">触发请求</param>
    /// <returns>处理结果</returns>
    [HttpPost("trigger-report")]
    [ProducesResponseType(typeof(TriggerReportResponse), 200)]
    [ProducesResponseType(typeof(ErrorResponse), 400)]
    [ProducesResponseType(typeof(ErrorResponse), 500)]
    public async Task<IActionResult> TriggerReport([FromBody] TriggerReportRequest request)
    {
        _logger.LogInformation("接收到报文触发请求: DataLength={Length}", request.AsduData?.Length ?? 0);

        if (request.AsduData == null || request.AsduData.Length == 0)
        {
            _logger.LogWarning("报文触发失败：ASDU 数据为空");
            return BadRequest(new ErrorResponse { Error = "ASDU 数据不能为空", Details = "asduData 参数为空或长度为 0" });
        }

        try
        {
            var startTime = DateTime.UtcNow;
            await _transferManager.ProcessAsduAsync(request.AsduData);
            var processingTime = (DateTime.UtcNow - startTime).TotalMilliseconds;

            _logger.LogInformation("报文处理成功，耗时: {ProcessingTime}ms", processingTime);

            return Ok(new TriggerReportResponse
            {
                Message = "报文触发并处理成功",
                DataLength = request.AsduData.Length,
                ProcessingTimeMs = processingTime
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "报文处理失败");
            return StatusCode(500, new ErrorResponse
            {
                Error = "报文处理失败",
                Details = ex.Message,
                StackTrace = ex.StackTrace
            });
        }
    }
}

/// <summary>
/// 触发报文请求
/// </summary>
public class TriggerReportRequest
{
    /// <summary>
    /// ASDU 数据
    /// </summary>
    public byte[] AsduData { get; set; } = Array.Empty<byte>();
}

/// <summary>
/// 文件上传响应
/// </summary>
public class UploadResponse
{
    /// <summary>
    /// 响应消息
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// 文件名
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// 文件大小（字节）
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    /// 公共地址
    /// </summary>
    public string CommonAddr { get; set; } = string.Empty;

    /// <summary>
    /// 类型标识
    /// </summary>
    public string TypeId { get; set; } = string.Empty;

    /// <summary>
    /// 处理时间（毫秒）
    /// </summary>
    public double ProcessingTimeMs { get; set; }
}

/// <summary>
/// 触发报文响应
/// </summary>
public class TriggerReportResponse
{
    /// <summary>
    /// 响应消息
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// 数据长度
    /// </summary>
    public int DataLength { get; set; }

    /// <summary>
    /// 处理时间（毫秒）
    /// </summary>
    public double ProcessingTimeMs { get; set; }
}

/// <summary>
/// 错误响应
/// </summary>
public class ErrorResponse
{
    /// <summary>
    /// 错误消息
    /// </summary>
    public string Error { get; set; } = string.Empty;

    /// <summary>
    /// 详细信息
    /// </summary>
    public string? Details { get; set; }

    /// <summary>
    /// 堆栈跟踪（仅开发环境）
    /// </summary>
    public string? StackTrace { get; set; }
}
