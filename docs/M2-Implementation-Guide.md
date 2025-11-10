# M2 Implementation Guide - SFTP Scheduler & SqlSugarCore Migration

This document describes the M2 milestone implementation for the LPS Gateway project, covering SFTP file download management, Quartz.NET scheduling integration, and SqlSugarCore migration for .NET 8 compatibility.

## Overview

M2 establishes the scheduled file download infrastructure with:
- SqlSugarCore migration for full .NET 8 compatibility
- SFTP Manager for remote file downloads with password/key authentication
- Quartz.NET scheduler for automated task execution
- Manual trigger API for on-demand downloads
- Background hosted service for scheduler lifecycle management

## SqlSugarCore Migration

### Why Migrate?

The original `SqlSugar` package (version 5.1.4.207) targets .NET Framework and produces compatibility warnings when used with .NET 8. The `SqlSugarCore` package is the official .NET Core/Standard compatible version.

### Migration Details

**Package Changes:**
```xml
<!-- Before -->
<PackageReference Include="SqlSugar" Version="5.1.4.207" />

<!-- After -->
<PackageReference Include="SqlSugarCore" Version="5.1.4.207" />
```

**Key Points:**
- SqlSugarCore uses the same `SqlSugar` namespace for backward compatibility
- No code changes required in existing repositories and models
- Build warnings reduced from 4 to 0
- Fully compatible with .NET 8

### Verification

```bash
# Build should complete with 0 warnings
dotnet build LpsGateway.sln

# All tests should pass
dotnet test LpsGateway.sln
```

## SFTP Manager

### Features

The `SftpManager` service provides:
- **Dual Authentication**: Password and private key (with optional passphrase)
- **Streaming Downloads**: Efficient file transfer without loading entire file in memory
- **Dynamic Paths**: Template-based path generation with date/time placeholders
- **Concurrency Control**: Semaphore-based limiting of concurrent SFTP operations
- **Connection Testing**: Health check capability for SFTP configurations

### API

```csharp
public interface ISftpManager
{
    Task<bool> DownloadFileAsync(int sftpConfigId, string remoteFilePath, 
        string localFilePath, CancellationToken cancellationToken = default);
    
    Task<List<string>> ListFilesAsync(int sftpConfigId, string remotePath, 
        CancellationToken cancellationToken = default);
    
    Task<bool> TestConnectionAsync(int sftpConfigId, 
        CancellationToken cancellationToken = default);
    
    string ParsePathTemplate(string template, DateTime dateTime);
}
```

### Path Templates

Supported placeholders:
- `{yyyy}` - 4-digit year (e.g., 2024)
- `{MM}` - 2-digit month (e.g., 11)
- `{dd}` - 2-digit day (e.g., 10)
- `{HH}` - 2-digit hour (e.g., 14)
- `{mm}` - 2-digit minute (e.g., 30)
- `{ss}` - 2-digit second (e.g., 45)

**Example:**
```csharp
var template = "/reports/{yyyy}/{MM}/{dd}/";
var path = sftpManager.ParsePathTemplate(template, DateTime.Now);
// Result: /reports/2024/11/10/
```

### Authentication

**Password Authentication:**
```json
{
  "name": "Production SFTP",
  "host": "sftp.example.com",
  "port": 22,
  "username": "user",
  "authType": "password",
  "password": "secret",
  "enabled": true
}
```

**Private Key Authentication:**
```json
{
  "name": "Production SFTP",
  "host": "sftp.example.com",
  "port": 22,
  "username": "user",
  "authType": "key",
  "keyPath": "/path/to/private_key",
  "keyPassphraseEncrypted": "base64_encoded_passphrase",
  "enabled": true
}
```

**Note**: Passwords are Base64 encoded for storage. In production, use proper encryption (KMS, DPAPI, etc.).

## Quartz.NET Scheduler

### Architecture

The scheduler consists of three main components:

1. **ScheduleManager**: Central scheduler management
2. **FileDownloadJob**: Quartz job for executing downloads
3. **ScheduleManagerHostedService**: ASP.NET Core background service

### Schedule Types

#### Daily Schedule

Execute at specific times every day:

```json
{
  "reportTypeId": 1,
  "scheduleType": "daily",
  "times": ["08:00", "14:00", "20:00"],
  "timezone": "Asia/Shanghai",
  "enabled": true
}
```

#### Monthly Schedule

Execute on specific days of the month:

```json
{
  "reportTypeId": 1,
  "scheduleType": "monthly",
  "monthDays": [1, 10, 20],
  "times": ["09:00"],
  "timezone": "Asia/Shanghai",
  "enabled": true
}
```

#### Cron Schedule

Use Quartz cron expressions for complex schedules:

