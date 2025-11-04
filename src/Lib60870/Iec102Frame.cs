namespace LpsGateway.Lib60870;

/// <summary>
/// IEC-102 帧类型
/// </summary>
public enum FrameType
{
    /// <summary>
    /// 固定长度帧（0x10）
    /// </summary>
    Fixed = 0x10,

    /// <summary>
    /// 可变长度帧（0x68）
    /// </summary>
    Variable = 0x68,

    /// <summary>
    /// 单字节帧（0xE5 - 确认）
    /// </summary>
    SingleByte = 0xE5
}

/// <summary>
/// IEC-102 帧结构
/// </summary>
/// <remarks>
/// 固定长度帧格式：
/// 0x10 | C | A | CS | 0x16
/// 
/// 可变长度帧格式：
/// 0x68 | L | L | 0x68 | C | A | ASDU | CS | 0x16
/// 
/// 单字节帧：
/// 0xE5 (肯定确认)
/// </remarks>
public class Iec102Frame
{
    /// <summary>
    /// 帧类型
    /// </summary>
    public FrameType Type { get; set; }

    /// <summary>
    /// 控制域
    /// </summary>
    public ControlField? ControlField { get; set; }

    /// <summary>
    /// 地址域（链路地址）
    /// </summary>
    public ushort Address { get; set; }

    /// <summary>
    /// 用户数据（ASDU）
    /// </summary>
    public byte[] UserData { get; set; } = Array.Empty<byte>();

    /// <summary>
    /// 校验和
    /// </summary>
    public byte Checksum { get; set; }

    /// <summary>
    /// 帧是否有效
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// 解析错误消息
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 从字节数组解析 IEC-102 帧
    /// </summary>
    /// <param name="data">原始字节数据</param>
    /// <returns>解析后的帧对象</returns>
    public static Iec102Frame Parse(byte[] data)
    {
        var frame = new Iec102Frame();

        if (data == null || data.Length == 0)
        {
            frame.IsValid = false;
            frame.ErrorMessage = "数据为空";
            return frame;
        }

        try
        {
            // 单字节确认帧
            if (data[0] == 0xE5)
            {
                frame.Type = FrameType.SingleByte;
                frame.IsValid = true;
                return frame;
            }

            // 固定长度帧
            if (data[0] == 0x10)
            {
                if (data.Length < 5)
                {
                    frame.IsValid = false;
                    frame.ErrorMessage = "固定长度帧数据不足";
                    return frame;
                }

                frame.Type = FrameType.Fixed;
                frame.ControlField = new ControlField(data[1]);
                frame.Address = data[2];
                frame.Checksum = data[3];

                // 验证校验和
                byte calculatedCs = (byte)(data[1] + data[2]);
                if (calculatedCs != frame.Checksum)
                {
                    frame.IsValid = false;
                    frame.ErrorMessage = $"校验和错误: 期望 {calculatedCs:X2}, 实际 {frame.Checksum:X2}";
                    return frame;
                }

                // 验证结束符
                if (data[4] != 0x16)
                {
                    frame.IsValid = false;
                    frame.ErrorMessage = "结束符错误";
                    return frame;
                }

                frame.IsValid = true;
                return frame;
            }

            // 可变长度帧
            if (data[0] == 0x68)
            {
                if (data.Length < 7)
                {
                    frame.IsValid = false;
                    frame.ErrorMessage = "可变长度帧数据不足";
                    return frame;
                }

                frame.Type = FrameType.Variable;
                byte length1 = data[1];
                byte length2 = data[2];

                // 验证长度字段
                if (length1 != length2)
                {
                    frame.IsValid = false;
                    frame.ErrorMessage = "长度字段不匹配";
                    return frame;
                }

                // 验证第二个起始符
                if (data[3] != 0x68)
                {
                    frame.IsValid = false;
                    frame.ErrorMessage = "第二个起始符错误";
                    return frame;
                }

                int frameLength = 4 + length1 + 2; // start(4) + data(length) + cs(1) + end(1)
                if (data.Length < frameLength)
                {
                    frame.IsValid = false;
                    frame.ErrorMessage = $"帧长度不足: 需要 {frameLength}, 实际 {data.Length}";
                    return frame;
                }

                frame.ControlField = new ControlField(data[4]);
                frame.Address = data[5];

                // 提取用户数据
                int userDataLength = length1 - 2; // length - control - address
                if (userDataLength > 0)
                {
                    frame.UserData = new byte[userDataLength];
                    Array.Copy(data, 6, frame.UserData, 0, userDataLength);
                }

                // 验证校验和
                frame.Checksum = data[4 + length1];
                byte calculatedCs = 0;
                for (int i = 4; i < 4 + length1; i++)
                {
                    calculatedCs += data[i];
                }

                if (calculatedCs != frame.Checksum)
                {
                    frame.IsValid = false;
                    frame.ErrorMessage = $"校验和错误: 期望 {calculatedCs:X2}, 实际 {frame.Checksum:X2}";
                    return frame;
                }

                // 验证结束符
                if (data[4 + length1 + 1] != 0x16)
                {
                    frame.IsValid = false;
                    frame.ErrorMessage = "结束符错误";
                    return frame;
                }

                frame.IsValid = true;
                return frame;
            }

            frame.IsValid = false;
            frame.ErrorMessage = $"未知的帧起始符: 0x{data[0]:X2}";
            return frame;
        }
        catch (Exception ex)
        {
            frame.IsValid = false;
            frame.ErrorMessage = $"解析异常: {ex.Message}";
            return frame;
        }
    }

