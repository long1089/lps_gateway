namespace LpsGateway.Services;

public interface IFileTransferManager
{
    Task ProcessAsduAsync(byte[] asduData);
}
