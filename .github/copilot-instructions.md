# Copilot Instructions for LPS Gateway

## Project Overview

LPS Gateway is an IEC-102 extended E-file reception, parsing, storage, and reporting system built on .NET 8 WebAPI with OpenGauss/PostgreSQL database and SqlSugarCore ORM. The system handles industrial protocol communication, file transfer, and data processing for energy management systems.

## Technology Stack

- **Framework**: .NET 8 WebAPI (upgraded from .NET 6)
- **Database**: OpenGauss/PostgreSQL with SqlSugarCore ORM (v5.1.4.207)
- **Protocol**: IEC-102 protocol for industrial communication
- **Scheduling**: Quartz.NET (v3.13.1) for job scheduling
- **File Transfer**: SSH.NET (v2024.2.0) for SFTP operations
- **Authentication**: Cookie-based authentication with BCrypt.Net-Next (v4.0.3)
- **Encoding**: System.Text.Encoding.CodePages (v9.0.10) for GBK encoding support
- **Testing**: xUnit with Moq

## Architecture

The project follows a layered architecture:

```
src/
├── Controllers/        # WebAPI and MVC endpoints
├── Data/              # Repository pattern with SqlSugarCore
│   ├── Models/        # Entity models
│   └── Repositories/  # Data access implementations
├── Services/          # Business logic layer
├── Lib60870/          # IEC-102 protocol implementation
│   ├── Link Layer     # TCP-based protocol handling
│   ├── ASDU Manager   # Application Service Data Unit handling
│   └── Frame Parser   # Protocol frame parsing
├── Protocol/          # Protocol specifications
├── HostedServices/    # Background services (Quartz scheduler)
├── Models/            # DTOs and view models
└── Views/             # MVC views
```

## Key Components

### 1. Protocol Layer (Lib60870)
- **Iec102Frame**: Parses 0x10 (fixed) and 0x68 (variable) frames with checksum validation
- **ILinkLayer/TcpLinkLayer**: TCP-based link layer implementation
- **AsduManager**: Handles ASDU encoding/decoding for Type IDs 0x90-0xA8
- **Mapping**: Bidirectional mapping between Type IDs and table names

### 2. Service Layer
- **FileTransferManager**: Multi-frame buffering and transfer management
- **EFileParser**: Parses GBK-encoded E-files with table-based structure
- **SftpDownloadService**: Manages SFTP file downloads with authentication
- **SchedulerService**: Quartz.NET-based job scheduling

### 3. Data Layer
- **SqlSugarCore**: ORM for database operations
- **Repositories**: Repository pattern for data access
- **Dynamic Tables**: Creates tables based on E-file structure

## Coding Conventions

### General Guidelines
1. **Namespaces**: Use `LpsGateway.*` for all code (e.g., `LpsGateway.Data`, `LpsGateway.Services`)
2. **Nullable**: Nullable reference types are enabled (`<Nullable>enable</Nullable>`)
3. **Implicit Usings**: Enabled for common namespaces
4. **Comments**: Use Chinese comments for business logic (业务逻辑注释使用中文)
5. **Logging**: Use `ILogger<T>` for all logging operations

### Naming Conventions
- **Classes**: PascalCase (e.g., `EFileParser`, `FileTransferManager`)
- **Interfaces**: Start with 'I' (e.g., `ILinkLayer`, `IEFileRepository`)
- **Private fields**: Use `_camelCase` (e.g., `_logger`, `_repository`)
- **Methods**: PascalCase with async suffix for async methods (e.g., `ParseAsync`, `DownloadFileAsync`)
- **Constants**: UPPER_CASE with underscores (e.g., `MAX_RETRIES`)

### Code Patterns

#### Dependency Injection
All services should be registered in `Program.cs`:
```csharp
builder.Services.AddScoped<IEFileRepository, EFileRepository>();
builder.Services.AddSingleton<IFileTransferManager, FileTransferManager>();
```

#### Repository Pattern
```csharp
public interface IEFileRepository
{
    Task<ReceivedEfile?> GetByIdAsync(int id);
    Task<bool> SaveAsync(ReceivedEfile efile);
}
```

#### Async/Await
- Always use async/await for I/O operations
- Suffix async methods with `Async`
- Use `ConfigureAwait(false)` when appropriate

#### Error Handling
```csharp
try
{
    // 业务逻辑
    await ProcessFileAsync(file);
}
catch (Exception ex)
{
    _logger.LogError(ex, "处理文件失败: {FileName}", file.Name);
    throw;
}
```

## Development Workflow

### Building the Project
```bash
# Build entire solution
dotnet build

# Build specific project
cd src
dotnet build
```

