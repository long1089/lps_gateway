using LpsGateway.Lib60870;

namespace LpsGateway.Services;

public class FileTransferManager : IFileTransferManager
{
    private readonly IEFileParser _parser;
    private readonly Dictionary<string, List<byte[]>> _fragments = new();

    public FileTransferManager(IEFileParser parser)
    {
        _parser = parser;
    }

    public async Task ProcessAsduAsync(byte[] asduData)
    {
        var asdu = AsduManager.ParseAsdu(asduData);
        var key = $"{asdu.CommonAddr}_{asdu.TypeId}";

        lock (_fragments)
        {
            if (!_fragments.ContainsKey(key))
            {
                _fragments[key] = new List<byte[]>();
            }
            _fragments[key].Add(asdu.Payload);
        }

        if (AsduManager.IsLastFrame(asdu.CauseOfTransmission))
        {
            List<byte[]> allFragments;
            lock (_fragments)
            {
                allFragments = _fragments[key];
                _fragments.Remove(key);
            }

            var completeData = allFragments.SelectMany(f => f).ToArray();
            var stream = new MemoryStream(completeData);
            var fileName = $"efile_{asdu.CommonAddr}_{asdu.TypeId}_{DateTime.UtcNow:yyyyMMddHHmmss}.txt";
            
            await _parser.ParseAndSaveAsync(stream, asdu.CommonAddr.ToString(), Mapping.GetTypeName(asdu.TypeId), fileName);
        }
    }
}
