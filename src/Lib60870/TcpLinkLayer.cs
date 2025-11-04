using System.Net;
using System.Net.Sockets;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace LpsGateway.Lib60870;

/// <summary>
/// TCP 链路层实现，支持超时重传、帧计数（FCB/FCV）和并发安全
/// </summary>
public class TcpLinkLayer : ILinkLayer
{
    private readonly int _port;
    private readonly ILogger<TcpLinkLayer> _logger;
    private readonly int _timeoutMs;
    private readonly int _maxRetries;
    private TcpListener? _listener;
    private readonly ConcurrentDictionary<string, TcpClient> _clients = new();
    private readonly ConcurrentDictionary<string, NetworkStream> _streams = new();
    private readonly ConcurrentDictionary<string, bool> _fcbStates = new();
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private CancellationTokenSource? _cts;
    private Task? _receiveTask;

    /// <summary>
    /// 数据接收事件
    /// </summary>
    public event EventHandler<byte[]>? DataReceived;

    /// <summary>
    /// 超时事件
    /// </summary>
    public event EventHandler<TimeoutEventArgs>? TimeoutOccurred;

    /// <summary>
    /// 重传事件
    /// </summary>
    public event EventHandler<RetransmissionEventArgs>? RetransmissionOccurred;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="port">监听端口</param>
    /// <param name="logger">日志记录器</param>
    /// <param name="timeoutMs">超时时间（毫秒），默认 5000</param>
    /// <param name="maxRetries">最大重传次数，默认 3</param>
    public TcpLinkLayer(int port, ILogger<TcpLinkLayer> logger, int timeoutMs = 5000, int maxRetries = 3)
    {
        _port = port;
        _logger = logger;
        _timeoutMs = timeoutMs;
        _maxRetries = maxRetries;
        _logger.LogInformation("TcpLinkLayer 已创建: Port={Port}, Timeout={TimeoutMs}ms, MaxRetries={MaxRetries}", 
            port, timeoutMs, maxRetries);
    }

    /// <summary>
    /// 启动链路层监听
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("启动 TCP 链路层监听，端口: {Port}", _port);
        _listener = new TcpListener(IPAddress.Any, _port);
        _listener.Start();
        _cts = new CancellationTokenSource();