```json
{
  "reportTypeId": 1,
  "scheduleType": "cron",
  "cronExpression": "0 0 */6 * * ?",
  "timezone": "UTC",
  "enabled": true
}
```

**Cron Format**: `seconds minutes hours day-of-month month day-of-week`

### Schedule Manager API

```csharp
public interface IScheduleManager
{
    Task InitializeAsync();
    Task StartAsync();
    Task StopAsync();
    Task ReloadSchedulesAsync();
    Task TriggerDownloadAsync(int reportTypeId);
}
```

### Lifecycle

The scheduler is managed by `ScheduleManagerHostedService`:

1. **Startup**: Initializes Quartz scheduler and loads all enabled schedules
2. **Runtime**: Executes jobs based on triggers
3. **Shutdown**: Gracefully shuts down all running jobs

## Manual Trigger API

### Endpoints

#### POST /api/download/trigger/{reportTypeId}

Manually trigger a download for a report type.

**Authorization**: Admin or Operator role

**Request:**
```bash
curl -X POST https://localhost:5001/api/download/trigger/1 \
  -H "Authorization: Bearer <jwt_token>"
```

**Response:**
```json
{
  "success": true,
  "message": "下载任务已触发",
  "reportTypeId": 1,
  "reportTypeName": "Daily Energy Report"
}
```

#### POST /api/download/reload-schedules

Reload all schedules from the database.

**Authorization**: Admin role only

**Request:**
```bash
curl -X POST https://localhost:5001/api/download/reload-schedules \
  -H "Authorization: Bearer <jwt_token>"
```

**Response:**
```json
{
  "success": true,
  "message": "调度已重新加载"
}
```

## Configuration

### appsettings.json

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=lps_gateway;Username=postgres;Password=postgres"
  },
  "JwtSettings": {
    "SecretKey": "LpsGateway-Secret-Key-Change-In-Production-Min32Chars!",
    "Issuer": "LpsGateway",
    "Audience": "LpsGatewayClients",
    "ExpirationMinutes": 480
  },
  "Lib60870": {
    "UseLib60870": false,
    "Port": 2404,
    "TimeoutMs": 5000,
    "MaxRetries": 3,
    "ConnectionString": ""
  }
}
```

### Dependency Injection

M2 services are registered in `Program.cs`:

```csharp
// M2 services
builder.Services.AddScoped<ISftpManager, SftpManager>();
builder.Services.AddSingleton<IScheduleManager, ScheduleManager>();
builder.Services.AddScoped<FileDownloadJob>();

// M2 hosted service
builder.Services.AddHostedService<ScheduleManagerHostedService>();
```

## File Download Workflow

### Automatic Downloads (Scheduled)

1. Quartz triggers `FileDownloadJob` based on schedule
2. Job retrieves report type and SFTP configuration
3. Job parses path template for current date/time
4. Job lists files on SFTP server
5. Job downloads each file to local storage
6. Job logs success/failure for each file

### Manual Downloads (API)

1. User calls `/api/download/trigger/{reportTypeId}`
2. API validates report type and authorization
3. API creates one-time job and executes immediately
4. Same download workflow as scheduled job

### File Storage

Downloaded files are stored in:
```
downloads/{reportTypeCode}/{filename}
```

Example:
```
downloads/DAILY_ENERGY/energy_2024-11-10.dat
```

## Dependencies

### NuGet Packages

**New in M2:**
- `SSH.NET` 2024.2.0 - SFTP client library
- `Quartz` 3.13.1 - Job scheduling framework

**Existing:**
- `SqlSugarCore` 5.1.4.207 - ORM (migrated from SqlSugar)
- `System.Text.Encoding.CodePages` 9.0.10 - GBK encoding
- `BCrypt.Net-Next` 4.0.3 - Password hashing

### Runtime Requirements

- .NET 8.0 SDK or later
- PostgreSQL or OpenGauss 12+
- SFTP server access (for testing)

## Security Considerations

### SFTP Credentials

**Current Implementation:**
- Passwords: Base64 encoding (reversible)
- Private keys: File path + optional passphrase

**Production Recommendations:**
1. Use Azure Key Vault, AWS KMS, or similar KMS
2. Implement proper secret rotation
3. Use private key authentication when possible
4. Store private keys in secure locations with restricted permissions
5. Enable audit logging for all SFTP access

### API Security

- All endpoints require JWT authentication
- Manual trigger requires Admin or Operator role
- Reload schedules requires Admin role only
- Schedule modifications trigger audit logs (if implemented)

### Network Security

- Use SFTP (SSH) instead of plain FTP
- Configure firewall rules for SFTP access
- Consider VPN or private networking for SFTP connections
- Implement connection timeouts and retries

## Testing

### Unit Tests

No additional unit tests were added in M2 milestone. Existing tests (10 total) continue to pass.

### Manual Testing

1. **Test SFTP Connection:**
```bash
# Create SFTP config via UI or API
curl -X POST http://localhost:5000/api/sftpconfigs \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer <token>" \
  -d '{
    "name": "Test SFTP",
    "host": "test.rebex.net",
    "port": 22,
    "username": "demo",
    "authType": "password",
    "password": "password",
    "basePathTemplate": "/",
    "enabled": true
  }'
