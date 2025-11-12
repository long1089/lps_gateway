using Microsoft.AspNetCore.Authentication.Cookies;
using SqlSugar;
using LpsGateway.Data;
using LpsGateway.Services;

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
    return db;
});

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

app.Run();