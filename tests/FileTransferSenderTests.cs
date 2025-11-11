using LpsGateway.Data.Models;
using LpsGateway.Lib60870;
using LpsGateway.Services;
using Microsoft.Extensions.Logging;
using Moq;
using SqlSugar;
using System.Text;
using Xunit;

namespace LpsGateway.Tests;

public class FileTransferSenderTests
{
    private readonly Mock<ISqlSugarClient> _mockDb;
    private readonly Mock<IIec102Slave> _mockSlave;
    private readonly Mock<ILogger<FileTransferSender>> _mockLogger;
    private readonly FileTransferSender _sender;

    public FileTransferSenderTests()
    {
        _mockDb = new Mock<ISqlSugarClient>();
        _mockSlave = new Mock<IIec102Slave>();
        _mockLogger = new Mock<ILogger<FileTransferSender>>();

        _sender = new FileTransferSender(_mockDb.Object, _mockSlave.Object, _mockLogger.Object);

        // 注册GBK编码提供者
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    [Fact]
    public void CreateSegments_SmallFile_CreatesCorrectSegments()
    {
        // Arrange
        var fileContent = Encoding.UTF8.GetBytes("Hello, World!"); // 13 bytes
        var filename = "test.txt";

        // Act
        var segments = InvokePrivateMethod<List<byte[]>>(_sender, "CreateSegments", filename, fileContent);

        // Assert
        Assert.Single(segments);
        var segment = segments[0];

        // 检查段的结构：64字节文件名 + 数据
        Assert.Equal(64 + 13, segment.Length);

        // 检查文件名部分（前64字节）
        var filenameBytes = segment.Take(64).ToArray();
        var gbk = Encoding.GetEncoding("GBK");
        var extractedFilename = gbk.GetString(filenameBytes).TrimEnd('\0');
        Assert.Equal(filename, extractedFilename);

        // 检查数据部分
        var data = segment.Skip(64).ToArray();
        Assert.Equal(fileContent, data);
    }

    [Fact]
    public void CreateSegments_LargeFile_CreatesMultipleSegments()
    {
        // Arrange
        var fileContent = new byte[1024]; // 1KB
        for (int i = 0; i < fileContent.Length; i++)
        {
            fileContent[i] = (byte)(i % 256);
        }
        var filename = "large.txt";

        // Act
        var segments = InvokePrivateMethod<List<byte[]>>(_sender, "CreateSegments", filename, fileContent);

        // Assert
        Assert.Equal(2, segments.Count); // 512 + 512 = 1024

        // 每个段应该是 64字节文件名 + 512字节数据
        Assert.Equal(64 + 512, segments[0].Length);
        Assert.Equal(64 + 512, segments[1].Length);

        // 验证数据完整性
        var reconstructedData = new List<byte>();
        foreach (var segment in segments)
        {
            reconstructedData.AddRange(segment.Skip(64));
        }
        Assert.Equal(fileContent, reconstructedData.ToArray());
    }

    [Fact]
    public void CreateSegments_ExactMultiple512_CreatesCorrectSegments()
    {
        // Arrange
        var fileContent = new byte[512]; // 恰好512字节
        var filename = "exact.txt";

        // Act
        var segments = InvokePrivateMethod<List<byte[]>>(_sender, "CreateSegments", filename, fileContent);

        // Assert
        Assert.Single(segments);
        Assert.Equal(64 + 512, segments[0].Length);
    }

    [Fact]
    public void CreateSegments_ChineseFilename_EncodesCorrectly()
    {
        // Arrange
        var fileContent = Encoding.UTF8.GetBytes("测试内容");
        var filename = "测试文件.txt";

        // Act
        var segments = InvokePrivateMethod<List<byte[]>>(_sender, "CreateSegments", filename, fileContent);

        // Assert
        Assert.Single(segments);
        var segment = segments[0];

        // 提取文件名并验证GBK编码
        var filenameBytes = segment.Take(64).ToArray();
        var gbk = Encoding.GetEncoding("GBK");
        var extractedFilename = gbk.GetString(filenameBytes).TrimEnd('\0');
        Assert.Equal(filename, extractedFilename);
    }

    [Fact]
    public void CreateSegments_MaxFile_Creates40Segments()
    {
        // Arrange
        var fileContent = new byte[20480]; // 20KB (max size)
        var filename = "max.txt";

        // Act
        var segments = InvokePrivateMethod<List<byte[]>>(_sender, "CreateSegments", filename, fileContent);

        // Assert
        Assert.Equal(40, segments.Count); // 20480 / 512 = 40
        
        // 验证每个段的大小
        foreach (var segment in segments)
        {
            Assert.Equal(64 + 512, segment.Length);
        }
    }

    // 辅助方法：反射调用私有方法
    private T InvokePrivateMethod<T>(object obj, string methodName, params object[] parameters)
    {
        var method = obj.GetType().GetMethod(methodName,
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (method == null)
        {
            throw new InvalidOperationException($"Method {methodName} not found");
        }

        var result = method.Invoke(obj, parameters);
        return result == null ? default! : (T)result;
    }
}