```

2. **Test Manual Download:**
```bash
curl -X POST http://localhost:5000/api/download/trigger/1 \
  -H "Authorization: Bearer <token>"
```

3. **Test Schedule Reload:**
```bash
curl -X POST http://localhost:5000/api/download/reload-schedules \
  -H "Authorization: Bearer <token>"
```

## Troubleshooting

### SFTP Connection Failures

**Issue**: "Unable to connect to SFTP server"

**Solutions**:
- Verify host and port are correct
- Check firewall rules
- Ensure SFTP service is running
- Test credentials manually with SSH client
- Check timeout settings

### Schedule Not Executing

**Issue**: Job doesn't run at scheduled time

**Solutions**:
- Verify schedule is enabled in database
- Check schedule times are in correct timezone
- Review cron expression syntax
- Check application logs for scheduler errors
- Call `/api/download/reload-schedules` to refresh

### Download Failures

**Issue**: Files fail to download

**Solutions**:
- Verify remote file path exists
- Check read permissions on SFTP server
- Ensure local download directory is writable
- Review SFTP configuration
- Check available disk space

### Quartz Job Errors

**Issue**: "Unable to create job instance"

**Solutions**:
- Ensure `FileDownloadJob` is registered in DI
- Check service dependencies are registered
- Review application startup logs
- Verify Quartz.NET is configured correctly

## Monitoring and Observability

### Logging

The system logs key events:

```csharp
// SFTP operations
_logger.LogInformation("连接到SFTP服务器: {Host}:{Port}", host, port);
_logger.LogInformation("开始下载文件: {RemoteFilePath}", remoteFilePath);

// Schedule operations
_logger.LogInformation("开始执行文件下载任务: ReportTypeId={ReportTypeId}", id);
_logger.LogInformation("调度已重新加载");

// Job execution
_logger.LogInformation("发现 {Count} 个待下载文件", files.Count);
_logger.LogInformation("文件下载成功: {RemoteFile}", remoteFile);
```

### Health Checks

Test SFTP connection health:

```csharp
var isHealthy = await sftpManager.TestConnectionAsync(configId);
```

### Future Enhancements

Consider adding:
1. Prometheus metrics for download success/failure rates
2. Grafana dashboards for schedule visualization
3. Alert notifications for failed downloads
4. Download history tracking in database
5. Retry mechanisms with exponential backoff

## Performance Considerations

### Concurrency

- SFTP operations use semaphore for concurrency control
- Default limit: 1 concurrent connection per SftpManager instance
- Configurable via `SftpConfig.ConcurrencyLimit`

### Large Files

- Streaming downloads prevent memory overflow
- 4KB buffer size for file operations
- No in-memory file accumulation

### Database Connections

- SqlSugarCore uses connection pooling
- Scoped lifetime for most services
- Singleton for ScheduleManager to maintain state

## Migration from M1 to M2

If upgrading from M1:

1. **Update packages:**
```bash
cd src
dotnet remove package SqlSugar
dotnet add package SqlSugarCore --version 5.1.4.207
dotnet add package SSH.NET --version 2024.2.0
dotnet add package Quartz --version 3.13.1
```

2. **Run database migrations** (if any schema changes)

3. **Update configuration:**
- No configuration changes required
- Optionally configure SFTP settings

4. **Restart application:**
```bash
dotnet run
```

5. **Verify:**
- Check logs for "调度管理器已启动"
- Test manual trigger API
- Create test schedule

## Next Steps (M3)

- [ ] Implement TCP Server for IEC-102 protocol
- [ ] Add control frame and variable frame handling
- [ ] Implement time synchronization (TYP=0x8B)
- [ ] Add protocol logging and diagnostics
- [ ] Create protocol state machine

## Support

For issues and questions:
- Check application logs for detailed error messages
- Review Quartz.NET documentation for cron expressions
- Test SFTP connections independently
- Verify database schedule configurations
- Check JWT token validity and roles

## Summary

M2 milestone deliverables:
- ✅ SqlSugarCore migration (0 build warnings)
- ✅ SFTP Manager with dual authentication
- ✅ Quartz.NET scheduler integration
- ✅ Daily, monthly, and cron schedules
- ✅ Manual trigger REST API
- ✅ Background hosted service
- ✅ Comprehensive logging
- ✅ All existing tests passing

The system is now ready for automated file downloads from SFTP servers on flexible schedules.
