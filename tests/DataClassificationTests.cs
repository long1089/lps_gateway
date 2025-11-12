using Xunit;
using LpsGateway.Lib60870;

namespace LpsGateway.Tests;

/// <summary>
/// 数据分类测试
/// </summary>
public class DataClassificationTests
{
    [Theory]
    [InlineData(0x9A, true)]  // 测风塔数据 - 1级
    [InlineData(0x9B, true)]  // 短期预测 - 1级
    [InlineData(0x9C, true)]  // 超短期预测 - 1级
    [InlineData(0x9D, true)]  // 天气预报 - 1级
    [InlineData(0xA1, true)]  // 光伏气象站 - 1级
    [InlineData(0x95, false)] // 风电场基础信息 - 2级
    [InlineData(0x96, false)] // 风电机组信息 - 2级
    [InlineData(0xA8, false)] // 光伏实时数据 - 2级
    public void IsClass1Data_ShouldReturnCorrectClassification(byte typeId, bool expectedClass1)
    {
        // Act
        var isClass1 = DataClassification.IsClass1Data(typeId);
        
        // Assert
        Assert.Equal(expectedClass1, isClass1);
    }

    [Theory]
    [InlineData(0x9A, false)] // 测风塔数据 - 1级，所以不是2级
    [InlineData(0x95, true)]  // 风电场基础信息 - 2级
    [InlineData(0xA8, true)]  // 光伏实时数据 - 2级
    public void IsClass2Data_ShouldReturnCorrectClassification(byte typeId, bool expectedClass2)
    {
        // Act
        var isClass2 = DataClassification.IsClass2Data(typeId);
        
        // Assert
        Assert.Equal(expectedClass2, isClass2);
    }

    [Theory]
    [InlineData("EFJ_FARM_INFO", 0x95)]
    [InlineData("EFJ_FIVE_WIND_TOWER", 0x9A)]
    [InlineData("EFJ_DQ_RESULT_UP", 0x9B)]
    [InlineData("EFJ_CDQ_RESULT_UP", 0x9C)]
    [InlineData("EFJ_NWP_UP", 0x9D)]
    [InlineData("EGF_FIVE_GF_QXZ", 0xA1)]
    [InlineData("EGF_REALTIME", 0xA8)]
    public void GetTypeIdByReportType_ShouldReturnCorrectTypeId(string reportType, byte expectedTypeId)
    {
        // Act
        var typeId = DataClassification.GetTypeIdByReportType(reportType);
        
        // Assert
        Assert.NotNull(typeId);
        Assert.Equal(expectedTypeId, typeId.Value);
    }

    [Fact]
    public void GetTypeIdByReportType_ShouldReturnNullForUnknownType()
    {
        // Act
        var typeId = DataClassification.GetTypeIdByReportType("UNKNOWN_TYPE");
        
        // Assert
        Assert.Null(typeId);
    }

    [Theory]
    [InlineData(0x95, "风电场基础信息表")]
    [InlineData(0x9A, "测风塔采集数据表 [1级]")]
    [InlineData(0x9B, "场站上报短期预测 [1级]")]
    public void GetTypeIdDescription_ShouldReturnCorrectDescription(byte typeId, string expectedDescription)
    {
        // Act
        var description = DataClassification.GetTypeIdDescription(typeId);
        
        // Assert
        Assert.Equal(expectedDescription, description);
    }

    [Fact]
    public void GetTypeIdDescription_ShouldHandleUnknownTypeId()
    {
        // Act
        var description = DataClassification.GetTypeIdDescription(0xFF);
        
        // Assert
        Assert.Contains("未知TypeId", description);
        Assert.Contains("0xFF", description);
    }

    [Fact]
    public void AllClass1TypeIds_ShouldBeIdentifiedCorrectly()
    {
        // Arrange - 所有1级数据TypeId
        var class1TypeIds = new byte[] { 0x9A, 0x9B, 0x9C, 0x9D, 0xA1 };
        
        // Act & Assert
        foreach (var typeId in class1TypeIds)
        {
            Assert.True(DataClassification.IsClass1Data(typeId), 
                $"TypeId 0x{typeId:X2} 应该被识别为1级数据");
            Assert.False(DataClassification.IsClass2Data(typeId),
                $"TypeId 0x{typeId:X2} 不应该被识别为2级数据");
        }
    }

