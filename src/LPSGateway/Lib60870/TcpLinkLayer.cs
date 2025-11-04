using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace LPSGateway.Lib60870
{
    /// <summary>
    /// TCP-based implementation of IEC-102 link layer
    /// </summary>
    public class TcpLinkLayer : ILinkLayer
    {
        public event EventHandler<Iec102Frame>? OnFrameReceived;

        private TcpListener? _listener;
        private TcpClient? _client;
        private NetworkStream? _stream;
        private CancellationTokenSource? _cts;
        private readonly byte[] _buffer = new byte[4096];

        public async Task StartServerAsync(string host, int port)
        {
            _listener = new TcpListener(IPAddress.Parse(host), port);
            _listener.Start();
            _cts = new CancellationTokenSource();

            // Accept one connection
            _client = await _listener.AcceptTcpClientAsync();
            _stream = _client.GetStream();

            // Start receiving
            _ = Task.Run(() => ReceiveLoop(_cts.Token));
        }

        public async Task ConnectAsync(string host, int port)
        {
            _client = new TcpClient();
            await _client.ConnectAsync(host, port);
            _stream = _client.GetStream();
            _cts = new CancellationTokenSource();

            // Start receiving
            _ = Task.Run(() => ReceiveLoop(_cts.Token));
        }

        public async Task SendFrameAsync(Iec102Frame frame)
        {
            if (_stream == null)
                throw new InvalidOperationException("Not connected");

            byte[] data;
            if (frame.IsVariable)
            {
                data = Iec102Frame.BuildVariableFrame(frame.ControlField[0], frame.Address, frame.Data);
            }
            else
            {
                data = Iec102Frame.BuildFixedFrame(frame.ControlField[0], frame.Address);
            }

            await _stream.WriteAsync(data, 0, data.Length);
            await _stream.FlushAsync();
        }

        public Task StopAsync()
        {
            _cts?.Cancel();
            _stream?.Close();
            _client?.Close();
            _listener?.Stop();
            return Task.CompletedTask;
        }

        private async Task ReceiveLoop(CancellationToken cancellationToken)
        {
            int offset = 0;
            int bytesInBuffer = 0;

            try
            {
                while (!cancellationToken.IsCancellationRequested && _stream != null)
                {
                    var bytesRead = await _stream.ReadAsync(_buffer, bytesInBuffer, _buffer.Length - bytesInBuffer, cancellationToken);
                    
                    if (bytesRead == 0)
                        break; // Connection closed

                    bytesInBuffer += bytesRead;

                    // Try to parse frames
                    offset = 0;
                    var frames = Iec102Frame.TryParseFrames(_buffer, ref offset);

                    foreach (var frame in frames)
                    {
                        OnFrameReceived?.Invoke(this, frame);
                    }

                    // Move remaining data to beginning of buffer
                    if (offset > 0 && offset < bytesInBuffer)
                    {
                        Array.Copy(_buffer, offset, _buffer, 0, bytesInBuffer - offset);
                        bytesInBuffer -= offset;
                    }
                    else if (offset >= bytesInBuffer)
                    {
                        bytesInBuffer = 0;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when stopping
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in receive loop: {ex.Message}");
            }
        }
    }
}
