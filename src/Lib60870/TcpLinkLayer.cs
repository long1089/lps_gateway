using System.Net;
using System.Net.Sockets;

namespace LpsGateway.Lib60870;

public class TcpLinkLayer : ILinkLayer
{
    private readonly int _port;
    private TcpListener? _listener;
    private TcpClient? _client;
    private NetworkStream? _stream;
    private CancellationTokenSource? _cts;
    private Task? _receiveTask;

    public event EventHandler<byte[]>? DataReceived;

    public TcpLinkLayer(int port)
    {
        _port = port;
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        _listener = new TcpListener(IPAddress.Any, _port);
        _listener.Start();
        _cts = new CancellationTokenSource();

        _receiveTask = Task.Run(async () =>
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    _client = await _listener.AcceptTcpClientAsync(_cts.Token);
                    _stream = _client.GetStream();

                    await ReceiveDataAsync(_cts.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error accepting client: {ex.Message}");
                }
            }
        }, cancellationToken);

        await Task.CompletedTask;
    }

    private async Task ReceiveDataAsync(CancellationToken cancellationToken)
    {
        if (_stream == null) return;

        var buffer = new byte[4096];
        while (!cancellationToken.IsCancellationRequested && _stream.CanRead)
        {
            try
            {
                var bytesRead = await _stream.ReadAsync(buffer, cancellationToken);
                if (bytesRead > 0)
                {
                    var data = new byte[bytesRead];
                    Array.Copy(buffer, data, bytesRead);
                    DataReceived?.Invoke(this, data);
                }
                else
                {
                    break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error receiving data: {ex.Message}");
                break;
            }
        }
    }

    public async Task StopAsync()
    {
        _cts?.Cancel();
        
        if (_receiveTask != null)
        {
            await _receiveTask;
        }

        _stream?.Close();
        _client?.Close();
        _listener?.Stop();
    }

    public async Task SendAsync(byte[] data)
    {
        if (_stream != null && _stream.CanWrite)
        {
            await _stream.WriteAsync(data);
            await _stream.FlushAsync();
        }
    }
}
