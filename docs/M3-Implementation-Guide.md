# M3 Implementation Guide - IEC-102 Master & Slave TCP Communication

This document describes the M3 milestone implementation for the LPS Gateway project, covering both TCP master (client) and TCP slave (server) functionality for IEC-102 Extended protocol communication.

## Overview

M3 establishes the bidirectional IEC-102 protocol communication infrastructure with:
- TCP Server (Slave/从站) for receiving connections from remote masters
- TCP Client (Master/主站) for connecting to remote slaves
- Full IEC-102 Extended protocol support (control frames, ASDU handling)
- FCB/FCV/ACD/DFC state management
- Time synchronization (TYP=0x8B)
- File request/cancel commands (TYP=0x8D, 0x8E)
- Comprehensive protocol logging

## Architecture

```
┌─────────────────────────────────────────────────────┐
│                  LPS Gateway                        │
│                                                     │
│  ┌──────────────────────────────────────────────┐  │
│  │         Iec102SlaveHostedService             │  │
│  │              (TCP Server)                    │  │
│  └────────────────┬─────────────────────────────┘  │
│                   │                                 │
│  ┌────────────────▼─────────────────────────────┐  │
│  │            Iec102Slave                       │  │
│  │  - Multi-client session management           │  │
│  │  - FCB/FCV validation                        │  │
│  │  - ACD/DFC support                           │  │
│  │  - Class 1/2 data queues                    │  │
│  └──────────────────────────────────────────────┘  │
│                                                     │
│  ┌──────────────────────────────────────────────┐  │
│  │         Iec102MasterHostedService            │  │
│  │              (TCP Client)                    │  │
│  └────────────────┬─────────────────────────────┘  │
│                   │                                 │
│  ┌────────────────▼─────────────────────────────┐  │
│  │            Iec102Master                      │  │
│  │  - Connection management                     │  │
│  │  - FCB state tracking                        │  │
│  │  - Request/response handling                 │  │
│  │  - Time sync & file commands                 │  │
│  └──────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────┘
              ▲                        │
              │ TCP                    │ TCP
              │ Port 3000              │ Port 3000
              │                        ▼
     ┌────────┴────────┐      ┌──────────────┐
     │ Remote Master   │      │ Remote Slave │
     │  (主站)         │      │  (从站)      │
     └─────────────────┘      └──────────────┘
```

## Components

### 1. Iec102Slave (TCP Server)

**File**: `src/Lib60870/Iec102Slave.cs`

**Purpose**: Acts as a slave (从站/从动端) to accept connections from remote masters and respond to their requests.

**Key Features**:
- Multi-client support with `ConcurrentDictionary<string, ClientSession>`
- Per-session FCB (Frame Count Bit) tracking
- ACD (Access Demand) flag for priority data signaling
- DFC (Data Flow Control) flag for flow control
- Dual data queues (Class 1 and Class 2)
- Automatic frame parsing from TCP stream

**API**:
```csharp
public class Iec102Slave : IDisposable
{
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync();
    void QueueClass1Data(byte typeId, byte cot, byte[] data);
    void QueueClass2Data(byte typeId, byte cot, byte[] data);
    
    event EventHandler<string>? ClientConnected;
    event EventHandler<string>? ClientDisconnected;
    event EventHandler<FrameReceivedEventArgs>? FrameReceived;
}
```

**Supported Commands**:
- `FC=0x00`: Reset Remote Link - Resets FCB state
- `FC=0x09`: Request Link Status - Returns status with ACD flag
- `FC=0x0A`: Request Class 1 Data - Dequeues priority data
- `FC=0x0B`: Request Class 2 Data - Dequeues normal data
- `FC=0x03`: User Data - Processes time sync, file requests, etc.

### 2. Iec102Master (TCP Client)

**File**: `src/Lib60870/Iec102Master.cs`

**Purpose**: Acts as a master (主站/启动端) to connect to remote slaves and initiate communication.

**Key Features**:
- Asynchronous connection management
- Automatic FCB toggling for new request rounds
- Event-driven frame reception
- Timeout and retry support
- Connection state tracking

**API**:
```csharp
public class Iec102Master : IDisposable
{
    Task<bool> ConnectAsync(CancellationToken cancellationToken = default);
    Task DisconnectAsync();
    
    // Link management
    Task<bool> ResetLinkAsync(CancellationToken cancellationToken = default);
    Task<bool> RequestLinkStatusAsync(CancellationToken cancellationToken = default);
    
    // Data requests
    Task<bool> RequestClass1DataAsync(CancellationToken cancellationToken = default);
    Task<bool> RequestClass2DataAsync(CancellationToken cancellationToken = default);
    
    // Extended commands
    Task<bool> SendTimeSyncAsync(DateTime utcTime, CancellationToken cancellationToken = default);
    Task<bool> SendFileRequestAsync(byte reportTypeCode, byte mode, DateTime referenceTime, 
        DateTime? endTime = null, CancellationToken cancellationToken = default);
    Task<bool> SendFileCancelAsync(byte reportTypeCode, byte cancelScope, 
        CancellationToken cancellationToken = default);
    
    event EventHandler<Iec102Frame>? FrameReceived;
    event EventHandler<bool>? ConnectionChanged;
}
```

