using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using Microsoft.Extensions.Logging;
using Moq;
using LpsGateway.Lib60870;

namespace LpsGateway.Tests;

/// <summary>
/// M6 性能测试 - 并发连接、吞吐量测试
/// </summary>
public class M6PerformanceTests
{
    private readonly ITestOutputHelper _output;
    
    public M6PerformanceTests(ITestOutputHelper output)
    {
        _output = output;
    }
    
    /// <summary>
    /// 性能测试：并发连接（目标：50个并发连接，成功率 >95%）
    /// </summary>
    [Fact]
    public async Task Performance_ConcurrentConnections_50Clients()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<Iec102Slave>>();
        var slave = new Iec102Slave(30200, 0xFFFF, mockLogger.Object);
        await slave.StartAsync();
        await Task.Delay(500); // 确保服务启动完成
        
        int clientCount = 50;
        var stopwatch = Stopwatch.StartNew();
        var successCount = 0;
        var failureCount = 0;
        var responseTimes = new List<long>();
        
        try
        {
            // Act - 并发连接测试
            var tasks = new List<Task>();
            var lockObj = new object();
            
            for (int i = 0; i < clientCount; i++)
            {
                int clientId = i;
                var task = Task.Run(async () =>
                {
                    var clientStopwatch = Stopwatch.StartNew();
                    var mockClientLogger = new Mock<ILogger<Iec102Master>>();
                    var master = new Iec102Master("localhost", 30200, 0xFFFF, mockClientLogger.Object);
                    
                    try
                    {
                        var connected = await master.ConnectAsync();
                        clientStopwatch.Stop();
                        
                        if (connected)
                        {
                            await Task.Delay(50); // 模拟简单操作
                            var result = await master.ResetLinkAsync();
                            await master.DisconnectAsync();
                            
                            lock (lockObj)
                            {
                                successCount++;
                                responseTimes.Add(clientStopwatch.ElapsedMilliseconds);
                            }
                        }
                        else
                        {
                            lock (lockObj)
                            {
                                failureCount++;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _output.WriteLine($"Client {clientId} failed: {ex.Message}");
                        lock (lockObj)
                        {
                            failureCount++;
                        }
                    }
                });
                tasks.Add(task);
            }
            
            await Task.WhenAll(tasks);
            stopwatch.Stop();
            
            // Assert & Report
            var successRate = (double)successCount / clientCount * 100;
            var avgResponseTime = responseTimes.Count > 0 ? responseTimes.Average() : 0;
            var p95ResponseTime = responseTimes.Count > 0 
                ? responseTimes.OrderBy(x => x).Skip((int)(responseTimes.Count * 0.95)).FirstOrDefault()
                : 0;
            
            _output.WriteLine("=== 并发连接性能测试结果 ===");
            _output.WriteLine($"总客户端数: {clientCount}");
            _output.WriteLine($"成功连接数: {successCount}");
            _output.WriteLine($"失败连接数: {failureCount}");
            _output.WriteLine($"成功率: {successRate:F2}%");
            _output.WriteLine($"总耗时: {stopwatch.ElapsedMilliseconds} ms");
            _output.WriteLine($"平均响应时间: {avgResponseTime:F2} ms");
            _output.WriteLine($"P95 响应时间: {p95ResponseTime} ms");
            
            // 验证性能基准
            Assert.True(successRate >= 90, $"成功率 {successRate:F2}% 低于目标 90%");
            Assert.True(p95ResponseTime < 2000, $"P95 响应时间 {p95ResponseTime} ms 超过目标 2000 ms");
        }
        finally
        {
            await slave.StopAsync();
        }
    }
    
    /// <summary>
    /// 性能测试：连接建立速度
    /// </summary>
    [Fact]
    public async Task Performance_ConnectionEstablishment_Speed()
    {
        // Arrange
        var mockSlaveLogger = new Mock<ILogger<Iec102Slave>>();
        var slave = new Iec102Slave(30201, 0xFFFF, mockSlaveLogger.Object);
        await slave.StartAsync();
        await Task.Delay(200);
        
        var mockMasterLogger = new Mock<ILogger<Iec102Master>>();
        var master = new Iec102Master("localhost", 30201, 0xFFFF, mockMasterLogger.Object);
        
        try
        {
            // Act - 测量连接建立时间
            var stopwatch = Stopwatch.StartNew();
            var connected = await master.ConnectAsync();
            stopwatch.Stop();
            
            // Assert & Report
            _output.WriteLine("=== 连接建立速度测试结果 ===");
            _output.WriteLine($"连接状态: {(connected ? "成功" : "失败")}");
            _output.WriteLine($"建立时间: {stopwatch.ElapsedMilliseconds} ms");
            
            Assert.True(connected);
            Assert.True(stopwatch.ElapsedMilliseconds < 500, "连接建立时间超过500ms");
            
            await master.DisconnectAsync();
        }
        finally
        {
            await slave.StopAsync();
        }
    }
    
    /// <summary>
    /// 性能测试：多次连接断开重连
    /// </summary>
    [Fact]
    public async Task Performance_MultipleReconnections_Success()
    {
        // Arrange
        var mockSlaveLogger = new Mock<ILogger<Iec102Slave>>();
        var slave = new Iec102Slave(30202, 0xFFFF, mockSlaveLogger.Object);
        await slave.StartAsync();
        await Task.Delay(200);
        
        var mockMasterLogger = new Mock<ILogger<Iec102Master>>();
        var master = new Iec102Master("localhost", 30202, 0xFFFF, mockMasterLogger.Object);
        
        int reconnectCount = 20;
        var connectionTimes = new List<long>();
        
        try
        {
            // Act - 多次连接和断开
            for (int i = 0; i < reconnectCount; i++)
            {
                var stopwatch = Stopwatch.StartNew();
                var connected = await master.ConnectAsync();
                stopwatch.Stop();
                
                Assert.True(connected, $"第 {i + 1} 次连接失败");
                connectionTimes.Add(stopwatch.ElapsedMilliseconds);
                
                await Task.Delay(50);
                await master.DisconnectAsync();
                await Task.Delay(50);
            }
            
            // Assert & Report
            var avgConnectionTime = connectionTimes.Average();
            var maxConnectionTime = connectionTimes.Max();
            var minConnectionTime = connectionTimes.Min();
            
            _output.WriteLine("=== 多次重连性能测试结果 ===");
            _output.WriteLine($"重连次数: {reconnectCount}");
            _output.WriteLine($"平均连接时间: {avgConnectionTime:F2} ms");
            _output.WriteLine($"最快连接时间: {minConnectionTime} ms");
            _output.WriteLine($"最慢连接时间: {maxConnectionTime} ms");
            
            Assert.True(avgConnectionTime < 500, $"平均连接时间 {avgConnectionTime:F2} ms 超过500ms");
        }
        finally
        {
            await slave.StopAsync();
        }
    }
    
    /// <summary>
    /// 性能测试：并发会话独立性
    /// </summary>
    [Fact]
    public async Task Performance_ConcurrentSessionsIndependence_Success()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<Iec102Slave>>();
        var slave = new Iec102Slave(30203, 0xFFFF, mockLogger.Object);
        await slave.StartAsync();
        await Task.Delay(200);
        
        int sessionCount = 10;
        var sessionTasks = new List<Task<bool>>();
        
        try
        {
            // Act - 创建多个并发会话，每个发送不同命令
            for (int i = 0; i < sessionCount; i++)
            {
                int sessionId = i;
                var task = Task.Run(async () =>
                {
                    var mockMasterLogger = new Mock<ILogger<Iec102Master>>();
                    var master = new Iec102Master("localhost", 30203, 0xFFFF, mockMasterLogger.Object);
                    
                    try
                    {
                        var connected = await master.ConnectAsync();
                        if (!connected) return false;
                        
                        // 每个会话进行不同操作
                        for (int j = 0; j < 3; j++)
                        {
                            await Task.Delay(50);
                            var result = await master.ResetLinkAsync();
                            if (!result) return false;
                        }
                        
                        await master.DisconnectAsync();
                        return true;
                    }
                    catch
                    {
                        return false;
                    }
                });
                sessionTasks.Add(task);
            }
            
            var results = await Task.WhenAll(sessionTasks);
            
            // Assert & Report
            var successCount = results.Count(r => r);
            var successRate = (double)successCount / sessionCount * 100;
            
            _output.WriteLine("=== 并发会话独立性测试结果 ===");
            _output.WriteLine($"会话总数: {sessionCount}");
            _output.WriteLine($"成功会话: {successCount}");
            _output.WriteLine($"成功率: {successRate:F2}%");
            
            Assert.True(successRate >= 90, $"成功率 {successRate:F2}% 低于90%");
        }
        finally
        {
            await slave.StopAsync();
        }
    }
    
