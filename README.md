# LPS Gateway - IEC-102 Extended E File Reception System

This project implements an IEC-102 extended E file reception, parsing, storage, and reporting system running on .NET 8 WebAPI with OpenGauss database and SqlSugar ORM.

## Features

- **WebAPI Support**: E file upload and trigger reporting endpoints
- **Link Layer**: TCP-based link layer compatible with IEC-102 protocol
- **ASDU Management**: Support for custom Type IDs (0x90-0xA8) with ASDU encoding/decoding
- **File Transfer**: Multi-frame file transfer with automatic reassembly
- **E File Parser**: GBK encoding support, table-based parsing with upsert/insert logic
- **Database**: OpenGauss/PostgreSQL with SqlSugar ORM
- **Testing**: Unit tests with xUnit and Moq
- **Tools**: Master station simulator for integration testing

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

### Production Considerations
- Full IEC-102 frame parsing (0x10/0x68 frames, control fields, checksums)
- Sequence numbers and retransmission
- Transaction management and concurrency
- Error recovery and resilience
- Complete field type mapping
- Authentication and authorization
- Logging and monitoring
- Performance optimization

### Library Alternatives
If lib60870.NET is available and preferred, replace `TcpLinkLayer` implementation with lib60870.NET API while keeping the same `ILinkLayer` interface.

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