### 3. Hosted Services

#### Iec102SlaveHostedService

**File**: `src/HostedServices/Iec102SlaveHostedService.cs`

Manages the lifecycle of the IEC-102 slave server within the ASP.NET Core application.

**Configuration**:
```json
{
  "Iec102Slave": {
    "Enabled": true,
    "Port": 3000,
    "StationAddress": 65535
  }
}
```

#### Iec102MasterHostedService

**File**: `src/HostedServices/Iec102MasterHostedService.cs`

Optional service for establishing outbound connections to remote slaves.

**Configuration**:
```json
{
  "Iec102Master": {
    "Enabled": false,
    "Host": "localhost",
    "Port": 3000,
    "StationAddress": 65535,
    "TimeoutMs": 5000,
    "MaxRetries": 3,
    "PollingIntervalSeconds": 0,
    "PollClass1Data": false
  }
}
```

**Polling**: When `PollingIntervalSeconds > 0`, the master automatically polls for Class 2 (and optionally Class 1) data at the specified interval.

## Protocol Details

### Control Field (C Byte)

**Master Frame (PRM=1)**:
```
Bit 7: Reserved (0)
Bit 6: PRM = 1 (Master)
Bit 5: FCB (Frame Count Bit)
Bit 4: FCV (Frame Count Valid)
Bit 3-0: Function Code (FC)
```

**Slave Frame (PRM=0)**:
```
Bit 7: Reserved (0)
Bit 6: PRM = 0 (Slave)
Bit 5: ACD (Access Demand)
Bit 4: DFC (Data Flow Control)
Bit 3-0: Function Code (FC)
```

### Function Codes

**Master → Slave**:
- `0x00`: Reset Remote Link (FCV=0)
- `0x03`: User Data (FCV=1)
- `0x09`: Request Link Status (FCV=0)
- `0x0A`: Request Class 1 Data (FCV=1)
- `0x0B`: Request Class 2 Data (FCV=1)

**Slave → Master**:
- `0x00`: Positive ACK
- `0x01`: Negative ACK (busy)
- `0x08`: User Data
- `0x09`: No Data Available
- `0x0B`: Link Status / Access Demand

### FCB (Frame Count Bit) Management

**Purpose**: Detect duplicate frames and ensure proper sequencing.

**Rules**:
1. Master maintains one FCB state per slave
2. FCB toggles on successful new request/response round (when FCV=1)
3. FCB does NOT toggle on retransmission
4. Slave validates received FCB against expected value
5. Reset command (FC=0x00) resets FCB to 0

**Example**:
```
Master sends: Request Class 2 Data (FCB=0, FCV=1)
Slave responds: User Data
Master updates: FCB := 1

Master sends: Request Class 2 Data (FCB=1, FCV=1)
(timeout, no response)
Master retransmits: Request Class 2 Data (FCB=1, FCV=1)  ← Same FCB
```

### ASDU Structure

```
┌─────────────────────────────────────────┐
│ TypeId (1 byte)                         │
│ VSQ (1 byte) = 0x01                     │
│ COT (1 byte)                            │
│ CommonAddr (2 bytes, little-endian)     │
│ RecordAddr (1 byte) = 0x00              │
│ Data (variable length)                  │
└─────────────────────────────────────────┘
```

### Extended Commands

#### Time Synchronization (TYP=0x8B)

**Master → Slave**:
```
TypeId: 0x8B
VSQ: 0x01
COT: 0x06 (Activate)
Data: CP56Time2a (7 bytes, UTC)
```

**Slave → Master**:
```
TypeId: 0x8B
VSQ: 0x01
COT: 0x07 (Activate Confirm)
Data: (empty)
```

**CP56Time2a Format** (7 bytes):
```
Byte 0-1: Milliseconds (0-59999, little-endian)
Byte 2: Minutes (0-59)
Byte 3: Hours (0-23)
Byte 4: Day (1-31)
Byte 5: Month (1-12)
Byte 6: Year (0-99, offset from 2000)
```

#### File Request (TYP=0x8D)

**Master → Slave**:
```
TypeId: 0x8D
VSQ: 0x01
COT: 0x06 (Activate)
Data:
  Byte 0: ReportTypeCode (1-19)
  Byte 1: Mode (bit 0: 0=latest, 1=range; bit 1: 1=compressed)
  Byte 2-8: Start/RefTime (CP56Time2a)
  Byte 9-15: EndTime (CP56Time2a, only if mode bit 0 = 1)
```

