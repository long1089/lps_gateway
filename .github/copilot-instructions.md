# Copilot Instructions for LPS Gateway

## Project Overview

LPS Gateway is an IEC-102 extended E-file reception, parsing, storage, and reporting system built on .NET 8 WebAPI with OpenGauss/PostgreSQL database and SqlSugarCore ORM. The system handles industrial protocol communication, file transfer, and data processing for energy management systems.

## Technology Stack

- **Framework**: .NET 8 MVC (upgraded from .NET 6)
- **Database**: OpenGauss/PostgreSQL with SqlSugarCore ORM (v5.1.4.207)
- **Protocol**: IEC-102 protocol for industrial communication
- **Scheduling**: Quartz.NET (v3.13.1) for job scheduling
- **File Transfer**: SSH.NET (v2024.2.0) for SFTP operations
- **Authentication**: Cookie-based authentication with BCrypt.Net-Next (v4.0.3)
- **Encoding**: System.Text.Encoding.CodePages (v9.0.10) for GBK encoding support
- **Testing**: xUnit with Moq

## Database and SQL Compatibility

### Database Version Requirements
- **Primary Target**: OpenGauss (based on PostgreSQL 9.2)
- **Also Compatible**: PostgreSQL 9.2+
- **Development/Testing**: PostgreSQL 9.6+ or 15+ recommended

### SQL Syntax Compatibility Guidelines

**IMPORTANT**: All SQL code must be compatible with OpenGauss and PostgreSQL 9.2+. Avoid using newer PostgreSQL syntax features.

#### Supported Syntax
- ✅ `CREATE TABLE IF NOT EXISTS`
- ✅ `ALTER TABLE table_name ADD COLUMN IF NOT EXISTS column_name type`
- ✅ `ALTER TABLE table_name ALTER COLUMN column_name TYPE new_type`
- ✅ `INSERT INTO ... SELECT ... WHERE NOT EXISTS(...)`
- ✅ `COMMENT ON TABLE/COLUMN`
- ✅ Standard SQL data types: `VARCHAR`, `INTEGER`, `BIGINT`, `BOOLEAN`, `TIMESTAMP`, `JSONB`, `TEXT`
- ✅ `SERIAL` for auto-increment columns

#### NOT Supported (OpenGauss/PostgreSQL 9.2)
- ❌ `ALTER TABLE table_name DROP COLUMN IF EXISTS column_name` (added in PostgreSQL 9.6)
- ❌ `CREATE INDEX IF NOT EXISTS` (added in PostgreSQL 9.5)
- ❌ Advanced window functions from PostgreSQL 11+
- ❌ `GENERATED` columns (added in PostgreSQL 12)

#### Required Workarounds

**Conditional DROP COLUMN** - Use DO block with information_schema:
```sql
-- ❌ NOT COMPATIBLE
ALTER TABLE table_name DROP COLUMN IF EXISTS column_name;

-- ✅ USE THIS INSTEAD
DO $$ 
BEGIN
    IF EXISTS (
        SELECT 1 
        FROM information_schema.columns 
        WHERE table_name = 'your_table_name' 
        AND column_name = 'your_column_name'
    ) THEN
        ALTER TABLE your_table_name DROP COLUMN your_column_name;
    END IF;
END $$;
```

**Conditional CREATE INDEX** - Check before creating:
```sql
-- ❌ NOT COMPATIBLE
CREATE INDEX IF NOT EXISTS idx_name ON table_name (column_name);

-- ✅ USE THIS INSTEAD
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_indexes 
        WHERE indexname = 'idx_name'
    ) THEN
        CREATE INDEX idx_name ON table_name (column_name);
    END IF;
END $$;
```

#### Testing SQL Compatibility
Before committing database changes:
1. Test on PostgreSQL 9.6 minimum (simulates OpenGauss environment)
2. Verify all migrations work without errors
3. Ensure idempotency - scripts can run multiple times safely
4. Use standard SQL features, avoid PostgreSQL-specific extensions when possible

#### Migration File Conventions
- Name migrations with numeric prefix: `001_description.sql`, `002_description.sql`
- Include rollback instructions in comments or README
- Always test migrations on clean database
- Document any OpenGauss-specific considerations

## Architecture

The project follows a layered architecture:

```
src/
├── Controllers/        # MVC endpoints
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
# Run the Web MVC
cd src
dotnet run

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
- Configure DbType as `DbType.PostgreSQL` (compatible with OpenGauss)
- Use transactions for multi-table operations
- Log masked connection strings (hide passwords)
- Follow SQL compatibility guidelines (see Database and SQL Compatibility section above)

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
