# M1 Implementation Guide - Database Design & Basic Infrastructure

This document describes the M1 milestone implementation for the LPS Gateway project, covering database design, project skeleton, and basic infrastructure setup.

## Overview

M1 establishes the foundation for the LPS Gateway system with:
- Complete database schema for configuration management
- Entity models and data access layer
- JWT-based authentication and authorization
- RESTful API for configuration management (ReportType, SftpConfig, Schedule)
- Layered architecture with dependency injection

## Database Schema

### Core Tables

#### Authentication & Audit
- **users** - User accounts with role-based access (Admin, Operator)
- **audit_logs** - Operation audit trail with JSON details

#### Configuration Management
- **report_types** - Report type definitions (e.g., DAILY_ENERGY, MONTHLY_SUMMARY)
- **sftp_configs** - SFTP server configurations with encrypted credentials
- **schedules** - Scheduling rules (daily, monthly, cron) for automated downloads

#### File Management
- **file_records** - Downloaded file metadata with retention policies
- **file_transfer_tasks** - IEC-102 file transfer task tracking

#### Protocol Support
- **tcp_session_logs** - TCP connection session logs
- **protocol_command_logs** - IEC-102 protocol frame logs

#### Legacy Support
- **received_efiles** - Backward compatibility with existing E-file tracking

### Schema Initialization

```bash
# Run the database schema
psql -h localhost -U postgres -d lps_gateway -f db/schema.sql
```

The schema includes:
- Proper foreign key relationships
- Indexes for query optimization
- JSON/JSONB support for flexible metadata
- Default admin user (username: admin, password: admin123)

## Project Structure

```
src/
├── Controllers/
│   ├── AuthController.cs           # JWT authentication
│   ├── ReportTypesController.cs    # Report type CRUD
│   ├── SftpConfigsController.cs    # SFTP config CRUD
│   ├── SchedulesController.cs      # Schedule CRUD
│   └── EFileController.cs          # (existing) E-file upload
├── Data/
│   ├── Models/
│   │   ├── User.cs                 # User entity
│   │   ├── AuditLog.cs            # Audit log entity
│   │   ├── ReportType.cs          # Report type entity
│   │   ├── SftpConfig.cs          # SFTP config entity
│   │   ├── Schedule.cs            # Schedule entity
│   │   ├── FileRecord.cs          # File record entity
│   │   └── FileTransferTask.cs    # Transfer task entity
│   ├── Repositories/
│   │   ├── IReportTypeRepository.cs
│   │   ├── ReportTypeRepository.cs
│   │   ├── ISftpConfigRepository.cs
│   │   ├── SftpConfigRepository.cs
│   │   ├── IScheduleRepository.cs
│   │   └── ScheduleRepository.cs
│   └── (existing repositories)
├── Models/
│   ├── AuthModels.cs              # Login request/response DTOs
│   └── ConfigurationModels.cs     # Configuration DTOs
├── Services/
│   ├── IAuthService.cs            # Authentication service interface
│   ├── AuthService.cs             # Authentication service impl
│   └── (existing services)
└── Program.cs                     # DI configuration + JWT setup
```

## Authentication & Authorization

### JWT Configuration

JWT settings are configured in `appsettings.json`:

```json
{
  "JwtSettings": {
    "SecretKey": "Your-Secret-Key-Min-32-Characters",
    "Issuer": "LpsGateway",
    "Audience": "LpsGatewayClients",
    "ExpirationMinutes": 480
  }
}
```

**Note**: Change the SecretKey in production!

### User Roles

- **Admin**: Full access to create, update, delete configurations
- **Operator**: Read-only access to configurations

### Using Authentication

1. **Login** to get JWT token:
```bash
curl -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username": "admin", "password": "admin123"}'
```

Response:
```json
{
  "success": true,
  "data": {
    "token": "eyJhbGciOiJIUzI1NiIs...",
    "username": "admin",
    "role": "Admin",
    "expiresAt": "2024-11-11T02:31:00Z"
  }
}
```

2. **Use token** in subsequent requests:
```bash
curl -X GET http://localhost:5000/api/reporttypes \
  -H "Authorization: Bearer eyJhbGciOiJIUzI1NiIs..."
```

## API Endpoints

### Authentication

#### POST /api/auth/login
Login and get JWT token.

**Request:**
```json
{
  "username": "admin",
  "password": "admin123"
}
```

**Response:**
```json
{
  "success": true,
  "data": {
    "token": "eyJhbGc...",
    "username": "admin",
    "role": "Admin",
    "expiresAt": "2024-11-11T02:31:00Z"
  }
}
```

### Report Types

#### GET /api/reporttypes
Get all report types. Query parameters:
- `enabled` (optional): Filter by enabled status

#### GET /api/reporttypes/{id}
Get report type by ID.

#### POST /api/reporttypes (Admin only)
Create new report type.

**Request:**
```json
{
  "code": "HOURLY_DATA",
  "name": "Hourly Data Report",
  "description": "Hourly measurement data",
  "defaultSftpConfigId": 1,
  "enabled": true
}
```

