using Microsoft.AspNetCore.Authentication.Cookies;
using SqlSugar;
using LpsGateway.Data;
using LpsGateway.Services;
using LpsGateway.Hubs;
using LpsGateway.Extensions;

var builder = WebApplication.CreateBuilder(args);

// 配置日志
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
builder.Logging.SetMinimumLevel(LogLevel.Information);

// Add MVC services
builder.Services.AddControllersWithViews();

// Add SignalR
builder.Services.AddSignalR();

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

// 注册 IHttpContextAccessor（获取当前用户必需，.NET 8 已默认注册，显式写更稳妥）
builder.Services.AddHttpContextAccessor();

// Configure SqlSugarCore
builder.Services.AddScoped<ISqlSugarClient>(provider =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    var db = new SqlSugarClient(new ConnectionConfig
    {
        ConnectionString = connectionString,
        DbType = DbType.PostgreSQL,
        IsAutoCloseConnection = true,
        InitKeyType = InitKeyType.Attribute
    });
    var httpContextAccessor = provider.GetRequiredService<IHttpContextAccessor>();
    db.EnabledAuditLog(httpContextAccessor);
    return db;
});

// Configure SqlSugar Insertable/Updateable/Deleteable auto call EnableDiffLogEvent method
StaticConfig.CompleteInsertableFunc =
    StaticConfig.CompleteUpdateableFunc =
        StaticConfig.CompleteDeleteableFunc = it => //it是具体的对象Updateable<T>等是个object
        {
            //反射的方法可能多个就需要用GetMethods().Where
            var method = it.GetType().GetMethod("EnableDiffLogEvent");
            method?.Invoke(it, new object[] {null});

            //技巧：
            //可以定义一个接口只要是这个接口的才走这个逻辑
            //if(db.GetType().GenericTypeArguments[0].GetInterfaces().Any(it=>it==typeof(IDiff))
            //可以根据类型写if
            //if(x.GetType().GenericTypeArguments[0] = typeof(Order)) {   }
        };

// Register M1 repositories
builder.Services.AddScoped<IReportTypeRepository, ReportTypeRepository>();
builder.Services.AddScoped<ISftpConfigRepository, SftpConfigRepository>();
builder.Services.AddScoped<IScheduleRepository, ScheduleRepository>();
builder.Services.AddScoped<IFileRecordRepository, FileRecordRepository>();
builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();

// Register M1 services
builder.Services.AddScoped<IAuthService, AuthService>();

// Register M2 services
builder.Services.AddScoped<ISftpManager, SftpManager>();
builder.Services.AddSingleton<IScheduleManager, ScheduleManager>();
builder.Services.AddScoped<LpsGateway.Services.Jobs.FileDownloadJob>();

// Register M4-additional services
builder.Services.AddScoped<IFileTransferInitializer, FileTransferInitializer>();

// Register M5 services
builder.Services.AddScoped<IDashboardService, DashboardService>();

// Register Communication Status Broadcaster as Singleton (shared state)
builder.Services.AddSingleton<ICommunicationStatusBroadcaster, CommunicationStatusBroadcaster>();

// Register M2 hosted service
builder.Services.AddHostedService<LpsGateway.HostedServices.ScheduleManagerHostedService>();

// Register M5 hosted service
builder.Services.AddHostedService<LpsGateway.HostedServices.RetentionWorkerHostedService>();

// Configure M3: IEC-102 Master/Slave options
builder.Services.Configure<LpsGateway.HostedServices.Iec102SlaveOptions>(
    builder.Configuration.GetSection("Iec102Slave"));
builder.Services.Configure<LpsGateway.HostedServices.Iec102MasterOptions>(
    builder.Configuration.GetSection("Iec102Master"));

// Register M3 hosted services
builder.Services.AddHostedService<LpsGateway.HostedServices.Iec102SlaveHostedService>();
builder.Services.AddHostedService<LpsGateway.HostedServices.Iec102MasterHostedService>();

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

// 配置 SignalR Hub
app.MapHub<CommunicationStatusHub>("/hubs/communicationStatus");

app.Run();