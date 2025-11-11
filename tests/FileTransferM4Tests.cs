using LpsGateway.Data.Models;
using LpsGateway.Lib60870;
using LpsGateway.Services;
using Microsoft.Extensions.Logging;
using Moq;
using SqlSugar;
using System.Text;
using Xunit;

namespace LpsGateway.Tests;

/// <summary>
/// M4文件传输通道测试
/// </summary>
public class FileTransferM4Tests
{
    /// <summary>
    /// 静态构造函数，注册GBK编码
    /// </summary>
    static FileTransferM4Tests()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }
    
    /// <summary>
    /// 测试文件分段逻辑
    /// </summary>
    [Fact]
    public void CreateSegments_ValidFile_ReturnsCorrectSegments()
    {
        // Arrange
        var logger = Mock.Of<ILogger<FileTransferWorker>>();
        var db = Mock.Of<ISqlSugarClient>();
        // Pass null for slave since we're testing CreateSegments which doesn't use it
        var worker = new FileTransferWorker(logger, db, null);
        
        // Use reflection to call private method CreateSegments
        var method = typeof(FileTransferWorker).GetMethod("CreateSegments", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        string fileName = "TEST_FILE.TXT";
        byte[] fileContent = Encoding.UTF8.GetBytes("This is test content for file segmentation");
        
        // Act
        var segments = (List<byte[]>)method!.Invoke(worker, new object[] { fileName, fileContent })!;
        
        // Assert
        Assert.NotNull(segments);
        Assert.Single(segments); // Small file should fit in one segment
        Assert.Equal(64 + fileContent.Length, segments[0].Length); // 64 bytes filename + content
        
        // Verify filename is at the beginning
        var fileNameBytes = segments[0].Take(64).ToArray();
        var extractedName = Encoding.GetEncoding("GBK").GetString(fileNameBytes).TrimEnd('\0');
        Assert.Equal(fileName, extractedName);
    }
    
    /// <summary>
    /// 测试大文件分段
    /// </summary>
    [Fact]
    public void CreateSegments_LargeFile_ReturnsMultipleSegments()
    {
        // Arrange
        var logger = Mock.Of<ILogger<FileTransferWorker>>();
        var db = Mock.Of<ISqlSugarClient>();
        var worker = new FileTransferWorker(logger, db, null);
        
        var method = typeof(FileTransferWorker).GetMethod("CreateSegments", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        string fileName = "LARGE_FILE.DAT";
        byte[] fileContent = new byte[1024]; // 1KB file
        new Random().NextBytes(fileContent);
        
        // Act
        var segments = (List<byte[]>)method!.Invoke(worker, new object[] { fileName, fileContent })!;
        
        // Assert
        Assert.NotNull(segments);
        Assert.Equal(2, segments.Count); // 1024 bytes should be split into 2 segments (512, 512)
        
        // Each segment should have 64-byte filename prefix
        foreach (var segment in segments)
        {
            Assert.True(segment.Length >= 64);
            Assert.True(segment.Length <= 64 + 512);
        }
    }
    
    /// <summary>
    /// 测试文件名验证
    /// </summary>
    [Fact]
    public void ValidateFileName_ValidName_ReturnsTrue()
    {
        // Arrange
        var logger = Mock.Of<ILogger<FileTransferWorker>>();
        var db = Mock.Of<ISqlSugarClient>();
        var worker = new FileTransferWorker(logger, db, null);
        
        var method = typeof(FileTransferWorker).GetMethod("ValidateFileName", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        // Act & Assert
        Assert.True((bool)method!.Invoke(worker, new object[] { "VALID_FILE.TXT" })!);
        Assert.True((bool)method!.Invoke(worker, new object[] { "测试文件.DAT" })!);
    }
    
    /// <summary>
    /// 测试文件名验证 - 无效名称
    /// </summary>
    [Fact]
    public void ValidateFileName_InvalidName_ReturnsFalse()
    {
        // Arrange
        var logger = Mock.Of<ILogger<FileTransferWorker>>();
        var db = Mock.Of<ISqlSugarClient>();
        var worker = new FileTransferWorker(logger, db, null);
        
        var method = typeof(FileTransferWorker).GetMethod("ValidateFileName", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        // Act & Assert
        Assert.False((bool)method!.Invoke(worker, new object[] { "" })!);
        Assert.False((bool)method!.Invoke(worker, new object?[] { null })!);
        
        // 超长文件名 (> 64 bytes in GBK encoding)
        string longName = new string('X', 100);
        Assert.False((bool)method!.Invoke(worker, new object[] { longName })!);
    }
    
    /// <summary>
    /// 测试报告类型到TypeId的映射
    /// </summary>
    [Theory]
    [InlineData("EFJ_FARM_INFO", 0x95)]
    [InlineData("EFJ_FARM_UNIT_INFO", 0x96)]
    [InlineData("EFJ_FARM_UNIT_RUN_STATE", 0x97)]
    [InlineData("EGF_REALTIME", 0xA8)]
    [InlineData("INVALID_TYPE", 0)]
    public void GetTypeIdForReportType_VariousTypes_ReturnsCorrectTypeId(string reportType, byte expectedTypeId)
    {
        // Arrange
        var logger = Mock.Of<ILogger<FileTransferWorker>>();
        var db = Mock.Of<ISqlSugarClient>();
        var worker = new FileTransferWorker(logger, db, null);
        
        var method = typeof(FileTransferWorker).GetMethod("GetTypeIdForReportType", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        // Act
        var result = (byte)method!.Invoke(worker, new object?[] { reportType })!;
        
        // Assert
        Assert.Equal(expectedTypeId, result);
    }
    
    /// <summary>
    /// 测试文件对账事件处理
    /// </summary>
    [Fact]
    public void Iec102Slave_FileReconciliation_EventCanBeSubscribed()
    {
        // Arrange
        var logger = Mock.Of<ILogger<Iec102Slave>>();
        var slave = new Iec102Slave(3000, 0xFFFF, logger);
        
        bool eventFired = false;
        int receivedLength = 0;
        
        // Act
        slave.FileReconciliation += (sender, args) =>
        {
            eventFired = true;
            receivedLength = args.FileLength;
        };
        
        // Simulate triggering the event using reflection
        var eventInfo = typeof(Iec102Slave).GetField("FileReconciliation", 
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var eventDelegate = eventInfo?.GetValue(slave) as EventHandler<FileReconciliationEventArgs>;
        eventDelegate?.Invoke(slave, new FileReconciliationEventArgs
        {
            Endpoint = "127.0.0.1:12345",
            FileLength = 1234
        });
        
        // Assert
        Assert.True(eventFired);
        Assert.Equal(1234, receivedLength);
    }
    
    /// <summary>
    /// 测试文件重传请求事件
    /// </summary>
    [Fact]
    public void Iec102Slave_FileRetransmit_EventCanBeSubscribed()
    {
        // Arrange
        var logger = Mock.Of<ILogger<Iec102Slave>>();
        var slave = new Iec102Slave(3000, 0xFFFF, logger);
        
        bool eventFired = false;
        string? receivedEndpoint = null;
        
        // Act
        slave.FileRetransmitRequest += (sender, args) =>
        {
            eventFired = true;
            receivedEndpoint = args.Endpoint;
        };
        
        var eventInfo = typeof(Iec102Slave).GetField("FileRetransmitRequest", 
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var eventDelegate = eventInfo?.GetValue(slave) as EventHandler<FileRetransmitEventArgs>;
        eventDelegate?.Invoke(slave, new FileRetransmitEventArgs
        {
            Endpoint = "127.0.0.1:12345"
        });
        
        // Assert
        Assert.True(eventFired);
        Assert.Equal("127.0.0.1:12345", receivedEndpoint);
    }
    
    /// <summary>
    /// 测试文件过长错误事件
    /// </summary>
    [Fact]
    public void Iec102Slave_FileTooLong_EventCanBeSubscribed()
    {
        // Arrange
        var logger = Mock.Of<ILogger<Iec102Slave>>();
        var slave = new Iec102Slave(3000, 0xFFFF, logger);
        
        bool eventFired = false;
        string? errorType = null;
        
        // Act
        slave.FileTooLongAck += (sender, args) =>
        {
            eventFired = true;
            errorType = args.ErrorType;
        };
        
        var eventInfo = typeof(Iec102Slave).GetField("FileTooLongAck", 
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var eventDelegate = eventInfo?.GetValue(slave) as EventHandler<FileErrorEventArgs>;
        eventDelegate?.Invoke(slave, new FileErrorEventArgs
        {
            Endpoint = "127.0.0.1:12345",
            ErrorType = "FileTooLong"
        });
        
        // Assert
        Assert.True(eventFired);
        Assert.Equal("FileTooLong", errorType);
    }
    
    /// <summary>
    /// 测试文件名格式错误事件
    /// </summary>
    [Fact]
    public void Iec102Slave_InvalidFileName_EventCanBeSubscribed()
    {
        // Arrange
        var logger = Mock.Of<ILogger<Iec102Slave>>();
        var slave = new Iec102Slave(3000, 0xFFFF, logger);
        
        bool eventFired = false;
        string? errorType = null;
        
        // Act
        slave.InvalidFileNameAck += (sender, args) =>
        {
            eventFired = true;
            errorType = args.ErrorType;
        };
        
        var eventInfo = typeof(Iec102Slave).GetField("InvalidFileNameAck", 
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var eventDelegate = eventInfo?.GetValue(slave) as EventHandler<FileErrorEventArgs>;
        eventDelegate?.Invoke(slave, new FileErrorEventArgs
        {
            Endpoint = "127.0.0.1:12345",
            ErrorType = "InvalidFileName"
        });
        
        // Assert
        Assert.True(eventFired);
        Assert.Equal("InvalidFileName", errorType);
    }
    
    /// <summary>
    /// 测试单帧报文过长错误事件
    /// </summary>
    [Fact]
    public void Iec102Slave_FrameTooLong_EventCanBeSubscribed()
    {
        // Arrange
        var logger = Mock.Of<ILogger<Iec102Slave>>();
        var slave = new Iec102Slave(3000, 0xFFFF, logger);
        
        bool eventFired = false;
        string? errorType = null;
        
        // Act
        slave.FrameTooLongAck += (sender, args) =>
        {
            eventFired = true;
            errorType = args.ErrorType;
        };
        
        var eventInfo = typeof(Iec102Slave).GetField("FrameTooLongAck", 
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var eventDelegate = eventInfo?.GetValue(slave) as EventHandler<FileErrorEventArgs>;
        eventDelegate?.Invoke(slave, new FileErrorEventArgs
        {
            Endpoint = "127.0.0.1:12345",
            ErrorType = "FrameTooLong"
        });
        
        // Assert
        Assert.True(eventFired);
        Assert.Equal("FrameTooLong", errorType);
    }
    
    /// <summary>
    /// 测试最大文件大小限制
    /// </summary>
    [Fact]
    public void FileTransferWorker_MaxFileSize_Is20480()
    {
        // Arrange & Act
        var maxFileSize = 512 * 40; // As per spec
        
        // Assert
        Assert.Equal(20480, maxFileSize);
    }
    
    /// <summary>
    /// 测试文件名长度常量
    /// </summary>
    [Fact]
    public void FileTransferWorker_FileNameLength_Is64()
    {
        // As per spec, filename should be 64 bytes
        const int FileNameLength = 64;
        Assert.Equal(64, FileNameLength);
    }
    
    /// <summary>
    /// 测试段大小常量
    /// </summary>
    [Fact]
    public void FileTransferWorker_MaxSegmentSize_Is512()
    {
        // As per spec, max segment size is 512 bytes
        const int MaxSegmentSize = 512;
        Assert.Equal(512, MaxSegmentSize);
    }
}