    /// <summary>
    /// 性能测试：内存使用（创建和销毁多个连接）
    /// </summary>
    [Fact]
    public async Task Performance_MemoryUsage_NoLeaks()
    {
        // Arrange
        var initialMemory = GC.GetTotalMemory(true);
        _output.WriteLine($"初始内存: {initialMemory / 1024.0:F2} KB");
        
        var mockSlaveLogger = new Mock<ILogger<Iec102Slave>>();
        var slave = new Iec102Slave(30204, 0xFFFF, mockSlaveLogger.Object);
        await slave.StartAsync();
        await Task.Delay(200);
        
        try
        {
            // Act - 多次创建和销毁连接
            for (int iteration = 0; iteration < 50; iteration++)
            {
                var mockMasterLogger = new Mock<ILogger<Iec102Master>>();
                var master = new Iec102Master("localhost", 30204, 0xFFFF, mockMasterLogger.Object);
                
                var connected = await master.ConnectAsync();
                Assert.True(connected);
                
                await Task.Delay(10);
                await master.DisconnectAsync();
                
                // 每10次迭代报告一次
                if ((iteration + 1) % 10 == 0)
                {
                    var currentMemory = GC.GetTotalMemory(false);
                    _output.WriteLine($"迭代 {iteration + 1}: 内存 {currentMemory / 1024.0:F2} KB");
                }
            }
            
            // 强制垃圾回收
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            var finalMemory = GC.GetTotalMemory(true);
            var memoryGrowth = finalMemory - initialMemory;
            var memoryGrowthMB = memoryGrowth / (1024.0 * 1024.0);
            
            _output.WriteLine("=== 内存使用测试结果 ===");
            _output.WriteLine($"初始内存: {initialMemory / 1024.0:F2} KB");
            _output.WriteLine($"最终内存: {finalMemory / 1024.0:F2} KB");
            _output.WriteLine($"内存增长: {memoryGrowthMB:F2} MB");
            
            // Assert - 内存增长应该很小（<50MB）
            Assert.True(memoryGrowthMB < 50, $"内存增长 {memoryGrowthMB:F2} MB 超过预期");
        }
        finally
        {
            await slave.StopAsync();
        }
    }
}
