using System;
using System.IO;
using System.Threading.Tasks;
using LPSGateway.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace LPSGateway.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EFileController : ControllerBase
    {
        private readonly IEFileParser _parser;
        private readonly ILogger<EFileController> _logger;

        public EFileController(IEFileParser parser, ILogger<EFileController> logger)
        {
            _parser = parser;
            _logger = logger;
        }

        /// <summary>
        /// Upload and process an E-file
        /// </summary>
        [HttpPost("upload")]
        public async Task<IActionResult> Upload(IFormFile file)
        {
            try
            {
                if (file == null || file.Length == 0)
                {
                    return BadRequest(new { error = "No file uploaded" });
                }

                _logger.LogInformation($"Received file upload: {file.FileName}, size: {file.Length} bytes");

                // Generate source identifier from filename and timestamp
                var sourceIdentifier = $"{file.FileName}_{DateTime.UtcNow:yyyyMMddHHmmss}";

                // Parse the file
                using (var stream = file.OpenReadStream())
                {
                    await _parser.ParseAsync(stream, sourceIdentifier);
                }

                return Ok(new
                {
                    success = true,
                    message = "File processed successfully",
                    sourceIdentifier
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing uploaded file");
                return StatusCode(500, new { error = "Internal server error", message = ex.Message });
            }
        }

        /// <summary>
        /// Health check endpoint
        /// </summary>
        [HttpGet("health")]
        public IActionResult Health()
        {
            return Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
        }
    }
}
