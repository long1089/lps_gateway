using System.Collections.Concurrent;
using LpsGateway.Lib60870;
using Microsoft.Extensions.Logging;

namespace LpsGateway.Services;

/// <summary>
/// 文件传输管理器，负责管理多帧文件传输、FCB 状态和分片重组
/// </summary>
public class FileTransferManager : IFileTransferManager
{
    private readonly IEFileParser _parser;
    private readonly ILogger<FileTransferManager> _logger;
    private readonly ConcurrentDictionary<string, List<byte[]>> _fragments = new();
    private readonly ConcurrentDictionary<string, DateTime> _lastReceiveTime = new();
    private readonly ConcurrentDictionary<string, bool> _fcbStates = new();
    private readonly SemaphoreSlim _processingLock = new(1, 1);
    private readonly int _fragmentTimeoutMs;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="parser">E 文件解析器</param>
    /// <param name="logger">日志记录器</param>
    /// <param name="fragmentTimeoutMs">分片超时时间（毫秒），默认 60000（1 分钟）</param>
    public FileTransferManager(IEFileParser parser, ILogger<FileTransferManager> logger, int fragmentTimeoutMs = 60000)
    {
        _parser = parser;
        _logger = logger;
        _fragmentTimeoutMs = fragmentTimeoutMs;
        _logger.LogInformation("FileTransferManager 已创建，分片超时时间: {TimeoutMs}ms", fragmentTimeoutMs);
    }

    /// <summary>
    /// 处理接收到的 ASDU 数据
    /// </summary>
    /// <param name="asduData">ASDU 字节数组</param>
    public async Task ProcessAsduAsync(byte[] asduData)
    {
        await _processingLock.WaitAsync();
        try
        {
            _logger.LogDebug("开始处理 ASDU 数据，长度: {Length} 字节", asduData.Length);

            // 解析 ASDU
            AsduData asdu;
            try
            {
                asdu = AsduManager.ParseAsdu(asduData);
                _logger.LogInformation("解析 ASDU 成功: TypeId={TypeId}, COT={COT}, CommonAddr={CommonAddr}, PayloadLen={PayloadLen}",
                    asdu.TypeId, asdu.CauseOfTransmission, asdu.CommonAddr, asdu.Payload.Length);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "解析 ASDU 失败");
                return;
            }

            var key = $"{asdu.CommonAddr}_{asdu.TypeId}";

            // 清理超时的分片
            CleanupTimedOutFragments();

            // 检查 FCB（如果需要）
            if (_fcbStates.ContainsKey(key))
            {
                _logger.LogDebug("检查 FCB 状态: {Key}", key);
                // FCB 检查逻辑可以在这里实现
            }

            // 添加分片
            if (!_fragments.ContainsKey(key))
            {
                _logger.LogInformation("创建新的文件传输会话: {Key}", key);
                _fragments[key] = new List<byte[]>();
            }

            _fragments[key].Add(asdu.Payload);
            _lastReceiveTime[key] = DateTime.UtcNow;
            
            _logger.LogDebug("添加分片 {FragmentIndex}/{Key}", _fragments[key].Count, key);

            // 检查是否为最后一帧
            if (AsduManager.IsLastFrame(asdu.CauseOfTransmission))
            {
                _logger.LogInformation("接收到最后一帧: {Key}，总分片数: {Count}", key, _fragments[key].Count);

                List<byte[]> allFragments = _fragments[key];
                _fragments.TryRemove(key, out _);
                _lastReceiveTime.TryRemove(key, out _);
                _fcbStates.TryRemove(key, out _);

                // 合并所有分片
                var completeData = allFragments.SelectMany(f => f).ToArray();
                _logger.LogInformation("文件合并完成: {Key}，总大小: {Size} 字节", key, completeData.Length);

                var stream = new MemoryStream(completeData);
                var fileName = $"efile_{asdu.CommonAddr}_{asdu.TypeId:X2}_{DateTime.UtcNow:yyyyMMddHHmmss}.txt";

                try
                {
                    await _parser.ParseAndSaveAsync(stream, asdu.CommonAddr.ToString(), Mapping.GetTypeName(asdu.TypeId), fileName);
                    _logger.LogInformation("文件解析并保存成功: {FileName}", fileName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "文件解析或保存失败: {FileName}", fileName);
                    throw;
                }
            }
            else
            {
                _logger.LogDebug("等待更多分片: {Key}，当前分片数: {Count}", key, _fragments[key].Count);
            }
        }
        finally
        {
            _processingLock.Release();
        }
    }

    /// <summary>
    /// 清理超时的分片
    /// </summary>
    private void CleanupTimedOutFragments()
    {
        var now = DateTime.UtcNow;
        var keysToRemove = new List<string>();

        foreach (var kvp in _lastReceiveTime)
        {
            var timeSinceLastReceive = (now - kvp.Value).TotalMilliseconds;
            if (timeSinceLastReceive > _fragmentTimeoutMs)
            {
                keysToRemove.Add(kvp.Key);
            }
        }

        foreach (var key in keysToRemove)
        {
            _logger.LogWarning("清理超时的文件传输会话: {Key}，超时时间: {TimeoutMs}ms", key, _fragmentTimeoutMs);
            _fragments.TryRemove(key, out _);
            _lastReceiveTime.TryRemove(key, out _);
            _fcbStates.TryRemove(key, out _);
        }
    }

    /// <summary>
    /// 获取 FCB 状态
    /// </summary>
    /// <param name="key">传输会话键</param>
    /// <returns>FCB 状态</returns>
    public bool GetFcbState(string key)
    {
        return _fcbStates.TryGetValue(key, out var fcb) && fcb;
    }

    /// <summary>
    /// 设置 FCB 状态
    /// </summary>
    /// <param name="key">传输会话键</param>
    /// <param name="fcb">FCB 状态</param>
    public void SetFcbState(string key, bool fcb)
    {
        _fcbStates[key] = fcb;
        _logger.LogDebug("设置 FCB 状态: {Key} -> {FCB}", key, fcb);
    }

    /// <summary>
    /// 获取当前活跃的传输会话数量
    /// </summary>
    /// <returns>活跃会话数量</returns>
    public int GetActiveSessionCount()
    {
        return _fragments.Count;
    }
}
