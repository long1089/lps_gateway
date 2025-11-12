using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using Microsoft.Extensions.Logging;
using Moq;
using LpsGateway.Lib60870;

namespace LpsGateway.Tests;

/// <summary>
/// M6 容灾测试 - TCP会话恢复、异常处理测试
/// </summary>
public class M6DisasterRecoveryTests
{
    private readonly ITestOutputHelper _output;
    
    public M6DisasterRecoveryTests(ITestOutputHelper output)
    {
        _output = output;
    }
    
    /// <summary>
    /// 容灾测试：TCP 会话中断和恢复
    /// </summary>
    [Fact]
    public async Task DisasterRecovery_TcpSessionInterruption_Recovers()
    {
        // Arrange
        var mockSlaveLogger = new Mock<ILogger<Iec102Slave>>();
        var mockMasterLogger = new Mock<ILogger<Iec102Master>>();
        
        var slave = new Iec102Slave(30300, 0x03E9, mockSlaveLogger.Object);
        await slave.StartAsync();
        await Task.Delay(100);
        
        var master = new Iec102Master("localhost", 30300, 0x03E9, mockMasterLogger.Object);
        
        try
        {
            // Act & Assert - 第一次连接和操作
            _output.WriteLine("步骤 1: 建立初始连接");
            var connected1 = await master.ConnectAsync();
            Assert.True(connected1);
            await Task.Delay(100);
            
            var reset1 = await master.ResetLinkAsync();
            Assert.True(reset1);
            _output.WriteLine("步骤 1: ✓ 初始连接成功");
            
            // 模拟中断：突然断开
            _output.WriteLine("步骤 2: 模拟连接中断");
            await master.DisconnectAsync();
            await Task.Delay(200);
            _output.WriteLine("步骤 2: ✓ 连接已中断");
            
            // 恢复连接
            _output.WriteLine("步骤 3: 尝试重新连接");
            var connected2 = await master.ConnectAsync();
            Assert.True(connected2);
            await Task.Delay(100);
            
            // 验证恢复后可正常工作
            var reset2 = await master.ResetLinkAsync();
            Assert.True(reset2);
            _output.WriteLine("步骤 3: ✓ 重连成功，通讯恢复");
            
            await master.DisconnectAsync();
        }
        finally
        {
            await slave.StopAsync();
        }
    }
    
    /// <summary>
    /// 容灾测试：服务器重启后客户端重连
    /// </summary>
    [Fact]
    public async Task DisasterRecovery_ServerRestart_ClientReconnects()
    {
        // Arrange
        var mockSlaveLogger = new Mock<ILogger<Iec102Slave>>();
        var mockMasterLogger = new Mock<ILogger<Iec102Master>>();
        
        _output.WriteLine("步骤 1: 启动服务器");
        var slave = new Iec102Slave(30301, 0x03E9, mockSlaveLogger.Object);
        await slave.StartAsync();
        await Task.Delay(200);
        
        var master = new Iec102Master("localhost", 30301, 0x03E9, mockMasterLogger.Object);
        
        try
        {
            // 第一次连接
            _output.WriteLine("步骤 2: 客户端连接");
            var connected1 = await master.ConnectAsync();
            Assert.True(connected1);
            await Task.Delay(100);
            _output.WriteLine("步骤 2: ✓ 连接成功");
            
            // 模拟服务器重启
            _output.WriteLine("步骤 3: 模拟服务器重启");
            await slave.StopAsync();
            await Task.Delay(500); // 等待端口释放
            
            // 客户端此时可能仍然显示连接（因为TCP还没超时）
            // 但下一次操作应该会失败
            _output.WriteLine("步骤 3: ✓ 服务器已停止");
            
            // 尝试操作会失败
            try
            {
                await master.ResetLinkAsync();
                _output.WriteLine("步骤 3: 注意 - 操作未抛出异常（TCP 可能还未超时）");
            }
            catch
            {
                _output.WriteLine("步骤 3: ✓ 操作失败（符合预期）");
            }
            
            // 重启服务器
            _output.WriteLine("步骤 4: 重新启动服务器");
            slave = new Iec102Slave(30301, 0x03E9, mockSlaveLogger.Object);
            await slave.StartAsync();
            await Task.Delay(200);
            _output.WriteLine("步骤 4: ✓ 服务器已重启");
            
            // 客户端重连
            _output.WriteLine("步骤 5: 客户端重新连接");
            await master.DisconnectAsync(); // 确保旧连接已关闭
            var connected2 = await master.ConnectAsync();
            Assert.True(connected2);
            await Task.Delay(100);
            
            // 验证通讯恢复
            var reset = await master.ResetLinkAsync();
            Assert.True(reset);
            _output.WriteLine("步骤 5: ✓ 重连成功，通讯正常");
            
            await master.DisconnectAsync();
        }
        finally
        {
            await slave.StopAsync();
        }
    }
    
