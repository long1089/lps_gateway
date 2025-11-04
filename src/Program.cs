using SqlSugar;
using LpsGateway.Data;
using LpsGateway.Services;
using LpsGateway.Lib60870;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure SqlSugar
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? "Host=localhost;Port=5432;Database=lps_gateway;Username=postgres;Password=postgres";

builder.Services.AddScoped<ISqlSugarClient>(provider =>
{
    var db = new SqlSugarClient(new ConnectionConfig
    {
        ConnectionString = connectionString,
        DbType = DbType.PostgreSQL,
        IsAutoCloseConnection = true,
        InitKeyType = InitKeyType.Attribute
    });
    return db;
});

// Register application services
builder.Services.AddScoped<IEFileRepository, EFileRepository>();
builder.Services.AddScoped<IEFileParser, EFileParser>();
builder.Services.AddSingleton<IFileTransferManager, FileTransferManager>();

// Register and start TCP link layer
builder.Services.AddSingleton<ILinkLayer>(provider =>
{
    var port = builder.Configuration.GetValue<int>("TcpLinkLayer:Port", 2404);
    return new TcpLinkLayer(port);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

// Start TCP Link Layer
var linkLayer = app.Services.GetRequiredService<ILinkLayer>();
var fileTransferManager = app.Services.GetRequiredService<IFileTransferManager>();

linkLayer.DataReceived += async (sender, data) =>
{
    try
    {
        await fileTransferManager.ProcessAsduAsync(data);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error processing ASDU: {ex.Message}");
    }
};

await linkLayer.StartAsync();

app.Run();

await linkLayer.StopAsync();
