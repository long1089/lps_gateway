using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
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

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "LPS Gateway API", Version = "v1" });
    
    // 添加JWT认证支持
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// 配置 JWT 认证
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"] ?? "LpsGateway-Default-Secret-Key-Change-In-Production-Min32Chars!";
var issuer = jwtSettings["Issuer"] ?? "LpsGateway";
var audience = jwtSettings["Audience"] ?? "LpsGatewayClients";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = issuer,
        ValidAudience = audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
    };
});

builder.Services.AddAuthorization();

// 配置 lib60870 选项
var lib60870Options = builder.Configuration.GetSection("Lib60870").Get<Lib60870Options>() ?? new Lib60870Options();
builder.Services.AddSingleton(lib60870Options);

// Configure SqlSugar
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
    logger.LogInformation("配置 SqlSugar 连接: {ConnectionString}", maskedConnectionString);
    
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
    logger.LogInformation("开发环境：启用 Swagger UI");
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

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
