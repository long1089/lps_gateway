using System.Net.Sockets;
using Microsoft.Extensions.Logging;

namespace LpsGateway.Lib60870;

/// <summary>
/// IEC-102 主站（TCP Client）实现
/// </summary>
/// <remarks>
/// 作为主站（启动端），连接到从站（子站）并发起通信
/// </remarks>
public class Iec102Master : IDisposable
{
    private readonly string _host;
    private readonly int _port;
    private readonly ushort _stationAddress;
    private readonly ILogger<Iec102Master> _logger;
    private readonly int _timeoutMs;
    private readonly int _maxRetries;
    
    private TcpClient? _client;
    private NetworkStream? _stream;
    private bool _fcbState;
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly CancellationTokenSource _cts = new();
    private Task? _receiveTask;
    
    /// <summary>
    /// 是否已连接
    /// </summary>
    public bool IsConnected => _client?.Connected ?? false;
    
    /// <summary>
    /// 数据接收事件
    /// </summary>
    public event EventHandler<Iec102Frame>? FrameReceived;
    
    /// <summary>
    /// 连接状态变化事件
    /// </summary>
    public event EventHandler<bool>? ConnectionChanged;
    
    public Iec102Master(
        string host, 
        int port, 
        ushort stationAddress,
        ILogger<Iec102Master> logger,
        int timeoutMs = 5000,
        int maxRetries = 3)
    {
        _host = host;
        _port = port;
        _stationAddress = stationAddress;
        _logger = logger;
        _timeoutMs = timeoutMs;
        _maxRetries = maxRetries;
        _fcbState = false;
    }
    
    /// <summary>
    /// 连接到从站
    /// </summary>
    public async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("连接到从站: {Host}:{Port}", _host, _port);
            
            _client = new TcpClient();
            await _client.ConnectAsync(_host, _port, cancellationToken);
            _stream = _client.GetStream();
            
            _logger.LogInformation("已成功连接到从站");
            
            // 启动接收任务
            _receiveTask = Task.Run(() => ReceiveLoopAsync(_cts.Token), cancellationToken);
            
