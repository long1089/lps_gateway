using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;

namespace LpsGateway.Lib60870;

/// <summary>
/// IEC-102 从站（TCP Server）实现
/// </summary>
/// <remarks>
/// 作为从站（从动端），监听来自主站的连接并响应请求
/// </remarks>
public class Iec102Slave : IIec102Slave, IDisposable
{
    private readonly int _port;
    private readonly ushort _stationAddress;
    private readonly ILogger<Iec102Slave> _logger;
    
    private TcpListener? _listener;
    private readonly ConcurrentDictionary<string, ClientSession> _sessions = new();
    private readonly CancellationTokenSource _cts = new();
    private Task? _acceptTask;
    
    /// <summary>
    /// 是否正在运行
    /// </summary>
    public bool IsRunning { get; private set; }
    
    /// <summary>
    /// 帧接收事件
    /// </summary>
    public event EventHandler<FrameReceivedEventArgs>? FrameReceived;
    
    /// <summary>
    /// 客户端连接事件
    /// </summary>
    public event EventHandler<string>? ClientConnected;
    
    /// <summary>
    /// 客户端断开事件
    /// </summary>
    public event EventHandler<string>? ClientDisconnected;
    
    /// <summary>
    /// 文件对账事件（主站确认接收）
    /// </summary>
    public event EventHandler<FileReconciliationEventArgs>? FileReconciliation;
    
    /// <summary>
    /// 文件重传请求事件
    /// </summary>
    public event EventHandler<FileRetransmitEventArgs>? FileRetransmitRequest;
    
    /// <summary>
    /// 文件过长确认事件（来自主站）
    /// </summary>
    public event EventHandler<FileErrorEventArgs>? FileTooLongAck;
    
    /// <summary>
    /// 文件名格式错误确认事件（来自主站）
    /// </summary>
    public event EventHandler<FileErrorEventArgs>? InvalidFileNameAck;
    
    /// <summary>
    /// 单帧报文过长确认事件（来自主站）
    /// </summary>
    public event EventHandler<FileErrorEventArgs>? FrameTooLongAck;
    
    public Iec102Slave(int port, ushort stationAddress, ILogger<Iec102Slave> logger)
    {
        _port = port;
        _stationAddress = stationAddress;
        _logger = logger;
    }
    
    /// <summary>
    /// 启动从站服务器
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (IsRunning)
        {
            _logger.LogWarning("从站服务器已在运行");
            return Task.CompletedTask;
        }
        
        _logger.LogInformation("启动从站服务器: Port={Port}, StationAddress=0x{StationAddress:X4}", 
            _port, _stationAddress);
        
        _listener = new TcpListener(IPAddress.Any, _port);
        _listener.Start();
        IsRunning = true;
        
        _acceptTask = Task.Run(() => AcceptClientsAsync(_cts.Token), cancellationToken);
        
        _logger.LogInformation("从站服务器已启动");
        
