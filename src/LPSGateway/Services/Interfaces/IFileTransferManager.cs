using System.Threading.Tasks;

namespace LPSGateway.Services.Interfaces
{
    /// <summary>
    /// Interface for managing IEC-102 E-file transfers
    /// </summary>
    public interface IFileTransferManager
    {
        /// <summary>
        /// Start listening for file transfers
        /// </summary>
        Task StartAsync();

        /// <summary>
        /// Stop listening for file transfers
        /// </summary>
        Task StopAsync();
    }
}