**Slave → Master**:
```
TypeId: 0x8D
VSQ: 0x01
COT: 0x07 (Activate Confirm)
```

Followed by file data frames (TYP=0x95-0xA8).

#### File Cancel (TYP=0x8E)

**Master → Slave**:
```
TypeId: 0x8E
VSQ: 0x01
COT: 0x06 (Activate)
Data:
  Byte 0: ReportTypeCode (1-19)
  Byte 1: CancelScope (0=all, 1=not started, 2=in progress)
```

**Slave → Master**:
```
TypeId: 0x8E
VSQ: 0x01
COT: 0x07 (Activate Confirm)
```

## Usage Examples

### Starting the Slave Server

```csharp
// Configure in appsettings.json
{
  "Iec102Slave": {
    "Enabled": true,
    "Port": 3000,
    "StationAddress": 65535
  }
}

// Server starts automatically via Iec102SlaveHostedService
// Log output:
// [Information] 启动 IEC-102 从站服务: Port=3000, StationAddress=0xFFFF
// [Information] 从站服务器已启动
```

### Using the Master Client

```csharp
var logger = loggerFactory.CreateLogger<Iec102Master>();
var master = new Iec102Master("192.168.1.100", 3000, 0xFFFF, logger);

// Subscribe to events
master.FrameReceived += (sender, frame) =>
{
    Console.WriteLine($"Received: {frame}");
};

// Connect
await master.ConnectAsync();

// Initialize link
await master.ResetLinkAsync();
await master.RequestLinkStatusAsync();

// Request data
await master.RequestClass2DataAsync();

// Send time sync
await master.SendTimeSyncAsync(DateTime.UtcNow);

// Request file
await master.SendFileRequestAsync(
    reportTypeCode: 1,
    mode: 0,  // Latest
    referenceTime: DateTime.UtcNow,
    endTime: null
);

// Disconnect
await master.DisconnectAsync();
```

### Queueing Data on Slave

```csharp
var slave = serviceProvider.GetRequiredService<Iec102Slave>();

// Queue Class 2 data
var fileData = Encoding.UTF8.GetBytes("Sample file content");
slave.QueueClass2Data(
    typeId: 0x95,  // EFJ_FARM_INFO
    cot: 0x08,     // Segment More
    data: fileData
);

// Master will receive this data when it requests Class 2 data
```

## Testing

### Unit Tests

**File**: `tests/Iec102MasterSlaveTests.cs`

**Test Coverage**:
1. `Slave_StartStop_WorksCorrectly` - Verifies slave lifecycle
2. `Master_ConnectDisconnect_WorksCorrectly` - Verifies master connection
3. `Master_SendResetLink_SlaveResponds` - Tests link reset
4. `Slave_QueueData_MasterCanRequest` - Tests data exchange
5. `Master_SendTimeSync_SlaveResponds` - Tests time synchronization
6. `Slave_MultipleClients_HandlesCorrectly` - Tests multiple connections
7. `ControlField_MasterFrame_CreatesCorrectly` - Tests control field
8. `ControlField_SlaveFrame_CreatesCorrectly` - Tests slave frames
9. `Iec102Frame_BuildVariableFrameWithAsdu_CreatesCorrectFrame` - Tests frame building

**Running Tests**:
```bash
cd /home/runner/work/lps_gateway/lps_gateway
dotnet test LpsGateway.sln

# Output:
# Total tests: 19
#      Passed: 19
```

### Integration Testing

**Tool**: `tools/MasterSimulatorM3/`

This interactive console application demonstrates full master functionality:

```bash
cd tools/MasterSimulatorM3
dotnet run [host] [port] [stationAddr]

# Example:
dotnet run localhost 3000
```

**Features**:
- Connect to slave
- Send link reset
- Request link status
- Request Class 1/2 data
- Send time synchronization
- Send file request
- Send file cancel
- Execute full initialization sequence

## Configuration

### appsettings.json

```json
{
  "Iec102Slave": {
    "Enabled": true,
    "Port": 3000,
    "StationAddress": 65535
  },
  "Iec102Master": {
    "Enabled": false,
    "Host": "localhost",
    "Port": 3000,
    "StationAddress": 65535,
    "TimeoutMs": 5000,
    "MaxRetries": 3,
    "PollingIntervalSeconds": 0,
    "PollClass1Data": false
  }
}
```

**Parameters**:

**Slave**:
- `Enabled`: Whether to start the slave server
- `Port`: TCP port to listen on
- `StationAddress`: IEC-102 station address (typically 0xFFFF)