    /// <summary>
    /// 构建固定长度帧
    /// </summary>
    /// <param name="control">控制域</param>
    /// <param name="address">地址</param>
    /// <returns>帧字节数组</returns>
    public static byte[] BuildFixedFrame(ControlField control, ushort address)
    {
        byte[] frame = new byte[5];
        frame[0] = 0x10;
        frame[1] = control.Build();
        frame[2] = (byte)address;
        frame[3] = (byte)(frame[1] + frame[2]); // 校验和
        frame[4] = 0x16;
        return frame;
    }

    /// <summary>
    /// 构建可变长度帧
    /// </summary>
    /// <param name="control">控制域</param>
    /// <param name="address">地址</param>
    /// <param name="userData">用户数据（ASDU）</param>
    /// <returns>帧字节数组</returns>
    public static byte[] BuildVariableFrame(ControlField control, ushort address, byte[] userData)
    {
        byte length = (byte)(userData.Length + 2); // control + address + userData
        byte[] frame = new byte[4 + length + 2]; // start(4) + data(length) + cs(1) + end(1)

        frame[0] = 0x68;
        frame[1] = length;
        frame[2] = length;
        frame[3] = 0x68;
        frame[4] = control.Build();
        frame[5] = (byte)address;

        // 复制用户数据
        Array.Copy(userData, 0, frame, 6, userData.Length);

        // 计算校验和
        byte checksum = 0;
        for (int i = 4; i < 4 + length; i++)
        {
            checksum += frame[i];
        }
        frame[4 + length] = checksum;
        frame[4 + length + 1] = 0x16;

        return frame;
    }

    /// <summary>
    /// 构建单字节确认帧
    /// </summary>
    /// <returns>确认字节</returns>
    public static byte[] BuildAckFrame()
    {
        return new byte[] { 0xE5 };
    }

    /// <summary>
    /// 获取帧的字符串表示
    /// </summary>
    /// <returns>帧描述</returns>
    public override string ToString()
    {
        if (!IsValid)
        {
            return $"Invalid Frame: {ErrorMessage}";
        }

        return Type switch
        {
            FrameType.SingleByte => "ACK Frame",
            FrameType.Fixed => $"Fixed Frame: {ControlField}, Addr={Address:X4}",
            FrameType.Variable => $"Variable Frame: {ControlField}, Addr={Address:X4}, DataLen={UserData.Length}",
            _ => "Unknown Frame"
        };
    }
}
