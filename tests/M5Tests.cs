using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using SqlSugar;
using LpsGateway.Data;
using LpsGateway.Data.Models;
using LpsGateway.Services;
using LpsGateway.Models;

namespace LpsGateway.Tests;

/// <summary>
/// M5 功能测试 - 仪表盘、审计日志和保留策略
/// </summary>
public class M5Tests
{
    private readonly Mock<ISqlSugarClient> _mockDb;
    private readonly Mock<IFileRecordRepository> _mockFileRecordRepo;
    private readonly Mock<IAuditLogRepository> _mockAuditLogRepo;
    private readonly Mock<ILogger<DashboardService>> _mockDashboardLogger;
    private readonly Mock<ILogger<AuditLogRepository>> _mockAuditLogger;
    private readonly Mock<ICommunicationStatusBroadcaster> _mockStatusBroadcaster;

    public M5Tests()
    {
        _mockDb = new Mock<ISqlSugarClient>();
        _mockFileRecordRepo = new Mock<IFileRecordRepository>();
        _mockAuditLogRepo = new Mock<IAuditLogRepository>();
        _mockDashboardLogger = new Mock<ILogger<DashboardService>>();
        _mockAuditLogger = new Mock<ILogger<AuditLogRepository>>();
        _mockStatusBroadcaster = new Mock<ICommunicationStatusBroadcaster>();
    }

    #region Dashboard Service Tests

    [Fact]
    public async Task DashboardService_GetDiskUsage_ReturnsValidModel()
    {
        // Arrange
        var service = new DashboardService(_mockDb.Object, _mockFileRecordRepo.Object, _mockDashboardLogger.Object, _mockStatusBroadcaster.Object);

        // Act
        var result = await service.GetDiskUsageAsync();

        // Assert
        Assert.NotNull(result);
        Assert.True(result.TotalSpaceBytes >= 0);
        Assert.True(result.FreeSpaceBytes >= 0);
        Assert.True(result.UsedSpaceBytes >= 0);
        Assert.True(result.UsagePercent >= 0 && result.UsagePercent <= 100);
    }

    [Fact]
    public void DiskUsageModel_IsWarning_ReturnsTrueWhenAbove80Percent()
    {
        // Arrange
        var model = new DiskUsageModel
        {
            TotalSpaceBytes = 100,
            UsedSpaceBytes = 85,
            FreeSpaceBytes = 15,
            UsagePercent = 85.0
        };

        // Assert
        Assert.True(model.IsWarning);
        Assert.False(model.IsCritical);
    }

    [Fact]
    public void DiskUsageModel_IsCritical_ReturnsTrueWhenAbove90Percent()
    {
        // Arrange
        var model = new DiskUsageModel
        {
            TotalSpaceBytes = 100,
            UsedSpaceBytes = 95,
            FreeSpaceBytes = 5,
            UsagePercent = 95.0
        };

        // Assert
        Assert.True(model.IsWarning);
        Assert.True(model.IsCritical);
    }

    [Fact]
    public void DiskUsageModel_IsNormal_ReturnsFalseForWarningAndCritical()
    {
        // Arrange
        var model = new DiskUsageModel
        {
            TotalSpaceBytes = 100,
            UsedSpaceBytes = 50,
            FreeSpaceBytes = 50,
            UsagePercent = 50.0
        };

        // Assert
        Assert.False(model.IsWarning);
        Assert.False(model.IsCritical);
    }

    #endregion

    #region Audit Log Repository Tests

    [Fact]
    public async Task AuditLogRepository_CreateAsync_SuccessfullyCreatesLog()
    {
        // Arrange
        var auditLog = new AuditLog
        {
            UserId = 1,
            Action = "Login",
            Resource = "System",
            IpAddress = "192.168.1.1",
            CreatedAt = DateTime.UtcNow
        };

        var mockInsertable = new Mock<IInsertable<AuditLog>>();
        mockInsertable.Setup(i => i.ExecuteReturnIdentityAsync())
            .ReturnsAsync(1);

        _mockDb.Setup(db => db.Insertable(It.IsAny<AuditLog>()))
            .Returns(mockInsertable.Object);

        var repository = new AuditLogRepository(_mockDb.Object, _mockAuditLogger.Object);

        // Act
        var result = await repository.CreateAsync(auditLog);

        // Assert
        Assert.Equal(1, result);
        _mockDb.Verify(db => db.Insertable(It.IsAny<AuditLog>()), Times.Once);
    }

