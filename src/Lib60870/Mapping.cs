namespace LpsGateway.Lib60870;

/// <summary>
/// IEC-102 类型标识映射类
/// </summary>
/// <remarks>
/// 提供 Type ID（类型标识）与类型名称的映射关系
/// IEC-102 扩展类型标识范围：0x90 - 0xA8，用于 E 文件传输
/// </remarks>
public static class Mapping
{
    /// <summary>
    /// 类型标识到类型名称的映射字典
    /// </summary>
    public static readonly Dictionary<byte, string> TypeIdMapping = new()
    {
        { 0x90, "TYPE_90" },
        { 0x91, "TYPE_91" },
        { 0x92, "TYPE_92" },
        { 0x93, "TYPE_93" },
        { 0x94, "TYPE_94" },
        { 0x95, "TYPE_95" },
        { 0x96, "TYPE_96" },
        { 0x97, "TYPE_97" },
        { 0x98, "TYPE_98" },
        { 0x99, "TYPE_99" },
        { 0x9A, "TYPE_9A" },
        { 0x9B, "TYPE_9B" },
        { 0x9C, "TYPE_9C" },
        { 0x9D, "TYPE_9D" },
        { 0x9E, "TYPE_9E" },
        { 0x9F, "TYPE_9F" },
        { 0xA0, "TYPE_A0" },
        { 0xA1, "TYPE_A1" },
        { 0xA2, "TYPE_A2" },
        { 0xA3, "TYPE_A3" },
        { 0xA4, "TYPE_A4" },
        { 0xA5, "TYPE_A5" },
        { 0xA6, "TYPE_A6" },
        { 0xA7, "TYPE_A7" },
        { 0xA8, "TYPE_A8" }
    };

    /// <summary>
    /// 根据类型标识获取类型名称
    /// </summary>
    /// <param name="typeId">类型标识字节</param>
    /// <returns>类型名称字符串</returns>
    public static string GetTypeName(byte typeId)
    {
        return TypeIdMapping.TryGetValue(typeId, out var name) ? name : $"TYPE_{typeId:X2}";
    }
}
