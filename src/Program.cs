using Microsoft.AspNetCore.Authentication.Cookies;
using SqlSugar;
using LpsGateway.Data;
using LpsGateway.Services;
using LpsGateway.Lib60870;

var builder = WebApplication.CreateBuilder(args);

// 配置日志
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
builder.Logging.SetMinimumLevel(LogLevel.Information);

// Add MVC services
builder.Services.AddControllersWithViews();

// 配置 Cookie 认证 (MVC模式)
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    });

builder.Services.AddAuthorization();

// 配置 lib60870 选项
var lib60870Options = builder.Configuration.GetSection("Lib60870").Get<Lib60870Options>() ?? new Lib60870Options();
builder.Services.AddSingleton(lib60870Options);

// Configure SqlSugarCore
var connectionString = lib60870Options.ConnectionString;
if (string.IsNullOrEmpty(connectionString))
{
    connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
        ?? "Host=localhost;Port=5432;Database=lps_gateway;Username=postgres;Password=postgres";
}

builder.Services.AddScoped<ISqlSugarClient>(provider =>
{
    var logger = provider.GetRequiredService<ILogger<Program>>();
    var maskedConnectionString = System.Text.RegularExpressions.Regex.Replace(connectionString, @"Password=[^;]*", "Password=***");
    logger.LogInformation("配置 SqlSugarCore 连接: {ConnectionString}", maskedConnectionString);
    
    var db = new SqlSugarClient(new ConnectionConfig
    {
        ConnectionString = connectionString,
        DbType = DbType.PostgreSQL,
        IsAutoCloseConnection = true,
        InitKeyType = InitKeyType.Attribute
    });
    return db;
});

// Register M1 repositories
builder.Services.AddScoped<IReportTypeRepository, ReportTypeRepository>();
builder.Services.AddScoped<ISftpConfigRepository, SftpConfigRepository>();
builder.Services.AddScoped<IScheduleRepository, ScheduleRepository>();

// Register M1 services
builder.Services.AddScoped<IAuthService, AuthService>();

// Register M2 services
builder.Services.AddScoped<ISftpManager, SftpManager>();
builder.Services.AddSingleton<IScheduleManager, ScheduleManager>();
builder.Services.AddScoped<LpsGateway.Services.Jobs.FileDownloadJob>();

// Register M2 hosted service
builder.Services.AddHostedService<LpsGateway.HostedServices.ScheduleManagerHostedService>();

// Register existing application services
builder.Services.AddScoped<IEFileRepository, EFileRepository>();
builder.Services.AddScoped<IEFileParser, EFileParser>();
builder.Services.AddScoped<IFileTransferManager, FileTransferManager>();

// Register and start TCP link layer
builder.Services.AddSingleton<ILinkLayer>(provider =>
{
    var logger = provider.GetRequiredService<ILogger<TcpLinkLayer>>();
    var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
    var wrapperLogger = loggerFactory.CreateLogger("Lib60870Wrapper");
    
    var useLib60870 = lib60870Options.UseLib60870;
    var port = lib60870Options.Port;
    var timeoutMs = lib60870Options.TimeoutMs;
    var maxRetries = lib60870Options.MaxRetries;
    
    logger.LogInformation("创建链路层: UseLib60870={UseLib60870}, Port={Port}, Timeout={TimeoutMs}ms, MaxRetries={MaxRetries}",
        useLib60870, port, timeoutMs, maxRetries);
    
    return Lib60870Wrapper.CreateLinkLayer(useLib60870, port, wrapperLogger, timeoutMs, maxRetries);
});

var app = builder.Build();

var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("LPS Gateway 正在启动...");

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    logger.LogInformation("开发环境：启用开发者异常页");
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// 配置 MVC 路由
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Start TCP Link Layer
var linkLayer = app.Services.GetRequiredService<ILinkLayer>();

linkLayer.DataReceived += async (sender, data) =>
{
    try
    {
        logger.LogDebug("链路层接收到数据: {Length} 字节", data.Length);
        using var scope = app.Services.CreateScope();
        var fileTransferManager = scope.ServiceProvider.GetRequiredService<IFileTransferManager>();
        await fileTransferManager.ProcessAsduAsync(data);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "处理 ASDU 数据时发生错误");
    }
};

// 启动链路层
try
{
    await linkLayer.StartAsync();
    logger.LogInformation("TCP 链路层已启动");
}
catch (Exception ex)
{
    logger.LogError(ex, "启动 TCP 链路层失败");
}

logger.LogInformation("LPS Gateway 已启动，正在监听请求...");

app.Run();

// 停止链路层
try
{
    await linkLayer.StopAsync();
    logger.LogInformation("TCP 链路层已停止");
}
catch (Exception ex)
{
    logger.LogError(ex, "停止 TCP 链路层时发生错误");
}