### Running the Application
```bash
# Run the WebAPI
cd src
dotnet run

# Access Swagger UI
# http://localhost:5000/swagger
```

### Running Tests
```bash
# Run all tests
dotnet test

# Run tests with coverage
cd tests
dotnet test
```

### Database Setup
```bash
# Using PostgreSQL with Docker
docker run --name lps-postgres -e POSTGRES_PASSWORD=postgres -e POSTGRES_DB=lps_gateway -p 5432:5432 -d postgres:15

# Apply schema
docker exec -i lps-postgres psql -U postgres -d lps_gateway < db/schema.sql
```

## IEC-102 Protocol Specifics

### Frame Structure
- **0x10 frames**: Fixed-length control frames
- **0x68 frames**: Variable-length data frames
- **Checksum**: Sum of all bytes modulo 256

### ASDU Format
```
Byte 0: Type ID (0x90-0xA8 for E files)
Byte 1: Payload length + 2
Byte 2: Cause of Transmission (0x06=intermediate, 0x07=last frame)
Byte 3-4: Common Address (little-endian)
Byte 5+: Payload data
```

### E-File Format
```
<table> TABLE_NAME
@Column1	Column2	Column3
#Value1	Value2	Value3
```
- `<table>`: Defines table name
- `@`: Column headers (tab-separated)
- `#`: Data rows (tab-separated)
- `-99`: Interpreted as NULL

## Configuration

### appsettings.json Structure
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=lps_gateway;..."
  },
  "Lib60870": {
    "UseLib60870": false,
    "Port": 2404,
    "TimeoutMs": 5000,
    "MaxRetries": 3
  },
  "Quartz": {
    "quartz.scheduler.instanceName": "LpsGatewayScheduler"
  }
}
```

## Common Tasks

### Adding a New Repository
1. Create interface in `Data/` folder
2. Implement interface with SqlSugarCore
3. Register in `Program.cs` DI container
4. Add unit tests

### Adding a New API Endpoint
1. Create controller in `Controllers/` folder
2. Use dependency injection for services
3. Add proper authentication/authorization attributes
4. Document with XML comments
5. Test via Swagger UI

### Adding a New Background Job
1. Create job class implementing `IJob`
2. Configure in Quartz settings
3. Register job in SchedulerService
4. Add logging for job execution

### Modifying Protocol Handling
1. Review IEC-102 specification in `docs/IEC102-Extended-Doc.md`
2. Update protocol layer in `Lib60870/`
3. Add tests for new protocol features
4. Update documentation

## Testing Guidelines

### Unit Tests
- Use xUnit framework
- Use Moq for mocking dependencies
- Follow Arrange-Act-Assert pattern
- Test file: `tests/LpsGateway.Tests.csproj`

### Integration Tests
- Use Master Simulator in `tools/MasterSimulator/`
- Test multi-frame file transfers
- Verify database persistence

## Documentation

- **README.md**: Main project documentation
- **docs/**: Detailed implementation guides
  - `Architecture-Design.md`: Architecture overview
  - `IEC102-Extended-ASDU-Spec.md`: Protocol specification
  - `M1-Implementation-Guide.md`: Milestone 1 guide
  - `M2-Implementation-Guide.md`: Milestone 2 guide
- **IMPLEMENTATION_SUMMARY.md**: Implementation statistics and summary

## Important Notes

### GBK Encoding
Always register CodePagesEncodingProvider at startup:
```csharp
System.Text.Encoding.RegisterProvider(System.Text.Encoding.CodePagesEncodingProvider.Instance);
```

### SqlSugarCore
- Use `ISqlSugarClient` interface
- Configure DbType as `DbType.PostgreSQL`
- Use transactions for multi-table operations
- Log masked connection strings (hide passwords)

### Authentication
- Cookie-based authentication for MVC
- JWT tokens can be added for API authentication
- BCrypt for password hashing

### Background Services
- Use `IHostedService` for background tasks
- Register with `AddHostedService<T>()`
- Properly handle cancellation tokens

## Current Development Focus

The project is currently in **Milestone M2** (Scheduling & SFTP), with focus on:
- Quartz.NET integration for scheduling
- SFTP file downloads with authentication
- Manual trigger APIs
- Background service lifecycle management

**Next Milestone (M3)**: TCP Server & Protocol Stack enhancements

## Tips for Using Copilot

1. **Context**: Always specify which layer you're working on (Protocol, Service, Data, Controller)
2. **Patterns**: Follow existing repository patterns and service interfaces
3. **Documentation**: Reference existing docs in `docs/` folder for specifications
4. **Chinese Comments**: Use Chinese for business logic comments when appropriate
5. **Testing**: Always include unit tests for new functionality
6. **Logging**: Add appropriate logging at Info, Debug, Warning, and Error levels
