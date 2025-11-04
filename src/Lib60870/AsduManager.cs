using System.Text;

namespace LpsGateway.Lib60870;

/// <summary>
/// ASDU（应用服务数据单元）数据结构
/// </summary>
public class AsduData
{
    /// <summary>
    /// 类型标识（Type ID）
    /// </summary>
    public byte TypeId { get; set; }

    /// <summary>
    /// 传输原因（Cause of Transmission）
    /// </summary>
    public byte CauseOfTransmission { get; set; }

    /// <summary>
    /// 公共地址（Common Address）
    /// </summary>
    public ushort CommonAddr { get; set; }

    /// <summary>
    /// 有效负载数据
    /// </summary>
    public byte[] Payload { get; set; } = Array.Empty<byte>();
}

/// <summary>
/// ASDU 管理器，提供 ASDU 的编码和解码功能
/// </summary>
/// <remarks>
/// ASDU 格式（简化版）：
/// Byte 0: Type ID (类型标识)
/// Byte 1: Length (长度 = 有效负载长度 + 2)
/// Byte 2: Cause of Transmission (传输原因)
/// Byte 3-4: Common Address (公共地址，小端序)
/// Byte 5+: Payload (有效负载)
/// </remarks>
public static class AsduManager
{
    /// <summary>
    /// 解析 ASDU 数据
    /// </summary>
    /// <param name="data">原始 ASDU 字节数组</param>
    /// <returns>解析后的 ASDU 数据对象</returns>
    /// <exception cref="ArgumentException">当数据长度不足时抛出</exception>
    public static AsduData ParseAsdu(byte[] data)
    {
        if (data.Length < 6)
        {
            throw new ArgumentException("ASDU 数据太短，至少需要 6 字节");
        }

        var asdu = new AsduData
        {
            TypeId = data[0],
            CauseOfTransmission = data[2],
            CommonAddr = BitConverter.ToUInt16(data, 3),
            Payload = new byte[data.Length - 5]
        };

        Array.Copy(data, 5, asdu.Payload, 0, asdu.Payload.Length);

        return asdu;
    }

    /// <summary>
    /// 构建 ASDU 数据
    /// </summary>
    /// <param name="typeId">类型标识</param>
    /// <param name="cot">传输原因</param>
    /// <param name="commonAddr">公共地址</param>
    /// <param name="payload">有效负载数据</param>
    /// <returns>完整的 ASDU 字节数组</returns>
    public static byte[] BuildAsdu(byte typeId, byte cot, ushort commonAddr, byte[] payload)
    {
        var asdu = new byte[5 + payload.Length];
        asdu[0] = typeId;
        asdu[1] = (byte)(payload.Length + 2);
        asdu[2] = cot;
        BitConverter.GetBytes(commonAddr).CopyTo(asdu, 3);
        payload.CopyTo(asdu, 5);
        return asdu;
    }

    /// <summary>
    /// 判断是否为最后一帧
    /// </summary>
    /// <param name="causeOfTransmission">传输原因</param>
    /// <returns>如果是最后一帧返回 true，否则返回 false</returns>
    /// <remarks>
    /// 0x06 = 中间帧
    /// 0x07 = 最后一帧（文件传输完成）
    /// </remarks>
    public static bool IsLastFrame(byte causeOfTransmission)
    {
        return causeOfTransmission == 0x07;
    }
}