        return Task.CompletedTask;
    }
    
    /// <summary>
    /// 停止从站服务器
    /// </summary>
    public async Task StopAsync()
    {
        if (!IsRunning)
        {
            return;
        }
        
        _logger.LogInformation("停止从站服务器");
        
        _cts.Cancel();
        IsRunning = false;
        
        if (_acceptTask != null)
        {
            try
            {
                await _acceptTask;
            }
            catch (OperationCanceledException)
            {
                // 预期的取消
            }
        }
        
        // 关闭所有会话
        foreach (var session in _sessions.Values)
        {
            session.Dispose();
        }
        _sessions.Clear();
        
        _listener?.Stop();
        
        _logger.LogInformation("从站服务器已停止");
    }
    
    /// <summary>
    /// 接受客户端连接
    /// </summary>
    private async Task AcceptClientsAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _listener != null)
        {
            try
            {
                var client = await _listener.AcceptTcpClientAsync(cancellationToken);
                var endpoint = client.Client.RemoteEndPoint?.ToString() ?? "unknown";
                
                _logger.LogInformation("接受主站连接: {Endpoint}", endpoint);
                
                var session = new ClientSession(client, endpoint, _logger);
                _sessions[endpoint] = session;
                
                ClientConnected?.Invoke(this, endpoint);
                
                // 启动会话处理
                _ = Task.Run(() => HandleSessionAsync(session, cancellationToken), cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "接受客户端连接时发生错误");
            }
        }
    }
    
    /// <summary>
    /// 处理客户端会话
    /// </summary>
    private async Task HandleSessionAsync(ClientSession session, CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];
        var frameBuffer = new List<byte>();
        
        while (!cancellationToken.IsCancellationRequested && session.Stream.CanRead)
        {
            try
            {
                var bytesRead = await session.Stream.ReadAsync(buffer, cancellationToken);
                if (bytesRead == 0)
                {
                    _logger.LogInformation("主站断开连接: {Endpoint}", session.Endpoint);
                    break;
                }
                
                frameBuffer.AddRange(buffer.Take(bytesRead));
                
                // 解析并处理帧
                while (TryExtractFrame(frameBuffer, out var frameData))
                {
                    var frame = Iec102Frame.Parse(frameData.ToArray());
                    if (frame.IsValid)
                    {
                        _logger.LogDebug("从 {Endpoint} 接收到帧: {Frame}", session.Endpoint, frame);
                        
                        // 触发事件
                        FrameReceived?.Invoke(this, new FrameReceivedEventArgs 
                        { 
                            Endpoint = session.Endpoint, 
                            Frame = frame 
                        });
                        
                        // 处理帧
                        await ProcessFrameAsync(session, frame, cancellationToken);
                    }
                    else
                    {
                        _logger.LogWarning("从 {Endpoint} 接收到无效帧: {ErrorMessage}", 
                            session.Endpoint, frame.ErrorMessage);
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "处理会话 {Endpoint} 时发生错误", session.Endpoint);
                break;
            }
        }
        
        // 清理会话
        _sessions.TryRemove(session.Endpoint, out _);
        session.Dispose();
        ClientDisconnected?.Invoke(this, session.Endpoint);
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
            int totalLength = 4 + length + 2;
            
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
    /// 处理接收到的帧
    /// </summary>
    private async Task ProcessFrameAsync(ClientSession session, Iec102Frame frame, CancellationToken cancellationToken)
    {
        if (frame.ControlField == null)
        {
            return;
        }
        
        // 主站发送的帧
        if (frame.ControlField.PRM)
        {
            var fc = frame.ControlField.FunctionCode;
            
            // 检查 FCB/FCV
            if (frame.ControlField.FCV)
            {
                var expectedFcb = session.GetExpectedFcb();
                if (frame.ControlField.FCB != expectedFcb)
                {
                    _logger.LogWarning("FCB 不匹配: 期望={Expected}, 实际={Actual}", 
                        expectedFcb, frame.ControlField.FCB);
                    // 重复帧或错误，忽略
                    return;
                }
                
                // 更新 FCB 期望值
                session.ToggleExpectedFcb();
            }
            
            switch (fc)
            {
                case FunctionCodes.ResetRemoteLink:
                    await HandleResetLinkAsync(session, cancellationToken);
                    break;
                    
                case FunctionCodes.RequestLinkStatus:
                    await HandleLinkStatusRequestAsync(session, cancellationToken);
                    break;
                    
                case FunctionCodes.RequestClass1Data:
                    await HandleClass1DataRequestAsync(session, cancellationToken);
                    break;
                    
                case FunctionCodes.RequestClass2Data:
                    await HandleClass2DataRequestAsync(session, cancellationToken);
                    break;
                    
                case FunctionCodes.UserData:
                    await HandleUserDataAsync(session, frame, cancellationToken);
                    break;
                    
                default:
                    _logger.LogWarning("未处理的功能码: {FunctionCode}", fc);
                    break;
            }
        }
    }
    
    /// <summary>
    /// 处理链路复位
    /// </summary>
    private async Task HandleResetLinkAsync(ClientSession session, CancellationToken cancellationToken)
    {
        _logger.LogInformation("处理链路复位请求: {Endpoint}", session.Endpoint);
        
        // 复位 FCB 状态
        session.ResetFcb();
        
        // 发送肯定确认
        await SendAckAsync(session, true, cancellationToken);
    }
    
    /// <summary>
    /// 处理链路状态请求
    /// </summary>
    private async Task HandleLinkStatusRequestAsync(ClientSession session, CancellationToken cancellationToken)
    {
        _logger.LogInformation("处理链路状态请求: {Endpoint}", session.Endpoint);
        
        // 检查该会话是否有1级数据待发送
        var hasClass1Data = session.HasClass1Data();
        
        // 发送链路状态响应
        var control = ControlField.CreateSlaveFrame(FunctionCodes.LinkStatusOrAccessDemand, hasClass1Data, false);
        var frame = Iec102Frame.BuildFixedFrame(control, _stationAddress);
        
        await SendFrameAsync(session, frame, cancellationToken);
    }
    
    /// <summary>
    /// 处理1级数据请求
    /// </summary>
    private async Task HandleClass1DataRequestAsync(ClientSession session, CancellationToken cancellationToken)
    {
        _logger.LogInformation("处理1级数据请求: {Endpoint}", session.Endpoint);
        
        // 从会话的1级队列中取数据
        if (session.TryDequeueClass1Data(out var queuedFrame))
        {
            // 检查是否还有更多1级数据（设置ACD标志）
            bool hasMoreClass1Data = session.HasClass1Data();
            
            await SendUserDataAsync(session, queuedFrame.TypeId, queuedFrame.Cot, queuedFrame.Data, hasMoreClass1Data, cancellationToken);
        }
        else
        {
            // 无数据，ACD=0
            await SendNoDataAsync(session, false, cancellationToken);
        }
    }
    
    /// <summary>
    /// 处理2级数据请求
    /// </summary>
    private async Task HandleClass2DataRequestAsync(ClientSession session, CancellationToken cancellationToken)
    {
        _logger.LogInformation("处理2级数据请求: {Endpoint}", session.Endpoint);
        
        // 从会话的2级队列中取数据
        if (session.TryDequeueClass2Data(out var queuedFrame))
        {
            // 检查是否有1级数据需要传输（设置ACD标志）
            bool hasClass1Data = session.HasClass1Data();
            
            await SendUserDataAsync(session, queuedFrame.TypeId, queuedFrame.Cot, queuedFrame.Data, hasClass1Data, cancellationToken);
        }
        else
        {
            // 无2级数据，检查是否有1级数据（设置ACD标志）
            bool hasClass1Data = session.HasClass1Data();
            await SendNoDataAsync(session, hasClass1Data, cancellationToken);
        }
    }
    
    /// <summary>
    /// 处理用户数据（主站下发）
    /// </summary>
    private async Task HandleUserDataAsync(ClientSession session, Iec102Frame frame, CancellationToken cancellationToken)
    {
        if (frame.UserData.Length < 6)
        {
            _logger.LogWarning("用户数据长度不足");
            return;
        }
        
        byte typeId = frame.UserData[0];
        byte cot = frame.UserData[2];
        
        _logger.LogInformation("处理用户数据: TypeId=0x{TypeId:X2}, COT=0x{Cot:X2}", typeId, cot);
        
        // 处理时间同步
        if (typeId == 0x8B && cot == 0x06)
        {
            _logger.LogInformation("收到时间同步请求");
            // 发送确认
            await SendTimeSyncConfirmAsync(session, cancellationToken);
        }
        // 处理文件点播
        else if (typeId == 0x8D && cot == 0x06)
        {
            _logger.LogInformation("收到文件点播请求");
            // 发送确认并准备文件传输
            await SendFileRequestConfirmAsync(session, cancellationToken);
        }
        // 处理文件取消
        else if (typeId == 0x8E && cot == 0x06)
        {
            _logger.LogInformation("收到文件点播取消请求");
            // 发送确认
            await SendFileCancelConfirmAsync(session, cancellationToken);
        }
        // 处理文件对账（主站确认接收）
        else if (typeId == 0x90 && cot == 0x0A)
        {
            await HandleFileReconciliationAsync(session, frame.UserData, cancellationToken);
        }
        // 处理文件重传请求
        else if (typeId == 0x91 && cot == 0x0D)
        {
            await HandleFileRetransmitAsync(session, frame.UserData, cancellationToken);
        }
        // 处理文件过长确认（主站）
        else if (typeId == 0x92 && cot == 0x0F)
        {
            await HandleFileTooLongFromMasterAsync(session, cancellationToken);
        }
        // 处理文件名格式错误确认（主站）
        else if (typeId == 0x93 && cot == 0x11)
        {
            await HandleInvalidFileNameFromMasterAsync(session, cancellationToken);
        }
        // 处理单帧报文过长确认（主站）
        else if (typeId == 0x94 && cot == 0x13)
        {
            await HandleFrameTooLongFromMasterAsync(session, cancellationToken);
        }
        else
        {
            // 发送肯定确认
            await SendAckAsync(session, true, cancellationToken);
        }
    }
    
    /// <summary>
    /// 发送确认帧
    /// </summary>
    private async Task SendAckAsync(ClientSession session, bool positive, CancellationToken cancellationToken)
    {
        var fc = positive ? FunctionCodes.AckPositive : FunctionCodes.AckNegative;
        var control = ControlField.CreateSlaveFrame(fc);
        var frame = Iec102Frame.BuildFixedFrame(control, _stationAddress);
        
        await SendFrameAsync(session, frame, cancellationToken);
    }
    
    /// <summary>
    /// 发送无数据响应
    /// </summary>
    private async Task SendNoDataAsync(ClientSession session, bool acd, CancellationToken cancellationToken)
    {
        var control = ControlField.CreateSlaveFrame(FunctionCodes.NoDataAvailable, acd, false);
        var frame = Iec102Frame.BuildFixedFrame(control, _stationAddress);
        
        await SendFrameAsync(session, frame, cancellationToken);
    }
    
    /// <summary>
    /// 发送用户数据
    /// </summary>
    private async Task SendUserDataAsync(
        ClientSession session, 
        byte typeId, 
        byte cot, 
        byte[] data,
        bool acd,
        CancellationToken cancellationToken)
    {
        // ACD标志表示是否有1级数据需要传输
        var control = ControlField.CreateSlaveFrame(FunctionCodes.ResponseUserData, acd, false);
        
        var asdu = new List<byte>
        {
            typeId,
            0x01, // VSQ
            cot,
            (byte)(_stationAddress & 0xFF),
            (byte)((_stationAddress >> 8) & 0xFF),
            0x00  // RecordAddr
        };
        asdu.AddRange(data);
        
        var frame = Iec102Frame.BuildVariableFrame(control, _stationAddress, asdu.ToArray());
        
        await SendFrameAsync(session, frame, cancellationToken);
    }
    
    /// <summary>
    /// 发送时间同步确认
    /// </summary>
    private async Task SendTimeSyncConfirmAsync(ClientSession session, CancellationToken cancellationToken)
    {
        var control = ControlField.CreateSlaveFrame(FunctionCodes.ResponseUserData);
        
        var asdu = new List<byte>
        {
            0x8B, // TypeId: TimeSync
            0x01, // VSQ
            0x07, // COT: ActConfirm
            (byte)(_stationAddress & 0xFF),
            (byte)((_stationAddress >> 8) & 0xFF),
            0x00  // RecordAddr
        };
        
        var frame = Iec102Frame.BuildVariableFrame(control, _stationAddress, asdu.ToArray());
        
        await SendFrameAsync(session, frame, cancellationToken);
    }
    
    /// <summary>
    /// 发送文件点播确认
    /// </summary>
    private async Task SendFileRequestConfirmAsync(ClientSession session, CancellationToken cancellationToken)
    {
        var control = ControlField.CreateSlaveFrame(FunctionCodes.ResponseUserData);
        
        var asdu = new List<byte>
        {
            0x8D, // TypeId: FileRequest
            0x01, // VSQ
            0x07, // COT: ActConfirm
            (byte)(_stationAddress & 0xFF),
            (byte)((_stationAddress >> 8) & 0xFF),
            0x00  // RecordAddr
        };
        
        var frame = Iec102Frame.BuildVariableFrame(control, _stationAddress, asdu.ToArray());
        
        await SendFrameAsync(session, frame, cancellationToken);
    }
    
    /// <summary>
    /// 发送文件取消确认
    /// </summary>
    private async Task SendFileCancelConfirmAsync(ClientSession session, CancellationToken cancellationToken)
    {
        var control = ControlField.CreateSlaveFrame(FunctionCodes.ResponseUserData);
        
        var asdu = new List<byte>
        {
            0x8E, // TypeId: FileCancel
            0x01, // VSQ
            0x07, // COT: ActConfirm
            (byte)(_stationAddress & 0xFF),
            (byte)((_stationAddress >> 8) & 0xFF),
            0x00  // RecordAddr
        };
        
        var frame = Iec102Frame.BuildVariableFrame(control, _stationAddress, asdu.ToArray());
        
        await SendFrameAsync(session, frame, cancellationToken);
    }
    
    /// <summary>
    /// 发送帧到会话
    /// </summary>
    private async Task SendFrameAsync(ClientSession session, byte[] frame, CancellationToken cancellationToken)
    {
        await session.SendLock.WaitAsync(cancellationToken);
        try
        {
            _logger.LogDebug("向 {Endpoint} 发送帧: {Frame}", session.Endpoint, BitConverter.ToString(frame));
            await session.Stream.WriteAsync(frame, cancellationToken);
            await session.Stream.FlushAsync(cancellationToken);
        }
        finally
        {
            session.SendLock.Release();
        }
    }
    
    /// <summary>
    /// 处理文件对账（主站确认接收）
    /// </summary>
    private async Task HandleFileReconciliationAsync(ClientSession session, byte[] userData, CancellationToken cancellationToken)
    {
        if (userData.Length < 10) // TYP(1) + VSQ(1) + COT(1) + CAddr(2) + RAddr(1) + FileLength(4)
        {
            _logger.LogWarning("文件对账数据长度不足");
            return;
        }
        
        // 提取文件长度（小端序）
        int fileLength = userData[6] | (userData[7] << 8) | (userData[8] << 16) | (userData[9] << 24);
        
        _logger.LogInformation("收到文件对账: FileLength={FileLength}", fileLength);
        
        // 触发事件
        FileReconciliation?.Invoke(this, new FileReconciliationEventArgs 
        { 
            Endpoint = session.Endpoint, 
            FileLength = fileLength 
        });
        
        // 发送确认（子站确认长度一致）
        var control = ControlField.CreateSlaveFrame(FunctionCodes.ResponseUserData);
        
        var asdu = new List<byte>
        {
            0x90, // TypeId: Reconciliation
            0x01, // VSQ
            0x0B, // COT: 子站确认主站接收长度一致
            (byte)(_stationAddress & 0xFF),
            (byte)((_stationAddress >> 8) & 0xFF),
            0x00  // RecordAddr
        };
        
        var frame = Iec102Frame.BuildVariableFrame(control, _stationAddress, asdu.ToArray());
        
        await SendFrameAsync(session, frame, cancellationToken);
    }
    
    /// <summary>
    /// 处理文件重传请求
    /// </summary>
    private async Task HandleFileRetransmitAsync(ClientSession session, byte[] userData, CancellationToken cancellationToken)
    {
        _logger.LogInformation("收到文件重传请求");
        
        // 触发事件
        FileRetransmitRequest?.Invoke(this, new FileRetransmitEventArgs 
        { 
            Endpoint = session.Endpoint 
        });
        
        // 发送确认（子站确认重传）
        var control = ControlField.CreateSlaveFrame(FunctionCodes.ResponseUserData);
        
        var asdu = new List<byte>
        {
            0x91, // TypeId: Retransmit
            0x01, // VSQ
            0x0E, // COT: 子站确认文件重传
            (byte)(_stationAddress & 0xFF),
            (byte)((_stationAddress >> 8) & 0xFF),
            0x00  // RecordAddr
        };
        
        var frame = Iec102Frame.BuildVariableFrame(control, _stationAddress, asdu.ToArray());
        
        await SendFrameAsync(session, frame, cancellationToken);
    }
    
    /// <summary>
    /// 处理文件过长确认（来自主站）
    /// </summary>
    private async Task HandleFileTooLongFromMasterAsync(ClientSession session, CancellationToken cancellationToken)
    {
        _logger.LogInformation("收到主站文件过长确认");
        
        // 触发事件
        FileTooLongAck?.Invoke(this, new FileErrorEventArgs 
        { 
            Endpoint = session.Endpoint, 
            ErrorType = "FileTooLong" 
        });
        
        // 发送确认
        await SendAckAsync(session, true, cancellationToken);
    }
    
    /// <summary>
    /// 处理文件名格式错误确认（来自主站）
    /// </summary>
    private async Task HandleInvalidFileNameFromMasterAsync(ClientSession session, CancellationToken cancellationToken)
    {
        _logger.LogInformation("收到主站文件名格式错误确认");
        
        // 触发事件
        InvalidFileNameAck?.Invoke(this, new FileErrorEventArgs 
        { 
            Endpoint = session.Endpoint, 
            ErrorType = "InvalidFileName" 
        });
        
        // 发送确认
        await SendAckAsync(session, true, cancellationToken);
    }
    
    /// <summary>
    /// 处理单帧报文过长确认（来自主站）
    /// </summary>
    private async Task HandleFrameTooLongFromMasterAsync(ClientSession session, CancellationToken cancellationToken)
    {
        _logger.LogInformation("收到主站单帧报文过长确认");
        
        // 触发事件
        FrameTooLongAck?.Invoke(this, new FileErrorEventArgs 
        { 
            Endpoint = session.Endpoint, 
            ErrorType = "FrameTooLong" 
        });
        
        // 发送确认
        await SendAckAsync(session, true, cancellationToken);
    }
    
    /// <summary>
    /// 排队1级数据到指定会话
    /// </summary>
    public void QueueClass1DataToSession(string endpoint, byte typeId, byte cot, byte[] data)
    {
        if (_sessions.TryGetValue(endpoint, out var session))
        {
            session.QueueClass1Data(new QueuedFrame { TypeId = typeId, Cot = cot, Data = data });
        }
        else
        {
            _logger.LogWarning("会话不存在，无法排队1级数据: {Endpoint}", endpoint);
        }
    }
    
    /// <summary>
    /// 排队2级数据到指定会话
    /// </summary>
    public void QueueClass2DataToSession(string endpoint, byte typeId, byte cot, byte[] data)
    {
        if (_sessions.TryGetValue(endpoint, out var session))
        {
            session.QueueClass2Data(new QueuedFrame { TypeId = typeId, Cot = cot, Data = data });
        }
        else
        {
            _logger.LogWarning("会话不存在，无法排队2级数据: {Endpoint}", endpoint);
        }
    }
    
    /// <summary>
    /// 排队1级数据到所有会话（广播）
    /// </summary>
    public void QueueClass1DataToAll(byte typeId, byte cot, byte[] data)
    {
        foreach (var session in _sessions.Values)
        {
            session.QueueClass1Data(new QueuedFrame { TypeId = typeId, Cot = cot, Data = data });
        }
        _logger.LogDebug("广播1级数据到所有会话: TypeId=0x{TypeId:X2}, 会话数={Count}", typeId, _sessions.Count);
    }
    
    /// <summary>
    /// 排队2级数据到所有会话（广播）
    /// </summary>
    public void QueueClass2DataToAll(byte typeId, byte cot, byte[] data)
    {
        foreach (var session in _sessions.Values)
        {
            session.QueueClass2Data(new QueuedFrame { TypeId = typeId, Cot = cot, Data = data });
        }
        _logger.LogDebug("广播2级数据到所有会话: TypeId=0x{TypeId:X2}, 会话数={Count}", typeId, _sessions.Count);
    }
    
    /// <summary>
    /// 获取所有活动会话的端点列表
    /// </summary>
    public IEnumerable<string> GetActiveSessionEndpoints()
    {
        return _sessions.Keys.ToList();
    }
    
    public void Dispose()
    {
        _cts.Cancel();
        _listener?.Stop();
        
        foreach (var session in _sessions.Values)
        {
            session.Dispose();
        }
        
        _cts.Dispose();
    }
}