            ConnectionChanged?.Invoke(this, true);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "连接从站失败");
            return false;
        }
    }
    
    /// <summary>
    /// 断开连接
    /// </summary>
    public async Task DisconnectAsync()
    {
        _logger.LogInformation("断开与从站的连接");
        
        _cts.Cancel();
        
        if (_receiveTask != null)
        {
            try
            {
                await _receiveTask;
            }
            catch (OperationCanceledException)
            {
                // 预期的取消
            }
        }
        
        _stream?.Close();
        _client?.Close();
        
        ConnectionChanged?.Invoke(this, false);
    }
    
    /// <summary>
    /// 接收数据循环
    /// </summary>
    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];
        var frameBuffer = new List<byte>();
        
        while (!cancellationToken.IsCancellationRequested && _stream != null)
        {
            try
            {
                var bytesRead = await _stream.ReadAsync(buffer, cancellationToken);
                if (bytesRead == 0)
                {
                    _logger.LogWarning("从站断开连接");
                    break;
                }
                
                // 添加到帧缓冲区
                frameBuffer.AddRange(buffer.Take(bytesRead));
                
                // 尝试解析完整帧
                while (TryExtractFrame(frameBuffer, out var frameData))
                {
                    var frame = Iec102Frame.Parse(frameData.ToArray());
                    if (frame.IsValid)
                    {
                        _logger.LogDebug("接收到有效帧: {Frame}", frame);
                        FrameReceived?.Invoke(this, frame);
                    }
                    else
                    {
                        _logger.LogWarning("接收到无效帧: {ErrorMessage}", frame.ErrorMessage);
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "接收数据时发生错误");
                break;
            }
        }
    }
    
    /// <summary>
    /// 从缓冲区提取完整帧
    /// </summary>
    private bool TryExtractFrame(List<byte> buffer, out List<byte> frameData)
    {
        frameData = new List<byte>();
        
        if (buffer.Count == 0)
            return false;
        
        // 单字节确认帧
        if (buffer[0] == 0xE5)
        {
            frameData.Add(buffer[0]);
            buffer.RemoveAt(0);
            return true;
        }
        
        // 固定长度帧
        if (buffer[0] == 0x10 && buffer.Count >= 5)
        {
            frameData.AddRange(buffer.Take(5));
            buffer.RemoveRange(0, 5);
            return true;
        }
        
        // 可变长度帧
        if (buffer[0] == 0x68 && buffer.Count >= 4)
        {
            byte length = buffer[1];
            int totalLength = 4 + length + 2; // start(4) + data(length) + cs(1) + end(1)
            
            if (buffer.Count >= totalLength)
            {
                frameData.AddRange(buffer.Take(totalLength));
                buffer.RemoveRange(0, totalLength);
                return true;
            }
        }
        
        return false;
    }
    
    /// <summary>
    /// 发送帧
    /// </summary>
    private async Task<bool> SendFrameAsync(byte[] frame, CancellationToken cancellationToken = default)
    {
        if (_stream == null || !IsConnected)
        {
            _logger.LogError("未连接到从站");
            return false;
        }
        
        await _sendLock.WaitAsync(cancellationToken);
        try
        {
            _logger.LogDebug("发送帧: {Frame}", BitConverter.ToString(frame));
            await _stream.WriteAsync(frame, cancellationToken);
            await _stream.FlushAsync(cancellationToken);
            return true;
        }
        finally
        {
            _sendLock.Release();
        }
    }
    
    /// <summary>
    /// 发送固定长度帧
    /// </summary>
    private async Task<bool> SendFixedFrameAsync(byte functionCode, bool fcv, CancellationToken cancellationToken = default)
    {
        var control = ControlField.CreateMasterFrame(functionCode, _fcbState, fcv);
        var frame = Iec102Frame.BuildFixedFrame(control, _stationAddress);
        
        var result = await SendFrameAsync(frame, cancellationToken);
        
        // 如果 FCV=1，且发送成功，切换 FCB
        if (result && fcv)
        {
            _fcbState = !_fcbState;
        }
        
        return result;
    }
    
    /// <summary>
    /// 复位远方链路
    /// </summary>
    public async Task<bool> ResetLinkAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("发送链路复位命令");
        _fcbState = false; // 复位时 FCB 归零
        return await SendFixedFrameAsync(FunctionCodes.ResetRemoteLink, false, cancellationToken);
    }
    
    /// <summary>
    /// 请求链路状态
    /// </summary>
    public async Task<bool> RequestLinkStatusAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("请求链路状态");
        return await SendFixedFrameAsync(FunctionCodes.RequestLinkStatus, false, cancellationToken);
    }
    
    /// <summary>
    /// 请求1级用户数据
    /// </summary>
    public async Task<bool> RequestClass1DataAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("请求1级用户数据");
        return await SendFixedFrameAsync(FunctionCodes.RequestClass1Data, true, cancellationToken);
    }
    
    /// <summary>
    /// 请求2级用户数据
    /// </summary>
    public async Task<bool> RequestClass2DataAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("请求2级用户数据");
        return await SendFixedFrameAsync(FunctionCodes.RequestClass2Data, true, cancellationToken);
    }
    
    /// <summary>
    /// 发送时间同步命令
    /// </summary>
    public async Task<bool> SendTimeSyncAsync(DateTime utcTime, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("发送时间同步命令: {Time}", utcTime);
        
        // 构建时间同步 ASDU
        var cp56Time = Gateway.Protocol.IEC102.Iec102Frame.BuildCp56Time2a(utcTime);
        var control = ControlField.CreateMasterFrame(FunctionCodes.UserData, _fcbState, true);
        
        // 构建 ASDU
        var asdu = new List<byte>
        {
            0x8B, // TypeId: TimeSync
            0x01, // VSQ
            0x06, // COT: Activate
            (byte)(_stationAddress & 0xFF),
            (byte)((_stationAddress >> 8) & 0xFF),
            0x00  // RecordAddr
        };
        asdu.AddRange(cp56Time);
        
        var frame = Iec102Frame.BuildVariableFrame(control, _stationAddress, asdu.ToArray());
        
        var result = await SendFrameAsync(frame, cancellationToken);
        if (result)
        {
            _fcbState = !_fcbState;
        }
        
        return result;
    }
    
    /// <summary>
    /// 发送文件点播请求
    /// </summary>
    public async Task<bool> SendFileRequestAsync(
        byte reportTypeCode, 
        byte mode, 
        DateTime referenceTime,
        DateTime? endTime = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("发送文件点播请求: ReportType={ReportType}, Mode={Mode}", reportTypeCode, mode);
        
        var cp56Ref = Gateway.Protocol.IEC102.Iec102Frame.BuildCp56Time2a(referenceTime);
        var cp56End = endTime.HasValue 
            ? Gateway.Protocol.IEC102.Iec102Frame.BuildCp56Time2a(endTime.Value) 
            : Array.Empty<byte>();
        
        var data = Gateway.Protocol.IEC102.Iec102Frame.BuildFileRequestData(
            reportTypeCode, mode, cp56Ref, cp56End);
        
        var control = ControlField.CreateMasterFrame(FunctionCodes.UserData, _fcbState, true);
        
        var asdu = new List<byte>
        {
            0x8D, // TypeId: FileRequest
            0x01, // VSQ
            0x06, // COT: Activate
            (byte)(_stationAddress & 0xFF),
            (byte)((_stationAddress >> 8) & 0xFF),
            0x00  // RecordAddr
        };
        asdu.AddRange(data);
        
        var frame = Iec102Frame.BuildVariableFrame(control, _stationAddress, asdu.ToArray());
        
        var result = await SendFrameAsync(frame, cancellationToken);
        if (result)
        {
            _fcbState = !_fcbState;
        }
        
        return result;
    }
    
    /// <summary>
    /// 发送文件点播取消
    /// </summary>
    public async Task<bool> SendFileCancelAsync(
        byte reportTypeCode,
        byte cancelScope,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("发送文件点播取消: ReportType={ReportType}, Scope={Scope}", 
            reportTypeCode, cancelScope);
        
        var control = ControlField.CreateMasterFrame(FunctionCodes.UserData, _fcbState, true);
        
        var asdu = new List<byte>
        {
            0x8E, // TypeId: FileCancel
            0x01, // VSQ
            0x06, // COT: Activate
            (byte)(_stationAddress & 0xFF),
            (byte)((_stationAddress >> 8) & 0xFF),
            0x00, // RecordAddr
            reportTypeCode,
            cancelScope
        };
        
        var frame = Iec102Frame.BuildVariableFrame(control, _stationAddress, asdu.ToArray());
        
        var result = await SendFrameAsync(frame, cancellationToken);
        if (result)
        {
            _fcbState = !_fcbState;
        }
        
        return result;
    }
    
    public void Dispose()
    {
        _cts.Cancel();
        _stream?.Dispose();
        _client?.Dispose();
        _sendLock.Dispose();
        _cts.Dispose();
    }
}
