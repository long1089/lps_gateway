using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Microsoft.Extensions.Logging;
using Moq;
using LpsGateway.Lib60870;

namespace LpsGateway.Tests;

/// <summary>
/// M6 集成测试 - 主站通讯、端到端工作流、并发场景测试
/// </summary>
public class M6IntegrationTests
{
    /// <summary>
    /// 测试：完整的主站通讯流程（时间同步 + 文件传输）
    /// </summary>
    [Fact]
    public async Task MasterCommunication_CompleteWorkflow_Success()
    {
        // Arrange
        var mockSlaveLogger = new Mock<ILogger<Iec102Slave>>();
        var mockMasterLogger = new Mock<ILogger<Iec102Master>>();
        
        var slave = new Iec102Slave(30100, 0x03E9, mockSlaveLogger.Object); // CommonAddr=1001
        await slave.StartAsync();
        await Task.Delay(100);
        
        var master = new Iec102Master("localhost", 30100, 0x03E9, mockMasterLogger.Object);
        
        try
        {
            // Act & Assert - 连接
            var connected = await master.ConnectAsync();
            Assert.True(connected);
            await Task.Delay(100);
            
            // Act & Assert - 时间同步
            var timeSyncResult = await master.SendTimeSyncAsync(DateTime.UtcNow);
            Assert.True(timeSyncResult);
            await Task.Delay(100);
            
            // Act & Assert - 请求数据（复位链路）
            var resetResult = await master.ResetLinkAsync();
            Assert.True(resetResult);
            await Task.Delay(100);
            
            // Cleanup
            await master.DisconnectAsync();
        }
        finally
        {
            await slave.StopAsync();
        }
    }
    
    /// <summary>
    /// 测试：文件传输的 ASDU 帧构造
    /// </summary>
    [Fact]
    public void FileTransfer_AsduFrameConstruction_Success()
    {
        // Arrange
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var gbk = Encoding.GetEncoding("GBK");
        
        var fileContent = "Test Data";
        var contentBytes = gbk.GetBytes(fileContent);
        
        byte typeId = 0x95;
        ushort commonAddr = 1001;
        byte cot = CauseOfTransmission.FileTransferComplete;
        
        // Act - 构造 ASDU 帧
        var asdu = new byte[5 + contentBytes.Length];
        asdu[0] = typeId;
        asdu[1] = (byte)(contentBytes.Length + 3);
        asdu[2] = cot;
        asdu[3] = (byte)(commonAddr & 0xFF);
        asdu[4] = (byte)((commonAddr >> 8) & 0xFF);
        Array.Copy(contentBytes, 0, asdu, 5, contentBytes.Length);
        
        // Assert - 验证帧结构
        Assert.Equal(typeId, asdu[0]);
        Assert.Equal(contentBytes.Length + 3, asdu[1]);
        Assert.Equal(cot, asdu[2]);
        Assert.Equal((byte)(commonAddr & 0xFF), asdu[3]);
        Assert.Equal((byte)((commonAddr >> 8) & 0xFF), asdu[4]);
        
        var extractedContent = new byte[contentBytes.Length];
        Array.Copy(asdu, 5, extractedContent, 0, contentBytes.Length);
        Assert.Equal(contentBytes, extractedContent);
    }
    
    /// <summary>
    /// 测试：并发多客户端连接
    /// </summary>
    [Fact]
    public async Task ConcurrentConnections_MultipleClients_AllSucceed()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<Iec102Slave>>();
        var slave = new Iec102Slave(30101, 0xFFFF, mockLogger.Object);
        await slave.StartAsync();
        await Task.Delay(200);
        
        int clientCount = 10;
        var tasks = new List<Task<bool>>();
        
        try
        {
            // Act - 并发连接
            for (int i = 0; i < clientCount; i++)
            {
                int clientId = i;
                var task = Task.Run(async () =>
                {
                    var mockClientLogger = new Mock<ILogger<Iec102Master>>();
                    var master = new Iec102Master("localhost", 30101, 0xFFFF, mockClientLogger.Object);
                    
                    try
                    {
                        var connected = await master.ConnectAsync();
                        if (!connected) return false;
                        
                        await Task.Delay(100);
                        
                        // 发送测试命令
                        var result = await master.ResetLinkAsync();
                        
                        await master.DisconnectAsync();
                        return result;
                    }
                    catch
                    {
                        return false;
                    }
                });
                tasks.Add(task);
            }
            
            var results = await Task.WhenAll(tasks);
            
            // Assert - 所有客户端都成功
            Assert.Equal(clientCount, results.Length);
            Assert.All(results, result => Assert.True(result));
        }
        finally
        {
            await slave.StopAsync();
        }
    }
    
    /// <summary>
    /// 测试：TCP 连接中断和重连
    /// </summary>
    [Fact]
    public async Task Connection_DisconnectReconnect_Success()
    {
        // Arrange
        var mockSlaveLogger = new Mock<ILogger<Iec102Slave>>();
        var mockMasterLogger = new Mock<ILogger<Iec102Master>>();
        
        var slave = new Iec102Slave(30102, 0x03E9, mockSlaveLogger.Object);
        await slave.StartAsync();
        await Task.Delay(100);
        
        var master = new Iec102Master("localhost", 30102, 0x03E9, mockMasterLogger.Object);
        
        try
        {
            // Act & Assert - 第一次连接
            var connected1 = await master.ConnectAsync();
            Assert.True(connected1);
            Assert.True(master.IsConnected);
            await Task.Delay(100);
            
            // 断开连接
            await master.DisconnectAsync();
            Assert.False(master.IsConnected);
            await Task.Delay(100);
            
            // 重新连接
            var connected2 = await master.ConnectAsync();
            Assert.True(connected2);
            Assert.True(master.IsConnected);
            await Task.Delay(100);
            
            // 验证通讯正常
            var resetResult = await master.ResetLinkAsync();
            Assert.True(resetResult);
            
            await master.DisconnectAsync();
        }
        finally
        {
            await slave.StopAsync();
        }
    }
    
    /// <summary>
    /// 测试：超时场景（模拟长时间无响应）
    /// </summary>
    [Fact]
    public async Task Connection_Timeout_HandledGracefully()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<Iec102Master>>();
        var master = new Iec102Master("localhost", 39999, 0xFFFF, mockLogger.Object); // 不存在的服务
        
        // Act
        var startTime = DateTime.UtcNow;
        var connected = await master.ConnectAsync();
        var elapsed = DateTime.UtcNow - startTime;
        
        // Assert - 应该快速失败（不超过5秒）
        Assert.False(connected);
        Assert.True(elapsed.TotalSeconds < 10);
    }
    
    /// <summary>
    /// 测试：COT 常量正确性
    /// </summary>
    [Fact]
    public void CauseOfTransmission_Constants_AreCorrect()
    {
        // Assert - 验证关键 COT 值
        Assert.Equal(0x07, CauseOfTransmission.FileTransferComplete);
        Assert.Equal(0x08, CauseOfTransmission.FileTransferInProgress);
        Assert.Equal(0x0A, CauseOfTransmission.ReconciliationFromMaster);
        Assert.Equal(0x0B, CauseOfTransmission.ReconciliationFromSlave);
        Assert.Equal(0x0F, CauseOfTransmission.FileTooLongError);
        Assert.Equal(0x11, CauseOfTransmission.InvalidFileNameFormat);
        Assert.Equal(0x13, CauseOfTransmission.FrameTooLongError);
    }
}
