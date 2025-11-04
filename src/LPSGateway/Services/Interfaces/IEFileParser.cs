using System.IO;
using System.Threading.Tasks;

namespace LPSGateway.Services.Interfaces
{
    /// <summary>
    /// Interface for parsing E-file data
    /// </summary>
    public interface IEFileParser
    {
        /// <summary>
        /// Parse E-file stream and store data in database
        /// </summary>
        Task ParseAsync(Stream stream, string sourceIdentifier);

        /// <summary>
        /// Parse E-file from byte array and store data in database
        /// </summary>
        Task ParseAsync(byte[] data, string sourceIdentifier);
    }
}
