using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using SqlSugar;
using LpsGateway.Data;
using LpsGateway.Data.Models;
using LpsGateway.Services;

namespace LpsGateway.Tests;

/// <summary>
/// M4-additional 功能测试
/// </summary>
public class M4AdditionalTests
{
    private readonly Mock<ISqlSugarClient> _mockDb;
    private readonly Mock<ILogger<FileRecordRepository>> _mockRepoLogger;
    private readonly Mock<ILogger<FileTransferInitializer>> _mockInitLogger;
    
    public M4AdditionalTests()
    {
        _mockDb = new Mock<ISqlSugarClient>();
        _mockRepoLogger = new Mock<ILogger<FileRecordRepository>>();
        _mockInitLogger = new Mock<ILogger<FileTransferInitializer>>();
    }
    
    [Fact]
    public void FileTransferInitializer_IsClass1Data_CorrectlyIdentifiesClass1Types()
    {
        // Arrange
        var mockFileRecordRepo = new Mock<IFileRecordRepository>();
        var mockReportTypeRepo = new Mock<IReportTypeRepository>();
        var initializer = new FileTransferInitializer(
            mockFileRecordRepo.Object,
            mockReportTypeRepo.Object,
            _mockDb.Object,
            _mockInitLogger.Object);
        
        // Act & Assert - Class 1 data types
        Assert.True(initializer.IsClass1Data("EFJ_FIVE_WIND_TOWER"));
        Assert.True(initializer.IsClass1Data("EFJ_DQ_RESULT_UP"));
        Assert.True(initializer.IsClass1Data("EFJ_CDQ_RESULT_UP"));
        Assert.True(initializer.IsClass1Data("EFJ_NWP_UP"));
        Assert.True(initializer.IsClass1Data("EGF_FIVE_GF_QXZ"));
        
        // Act & Assert - Class 2 data types
        Assert.False(initializer.IsClass1Data("EFJ_FARM_INFO"));
        Assert.False(initializer.IsClass1Data("EFJ_FARM_UNIT_INFO"));
        Assert.False(initializer.IsClass1Data("EGF_REALTIME"));
        Assert.False(initializer.IsClass1Data("UNKNOWN_TYPE"));
    }
    
    [Theory]
    [InlineData(0x9A, true)]   // EFJ_FIVE_WIND_TOWER
    [InlineData(0x9B, true)]   // EFJ_DQ_RESULT_UP
    [InlineData(0x9C, true)]   // EFJ_CDQ_RESULT_UP
    [InlineData(0x9D, true)]   // EFJ_NWP_UP
    [InlineData(0xA1, true)]   // EGF_FIVE_GF_QXZ
    [InlineData(0x95, false)]  // EFJ_FARM_INFO (Class 2)
    [InlineData(0x96, false)]  // EFJ_FARM_UNIT_INFO (Class 2)
    [InlineData(0xA8, false)]  // EGF_REALTIME (Class 2)
    public void FileTransferInitializer_IsClass1DataByTypeId_CorrectlyIdentifiesTypes(byte typeId, bool expected)
    {
        // Arrange
        var mockFileRecordRepo = new Mock<IFileRecordRepository>();
        var mockReportTypeRepo = new Mock<IReportTypeRepository>();
        var initializer = new FileTransferInitializer(
            mockFileRecordRepo.Object,
            mockReportTypeRepo.Object,
            _mockDb.Object,
            _mockInitLogger.Object);
        
        // Act
        var result = initializer.IsClass1DataByTypeId(typeId);
        
        // Assert
        Assert.Equal(expected, result);
    }
    
    [Fact]
    public async Task FileRecordRepository_CreateAsync_SetsTimestamps()
    {
        // Arrange
        var mockQueryable = new Mock<ISugarQueryable<FileRecord>>();
        var mockInsertable = new Mock<IInsertable<FileRecord>>();
        
        _mockDb.Setup(db => db.Insertable(It.IsAny<FileRecord>()))
            .Returns(mockInsertable.Object);
        
        mockInsertable.Setup(i => i.ExecuteReturnIdentityAsync())
            .ReturnsAsync(1);
        
        var repository = new FileRecordRepository(_mockDb.Object, _mockRepoLogger.Object);
        
        var fileRecord = new FileRecord
        {
            ReportTypeId = 1,
            OriginalFilename = "test.txt",
            StoragePath = "/path/to/test.txt",
            FileSize = 1024,
            Status = "downloaded"
        };
        
        // Act
        var id = await repository.CreateAsync(fileRecord);
        
        // Assert
        Assert.True(id > 0);
        Assert.True(fileRecord.CreatedAt != default);
        Assert.True(fileRecord.UpdatedAt != default);
    }
    