    [Fact]
    public void AuditLog_CanBeConstructed()
    {
        // Arrange & Act
        var log = new AuditLog
        {
            Id = 1,
            UserId = 1,
            Action = "Login",
            Resource = "System",
            Details = "{\"key\":\"value\"}",
            IpAddress = "192.168.1.1",
            CreatedAt = DateTime.UtcNow
        };

        // Assert
        Assert.Equal(1, log.Id);
        Assert.Equal(1, log.UserId);
        Assert.Equal("Login", log.Action);
        Assert.Equal("System", log.Resource);
        Assert.NotNull(log.Details);
        Assert.Equal("192.168.1.1", log.IpAddress);
    }

    #endregion

    #region View Model Tests

    [Fact]
    public void ErrorAlertModel_DefaultSeverity_IsWarning()
    {
        // Arrange & Act
        var alert = new ErrorAlertModel
        {
            Type = "Test",
            Message = "Test message",
            Timestamp = DateTime.UtcNow
        };

        // Assert
        Assert.Equal("warning", alert.Severity);
    }

    [Fact]
    public void FileDownloadRecordModel_PropertiesSetCorrectly()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var model = new FileDownloadRecordModel
        {
            Id = 1,
            FileName = "test.txt",
            ReportTypeName = "Test Report",
            FileSizeBytes = 1024,
            Status = "downloaded",
            DownloadTime = now,
            ErrorMessage = null
        };

        // Assert
        Assert.Equal(1, model.Id);
        Assert.Equal("test.txt", model.FileName);
        Assert.Equal("Test Report", model.ReportTypeName);
        Assert.Equal(1024, model.FileSizeBytes);
        Assert.Equal("downloaded", model.Status);
        Assert.Equal(now, model.DownloadTime);
        Assert.Null(model.ErrorMessage);
    }

    [Fact]
    public void FileTransferRecordModel_ProgressCalculation()
    {
        // Arrange
        var model = new FileTransferRecordModel
        {
            Id = 1,
            FileName = "test.txt",
            Status = "in_progress",
            Progress = 50,
            TotalSegments = 10,
            SentSegments = 5,
            CreatedAt = DateTime.UtcNow
        };

        // Assert
        Assert.Equal(50, model.Progress);
        Assert.Equal(5, model.SentSegments);
        Assert.Equal(10, model.TotalSegments);
    }

    [Fact]
    public void CommunicationStatusModel_DefaultValues()
    {
        // Arrange & Act
        var model = new CommunicationStatusModel();

        // Assert
        Assert.False(model.MasterIsRunning);
        Assert.Equal(0, model.ActiveConnections);
        Assert.Equal(0, model.TodaySentFrames);
        Assert.Null(model.LastActivityTime);
    }

    [Fact]
    public void SystemStatusModel_AllPropertiesInitializable()
    {
        // Arrange & Act
        var model = new SystemStatusModel
        {
            TotalDownloadedFiles = 100,
            TodayDownloadedFiles = 10,
            TotalTransferTasks = 50,
            PendingTasks = 5,
            InProgressTasks = 3,
            FailedTasks = 2,
            UptimeSeconds = 3600
        };

        // Assert
        Assert.Equal(100, model.TotalDownloadedFiles);
        Assert.Equal(10, model.TodayDownloadedFiles);
        Assert.Equal(50, model.TotalTransferTasks);
        Assert.Equal(5, model.PendingTasks);
        Assert.Equal(3, model.InProgressTasks);
        Assert.Equal(2, model.FailedTasks);
        Assert.Equal(3600, model.UptimeSeconds);
    }

    #endregion
}
