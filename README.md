# LPS Gateway - IEC-102 Extended E File Reception System

This project implements an IEC-102 extended E file reception, parsing, storage, and reporting system running on .NET 8 WebAPI with OpenGauss database and SqlSugarCore ORM.

## Features

- **WebAPI Support**: E file upload and trigger reporting endpoints
- **Link Layer**: TCP-based link layer compatible with IEC-102 protocol
- **ASDU Management**: Support for custom Type IDs (0x90-0xA8) with ASDU encoding/decoding
- **File Transfer**: Multi-frame file transfer with automatic reassembly
- **E File Parser**: GBK encoding support, table-based parsing with upsert/insert logic
- **Database**: OpenGauss/PostgreSQL with SqlSugarCore ORM
- **Authentication**: JWT-based authentication with role-based authorization
- **SFTP Downloads**: Automated file downloads from SFTP servers
- **Scheduling**: Quartz.NET-based job scheduling (daily, monthly, cron)
- **Manual Triggers**: REST API for on-demand file downloads
- **Testing**: Unit tests with xUnit and Moq
- **Tools**: Master station simulator for integration testing

## Milestones

### ✅ M0: Requirements & Design Freeze
- Extended ASDU specification (time sync, on-demand)
- Data model design with indexing strategy
- API design with permission model

### ✅ M1: Project Skeleton & Infrastructure
- .NET 8 MVC project with layered architecture
- SqlSugarCore + OpenGauss connection and migrations
- JWT authentication with role-based authorization
- Configuration management UI (ReportType, SftpConfig, Schedule)
- **Documentation**: [M1-Implementation-Guide.md](docs/M1-Implementation-Guide.md)

### ✅ M2: Scheduling & SFTP
- **SqlSugarCore Migration**: Full .NET 8 compatibility (0 build warnings)
- **Quartz.NET Integration**: Daily, monthly, and cron schedules
- **SFTP Manager**: Password/key authentication, streaming downloads, path templates
- **Manual Trigger API**: On-demand file downloads via REST API
- **Background Service**: Scheduler lifecycle management
- **Documentation**: [M2-Implementation-Guide.md](docs/M2-Implementation-Guide.md)

### ✅ M3: TCP Server & Protocol Stack
- Async TCP Server
- Control/Fixed/Variable frame handling
- FCB/FCV/ACD/DFC processing
- Time synchronization extension (TYP=0x8B)
- Protocol logging

### ✅ M4: File Transfer Channel
- File segment upload (TYP=0x95-0xA8)
- Reconciliation frame (0x90)
- Error control (0x91-0x94)
- FileTransferTask worker with backpressure

### 📋 M5: Retention & Observability  (Next)
- Retention worker
- Prometheus metrics & Grafana dashboards
- Operation audit logging
- Disk usage alerts

### 📋 M6: Integration & Testing
- Master station integration testing
- Performance testing (concurrency, bandwidth, DB)
- Disaster recovery testing
- Documentation & operation manual

## Architecture

```
src/
├── Controllers/
│   └── EFileController.cs      # WebAPI endpoints
├── Data/
│   ├── Models/
│   │   └── ReceivedEfile.cs    # Data model
│   ├── IEFileRepository.cs     # Repository interface
│   └── EFileRepository.cs      # Repository implementation
├── Lib60870/
│   ├── ILinkLayer.cs           # Link layer interface
│   ├── TcpLinkLayer.cs         # TCP link layer implementation
│   ├── AsduManager.cs          # ASDU encoding/decoding
│   └── Mapping.cs              # Type ID mapping
├── Services/
│   ├── IEFileParser.cs         # Parser interface
│   ├── EFileParser.cs          # E file parser implementation
│   ├── IFileTransferManager.cs # File transfer interface
│   └── FileTransferManager.cs  # File transfer manager
└── Program.cs                  # Application entry point

db/
└── schema.sql                  # Database schema

tests/
└── EFileParserTests.cs         # Unit tests

tools/
└── MasterSimulator/
    └── Program.cs              # Master station simulator
```

