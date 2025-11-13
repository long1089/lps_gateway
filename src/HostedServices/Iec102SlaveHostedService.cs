using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using LpsGateway.Services;

namespace LpsGateway.HostedServices;

/// <summary>
/// IEC-102 从站（TCP Server）托管服务
/// </summary>
public class Iec102SlaveHostedService : IHostedService
{
    private readonly Lib60870.Iec102Slave _slave;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<Iec102SlaveHostedService> _logger;
    private readonly Iec102SlaveOptions _options;
    
    public Iec102SlaveHostedService(
        ILogger<Iec102SlaveHostedService> logger,
        IOptions<Iec102SlaveOptions> options,
        IServiceProvider serviceProvider,
        ILoggerFactory loggerFactory)  // 添加 ILoggerFactory 参数
    {
        _logger = logger;
        _options = options.Value;
        _serviceProvider = serviceProvider;
        
        // 使用 ILoggerFactory 创建正确类型的 logger
        var slaveLogger = loggerFactory.CreateLogger<Lib60870.Iec102Slave>();
        
        _slave = new Lib60870.Iec102Slave(  
            _options.Port,
            _options.StationAddress,
            slaveLogger);
        
        // 设置服务提供者工厂，用于文件传输初始化
        _slave.SetServiceProviderFactory(() => _serviceProvider);
        
        // 订阅事件
        _slave.ClientConnected += OnClientConnected;
        _slave.ClientDisconnected += OnClientDisconnected;
        _slave.FrameReceived += OnFrameReceived;
    }
    
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("IEC-102 从站服务已禁用");
            return;
        }
        
        _logger.LogInformation("启动 IEC-102 从站服务: Port={Port}, StationAddress=0x{StationAddress:X4}", 
            _options.Port, _options.StationAddress);
        
        await _slave.StartAsync(cancellationToken);
    }
    
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("停止 IEC-102 从站服务");
        await _slave.StopAsync();
    }
    
    private void OnClientConnected(object? sender, string endpoint)
    {
        _logger.LogInformation("主站已连接: {Endpoint}", endpoint);
        // 不再在连接时自动初始化，而是在收到2级数据请求时处理
    }
    
    private void OnClientDisconnected(object? sender, string endpoint)
    {
        _logger.LogInformation("主站已断开: {Endpoint}", endpoint);
    }
    
    private void OnFrameReceived(object? sender, Lib60870.FrameReceivedEventArgs e)
    {
        _logger.LogDebug("从 {Endpoint} 接收到帧: TypeId=0x{TypeId:X2}", 
            e.Endpoint, 
            e.Frame.UserData.Length > 0 ? e.Frame.UserData[0] : 0);
    }
}

/// <summary>
/// IEC-102 从站配置选项
/// </summary>
public class Iec102SlaveOptions
{
    /// <summary>
    /// 是否启用从站服务
    /// </summary>
    public bool Enabled { get; set; } = true;
    
    /// <summary>
    /// 监听端口
    /// </summary>
    public int Port { get; set; } = 3000;
    
    /// <summary>
    /// 站地址
    /// </summary>
    public ushort StationAddress { get; set; } = 0xFFFF;
}
