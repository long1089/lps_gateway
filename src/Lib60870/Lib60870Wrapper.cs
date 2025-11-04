using Microsoft.Extensions.Logging;

namespace LpsGateway.Lib60870;

/// <summary>
/// lib60870.NET 库封装器，提供工厂模式创建链路层
/// </summary>
/// <remarks>
/// 当配置 UseLib60870=true 时，尝试使用 lib60870.NET v2.3.0 的 API
/// 如果库不可用，则回退到 TcpLinkLayer 实现
/// </remarks>
public static class Lib60870Wrapper
{
    /// <summary>
    /// 创建链路层实例
    /// </summary>
    /// <param name="useLib60870">是否使用 lib60870.NET</param>
    /// <param name="port">端口号</param>
    /// <param name="logger">日志记录器</param>
    /// <param name="timeoutMs">超时时间（毫秒）</param>
    /// <param name="maxRetries">最大重传次数</param>
    /// <returns>链路层实例</returns>
    public static ILinkLayer CreateLinkLayer(
        bool useLib60870,
        int port,
        ILogger logger,
        int timeoutMs = 5000,
        int maxRetries = 3)
    {
        if (useLib60870)
        {
            logger.LogInformation("尝试使用 lib60870.NET 创建链路层");
            
            try
            {
                // 尝试创建 lib60870.NET 链路层
                var lib60870LinkLayer = TryCreateLib60870LinkLayer(port, logger, timeoutMs, maxRetries);
                if (lib60870LinkLayer != null)
                {
                    logger.LogInformation("成功使用 lib60870.NET 创建链路层");
                    return lib60870LinkLayer;
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "无法加载 lib60870.NET，回退到 TcpLinkLayer 实现");
            }
        }

        logger.LogInformation("使用 TcpLinkLayer 创建链路层");
        return new TcpLinkLayer(port, (ILogger<TcpLinkLayer>)logger, timeoutMs, maxRetries);
    }

    /// <summary>
    /// 尝试创建 lib60870.NET 链路层
    /// </summary>
    /// <param name="port">端口号</param>
    /// <param name="logger">日志记录器</param>
    /// <param name="timeoutMs">超时时间</param>
    /// <param name="maxRetries">最大重传次数</param>
    /// <returns>链路层实例，如果失败返回 null</returns>
    private static ILinkLayer? TryCreateLib60870LinkLayer(int port, ILogger logger, int timeoutMs, int maxRetries)
    {
        // 注意：lib60870.NET 的实际 API 可能与此处示例不同
        // 这里提供一个基于 v2.3.0 的示例实现框架
        // 实际使用时需要引用 lib60870.NET NuGet 包并调整代码

        try
        {
            // 示例：使用反射检查 lib60870.NET 是否可用
            // 注意：使用 Assembly.Load 可能抛出 FileNotFoundException
            var lib60870Assembly = System.Reflection.Assembly.Load("lib60870.NET");
            if (lib60870Assembly != null)
            {
                logger.LogInformation("检测到 lib60870.NET 程序集");
                
                // 这里应该创建实际的 lib60870.NET 链路层实例
                // 例如：
                // var server = new Server();
                // server.SetLocalPort(port);
                // return new Lib60870LinkLayerAdapter(server, logger);
                
                // 由于 lib60870.NET 不一定安装，这里返回 null
                logger.LogWarning("lib60870.NET 已检测到但未实现适配器");
                return null;
            }
        }
        catch (FileNotFoundException)
        {
            logger.LogDebug("lib60870.NET 程序集未找到");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "加载 lib60870.NET 时发生错误");
        }

        return null;
    }
}

/// <summary>
/// lib60870.NET 链路层适配器（示例实现）
/// </summary>
/// <remarks>
/// 这是一个示例适配器类，展示如何封装 lib60870.NET
/// 实际实现需要引用 lib60870.NET NuGet 包
/// </remarks>
public class Lib60870LinkLayerAdapter : ILinkLayer
{
    private readonly ILogger _logger;
    // private readonly lib60870.Server _server; // 实际的 lib60870.NET Server 对象

    /// <summary>
    /// 数据接收事件
    /// </summary>
    public event EventHandler<byte[]>? DataReceived;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="logger">日志记录器</param>
    public Lib60870LinkLayerAdapter(ILogger logger)
    {
        _logger = logger;
        _logger.LogInformation("Lib60870LinkLayerAdapter 已创建");
    }

    /// <summary>
    /// 启动链路层
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("启动 lib60870.NET 链路层");
        
        // 示例代码（需要实际的 lib60870.NET API）:
        // _server.Start();
        // _server.SetASDUReceivedHandler(OnAsduReceived);
        
        await Task.CompletedTask;
    }

    /// <summary>
    /// 停止链路层
    /// </summary>
    public async Task StopAsync()
    {
        _logger.LogInformation("停止 lib60870.NET 链路层");
        
        // 示例代码:
        // _server.Stop();
        
        await Task.CompletedTask;
    }

    /// <summary>
    /// 发送数据
    /// </summary>
    /// <param name="data">要发送的数据</param>
    public async Task SendAsync(byte[] data)
    {
        _logger.LogDebug("通过 lib60870.NET 发送 {Length} 字节数据", data.Length);
        
        // 示例代码:
        // var asdu = ParseAsduFromBytes(data);
        // _server.EnqueueASDU(asdu);
        
        await Task.CompletedTask;
    }

    /// <summary>
    /// ASDU 接收处理器（示例）
    /// </summary>
    /// <param name="asduData">ASDU 数据</param>
    private void OnAsduReceived(byte[] asduData)
    {
        _logger.LogDebug("lib60870.NET 接收到 ASDU 数据: {Length} 字节", asduData.Length);
        DataReceived?.Invoke(this, asduData);
    }
}

/// <summary>
/// lib60870.NET 配置选项
/// </summary>
public class Lib60870Options
{
    /// <summary>
    /// 是否使用 lib60870.NET（默认 false）
    /// </summary>
    public bool UseLib60870 { get; set; } = false;

    /// <summary>
    /// 监听端口
    /// </summary>
    public int Port { get; set; } = 2404;

    /// <summary>
    /// 超时时间（毫秒）
    /// </summary>
    public int TimeoutMs { get; set; } = 5000;

    /// <summary>
    /// 最大重传次数
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// FCB 初始值
    /// </summary>
    public bool InitialFcb { get; set; } = false;

    /// <summary>
    /// 连接字符串（OpenGauss/PostgreSQL）
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;
}
