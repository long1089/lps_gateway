using Microsoft.AspNetCore.Mvc;
using LpsGateway.Services;

namespace LpsGateway.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EFileController : ControllerBase
{
    private readonly IEFileParser _parser;
    private readonly IFileTransferManager _transferManager;

    public EFileController(IEFileParser parser, IFileTransferManager transferManager)
    {
        _parser = parser;
        _transferManager = transferManager;
    }

    [HttpPost("upload")]
    public async Task<IActionResult> Upload([FromForm] IFormFile file, [FromForm] string commonAddr, [FromForm] string typeId)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest("No file uploaded");
        }

        try
        {
            using var stream = file.OpenReadStream();
            await _parser.ParseAndSaveAsync(stream, commonAddr, typeId, file.FileName);
            return Ok(new { message = "File uploaded and processed successfully" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("trigger-report")]
    public async Task<IActionResult> TriggerReport([FromBody] TriggerReportRequest request)
    {
        try
        {
            await _transferManager.ProcessAsduAsync(request.AsduData);
            return Ok(new { message = "Report triggered successfully" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }
}

public class TriggerReportRequest
{
    public byte[] AsduData { get; set; } = Array.Empty<byte>();
}
