namespace LpsGateway.Lib60870;

/// <summary>
/// IEC-102 控制域帮助类，用于解析和构建控制字节
/// </summary>
/// <remarks>
/// 控制域格式（8位）：
/// - PRM (bit 6): 启动标志位 (1=主站, 0=从站)
/// - FCB (bit 5): 帧计数位（用于检测重复帧）
/// - FCV (bit 4): 帧计数有效位（1=FCB有效）
/// - FC (bits 0-3): 功能码
/// - ACD (bit 5 in slave): 要求访问位
/// - DFC (bit 4 in slave): 数据流控制位
/// </remarks>
public class ControlField
{
    /// <summary>
    /// 启动标志位：1=主站发送，0=从站发送
    /// </summary>
    public bool PRM { get; set; }

    /// <summary>
    /// 帧计数位：用于检测重复帧和确认帧序列
    /// </summary>
    public bool FCB { get; set; }

    /// <summary>
    /// 帧计数有效位：1=FCB位有效，0=FCB位无效
    /// </summary>
    public bool FCV { get; set; }

    /// <summary>
    /// 功能码（0-15）
    /// </summary>
    public byte FunctionCode { get; set; }

    /// <summary>
    /// 要求访问位（从站使用，代替 FCB）
    /// </summary>
    public bool ACD { get; set; }

    /// <summary>
    /// 数据流控制位（从站使用，代替 FCV）
    /// </summary>
    public bool DFC { get; set; }

    /// <summary>
    /// 构造空的控制域
    /// </summary>
    public ControlField()
    {
    }

    /// <summary>
    /// 从字节解析控制域
    /// </summary>
    /// <param name="controlByte">控制字节</param>
    public ControlField(byte controlByte)
    {
        Parse(controlByte);
    }

    /// <summary>
    /// 解析控制字节
    /// </summary>
    /// <param name="controlByte">控制字节</param>
    public void Parse(byte controlByte)
    {
        PRM = (controlByte & 0x40) != 0;
        FunctionCode = (byte)(controlByte & 0x0F);

        if (PRM)
        {
            // 主站帧
            FCB = (controlByte & 0x20) != 0;
            FCV = (controlByte & 0x10) != 0;
        }
        else
        {
            // 从站帧
            ACD = (controlByte & 0x20) != 0;
            DFC = (controlByte & 0x10) != 0;
        }
    }

    /// <summary>
    /// 构建控制字节
    /// </summary>
    /// <returns>控制字节</returns>
    public byte Build()
    {
        byte result = FunctionCode;

        if (PRM)
        {
            result |= 0x40; // PRM = 1
            if (FCB) result |= 0x20;
            if (FCV) result |= 0x10;
        }
        else
        {
            // PRM = 0 (从站)
            if (ACD) result |= 0x20;
            if (DFC) result |= 0x10;
        }

        return result;
    }

    /// <summary>
    /// 创建主站发送帧的控制域
    /// </summary>
    /// <param name="functionCode">功能码</param>
    /// <param name="fcb">帧计数位</param>
    /// <param name="fcv">帧计数有效位</param>
    /// <returns>控制域对象</returns>
    public static ControlField CreateMasterFrame(byte functionCode, bool fcb, bool fcv)
    {
        return new ControlField
        {
            PRM = true,
            FunctionCode = functionCode,
            FCB = fcb,
            FCV = fcv
        };
    }

    /// <summary>
    /// 创建从站响应帧的控制域
    /// </summary>
    /// <param name="functionCode">功能码</param>
    /// <param name="acd">要求访问位</param>
    /// <param name="dfc">数据流控制位</param>
    /// <returns>控制域对象</returns>
    public static ControlField CreateSlaveFrame(byte functionCode, bool acd = false, bool dfc = false)
    {
        return new ControlField
        {
            PRM = false,
            FunctionCode = functionCode,
            ACD = acd,
            DFC = dfc
        };
    }

    /// <summary>
    /// 获取控制域的字符串表示
    /// </summary>
    /// <returns>控制域描述</returns>
    public override string ToString()
    {
        if (PRM)
        {
            return $"Master: FC={FunctionCode:X2}, FCB={FCB}, FCV={FCV}";
        }
        else
        {
            return $"Slave: FC={FunctionCode:X2}, ACD={ACD}, DFC={DFC}";
        }
    }
}

/// <summary>
/// IEC-102 常用功能码定义
/// </summary>
public static class FunctionCodes
{
    /// <summary>
    /// 复位远方链路
    /// </summary>
    public const byte ResetRemoteLink = 0x00;

    /// <summary>
    /// 复位用户进程
    /// </summary>
    public const byte ResetUserProcess = 0x01;

    /// <summary>
    /// 测试功能
    /// </summary>
    public const byte TestFunction = 0x02;

    /// <summary>
    /// 发送/确认用户数据
    /// </summary>
    public const byte UserData = 0x03;

    /// <summary>
    /// 发送/无应答用户数据
    /// </summary>
    public const byte UserDataNoReply = 0x04;

    /// <summary>
    /// 请求访问需求
    /// </summary>
    public const byte AccessDemand = 0x08;

    /// <summary>
    /// 请求链路状态
    /// </summary>
    public const byte RequestLinkStatus = 0x09;

    /// <summary>
    /// 请求1级用户数据
    /// </summary>
    public const byte RequestClass1Data = 0x0A;

    /// <summary>
    /// 请求2级用户数据
    /// </summary>
    public const byte RequestClass2Data = 0x0B;

    /// <summary>
    /// 确认（肯定）
    /// </summary>
    public const byte AckPositive = 0x00;

    /// <summary>
    /// 确认（否定）
    /// </summary>
    public const byte AckNegative = 0x01;

    /// <summary>
    /// 用户数据
    /// </summary>
    public const byte ResponseUserData = 0x08;

    /// <summary>
    /// 无所请求的数据
    /// </summary>
    public const byte NoDataAvailable = 0x09;

    /// <summary>
    /// 链路状态或访问需求
    /// </summary>
    public const byte LinkStatusOrAccessDemand = 0x0B;
}
