using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LPSGateway.Lib60870;
using LPSGateway.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace LPSGateway.Services
{
    /// <summary>
    /// Manages IEC-102 E-file transfers by listening to link layer frames
    /// and buffering multi-frame file data
    /// </summary>
    public class FileTransferManager : IFileTransferManager
    {
        private readonly ILinkLayer _linkLayer;
        private readonly IEFileParser _parser;
        private readonly ILogger<FileTransferManager> _logger;
        private readonly Dictionary<string, List<byte>> _fileBuffers = new();

        public FileTransferManager(ILinkLayer linkLayer, IEFileParser parser, ILogger<FileTransferManager> logger)
        {
            _linkLayer = linkLayer;
            _parser = parser;
            _logger = logger;
        }

        public Task StartAsync()
        {
            _linkLayer.OnFrameReceived += OnFrameReceived;
            _logger.LogInformation("File transfer manager started");
            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            _linkLayer.OnFrameReceived -= OnFrameReceived;
            _logger.LogInformation("File transfer manager stopped");
            return Task.CompletedTask;
        }

        private async void OnFrameReceived(object? sender, Iec102Frame frame)
        {
            try
            {
                if (!frame.IsVariable || frame.Data.Length == 0)
                    return;

                // Parse ASDU
                var asdu = AsduManager.ParseAsdu(frame.Data);

                // Check if it's an E-file TYPE ID
                if (!AsduManager.IsEFileTypeId(asdu.TypeId))
                    return;

                // Generate buffer key from address and type
                var key = $"{asdu.CommonAddress:X4}_{asdu.TypeId:X2}";

                // Initialize buffer if needed
                if (!_fileBuffers.ContainsKey(key))
                {
                    _fileBuffers[key] = new List<byte>();
                }

                // Add data to buffer
                _fileBuffers[key].AddRange(asdu.Data);

                _logger.LogDebug($"Buffered {asdu.Data.Length} bytes for key {key}, COT={asdu.CauseOfTransmission:X2}");

                // If COT == 0x07 (end of transfer), process the complete file
                if (asdu.CauseOfTransmission == 0x07)
                {
                    _logger.LogInformation($"End of transfer for key {key}, processing file");
                    
                    var fileData = _fileBuffers[key].ToArray();
                    _fileBuffers.Remove(key);

                    // Parse the E-file
                    await _parser.ParseAsync(fileData, key);
                    
                    _logger.LogInformation($"File {key} processed successfully");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing frame");
            }
        }
    }
}
