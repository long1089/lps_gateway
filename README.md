# LPS Gateway - IEC-102 E-file Reception and Processing

This project implements a .NET 6 WebAPI for receiving and processing IEC-102 E-files from power stations.

## Features

- **IEC-102 Link Layer**: TCP-based implementation with frame parsing (0x10 fixed and 0x68 variable frames)
- **ASDU Management**: Support for TYPE IDs 0x90-0xA8 with custom mapping
- **E-file Parser**: GBK-encoded file parsing with table/data extraction
- **Database Storage**: SqlSugar-based OpenGauss/PostgreSQL repository
- **RESTful API**: File upload endpoint for manual E-file submission
- **Master Simulator**: Testing tool to simulate IEC-102 E-file transfers

## Architecture

```
┌─────────────────────────────────────────────┐
│           LPS Gateway WebAPI                │
├─────────────────────────────────────────────┤
│                                             │
│  ┌──────────────┐      ┌─────────────────┐ │
│  │ EFileController│────▶│  EFileParser    │ │
│  └──────────────┘      └─────────────────┘ │
│                              │              │
│  ┌──────────────────┐        │              │
│  │FileTransferManager│        │              │
│  │  (IEC-102 Server) │        ▼              │
│  └──────────────────┘   ┌──────────────┐    │
│         │               │EFileRepository│    │
│         ▼               └──────────────┘    │
│  ┌──────────────┐             │             │
│  │ TcpLinkLayer │             ▼             │
│  │(IEC-102 Link)│      OpenGauss/PostgreSQL │
│  └──────────────┘                           │
└─────────────────────────────────────────────┘
```

## Prerequisites

- .NET 6.0 SDK (or higher with .NET 6.0 targeting)
- OpenGauss or PostgreSQL database
- (Optional) Docker for containerized database

## Database Setup

1. Install OpenGauss or PostgreSQL:
```bash
# Using Docker
docker run -d \
  --name lpsgateway-db \
  -e POSTGRES_PASSWORD=postgres \
  -e POSTGRES_DB=lpsgateway \
  -p 5432:5432 \
  postgres:15
```

2. Initialize the schema:
```bash
psql -h localhost -U postgres -d lpsgateway -f db/schema.sql
```

## Configuration

Edit `src/LPSGateway/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=lpsgateway;Username=postgres;Password=YOUR_PASSWORD"
  },
  "Iec102": {
    "Host": "0.0.0.0",
    "Port": 2404
  }
}
```

## Running Locally

### 1. Start the Gateway Service

```bash
cd src/LPSGateway
dotnet run
```

The service will:
- Start an HTTP API on `https://localhost:5001` (or configured port)
- Start an IEC-102 TCP server on port `2404` (configurable)
- Accept both protocol-based and HTTP-based E-file submissions

### 2. Test with Master Simulator

In a separate terminal, run the simulator to send test E-files via IEC-102:

```bash
cd tools/MasterSimulator
dotnet run
```

Or specify custom host/port:

```bash
dotnet run -- 127.0.0.1 2404
```

The simulator will:
- Connect to the IEC-102 server
- Send a sample E-file split across two frames
- Demonstrate multi-frame buffering and COT=0x07 end-of-transfer signaling

### 3. Upload via HTTP API

You can also upload E-files directly via the REST API:

```bash
curl -X POST http://localhost:5000/api/efile/upload \
  -F "file=@/path/to/efile.txt" \
  -H "Content-Type: multipart/form-data"
```

## Running Tests

```bash
dotnet test
```

**Note:** Tests require .NET 6.0 runtime. If you encounter runtime errors, ensure .NET 6.0 is installed or update the test project to target your available runtime.

## Project Structure

```
lps_gateway/
├── src/
│   └── LPSGateway/
│       ├── Controllers/         # API endpoints
│       ├── Data/               # Repository and models
│       ├── Lib60870/           # IEC-102 protocol implementation
│       ├── Services/           # Business logic services
│       └── Program.cs          # Application entry point
├── tests/
│   └── LPSGateway.Tests/       # xUnit tests
├── tools/
│   └── MasterSimulator/        # IEC-102 master simulator
└── db/
    └── schema.sql              # Database schema
```

## E-file Format

E-files are GBK-encoded text files with the following structure:

```
<table_name>
@header_key1	header_value1
@header_key2	header_value2
#data_col1	data_col2	data_col3
#data_col1	data_col2	-99
```

- Lines starting with `<>` define table blocks
- Lines starting with `@` define header metadata
- Lines starting with `#` define data rows (tab-separated)
- `-99` values are mapped to `NULL` in the database

## IEC-102 Protocol Details

### Frame Types

- **Fixed Frame (0x10)**: Short commands without user data
  - Format: `10 C A A CS 16`
  
- **Variable Frame (0x68)**: Data transfer frames
  - Format: `68 L L 68 C A A DATA CS 16`

### ASDU Structure

- **Type ID**: 0x90-0xA8 (E-file types)
- **Cause of Transmission (COT)**:
  - `0x06`: Data transfer in progress
  - `0x07`: End of transfer (triggers processing)

### Type ID Mappings

| Type ID | Table Name         |
|---------|--------------------|
| 0x90    | basic_info         |
| 0x91    | power_quality      |
| 0x92    | voltage_data       |
| 0x93    | current_data       |
| 0x94    | power_data         |
| 0x95    | energy_data        |
| ...     | ...                |

See `src/LPSGateway/Lib60870/Mapping.cs` for complete mapping.

## API Endpoints

### POST /api/efile/upload

Upload an E-file for processing.

**Request:**
```
Content-Type: multipart/form-data
file: <binary file data>
```

**Response:**
```json
{
  "success": true,
  "message": "File processed successfully",
  "sourceIdentifier": "efile.txt_20241104070000"
}
```

### GET /api/efile/health

Health check endpoint.

**Response:**
```json
{
  "status": "healthy",
  "timestamp": "2024-11-04T07:00:00Z"
}
```

## Development

### Adding New Table Mappings

Edit `src/LPSGateway/Lib60870/Mapping.cs` to add new TYPE ID mappings:

```csharp
{ 0xA9, "new_table_name" }
```

### Database Schema

The application creates tables dynamically:
- `{table_name}_info`: Key-value pairs for header metadata
- `{table_name}_data`: Columns based on E-file content

## Troubleshooting

### Connection Refused

If the simulator can't connect, ensure:
1. The gateway is running
2. Firewall allows port 2404
3. Configuration matches (host/port)

### Database Errors

Check:
1. Database is running and accessible
2. Connection string is correct
3. Database user has CREATE TABLE permissions

### GBK Encoding Issues

The application registers the GBK encoding provider on startup. If you encounter encoding errors, ensure the E-file is actually GBK-encoded.

## License

MIT License

## Contributing

Please submit issues and pull requests to the GitHub repository.