        _receiveTask = Task.Run(async () =>
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    var client = await _listener.AcceptTcpClientAsync(_cts.Token);
                    var endpoint = client.Client.RemoteEndPoint?.ToString() ?? "unknown";
                    _logger.LogInformation("接受新客户端连接: {Endpoint}", endpoint);

                    _clients[endpoint] = client;
                    var stream = client.GetStream();
                    _streams[endpoint] = stream;

                    // 初始化 FCB 状态
                    _fcbStates[endpoint] = false;

                    _ = Task.Run(() => ReceiveDataAsync(endpoint, stream, _cts.Token), _cts.Token);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogDebug("接受客户端连接任务已取消");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "接受客户端连接时发生错误");
                }
            }
        }, cancellationToken);

        await Task.CompletedTask;
    }

    /// <summary>
    /// 接收数据
    /// </summary>
    /// <param name="endpoint">客户端端点</param>
    /// <param name="stream">网络流</param>
    /// <param name="cancellationToken">取消令牌</param>
    private async Task ReceiveDataAsync(string endpoint, NetworkStream stream, CancellationToken cancellationToken)
    {
        _logger.LogDebug("开始接收来自 {Endpoint} 的数据", endpoint);
        var buffer = new byte[4096];
        
        while (!cancellationToken.IsCancellationRequested && stream.CanRead)
        {
            try
            {
                var bytesRead = await stream.ReadAsync(buffer, cancellationToken);
                if (bytesRead > 0)
                {
                    var data = new byte[bytesRead];
                    Array.Copy(buffer, data, bytesRead);
                    
                    _logger.LogDebug("从 {Endpoint} 接收到 {BytesRead} 字节数据: {Data}", 
                        endpoint, bytesRead, BitConverter.ToString(data));

                    // 尝试解析帧
                    var frame = Iec102Frame.Parse(data);
                    if (frame.IsValid)
                    {
                        _logger.LogInformation("接收到有效帧: {Frame}", frame);
                        
                        // 如果是可变长度帧，处理 FCB
                        if (frame.Type == FrameType.Variable && frame.ControlField != null)
                        {
                            if (!frame.ControlField.PRM && frame.ControlField.FCV)
                            {
                                // 从站响应，检查 FCB
                                _logger.LogDebug("处理从站响应 FCB: {FCB}", frame.ControlField.FCB);
                            }
                        }
                    }
                    else
                    {
                        _logger.LogWarning("接收到无效帧: {ErrorMessage}", frame.ErrorMessage);
                    }

                    DataReceived?.Invoke(this, data);
                }
                else
                {
                    _logger.LogInformation("客户端 {Endpoint} 断开连接", endpoint);
                    break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "从 {Endpoint} 接收数据时发生错误", endpoint);
                break;
            }
        }

        // 清理连接
        CleanupConnection(endpoint);
    }

    /// <summary>
    /// 清理连接
    /// </summary>
    /// <param name="endpoint">客户端端点</param>
    private void CleanupConnection(string endpoint)
    {
        _logger.LogInformation("清理连接: {Endpoint}", endpoint);
        
        if (_streams.TryRemove(endpoint, out var stream))
        {
            try { stream.Close(); } catch { }
        }
        
        if (_clients.TryRemove(endpoint, out var client))
        {
            try { client.Close(); } catch { }
        }

        _fcbStates.TryRemove(endpoint, out _);
    }

    /// <summary>
    /// 停止链路层
    /// </summary>
    public async Task StopAsync()
    {
        _logger.LogInformation("停止 TCP 链路层");
        
        _cts?.Cancel();
        
        if (_receiveTask != null)
        {
            await _receiveTask;
        }

        // 清理所有连接
        foreach (var endpoint in _clients.Keys.ToList())
        {
            CleanupConnection(endpoint);
        }

        _listener?.Stop();
        _sendLock.Dispose();
        
        _logger.LogInformation("TCP 链路层已停止");
    }

    /// <summary>
    /// 发送数据（无重传）
    /// </summary>
    /// <param name="data">要发送的数据</param>
    public async Task SendAsync(byte[] data)
    {
        await _sendLock.WaitAsync();
        try
        {
            // 发送到所有连接的客户端
            foreach (var kvp in _streams)
            {
                var endpoint = kvp.Key;
                var stream = kvp.Value;
                
                if (stream.CanWrite)
                {
                    _logger.LogDebug("向 {Endpoint} 发送 {ByteCount} 字节数据: {Data}", 
                        endpoint, data.Length, BitConverter.ToString(data));
                    
                    await stream.WriteAsync(data);
                    await stream.FlushAsync();
                    
                    _logger.LogInformation("成功向 {Endpoint} 发送数据", endpoint);
                }
                else
                {
                    _logger.LogWarning("无法向 {Endpoint} 发送数据：流不可写", endpoint);
                }
            }
        }
        finally
        {
            _sendLock.Release();
        }
    }

    /// <summary>
    /// 带重传的发送数据
    /// </summary>
    /// <param name="data">要发送的数据</param>
    /// <param name="endpoint">目标端点（如果为 null，发送到所有连接）</param>
    /// <returns>是否发送成功</returns>
    public async Task<bool> SendWithRetryAsync(byte[] data, string? endpoint = null)
    {
        var frame = Iec102Frame.Parse(data);
        var isFirstSend = true;
        
        for (int attempt = 0; attempt <= _maxRetries; attempt++)
        {
            try
            {
                if (attempt > 0)
                {
                    _logger.LogWarning("重传帧，尝试 {Attempt}/{MaxRetries}", attempt, _maxRetries);
                    RetransmissionOccurred?.Invoke(this, new RetransmissionEventArgs 
                    { 
                        Attempt = attempt, 
                        Frame = frame,
                        Endpoint = endpoint 
                    });
                }

                await _sendLock.WaitAsync();
                try
                {
                    if (endpoint != null)
                    {
                        // 发送到指定端点
                        if (_streams.TryGetValue(endpoint, out var stream) && stream.CanWrite)
                        {
                            _logger.LogDebug("向 {Endpoint} 发送数据（尝试 {Attempt}）", endpoint, attempt + 1);
                            await stream.WriteAsync(data);
                            await stream.FlushAsync();
                        }
                        else
                        {
                            _logger.LogError("端点 {Endpoint} 不可用", endpoint);
                            return false;
                        }
                    }
                    else
                    {
                        // 发送到所有连接
                        foreach (var kvp in _streams)
                        {
                            if (kvp.Value.CanWrite)
                            {
                                await kvp.Value.WriteAsync(data);
                                await kvp.Value.FlushAsync();
                            }
                        }
                    }
                }
                finally
                {
                    _sendLock.Release();
                }

                // 等待响应（带超时）
                using var cts = new CancellationTokenSource(_timeoutMs);
                var responseReceived = await WaitForResponseAsync(cts.Token);
                
                if (responseReceived)
                {
                    _logger.LogInformation("成功发送并收到响应");
                    
                    // 如果是新一轮发送（非重传），切换 FCB
                    if (isFirstSend && frame.IsValid && frame.ControlField != null && frame.ControlField.PRM)
                    {
                        var targetEndpoint = endpoint ?? _fcbStates.Keys.FirstOrDefault();
                        if (targetEndpoint != null)
                        {
                            // 确保端点存在于 FCB 状态字典中
                            if (!_fcbStates.ContainsKey(targetEndpoint))
                            {
                                _fcbStates[targetEndpoint] = false;
                                _logger.LogDebug("初始化 FCB 状态: {Endpoint} -> False", targetEndpoint);
                            }
                            
                            var currentFcb = _fcbStates[targetEndpoint];
                            _fcbStates[targetEndpoint] = !currentFcb;
                            _logger.LogDebug("切换 FCB 状态: {Endpoint} -> {FCB}", targetEndpoint, !currentFcb);
                        }
                        else
                        {
                            _logger.LogWarning("无法切换 FCB：未找到目标端点");
                        }
                    }
                    
                    return true;
                }
                else
                {
                    _logger.LogWarning("发送超时（{TimeoutMs}ms），尝试 {Attempt}/{MaxRetries}", 
                        _timeoutMs, attempt + 1, _maxRetries + 1);
                    
                    TimeoutOccurred?.Invoke(this, new TimeoutEventArgs 
                    { 
                        Attempt = attempt, 
                        TimeoutMs = _timeoutMs,
                        Frame = frame,
                        Endpoint = endpoint
                    });
                }

                isFirstSend = false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "发送数据时发生错误（尝试 {Attempt}/{MaxRetries}）", 
                    attempt + 1, _maxRetries + 1);
            }
        }

        _logger.LogError("发送失败：已达到最大重传次数 {MaxRetries}", _maxRetries);
        return false;
    }

    /// <summary>
    /// 等待响应（简化实现）
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否收到响应</returns>
    /// <remarks>
    /// 注意：这是一个简化实现，用于演示超时机制。
    /// 生产环境中应该维护一个等待响应的队列，根据实际收到的响应帧来判断。
    /// 当前实现总是超时，以便演示重传逻辑。
    /// </remarks>
    private async Task<bool> WaitForResponseAsync(CancellationToken cancellationToken)
    {
        try
        {
            // 实际应该等待数据接收事件或响应队列
            // 当前实现会超时，触发重传机制演示
            await Task.Delay(_timeoutMs, cancellationToken);
            return false; // 超时，未收到响应
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    /// <summary>
    /// 获取指定端点的 FCB 状态
    /// </summary>
    /// <param name="endpoint">端点标识</param>
    /// <returns>FCB 状态</returns>
    public bool GetFcbState(string endpoint)
    {
        return _fcbStates.TryGetValue(endpoint, out var fcb) && fcb;
    }

    /// <summary>
    /// 设置指定端点的 FCB 状态
    /// </summary>
    /// <param name="endpoint">端点标识</param>
    /// <param name="fcb">FCB 状态</param>
    public void SetFcbState(string endpoint, bool fcb)
    {
        _fcbStates[endpoint] = fcb;
        _logger.LogDebug("设置 FCB 状态: {Endpoint} -> {FCB}", endpoint, fcb);
    }
}

/// <summary>
/// 超时事件参数
/// </summary>
public class TimeoutEventArgs : EventArgs
{
    /// <summary>
    /// 尝试次数
    /// </summary>
    public int Attempt { get; set; }

    /// <summary>
    /// 超时时间（毫秒）
    /// </summary>
    public int TimeoutMs { get; set; }

    /// <summary>
    /// 超时的帧
    /// </summary>
    public Iec102Frame? Frame { get; set; }

    /// <summary>
    /// 目标端点
    /// </summary>
    public string? Endpoint { get; set; }
}

/// <summary>
/// 重传事件参数
/// </summary>
public class RetransmissionEventArgs : EventArgs
{
    /// <summary>
    /// 重传尝试次数
    /// </summary>
    public int Attempt { get; set; }

    /// <summary>
    /// 重传的帧
    /// </summary>
    public Iec102Frame? Frame { get; set; }

    /// <summary>
    /// 目标端点
    /// </summary>
    public string? Endpoint { get; set; }
}
