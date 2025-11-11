using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LpsGateway.HostedServices;

/// <summary>
/// IEC-102 主站（TCP Client）托管服务
/// </summary>
/// <remarks>
/// 可选服务，用于主动连接到远程从站
/// </remarks>
public class Iec102MasterHostedService : IHostedService, IDisposable
{
    private readonly Lib60870.Iec102Master _master;
    private readonly ILogger<Iec102MasterHostedService> _logger;
    private readonly Iec102MasterOptions _options;
    private Timer? _pollingTimer;
    
    public Iec102MasterHostedService(
        ILogger<Iec102MasterHostedService> logger,
        IOptions<Iec102MasterOptions> options)
    {
        _logger = logger;
        _options = options.Value;
        
        var masterLogger = logger as ILogger<Lib60870.Iec102Master>
            ?? throw new InvalidOperationException("Unable to create master logger");
        
        _master = new Lib60870.Iec102Master(
            _options.Host,
            _options.Port,
            _options.StationAddress,
            masterLogger,
            _options.TimeoutMs,
            _options.MaxRetries);
        
        // 订阅事件
        _master.FrameReceived += OnFrameReceived;
        _master.ConnectionChanged += OnConnectionChanged;
    }
    
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("IEC-102 主站服务已禁用");
            return;
        }
        
        _logger.LogInformation("启动 IEC-102 主站服务: Host={Host}:{Port}, StationAddress=0x{StationAddress:X4}", 
            _options.Host, _options.Port, _options.StationAddress);
        
        // 连接到从站
        var connected = await _master.ConnectAsync(cancellationToken);
        if (!connected)
        {
            _logger.LogError("无法连接到从站，将在后台重试");
            return;
        }
        
        // 初始化链路
        await _master.ResetLinkAsync(cancellationToken);
        await Task.Delay(100, cancellationToken);
        await _master.RequestLinkStatusAsync(cancellationToken);
        
        // 启动轮询定时器（如果配置了）
        if (_options.PollingIntervalSeconds > 0)
        {
            _pollingTimer = new Timer(
                async _ => await PollDataAsync(),
                null,
                TimeSpan.FromSeconds(_options.PollingIntervalSeconds),
                TimeSpan.FromSeconds(_options.PollingIntervalSeconds));
        }
    }
    
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("停止 IEC-102 主站服务");
        
        _pollingTimer?.Dispose();
        await _master.DisconnectAsync();
    }
    
    /// <summary>
    /// 轮询数据
    /// </summary>
    private async Task PollDataAsync()
    {
        if (!_master.IsConnected)
        {
            _logger.LogWarning("未连接到从站，跳过轮询");
            return;
        }
        
        try
        {
            _logger.LogDebug("开始轮询数据");
            
            // 请求2级数据（常规数据）
            await _master.RequestClass2DataAsync();
            
            // 如果需要，也可以请求1级数据（优先级数据）
            if (_options.PollClass1Data)
            {
                await Task.Delay(500); // 短暂延迟
                await _master.RequestClass1DataAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "轮询数据时发生错误");
        }
    }
    
    private void OnFrameReceived(object? sender, Lib60870.Iec102Frame frame)
    {
        if (frame.UserData.Length > 0)
        {
            _logger.LogDebug("接收到数据帧: TypeId=0x{TypeId:X2}, Length={Length}", 
                frame.UserData[0], frame.UserData.Length);
        }
    }
    
    private void OnConnectionChanged(object? sender, bool isConnected)
    {
        if (isConnected)
        {
            _logger.LogInformation("已连接到从站");
        }
        else
        {
            _logger.LogWarning("与从站的连接已断开");
        }
    }
    
    public void Dispose()
    {
        _pollingTimer?.Dispose();
        _master?.Dispose();
    }
}

/// <summary>
/// IEC-102 主站配置选项
/// </summary>
public class Iec102MasterOptions
{
    /// <summary>
    /// 是否启用主站服务
    /// </summary>
    public bool Enabled { get; set; } = false;
    
    /// <summary>
    /// 远程从站主机
    /// </summary>
    public string Host { get; set; } = "localhost";
    
    /// <summary>
    /// 远程从站端口
    /// </summary>
    public int Port { get; set; } = 3000;
    
    /// <summary>
    /// 站地址
    /// </summary>
    public ushort StationAddress { get; set; } = 0xFFFF;
    
    /// <summary>
    /// 超时时间（毫秒）
    /// </summary>
    public int TimeoutMs { get; set; } = 5000;
    
    /// <summary>
    /// 最大重试次数
    /// </summary>
    public int MaxRetries { get; set; } = 3;
    
    /// <summary>
    /// 轮询间隔（秒），0表示不轮询
    /// </summary>
    public int PollingIntervalSeconds { get; set; } = 0;
    
    /// <summary>
    /// 是否轮询1级数据
    /// </summary>
    public bool PollClass1Data { get; set; } = false;
}