    [Fact]
    public void AllClass2TypeIds_ShouldBeIdentifiedCorrectly()
    {
        // Arrange - 一些2级数据TypeId
        var class2TypeIds = new byte[] { 0x95, 0x96, 0x97, 0x98, 0x99, 0x9E, 0x9F, 0xA0, 0xA2, 0xA3, 0xA4, 0xA6, 0xA7, 0xA8 };
        
        // Act & Assert
        foreach (var typeId in class2TypeIds)
        {
            Assert.True(DataClassification.IsClass2Data(typeId),
                $"TypeId 0x{typeId:X2} 应该被识别为2级数据");
            Assert.False(DataClassification.IsClass1Data(typeId),
                $"TypeId 0x{typeId:X2} 不应该被识别为1级数据");
        }
    }
}

/// <summary>
/// COT (Cause of Transmission) 测试
/// </summary>
public class CauseOfTransmissionTests
{
    [Theory]
    [InlineData(0x07, true)]  // FileTransferComplete
    [InlineData(0x08, true)]  // FileTransferInProgress (修正为0x08)
    [InlineData(0x0A, true)]  // ReconciliationFromMaster
    [InlineData(0x0B, true)]  // ReconciliationFromSlave
    [InlineData(0x0C, true)]  // ReconciliationReconfirm
    [InlineData(0x0D, true)]  // RetransmitNotification
    [InlineData(0x0E, true)]  // RetransmitNotificationAck
    [InlineData(0x0F, true)]  // FileTooLongError
    [InlineData(0x10, true)]  // FileTooLongAck
    [InlineData(0x11, true)]  // InvalidFileNameFormat
    [InlineData(0x12, true)]  // InvalidFileNameFormatAck
    [InlineData(0x13, true)]  // FrameTooLongError
    [InlineData(0x14, true)]  // FrameTooLongAck
    [InlineData(0x01, false)] // 非文件传输COT
    [InlineData(0x09, false)] // 非文件传输COT（不再使用）
    [InlineData(0xFF, false)] // 非文件传输COT
    public void IsFileTransferCot_ShouldReturnCorrectResult(byte cot, bool expected)
    {
        // Act
        var result = CauseOfTransmission.IsFileTransferCot(cot);
        
        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(0x07, "文件传输结束 (COT=0x07)")]
    [InlineData(0x08, "文件未传输结束 (COT=0x08)")]
    [InlineData(0x0A, "对账：主站确认接收 (COT=0x0A)")]
    [InlineData(0x0B, "对账：子站确认文件传送成功 (COT=0x0B)")]
    [InlineData(0x0F, "文件过长错误 (COT=0x0F)")]
    [InlineData(0x11, "文件名格式错误 (COT=0x11)")]
    [InlineData(0x13, "单帧数据过长 (COT=0x13)")]
    public void GetDescription_ShouldReturnCorrectDescription(byte cot, string expectedDescription)
    {
        // Act
        var description = CauseOfTransmission.GetDescription(cot);
        
        // Assert
        Assert.Equal(expectedDescription, description);
    }

    [Fact]
    public void Constants_ShouldHaveCorrectValues()
    {
        // 根据IEC-102协议规范验证COT值
        Assert.Equal(0x07, CauseOfTransmission.FileTransferComplete);
        Assert.Equal(0x08, CauseOfTransmission.FileTransferInProgress); // 修正为0x08
        Assert.Equal(0x0A, CauseOfTransmission.ReconciliationFromMaster);
        Assert.Equal(0x0B, CauseOfTransmission.ReconciliationFromSlave);
        Assert.Equal(0x0C, CauseOfTransmission.ReconciliationReconfirm);
        Assert.Equal(0x0D, CauseOfTransmission.RetransmitNotification);
        Assert.Equal(0x0E, CauseOfTransmission.RetransmitNotificationAck);
        Assert.Equal(0x0F, CauseOfTransmission.FileTooLongError);
        Assert.Equal(0x10, CauseOfTransmission.FileTooLongAck);
        Assert.Equal(0x11, CauseOfTransmission.InvalidFileNameFormat);
        Assert.Equal(0x12, CauseOfTransmission.InvalidFileNameFormatAck);
        Assert.Equal(0x13, CauseOfTransmission.FrameTooLongError);
        Assert.Equal(0x14, CauseOfTransmission.FrameTooLongAck);
    }
}
