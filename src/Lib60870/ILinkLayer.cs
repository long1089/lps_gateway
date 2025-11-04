namespace LpsGateway.Lib60870;

public interface ILinkLayer
{
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync();
    Task SendAsync(byte[] data);
    event EventHandler<byte[]>? DataReceived;
}
