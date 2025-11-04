using System;
using System.Threading.Tasks;

namespace LPSGateway.Lib60870
{
    /// <summary>
    /// Interface for IEC-102 link layer implementations
    /// </summary>
    public interface ILinkLayer
    {
        /// <summary>
        /// Event triggered when a frame is received
        /// </summary>
        event EventHandler<Iec102Frame>? OnFrameReceived;

        /// <summary>
        /// Start the server and listen for connections
        /// </summary>
        Task StartServerAsync(string host, int port);

        /// <summary>
        /// Connect to a remote server as client
        /// </summary>
        Task ConnectAsync(string host, int port);

        /// <summary>
        /// Send a frame
        /// </summary>
        Task SendFrameAsync(Iec102Frame frame);

        /// <summary>
        /// Stop the link layer
        /// </summary>
        Task StopAsync();
    }
}