## Prerequisites

- .NET 8 SDK
- OpenGauss or PostgreSQL database
- (Optional) Docker for running database locally

## Database Setup

### Option 1: Using PostgreSQL/OpenGauss directly

1. Install PostgreSQL or OpenGauss
2. Create database:
   ```bash
   createdb lps_gateway
   ```

3. Run schema migration:
   ```bash
   psql -d lps_gateway -f db/schema.sql
   ```

### Option 2: Using Docker

```bash
# Run PostgreSQL in Docker
docker run --name lps-postgres -e POSTGRES_PASSWORD=postgres -e POSTGRES_DB=lps_gateway -p 5432:5432 -d postgres:15

# Apply schema
docker exec -i lps-postgres psql -U postgres -d lps_gateway < db/schema.sql
```

## Configuration

Update connection string in `src/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=lps_gateway;Username=postgres;Password=postgres"
  },
  "TcpLinkLayer": {
    "Port": 2404
  }
}
```

## Running the Application

### 1. Build the solution

```bash
dotnet build
```

### 2. Run the WebAPI

```bash
cd src
dotnet run
```

The API will start on `http://localhost:5000` (and `https://localhost:5001` for HTTPS).

Swagger UI is available at: `http://localhost:5000/swagger`

### 3. Run the Master Simulator (for testing)

In a separate terminal:

```bash
cd tools/MasterSimulator
dotnet run
```

Or connect to a different host/port:

```bash
dotnet run -- hostname 2404
```

The simulator provides options to:
1. Send single-frame E file data
2. Send multi-frame E file data
3. Send custom ASDU frames

## Running Tests

```bash
cd tests
dotnet test
```

Or run from the solution root:

```bash
dotnet test
```

## API Endpoints

### Upload E File

```http
POST /api/efile/upload
Content-Type: multipart/form-data

file: <file>
commonAddr: 1001
typeId: TYPE_90
```

### Trigger Report

```http
POST /api/efile/trigger-report
Content-Type: application/json

{
  "asduData": [0x90, 0x10, 0x07, 0xE9, 0x03, ...]
}
```

## E File Format

E files use the following format:

```
<table> TABLE_NAME
@Column1	Column2	Column3
#Value1	Value2	Value3
#Value1	-99	Value3
```

- Lines starting with `<table>` define table name
- Lines starting with `@` define column headers (tab-separated)
- Lines starting with `#` contain data rows (tab-separated)
- `-99` is interpreted as NULL
- Tables ending with `_INFO` use upsert logic (based on ID field)
- Other tables use bulk insert

## ASDU Format

Simple ASDU format (not full IEC-102 implementation):

```
Byte 0: Type ID (0x90-0xA8 for E files)
Byte 1: Payload length + 2
Byte 2: Cause of Transmission (0x06=intermediate, 0x07=last frame)
Byte 3-4: Common Address (little-endian)
Byte 5+: Payload
```

## Multi-Frame File Transfer

For large files:
1. File is split into multiple ASDU frames
2. Each frame has COT=0x06 (intermediate) except the last
3. Last frame has COT=0x07 (file complete)
4. Frames are reassembled by CommonAddr + TypeId
5. Complete file is parsed and saved to database

## Testing Workflow

1. Start the database
2. Apply schema with `db/schema.sql`
3. Start the WebAPI: `cd src && dotnet run`
4. Start the simulator: `cd tools/MasterSimulator && dotnet run`
5. Use simulator option 1 or 2 to send test data
6. Check database for received files:
   ```sql
   SELECT * FROM RECEIVED_EFILES;
   SELECT * FROM STATION_INFO;
   ```

## Database Tables

### RECEIVED_EFILES
Tracks all received E files with status and error information.

### STATION_INFO (Example)
Station information table with upsert support.

### DEVICE_INFO (Example)
Device information table with upsert support.

### ENERGY_DATA (Example)
Energy measurement data with bulk insert.

## Implementation Notes

