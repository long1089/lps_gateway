namespace LpsGateway.Services;

public interface IEFileParser
{
    Task ParseAndSaveAsync(Stream fileStream, string commonAddr, string typeId, string fileName);
}