    [Fact]
    public async Task FileRecordRepository_GetByStatusAsync_ReturnsMatchingRecords()
    {
        // Arrange
        var testRecords = new List<FileRecord>
        {
            new FileRecord { Id = 1, Status = "downloaded", OriginalFilename = "file1.txt" },
            new FileRecord { Id = 2, Status = "downloaded", OriginalFilename = "file2.txt" },
            new FileRecord { Id = 3, Status = "processing", OriginalFilename = "file3.txt" }
        };
        
        var mockQueryable = new Mock<ISugarQueryable<FileRecord>>();
        
        _mockDb.Setup(db => db.Queryable<FileRecord>())
            .Returns(mockQueryable.Object);
        
        mockQueryable.Setup(q => q.Where(It.IsAny<System.Linq.Expressions.Expression<Func<FileRecord, bool>>>()))
            .Returns(mockQueryable.Object);
        
        mockQueryable.Setup(q => q.OrderBy(It.IsAny<System.Linq.Expressions.Expression<Func<FileRecord, object>>>(), OrderByType.Desc))
            .Returns(mockQueryable.Object);
        
        mockQueryable.Setup(q => q.ToListAsync())
            .ReturnsAsync(testRecords.Where(r => r.Status == "downloaded").ToList());
        
        var repository = new FileRecordRepository(_mockDb.Object, _mockRepoLogger.Object);
        
        // Act
        var results = await repository.GetByStatusAsync("downloaded");
        
        // Assert
        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.Equal("downloaded", r.Status));
    }
    
    [Fact]
    public async Task FileRecordRepository_GetDownloadedFilesForTransferAsync_ReturnsOnlyDownloadedFiles()
    {
        // Arrange
        var testRecords = new List<FileRecord>
        {
            new FileRecord { Id = 1, Status = "downloaded", ReportTypeId = 1 },
            new FileRecord { Id = 2, Status = "downloaded", ReportTypeId = 2 },
            new FileRecord { Id = 3, Status = "sent", ReportTypeId = 1 }
        };
        
        var mockQueryable = new Mock<ISugarQueryable<FileRecord>>();
        
        _mockDb.Setup(db => db.Queryable<FileRecord>())
            .Returns(mockQueryable.Object);
        
        mockQueryable.Setup(q => q.Where(It.IsAny<System.Linq.Expressions.Expression<Func<FileRecord, bool>>>()))
            .Returns(mockQueryable.Object);
        
        mockQueryable.Setup(q => q.OrderBy(It.IsAny<System.Linq.Expressions.Expression<Func<FileRecord, object>>>(), OrderByType.Asc))
            .Returns(mockQueryable.Object);
        
        mockQueryable.Setup(q => q.ToListAsync())
            .ReturnsAsync(testRecords.Where(r => r.Status == "downloaded").ToList());
        
        var repository = new FileRecordRepository(_mockDb.Object, _mockRepoLogger.Object);
        
        // Act
        var results = await repository.GetDownloadedFilesForTransferAsync();
        
        // Assert
        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.Equal("downloaded", r.Status));
    }
    
    [Fact]
    public void FileRecord_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var fileRecord = new FileRecord();
        
        // Assert
        Assert.Equal(string.Empty, fileRecord.OriginalFilename);
        Assert.Equal(string.Empty, fileRecord.StoragePath);
        Assert.Equal("downloaded", fileRecord.Status);
        Assert.Equal(0, fileRecord.FileSize);
    }
    
    [Fact]
    public void FileTransferTask_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var task = new FileTransferTask();
        
        // Assert
        Assert.Equal("pending", task.Status);
        Assert.Equal(0, task.Progress);
        Assert.Equal(0, task.SentSegments);
        Assert.Null(task.TotalSegments);
    }
}