### Current Implementation
- Basic TCP link layer (no full IEC-102 frame format)
- Simple ASDU encoding/decoding
- GBK encoding support for E files
- Tab-separated value parsing
- Upsert for *_INFO tables, insert for others
- Multi-frame reassembly

### 增强功能（v2.0）

本版本实现了以下生产级增强：

#### ✅ 完整 IEC-102 帧解析
- 支持固定长度帧（0x10）、可变长度帧（0x68）和单字节确认帧（0xE5）
- 控制域（ControlField）解析：PRM/FCB/FCV/FC/ACD/DFC 位
- 校验和验证和帧完整性检查

#### ✅ FCB/FCV 帧计数逻辑
- 主站/从站 FCB 状态维护
- 新一轮消息时切换 FCB，重传时不切换
- 支持检测重复帧

#### ✅ 超时与重传策略
- 可配置超时时间（默认 5 秒）
- 可配置最大重传次数（默认 3 次）
- 超时和重传事件暴露给上层
- 详细的超时和重传日志

#### ✅ 并发安全性
- ConcurrentDictionary 用于多连接管理
- SemaphoreSlim 用于发送同步
- 异步队列处理
- 安全的资源释放

#### ✅ 字段类型精确转换
- 支持 int、double、decimal、datetime、bool、string 类型转换
- 可配置的列名映射
- 转换失败时记录错误并置 NULL 或默认值
- 详细的类型转换日志

#### ✅ 完整的日志记录
- ILogger 集成到所有核心组件
- Info/Debug/Warning/Error 级别日志
- 帧收发、ASDU 解析、文件合并、数据库操作日志
- 超时、重传、错误事件日志

#### ✅ lib60870.NET 封装支持
- Lib60870Wrapper 工厂模式
- 通过 UseLib60870 配置切换
- 自动回退到 TcpLinkLayer
- 保持 ILinkLayer 接口兼容

#### ✅ 中文 XML 文档注释
- 所有类、方法、属性添加完整中文注释
- 参数和返回值说明
- 使用示例和注意事项

## 配置说明

### appsettings.json 配置项

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "LpsGateway": "Debug"
    }
  },
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=lps_gateway;Username=postgres;Password=postgres"
  },
  "Lib60870": {
    "UseLib60870": false,        // 是否使用 lib60870.NET（默认 false）
    "Port": 2404,                // TCP 监听端口
    "TimeoutMs": 5000,           // 超时时间（毫秒）
    "MaxRetries": 3,             // 最大重传次数
    "InitialFcb": false,         // FCB 初始值
    "ConnectionString": ""       // 数据库连接字符串（可选，优先级高于 ConnectionStrings.DefaultConnection）
  }
}
```

### 配置项说明

#### UseLib60870
- **类型**: bool
- **默认值**: false
- **说明**: 是否尝试使用 lib60870.NET 库。如果设置为 true 但库不可用，将自动回退到 TcpLinkLayer 实现。

#### Port
- **类型**: int
- **默认值**: 2404
- **说明**: TCP 链路层监听端口，IEC-102 标准端口为 2404。

#### TimeoutMs
- **类型**: int
- **默认值**: 5000
- **说明**: 发送帧后等待响应的超时时间（毫秒）。超时后将触发重传。

#### MaxRetries
- **类型**: int
- **默认值**: 3
- **说明**: 最大重传次数。达到此次数后将放弃发送并记录错误。

#### InitialFcb
- **类型**: bool
- **默认值**: false
- **说明**: 帧计数位（FCB）的初始值。每次发送新一轮消息时会切换此位。

#### ConnectionString
- **类型**: string
- **默认值**: ""
- **说明**: OpenGauss/PostgreSQL 数据库连接字符串。如果配置了此项，将优先使用，否则使用 ConnectionStrings.DefaultConnection。

### 日志级别配置

推荐的日志级别：
- **开发环境**: Debug
- **测试环境**: Information
- **生产环境**: Warning

可以针对不同命名空间配置不同级别：
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "LpsGateway.Lib60870": "Debug",           // 链路层详细日志
      "LpsGateway.Services": "Information",      // 服务层日志
      "LpsGateway.Data": "Information"           // 数据层日志
    }
  }
}
```

