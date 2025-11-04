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
        
        var mockLogger = new Mock<Microsoft.Extensions.Logging.ILogger<EFileParser>>();
        var parser = new EFileParser(mockRepo.Object, mockLogger.Object);

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
        
        var mockLogger = new Mock<Microsoft.Extensions.Logging.ILogger<EFileParser>>();
        var parser = new EFileParser(mockRepo.Object, mockLogger.Object);

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
        
        var mockLogger = new Mock<Microsoft.Extensions.Logging.ILogger<EFileParser>>();
        var parser = new EFileParser(mockRepo.Object, mockLogger.Object);
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
        
        var mockLogger = new Mock<Microsoft.Extensions.Logging.ILogger<EFileParser>>();
        var parser = new EFileParser(mockRepo.Object, mockLogger.Object);

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

    [Fact]
    public void ControlField_ParseAndBuild_MasterFrame()
    {
        // Arrange - 主站帧，FCB=1, FCV=1, FC=03
        byte controlByte = 0x73; // 0111 0011

        // Act
        var control = new LpsGateway.Lib60870.ControlField(controlByte);
        var rebuilt = control.Build();

        // Assert
        Assert.True(control.PRM);
        Assert.True(control.FCB);
        Assert.True(control.FCV);
        Assert.Equal(0x03, control.FunctionCode);
        Assert.Equal(controlByte, rebuilt);
    }

    [Fact]
    public void ControlField_ParseAndBuild_SlaveFrame()
    {
        // Arrange - 从站帧，ACD=1, DFC=0, FC=08
        byte controlByte = 0x28; // 0010 1000

        // Act
        var control = new LpsGateway.Lib60870.ControlField(controlByte);
        var rebuilt = control.Build();

        // Assert
        Assert.False(control.PRM);
        Assert.True(control.ACD);
        Assert.False(control.DFC);
        Assert.Equal(0x08, control.FunctionCode);
        Assert.Equal(controlByte, rebuilt);
    }

    [Fact]
    public void Iec102Frame_ParseFixedFrame_Valid()
    {
        // Arrange - 固定长度帧：0x10 C A CS 0x16
        byte[] frameData = new byte[] { 0x10, 0x40, 0x01, 0x41, 0x16 };

        // Act
        var frame = LpsGateway.Lib60870.Iec102Frame.Parse(frameData);

        // Assert
        Assert.True(frame.IsValid);
        Assert.Equal(LpsGateway.Lib60870.FrameType.Fixed, frame.Type);
        Assert.NotNull(frame.ControlField);
        Assert.Equal(0x01, frame.Address);
    }

    [Fact]
    public void Iec102Frame_ParseVariableFrame_Valid()
    {
        // Arrange - 可变长度帧：0x68 L L 0x68 C A DATA CS 0x16
        byte[] userData = new byte[] { 0x90, 0x10, 0x07 };
        byte length = (byte)(userData.Length + 2); // C + A + userData
        
        var frameData = new List<byte> { 0x68, length, length, 0x68, 0x40, 0x01 };
        frameData.AddRange(userData);
        byte checksum = (byte)(0x40 + 0x01 + userData.Sum(b => b));
        frameData.Add(checksum);
        frameData.Add(0x16);

        // Act
        var frame = LpsGateway.Lib60870.Iec102Frame.Parse(frameData.ToArray());

        // Assert
        Assert.True(frame.IsValid);
        Assert.Equal(LpsGateway.Lib60870.FrameType.Variable, frame.Type);
        Assert.Equal(0x01, frame.Address);
        Assert.Equal(userData.Length, frame.UserData.Length);
    }

    [Fact]
    public void Iec102Frame_ParseAckFrame_Valid()
    {
        // Arrange
        byte[] frameData = new byte[] { 0xE5 };

        // Act
        var frame = LpsGateway.Lib60870.Iec102Frame.Parse(frameData);

        // Assert
        Assert.True(frame.IsValid);
        Assert.Equal(LpsGateway.Lib60870.FrameType.SingleByte, frame.Type);
    }

    [Fact]
    public void Iec102Frame_BuildFixedFrame_Valid()
    {
        // Arrange
        var control = LpsGateway.Lib60870.ControlField.CreateMasterFrame(0x09, false, false);

        // Act
        var frameData = LpsGateway.Lib60870.Iec102Frame.BuildFixedFrame(control, 0x01);

        // Assert
        Assert.Equal(5, frameData.Length);
        Assert.Equal(0x10, frameData[0]);
        Assert.Equal(0x16, frameData[4]);
        
        // 验证可以解析回来
        var parsed = LpsGateway.Lib60870.Iec102Frame.Parse(frameData);
        Assert.True(parsed.IsValid);
    }
}