/// <summary>
/// 客户端会话
/// </summary>
/// <remarks>
/// 每个会话维护独立的数据队列和FCB状态，支持多客户端并发
/// </remarks>
internal class ClientSession : IDisposable
{
    public TcpClient Client { get; }
    public NetworkStream Stream { get; }
    public string Endpoint { get; }
    public SemaphoreSlim SendLock { get; } = new(1, 1);
    
    // 会话级数据队列
    private readonly ConcurrentQueue<QueuedFrame> _class1Queue = new();
    private readonly ConcurrentQueue<QueuedFrame> _class2Queue = new();
    
    private bool _expectedFcb;
    private readonly ILogger _logger;
    
    public ClientSession(TcpClient client, string endpoint, ILogger logger)
    {
        Client = client;
        Stream = client.GetStream();
        Endpoint = endpoint;
        _expectedFcb = false;
        _logger = logger;
    }
    
    public bool GetExpectedFcb() => _expectedFcb;
    
    public void ToggleExpectedFcb()
    {
        _expectedFcb = !_expectedFcb;
        _logger.LogDebug("FCB 状态切换: {Endpoint} -> {FCB}", Endpoint, _expectedFcb);
    }
    
    public void ResetFcb()
    {
        _expectedFcb = false;
        _logger.LogDebug("FCB 状态复位: {Endpoint}", Endpoint);
    }
    
