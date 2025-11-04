using System.Text;
using LPSGateway.Data;
using LPSGateway.Lib60870;
using LPSGateway.Services;
using LPSGateway.Services.Interfaces;
using SqlSugar;

// Register GBK encoding provider for E-file parsing
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure SqlSugar for OpenGauss/PostgreSQL
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? "Host=localhost;Port=5432;Database=lpsgateway;Username=postgres;Password=postgres";

builder.Services.AddScoped<ISqlSugarClient>(provider =>
{
    return new SqlSugarClient(new ConnectionConfig
    {
        ConnectionString = connectionString,
        DbType = DbType.PostgreSQL,
        IsAutoCloseConnection = true,
        InitKeyType = InitKeyType.Attribute
    });
});

// Register services
builder.Services.AddSingleton<ILinkLayer, TcpLinkLayer>();
builder.Services.AddScoped<IEFileRepository, EFileRepository>();
builder.Services.AddScoped<IEFileParser, EFileParser>();
builder.Services.AddSingleton<IFileTransferManager, FileTransferManager>();

// Add hosted service to start file transfer manager
builder.Services.AddHostedService<FileTransferHostedService>();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();

// Hosted service to manage file transfer lifecycle
public class FileTransferHostedService : IHostedService
{
    private readonly IFileTransferManager _fileTransferManager;
    private readonly ILinkLayer _linkLayer;
    private readonly IConfiguration _configuration;

    public FileTransferHostedService(
        IFileTransferManager fileTransferManager,
        ILinkLayer linkLayer,
        IConfiguration configuration)
    {
        _fileTransferManager = fileTransferManager;
        _linkLayer = linkLayer;
        _configuration = configuration;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var host = _configuration.GetValue<string>("Iec102:Host") ?? "0.0.0.0";
        var port = _configuration.GetValue<int>("Iec102:Port", 2404);

        await _fileTransferManager.StartAsync();
        
        // Start IEC-102 server
        _ = Task.Run(async () =>
        {
            try
            {
                await _linkLayer.StartServerAsync(host, port);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error starting IEC-102 server: {ex.Message}");
            }
        }, cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _fileTransferManager.StopAsync();
        await _linkLayer.StopAsync();
    }
}