#### PUT /api/reporttypes/{id} (Admin only)
Update report type.

#### DELETE /api/reporttypes/{id} (Admin only)
Delete report type.

### SFTP Configurations

#### GET /api/sftpconfigs
Get all SFTP configurations (sensitive data masked).

#### GET /api/sftpconfigs/{id}
Get SFTP configuration by ID.

#### POST /api/sftpconfigs (Admin only)
Create new SFTP configuration.

**Request:**
```json
{
  "name": "Production SFTP",
  "host": "sftp.example.com",
  "port": 22,
  "username": "sftpuser",
  "authType": "password",
  "password": "secret",
  "basePathTemplate": "/reports/{yyyy}/{MM}/{dd}/",
  "concurrencyLimit": 5,
  "timeoutSec": 30,
  "enabled": true
}
```

**Note**: Passwords are automatically encrypted when stored.

#### PUT /api/sftpconfigs/{id} (Admin only)
Update SFTP configuration.

#### DELETE /api/sftpconfigs/{id} (Admin only)
Delete SFTP configuration.

### Schedules

#### GET /api/schedules
Get all schedules.

#### GET /api/schedules/reporttype/{reportTypeId}
Get schedules for a specific report type.

#### GET /api/schedules/{id}
Get schedule by ID.

#### POST /api/schedules (Admin only)
Create new schedule.

**Daily Schedule Example:**
```json
{
  "reportTypeId": 1,
  "scheduleType": "daily",
  "times": ["08:00", "14:00", "20:00"],
  "timezone": "Asia/Shanghai",
  "enabled": true
}
```

**Monthly Schedule Example:**
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

**Cron Schedule Example:**
```json
{
  "reportTypeId": 1,
  "scheduleType": "cron",
  "cronExpression": "0 0 */6 * * ?",
  "timezone": "UTC",
  "enabled": true
}
```

#### PUT /api/schedules/{id} (Admin only)
Update schedule.

#### DELETE /api/schedules/{id} (Admin only)
Delete schedule.

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
    "MaxRetries": 3
  }
}
```

## Running the Application

### Prerequisites
- .NET 8.0 SDK
- PostgreSQL or OpenGauss database

### Setup

1. **Create database:**
```bash
createdb lps_gateway
```

2. **Initialize schema:**
```bash
psql -d lps_gateway -f db/schema.sql
```

3. **Update connection string** in `appsettings.json`

4. **Run the application:**
```bash
cd src
dotnet run
```

The application will start on:
- HTTP: http://localhost:5000
- HTTPS: https://localhost:5001
- Swagger UI: http://localhost:5000/swagger

### Docker Support (Coming in M6)

```bash
docker-compose up -d
```

## Development

### Building

```bash
dotnet build LpsGateway.sln
```

### Running Tests

```bash
dotnet test
```

### Code Structure

The project follows a layered architecture:

1. **Controllers** - HTTP API endpoints with input validation
2. **Services** - Business logic and orchestration
3. **Repositories** - Data access abstraction
4. **Models** - Entity models and DTOs

### Adding New Entities

1. Create entity model in `Data/Models/`
2. Create repository interface in `Data/`
3. Implement repository in `Data/`
4. Create DTO in `Models/`
5. Create controller in `Controllers/`
6. Register repository in `Program.cs` DI container

## Security Considerations

### Password Storage
- Passwords are hashed using BCrypt with salt
- SFTP passwords use Base64 encoding (improve in production with KMS)

### JWT Tokens
- Token expiration: 8 hours (configurable)
- Tokens include user ID, username, and role claims
- Symmetric key signing (HS256)

### API Security
- All configuration endpoints require authentication
- Admin operations require Admin role
- Sensitive data (passwords) masked in responses

### Production Recommendations
1. Use strong JWT secret key (>32 chars)
2. Use HTTPS only
3. Implement rate limiting
4. Use proper credential encryption (KMS/DPAPI)
5. Enable audit logging
6. Implement IP whitelisting for admin operations

## Troubleshooting

### Database Connection Issues
- Verify PostgreSQL is running
- Check connection string in appsettings.json
- Ensure database exists and schema is initialized

### Authentication Issues
- Verify JWT settings in appsettings.json
- Check token expiration
- Ensure user exists and is enabled

### Build Warnings
- SqlSugar compatibility warning is expected (package targets .NET Framework but works with .NET 8)

## Next Steps (M2) - ✅ COMPLETED

See [M2-Implementation-Guide.md](M2-Implementation-Guide.md) for details.

- [x] Implement SFTP Manager for file downloads
- [x] Integrate Quartz.NET for scheduling
- [x] Implement manual trigger API
- [x] Add file download workflow
- [ ] Add distributed locking (PostgreSQL advisory locks or Redis) - Deferred to future milestone

## Support

For issues and questions:
- Check logs in console output
- Review database constraints and foreign keys
- Verify JWT token validity
- Check Swagger UI for API documentation

## License

[Your License Here]