## 使用 lib60870.NET

如果要使用 lib60870.NET v2.3.0 库：

1. 安装 lib60870.NET NuGet 包：
   ```bash
   cd src
   dotnet add package lib60870.NET --version 2.3.0
   ```

2. 在 `appsettings.json` 中启用：
   ```json
   {
     "Lib60870": {
       "UseLib60870": true
     }
   }
   ```

3. 如果库不可用，系统会自动回退到 TcpLinkLayer 实现，并在日志中记录警告。

## 运行模拟器与测试

### 运行主站模拟器

模拟器用于测试文件传输功能：

```bash
cd tools/MasterSimulator
dotnet run
```

或连接到不同的主机和端口：
```bash
dotnet run -- <hostname> <port>
```

模拟器提供以下选项：
1. 发送单帧 E 文件数据
2. 发送多帧 E 文件数据（测试分片重组）
3. 发送自定义 ASDU 帧
4. 退出

### FCB/FCV 测试场景

测试 FCB/FCV 功能：

1. 启动 WebAPI 和模拟器
2. 发送第一个多帧文件，观察 FCB 状态
3. 模拟超时（暂停网络），观察重传行为
4. 发送第二个多帧文件，验证 FCB 切换

日志示例：
```
[Info] 接收到有效帧: Variable Frame: Master: FC=03, FCB=False, FCV=True, Addr=0001, DataLen=256
[Debug] 设置 FCB 状态: 1001_144 -> False
[Info] 接收到最后一帧: 1001_144，总分片数: 3
[Debug] 切换 FCB 状态: endpoint -> True
```

### 超时与重传测试

测试超时重传机制：

1. 配置较短的超时时间（如 2000ms）：
   ```json
   {
     "Lib60870": {
       "TimeoutMs": 2000,
       "MaxRetries": 2
     }
   }
   ```

2. 启动应用，发送数据但不响应
3. 观察日志中的超时和重传记录

日志示例：
```
[Warning] 发送超时（2000ms），尝试 1/3
[Warning] 重传帧，尝试 1/2
[Error] 发送失败：已达到最大重传次数 2
```

## 字段类型转换配置

在代码中配置列类型转换：

```csharp
var parser = serviceProvider.GetRequiredService<IEFileParser>() as EFileParser;

// 配置 STATION_INFO 表的列类型
parser.ConfigureColumnTypes("STATION_INFO", new Dictionary<string, Type>
{
    { "Latitude", typeof(decimal) },
    { "Longitude", typeof(decimal) },
    { "Capacity", typeof(decimal) }
});

// 配置列名映射（从文件列名到数据库列名）
parser.ConfigureColumnMapping("ENERGY_DATA", new Dictionary<string, string>
{
    { "站点ID", "StationId" },
    { "有功功率", "ActivePower" },
    { "无功功率", "ReactivePower" }
});
```

## Production Considerations

### 已实现
- ✅ 完整 IEC-102 帧解析（0x10/0x68 frames, control fields, checksums）
- ✅ 序列号和重传机制
- ✅ 事务管理和并发控制
- ✅ 完整字段类型映射
- ✅ 日志记录和监控

### 仍需考虑
- 身份验证和授权
- 性能优化（批量操作、连接池调优）
- 高可用性和灾难恢复
- 监控告警集成（Prometheus/Grafana）
- API 速率限制

## Troubleshooting

### Connection refused
- Ensure the WebAPI is running
- Check the port configuration (default: 2404)
- Verify firewall settings

### Database connection errors
- Verify OpenGauss/PostgreSQL is running
- Check connection string in appsettings.json
- Ensure database exists and schema is applied

### GBK encoding issues
- Ensure System.Text.Encoding.CodePages package is installed
- Verify `Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)` is called

## License

MIT

## Contributing

Pull requests are welcome. For major changes, please open an issue first to discuss what you would like to change.
