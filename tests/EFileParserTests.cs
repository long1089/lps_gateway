using System.Text;
using Xunit;
using Moq;
using LpsGateway.Services;
using LpsGateway.Data;

namespace LpsGateway.Tests;

public class EFileParserTests
{
    [Fact]
    public async Task ParseAndSaveAsync_WithValidGBKContent_ParsesCorrectly()
    {
        // Arrange
        var mockRepo = new Mock<IEFileRepository>();
        mockRepo.Setup(r => r.IsFileProcessedAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(false);
        
        var parser = new EFileParser(mockRepo.Object);

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var gbk = Encoding.GetEncoding("GBK");
        
        var testContent = @"<table> STATION_INFO
@ID	Name	Capacity
#S001	测试站点	100.5
#S002	Another Station	200.0
<table> DEVICE_INFO
@ID	DeviceType	Status
#D001	Transformer	Active
#D002	Breaker	-99";

        var bytes = gbk.GetBytes(testContent);
        var stream = new MemoryStream(bytes);

        // Act
        await parser.ParseAndSaveAsync(stream, "1001", "TYPE_90", "test.txt");

        // Assert
        mockRepo.Verify(r => r.UpsertInfoTableAsync("STATION_INFO", It.IsAny<Dictionary<string, object?>>(), "ID"), Times.Exactly(2));
        mockRepo.Verify(r => r.UpsertInfoTableAsync("DEVICE_INFO", It.IsAny<Dictionary<string, object?>>(), "ID"), Times.Exactly(2));
        mockRepo.Verify(r => r.MarkFileProcessedAsync("1001", "TYPE_90", "test.txt", It.IsAny<int>(), "SUCCESS", null), Times.Once);
    }

    [Fact]
    public async Task ParseAndSaveAsync_WithNullValues_HandlesCorrectly()
    {
        // Arrange
        var mockRepo = new Mock<IEFileRepository>();
        mockRepo.Setup(r => r.IsFileProcessedAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(false);

        Dictionary<string, object?>? capturedRecord = null;
        mockRepo.Setup(r => r.UpsertInfoTableAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object?>>(), It.IsAny<string>()))
            .Callback<string, Dictionary<string, object?>, string>((table, record, key) => 
            {
                if (capturedRecord == null)
                {
                    capturedRecord = new Dictionary<string, object?>(record);
                }
            })
            .Returns(Task.CompletedTask);
        
        var parser = new EFileParser(mockRepo.Object);

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var gbk = Encoding.GetEncoding("GBK");
        
        var testContent = @"<table> TEST_INFO
@ID	Value
#T001	-99";

        var bytes = gbk.GetBytes(testContent);
        var stream = new MemoryStream(bytes);

        // Act
        await parser.ParseAndSaveAsync(stream, "1001", "TYPE_91", "test2.txt");

        // Assert
        Assert.NotNull(capturedRecord);
        Assert.True(capturedRecord.ContainsKey("Value"));
        Assert.Null(capturedRecord["Value"]);
    }

    [Fact]
    public async Task ParseAndSaveAsync_WithAlreadyProcessedFile_SkipsProcessing()
    {
        // Arrange
        var mockRepo = new Mock<IEFileRepository>();
        mockRepo.Setup(r => r.IsFileProcessedAsync("1001", "TYPE_90", "test.txt"))
            .ReturnsAsync(true);
        
        var parser = new EFileParser(mockRepo.Object);
        var stream = new MemoryStream(Encoding.UTF8.GetBytes("dummy content"));

        // Act
        await parser.ParseAndSaveAsync(stream, "1001", "TYPE_90", "test.txt");

        // Assert
        mockRepo.Verify(r => r.UpsertInfoTableAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object?>>(), It.IsAny<string>()), Times.Never);
        mockRepo.Verify(r => r.InsertRecordsAsync(It.IsAny<string>(), It.IsAny<List<Dictionary<string, object?>>>()), Times.Never);
    }

    [Fact]
    public async Task ParseAndSaveAsync_WithNonInfoTable_InsertsRecords()
    {
        // Arrange
        var mockRepo = new Mock<IEFileRepository>();
        mockRepo.Setup(r => r.IsFileProcessedAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(false);
        
        var parser = new EFileParser(mockRepo.Object);

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var gbk = Encoding.GetEncoding("GBK");
        
        var testContent = @"<table> ENERGY_DATA
@StationId	ActivePower
#S001	150.5
#S002	200.0";

        var bytes = gbk.GetBytes(testContent);
        var stream = new MemoryStream(bytes);

        // Act
        await parser.ParseAndSaveAsync(stream, "1001", "TYPE_92", "test3.txt");

        // Assert
        mockRepo.Verify(r => r.InsertRecordsAsync("ENERGY_DATA", It.Is<List<Dictionary<string, object?>>>(l => l.Count == 2)), Times.Once);
        mockRepo.Verify(r => r.UpsertInfoTableAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object?>>(), It.IsAny<string>()), Times.Never);
    }
}