**Master**:
- `Enabled`: Whether to start the master client
- `Host`: Remote slave hostname/IP
- `Port`: Remote slave TCP port
- `StationAddress`: IEC-102 station address for remote slave
- `TimeoutMs`: Response timeout in milliseconds
- `MaxRetries`: Maximum retry attempts for failed transmissions
- `PollingIntervalSeconds`: Auto-poll interval (0 = disabled)
- `PollClass1Data`: Also poll Class 1 data during auto-poll

## Logging

The implementation provides comprehensive logging at multiple levels:

### Debug Level
```
[Debug] 发送帧: 10-40-FF-FF-3F-16
[Debug] 接收到有效帧: Fixed Frame: Master: FC=00, FCB=False, FCV=False, Addr=FFFF
[Debug] FCB 状态切换: 192.168.1.100:54321 -> True
```

### Information Level
```
[Information] 启动从站服务器: Port=3000, StationAddress=0xFFFF
[Information] 接受主站连接: 192.168.1.100:54321
[Information] 处理链路复位请求: 192.168.1.100:54321
[Information] 连接到从站: localhost:3000
[Information] 发送链路复位命令
```

### Error Level
```
[Error] 连接从站失败
[Error] 端点 192.168.1.100:54321 不可用
[Error] 发送失败：已达到最大重传次数 3
```

## Security Considerations

### Network Security
1. Use firewall rules to restrict access to IEC-102 ports
2. Consider VPN or private networking for production deployments
3. Monitor connection attempts and failed authentication

### Protocol Security
1. Station address validation (ensure correct address)
2. Frame validation (checksum, length, structure)
3. Rate limiting for connection attempts
4. Session timeout management

### Application Security
1. Log all protocol events for audit trails
2. Monitor for abnormal communication patterns
3. Implement connection limits per client
4. Validate all incoming data before processing

## Troubleshooting

### Slave Not Accepting Connections

**Symptoms**: Master cannot connect, "Connection refused" error

**Solutions**:
- Verify `Iec102Slave.Enabled = true` in configuration
- Check firewall rules allow incoming connections on the configured port
- Verify port is not already in use: `netstat -an | grep 3000`
- Check application logs for startup errors

### Master Cannot Connect to Slave

**Symptoms**: `ConnectAsync()` returns false

**Solutions**:
- Verify slave host/port are correct
- Check network connectivity: `ping <host>`
- Verify slave is running and listening
- Check timeout settings (increase `TimeoutMs` if needed)

### FCB Validation Failures

**Symptoms**: "FCB 不匹配" warnings in logs

**Solutions**:
- Ensure proper link reset sequence before data exchange
- Verify master correctly toggles FCB on successful responses
- Check for duplicate or out-of-order frames
- Reset link: `master.ResetLinkAsync()`

### No Data Received

**Symptoms**: Master requests data but receives "No Data Available"

**Solutions**:
- Verify slave has queued data: `slave.QueueClass2Data(...)`
- Check correct data class (Class 1 vs Class 2)
- Ensure slave is not sending data to different client
- Review ACD flag logic for Class 1 data

## Performance Considerations

### Concurrency
- Slave supports multiple simultaneous client connections
- Each client session has independent FCB state
- Send operations use `SemaphoreSlim` for thread safety

### Memory Usage
- Frame buffers use `List<byte>` for dynamic sizing
- Completed frames removed from buffer immediately
- Data queues use `ConcurrentQueue` for efficiency

### Network Efficiency
- TCP KeepAlive recommended for long-lived connections
- Batch data requests when possible
- Use appropriate polling intervals (avoid excessive polling)

## Future Enhancements

Potential improvements for consideration:

1. **Connection Management**
   - Automatic reconnection with exponential backoff
   - Connection pooling for multiple slaves
   - Health monitoring and alerting

2. **Protocol Extensions**
   - Support for 0x91-0x94 file control frames
   - Enhanced file transfer with chunking
   - Compression support

3. **Monitoring**
   - Prometheus metrics (connections, frames, errors)
   - Grafana dashboards
   - Real-time protocol trace viewer

4. **Testing**
   - Load testing with multiple concurrent clients
   - Protocol fuzzing for robustness
   - Automated integration test suite

## Summary

M3 milestone deliverables:
- ✅ Iec102Master TCP client implementation
- ✅ Iec102Slave TCP server implementation
- ✅ FCB/FCV/ACD/DFC state management
- ✅ Time synchronization (TYP=0x8B)
- ✅ File request/cancel (TYP=0x8D, 0x8E)
- ✅ Hosted services for both master and slave
- ✅ Configuration support via appsettings.json
- ✅ Comprehensive logging
- ✅ 19 unit tests (all passing)
- ✅ MasterSimulatorM3 interactive tool
- ✅ Zero build warnings or errors

The system now supports full bidirectional IEC-102 Extended protocol communication.
