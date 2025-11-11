using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Microsoft.Extensions.Logging;
using Moq;
using LpsGateway.Lib60870;

namespace LpsGateway.Tests;

public class Iec102MasterSlaveTests
{
    [Fact]
    public async Task Slave_StartStop_WorksCorrectly()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<Iec102Slave>>();
        var slave = new Iec102Slave(30001, 0xFFFF, mockLogger.Object);
        
        // Act
        await slave.StartAsync();
        var isRunning = slave.IsRunning;
        await slave.StopAsync();
        var isStoppedCorrectly = !slave.IsRunning;
        
        // Assert
        Assert.True(isRunning);
        Assert.True(isStoppedCorrectly);
    }
    
    [Fact]
    public async Task Master_ConnectDisconnect_WorksCorrectly()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<Iec102Master>>();
        var mockSlaveLogger = new Mock<ILogger<Iec102Slave>>();
        
        // Start a slave server first
        var slave = new Iec102Slave(30002, 0xFFFF, mockSlaveLogger.Object);
        await slave.StartAsync();
        
        // Give server time to start
        await Task.Delay(100);
        
        var master = new Iec102Master("localhost", 30002, 0xFFFF, mockLogger.Object);
        
        // Act
        var connected = await master.ConnectAsync();
        await Task.Delay(100); // Allow connection to establish
        var isConnected = master.IsConnected;
        
        await master.DisconnectAsync();
        await Task.Delay(100);
        var isDisconnected = !master.IsConnected;
        
        // Cleanup
        await slave.StopAsync();
        
        // Assert
        Assert.True(connected);
        Assert.True(isConnected);
        Assert.True(isDisconnected);
    }
    
    [Fact]
    public async Task Master_SendResetLink_SlaveResponds()
    {
        // Arrange
        var mockMasterLogger = new Mock<ILogger<Iec102Master>>();
        var mockSlaveLogger = new Mock<ILogger<Iec102Slave>>();
        
        var slave = new Iec102Slave(30003, 0xFFFF, mockSlaveLogger.Object);
        await slave.StartAsync();
        await Task.Delay(100);
        
        var master = new Iec102Master("localhost", 30003, 0xFFFF, mockMasterLogger.Object);
        await master.ConnectAsync();
        await Task.Delay(100);
        
        Iec102Frame? receivedFrame = null;
        master.FrameReceived += (sender, frame) =>
        {
            receivedFrame = frame;
        };
        
        // Act
        await master.ResetLinkAsync();
        await Task.Delay(200); // Wait for response
        
        // Cleanup
        await master.DisconnectAsync();
        await slave.StopAsync();
        
        // Assert
        Assert.NotNull(receivedFrame);
        Assert.True(receivedFrame.IsValid);
    }
    
    [Fact]
    public async Task Slave_QueueData_MasterCanRequest()
    {
        // Arrange
        var mockMasterLogger = new Mock<ILogger<Iec102Master>>();
        var mockSlaveLogger = new Mock<ILogger<Iec102Slave>>();
        
        var slave = new Iec102Slave(30004, 0xFFFF, mockSlaveLogger.Object);
        await slave.StartAsync();
        await Task.Delay(100);
        
        var master = new Iec102Master("localhost", 30004, 0xFFFF, mockMasterLogger.Object);
        await master.ConnectAsync();
        await Task.Delay(100);
        
        // Queue some Class 2 data to all sessions
        var testData = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 };
        slave.QueueClass2DataToAll(0x95, 0x08, testData);
        
        Iec102Frame? receivedFrame = null;
        master.FrameReceived += (sender, frame) =>
        {
            if (frame.UserData.Length > 0 && frame.UserData[0] == 0x95)
            {
                receivedFrame = frame;
            }
        };
        
        // Act
        await master.ResetLinkAsync();
        await Task.Delay(100);
        await master.RequestClass2DataAsync();
        await Task.Delay(200); // Wait for response
        
        // Cleanup
        await master.DisconnectAsync();
        await slave.StopAsync();
        
        // Assert
        Assert.NotNull(receivedFrame);
        Assert.True(receivedFrame.IsValid);
        Assert.True(receivedFrame.UserData.Length > 0);
        Assert.Equal(0x95, receivedFrame.UserData[0]);
    }
    
    [Fact]
    public async Task Master_SendTimeSync_SlaveResponds()
    {
        // Arrange
        var mockMasterLogger = new Mock<ILogger<Iec102Master>>();
        var mockSlaveLogger = new Mock<ILogger<Iec102Slave>>();
        
        var slave = new Iec102Slave(30005, 0xFFFF, mockSlaveLogger.Object);
        await slave.StartAsync();
        await Task.Delay(100);
        
        var master = new Iec102Master("localhost", 30005, 0xFFFF, mockMasterLogger.Object);
        await master.ConnectAsync();
        await Task.Delay(100);
        
        Iec102Frame? receivedFrame = null;
        master.FrameReceived += (sender, frame) =>
        {
            if (frame.UserData.Length > 0 && frame.UserData[0] == 0x8B)
            {
                receivedFrame = frame;
            }
        };
        
        // Act
        await master.ResetLinkAsync();
        await Task.Delay(100);
        await master.SendTimeSyncAsync(DateTime.UtcNow);
        await Task.Delay(200);
        
        // Cleanup
        await master.DisconnectAsync();
        await slave.StopAsync();
        
        // Assert
        Assert.NotNull(receivedFrame);
        Assert.True(receivedFrame.IsValid);
        Assert.Equal(0x8B, receivedFrame.UserData[0]); // TimeSync TypeId
    }
    
    [Fact]
    public async Task Slave_MultipleClients_HandlesCorrectly()
    {
        // Arrange
        var mockSlaveLogger = new Mock<ILogger<Iec102Slave>>();
        var mockMasterLogger1 = new Mock<ILogger<Iec102Master>>();
        var mockMasterLogger2 = new Mock<ILogger<Iec102Master>>();
        
        var slave = new Iec102Slave(30006, 0xFFFF, mockSlaveLogger.Object);
        await slave.StartAsync();
        await Task.Delay(100);
        
        var master1 = new Iec102Master("localhost", 30006, 0xFFFF, mockMasterLogger1.Object);
        var master2 = new Iec102Master("localhost", 30006, 0xFFFF, mockMasterLogger2.Object);
        
        // Act
        var connected1 = await master1.ConnectAsync();
        await Task.Delay(100);
        var connected2 = await master2.ConnectAsync();
        await Task.Delay(100);
        
        var isConnected1 = master1.IsConnected;
        var isConnected2 = master2.IsConnected;
        
        // Cleanup
        await master1.DisconnectAsync();
        await master2.DisconnectAsync();
        await slave.StopAsync();
        
        // Assert
        Assert.True(connected1);
        Assert.True(connected2);
        Assert.True(isConnected1);
        Assert.True(isConnected2);
    }
    
    [Fact]
    public void ControlField_MasterFrame_CreatesCorrectly()
    {
        // Arrange & Act
        var control = ControlField.CreateMasterFrame(0x0B, true, true);
        var built = control.Build();
        
        // Assert
        Assert.True(control.PRM);
        Assert.True(control.FCB);
        Assert.True(control.FCV);
        Assert.Equal(0x0B, control.FunctionCode);
        
        // Verify bit pattern: PRM=1, FCB=1, FCV=1, FC=0x0B
        // Expected: 0111 1011 = 0x7B
        Assert.Equal(0x7B, built);
    }
    
    [Fact]
    public void ControlField_SlaveFrame_CreatesCorrectly()
    {
        // Arrange & Act
        var control = ControlField.CreateSlaveFrame(0x08, true, false);
        var built = control.Build();
        
        // Assert
        Assert.False(control.PRM);
        Assert.True(control.ACD);
        Assert.False(control.DFC);
        Assert.Equal(0x08, control.FunctionCode);
        
        // Verify bit pattern: PRM=0, ACD=1, DFC=0, FC=0x08
        // Expected: 0010 1000 = 0x28
        Assert.Equal(0x28, built);
    }
    
    [Fact]
    public void Iec102Frame_BuildVariableFrameWithAsdu_CreatesCorrectFrame()
    {
        // Arrange
        var control = ControlField.CreateMasterFrame(0x03, false, true);
        var asdu = new byte[] { 0x95, 0x01, 0x08, 0xFF, 0xFF, 0x00, 0x01, 0x02, 0x03 };
        
        // Act
        var frame = Iec102Frame.BuildVariableFrame(control, 0xFFFF, asdu);
        
        // Assert
        Assert.NotNull(frame);
        Assert.Equal(0x68, frame[0]); // Start
        Assert.Equal(0x68, frame[3]); // Start repeat
        Assert.Equal(0x16, frame[^1]); // End
        
        // Parse it back
        var parsed = Iec102Frame.Parse(frame);
        Assert.True(parsed.IsValid);
        Assert.Equal(asdu.Length, parsed.UserData.Length);
    }
}