    /// <summary>
    /// 容灾测试：多次连接失败后成功
    /// </summary>
    [Fact]
    public async Task DisasterRecovery_MultipleFailedAttemptsBeforeSuccess_Works()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<Iec102Master>>();
        var master = new Iec102Master("localhost", 30302, 0xFFFF, mockLogger.Object);
        
        _output.WriteLine("测试：多次连接失败场景");
        
        // Act - 尝试连接不存在的服务（会失败）
        _output.WriteLine("步骤 1-3: 尝试连接不存在的服务（预期失败）");
        for (int i = 0; i < 3; i++)
        {
            var connected = await master.ConnectAsync();
            Assert.False(connected);
            _output.WriteLine($"  尝试 {i + 1}: 失败（符合预期）");
            await Task.Delay(100);
        }
        
        // 现在启动服务器
        _output.WriteLine("步骤 4: 启动服务器");
        var mockSlaveLogger = new Mock<ILogger<Iec102Slave>>();
        var slave = new Iec102Slave(30302, 0xFFFF, mockSlaveLogger.Object);
        await slave.StartAsync();
        await Task.Delay(200);
        
        try
        {
            // 尝试连接应该成功
            _output.WriteLine("步骤 5: 再次尝试连接（现在应该成功）");
            var connectedFinal = await master.ConnectAsync();
            Assert.True(connectedFinal);
            _output.WriteLine("步骤 5: ✓ 连接成功");
            
            await master.DisconnectAsync();
        }
        finally
        {
            await slave.StopAsync();
        }
    }
    
    /// <summary>
    /// 容灾测试：并发场景下的会话隔离
    /// </summary>
    [Fact]
    public async Task DisasterRecovery_SessionIsolation_UnderConcurrency()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<Iec102Slave>>();
        var slave = new Iec102Slave(30303, 0xFFFF, mockLogger.Object);
        await slave.StartAsync();
        await Task.Delay(200);
        
        _output.WriteLine("测试：并发场景下的会话隔离");
        
        try
        {
            // Act - 创建多个客户端，其中一些会断开
            var tasks = new List<Task>();
            var results = new List<bool>();
            var lockObj = new object();
            
            for (int i = 0; i < 10; i++)
            {
                int clientId = i;
                var task = Task.Run(async () =>
                {
                    var mockMasterLogger = new Mock<ILogger<Iec102Master>>();
                    var master = new Iec102Master("localhost", 30303, 0xFFFF, mockMasterLogger.Object);
                    
                    try
                    {
                        var connected = await master.ConnectAsync();
                        if (!connected)
                        {
                            lock (lockObj) results.Add(false);
                            return;
                        }
                        
                        // 客户端 0, 2, 4 会在操作中断开
                        if (clientId % 2 == 0)
                        {
                            _output.WriteLine($"  客户端 {clientId}: 模拟中断");
                            await master.DisconnectAsync();
                            await Task.Delay(100);
                            
                            // 尝试重连
                            var reconnected = await master.ConnectAsync();
                            lock (lockObj) results.Add(reconnected);
                            
                            if (reconnected)
                            {
                                await master.DisconnectAsync();
                            }
                        }
                        else
                        {
                            // 其他客户端正常操作
                            await Task.Delay(100);
                            var result = await master.ResetLinkAsync();
                            lock (lockObj) results.Add(result);
                            await master.DisconnectAsync();
                        }
                    }
                    catch (Exception ex)
                    {
                        _output.WriteLine($"  客户端 {clientId} 异常: {ex.Message}");
                        lock (lockObj) results.Add(false);
                    }
                });
                tasks.Add(task);
            }
            
            await Task.WhenAll(tasks);
            
            // Assert
            var successCount = results.Count(r => r);
            var successRate = (double)successCount / results.Count * 100;
            
            _output.WriteLine($"总客户端: {results.Count}, 成功: {successCount}, 成功率: {successRate:F2}%");
            Assert.True(successRate >= 80, $"成功率 {successRate:F2}% 低于预期");
        }
        finally
        {
            await slave.StopAsync();
        }
    }
    
    /// <summary>
    /// 容灾测试：超时场景恢复
    /// </summary>
    [Fact]
    public async Task DisasterRecovery_TimeoutRecovery_Success()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<Iec102Master>>();
        
        _output.WriteLine("测试：连接超时后的恢复");
        
        // Act - 尝试连接不存在的服务（会超时）
        _output.WriteLine("步骤 1: 尝试连接不存在的服务");
        var master1 = new Iec102Master("localhost", 39998, 0xFFFF, mockLogger.Object);
        var connected1 = await master1.ConnectAsync();
        Assert.False(connected1);
        _output.WriteLine("步骤 1: ✓ 超时失败，符合预期");
        
        // 验证可以创建新的连接到有效服务
        _output.WriteLine("步骤 2: 启动有效服务并连接");
        var mockSlaveLogger = new Mock<ILogger<Iec102Slave>>();
        var slave = new Iec102Slave(30304, 0xFFFF, mockSlaveLogger.Object);
        await slave.StartAsync();
        await Task.Delay(200);
        
        try
        {
            var master2 = new Iec102Master("localhost", 30304, 0xFFFF, mockLogger.Object);
            var connected2 = await master2.ConnectAsync();
            Assert.True(connected2);
            _output.WriteLine("步骤 2: ✓ 连接成功");
            
            await master2.DisconnectAsync();
        }
        finally
        {
            await slave.StopAsync();
        }
        
        _output.WriteLine("超时恢复测试通过 ✓");
    }
    
    /// <summary>
    /// 容灾测试：快速连接断开循环
    /// </summary>
    [Fact]
    public async Task DisasterRecovery_RapidConnectDisconnectCycle_Stable()
    {
        // Arrange
        var mockSlaveLogger = new Mock<ILogger<Iec102Slave>>();
        var mockMasterLogger = new Mock<ILogger<Iec102Master>>();
        
        var slave = new Iec102Slave(30305, 0xFFFF, mockSlaveLogger.Object);
        await slave.StartAsync();
        await Task.Delay(200);
        
        var master = new Iec102Master("localhost", 30305, 0xFFFF, mockMasterLogger.Object);
        
        _output.WriteLine("测试：快速连接断开循环");
        
        try
        {
            // Act - 快速连接和断开50次
            int cycleCount = 50;
            int successCount = 0;
            
            for (int i = 0; i < cycleCount; i++)
            {
                var connected = await master.ConnectAsync();
                if (connected)
                {
                    successCount++;
                    await Task.Delay(10); // 极短的操作时间
                    await master.DisconnectAsync();
                    await Task.Delay(10); // 极短的间隔
                }
                else
                {
                    _output.WriteLine($"  第 {i + 1} 次连接失败");
                }
            }
            
            // Assert
            var successRate = (double)successCount / cycleCount * 100;
            _output.WriteLine($"循环次数: {cycleCount}, 成功: {successCount}, 成功率: {successRate:F2}%");
            
            Assert.True(successRate >= 95, $"成功率 {successRate:F2}% 低于95%");
        }
        finally
        {
            await slave.StopAsync();
        }
    }
    
    /// <summary>
    /// 容灾测试：长时间空闲后重新连接
    /// </summary>
    [Fact]
    public async Task DisasterRecovery_LongIdleBeforeReconnect_Success()
    {
        // Arrange
        var mockSlaveLogger = new Mock<ILogger<Iec102Slave>>();
        var mockMasterLogger = new Mock<ILogger<Iec102Master>>();
        
        var slave = new Iec102Slave(30306, 0xFFFF, mockSlaveLogger.Object);
        await slave.StartAsync();
        await Task.Delay(200);
        
        var master = new Iec102Master("localhost", 30306, 0xFFFF, mockMasterLogger.Object);
        
        _output.WriteLine("测试：长时间空闲后重新连接");
        
        try
        {
            // Act - 连接
            _output.WriteLine("步骤 1: 初始连接");
            var connected1 = await master.ConnectAsync();
            Assert.True(connected1);
            _output.WriteLine("步骤 1: ✓ 连接成功");
            
            await master.DisconnectAsync();
            
            // 长时间等待（模拟空闲）
            _output.WriteLine("步骤 2: 等待5秒（模拟长时间空闲）");
            await Task.Delay(5000);
            _output.WriteLine("步骤 2: ✓ 等待完成");
            
            // 重新连接
            _output.WriteLine("步骤 3: 重新连接");
            var connected2 = await master.ConnectAsync();
            Assert.True(connected2);
            _output.WriteLine("步骤 3: ✓ 重连成功");
            
            // 验证操作正常
            var reset = await master.ResetLinkAsync();
            Assert.True(reset);
            _output.WriteLine("步骤 3: ✓ 操作正常");
            
            await master.DisconnectAsync();
        }
        finally
        {
            await slave.StopAsync();
        }
    }
}