    /// <summary>
    /// 排队1级数据
    /// </summary>
    public void QueueClass1Data(QueuedFrame frame)
    {
        _class1Queue.Enqueue(frame);
        _logger.LogDebug("会话 {Endpoint} 排队1级数据: TypeId=0x{TypeId:X2}", Endpoint, frame.TypeId);
    }
    
    /// <summary>
    /// 排队2级数据
    /// </summary>
    public void QueueClass2Data(QueuedFrame frame)
    {
        _class2Queue.Enqueue(frame);
        _logger.LogDebug("会话 {Endpoint} 排队2级数据: TypeId=0x{TypeId:X2}", Endpoint, frame.TypeId);
    }
    
    /// <summary>
    /// 检查是否有1级数据
    /// </summary>
    public bool HasClass1Data() => !_class1Queue.IsEmpty;
    
    /// <summary>
    /// 检查是否有2级数据
    /// </summary>
    public bool HasClass2Data() => !_class2Queue.IsEmpty;
    
    /// <summary>
    /// 尝试取出1级数据
    /// </summary>
    public bool TryDequeueClass1Data(out QueuedFrame? frame)
    {
        return _class1Queue.TryDequeue(out frame);
    }
    
    /// <summary>
    /// 尝试取出2级数据
    /// </summary>
    public bool TryDequeueClass2Data(out QueuedFrame? frame)
    {
        return _class2Queue.TryDequeue(out frame);
    }
    
    public void Dispose()
    {
        SendLock.Dispose();
        Stream.Dispose();
        Client.Dispose();
    }
}

/// <summary>
/// 排队的帧数据
/// </summary>
internal class QueuedFrame
{
    public byte TypeId { get; set; }
    public byte Cot { get; set; }
    public byte[] Data { get; set; } = Array.Empty<byte>();
}

/// <summary>
/// 帧接收事件参数
/// </summary>
public class FrameReceivedEventArgs : EventArgs
{
    public string Endpoint { get; set; } = string.Empty;
    public Iec102Frame Frame { get; set; } = null!;
}

/// <summary>
/// 文件对账事件参数
/// </summary>
public class FileReconciliationEventArgs : EventArgs
{
    public string Endpoint { get; set; } = string.Empty;
    public int FileLength { get; set; }
}

/// <summary>
/// 文件重传事件参数
/// </summary>
public class FileRetransmitEventArgs : EventArgs
{
    public string Endpoint { get; set; } = string.Empty;
}

/// <summary>
/// 文件错误事件参数
/// </summary>
public class FileErrorEventArgs : EventArgs
{
    public string Endpoint { get; set; } = string.Empty;
    public string ErrorType { get; set; } = string.Empty;
}

