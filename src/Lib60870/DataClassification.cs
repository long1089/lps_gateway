namespace LpsGateway.Lib60870;

/// <summary>
/// IEC-102 数据分类辅助类
/// </summary>
/// <remarks>
/// 根据IEC-102协议规范，区分1级数据（优先级高）和2级数据（常规）
/// </remarks>
public static class DataClassification
{
    /// <summary>
    /// 1级数据类型ID集合（优先级高，需要ACD标志管理）
    /// </summary>
    /// <remarks>
    /// 包括：
    /// - 0x9A: EFJ_FIVE_WIND_TOWER (测风塔采集数据)
    /// - 0x9B: EFJ_DQ_RESULT_UP (短期预测)
    /// - 0x9C: EFJ_CDQ_RESULT_UP (超短期预测)
    /// - 0x9D: EFJ_NWP_UP (天气预报)
    /// - 0xA1: EGF_FIVE_GF_QXZ (气象站采集数据)
    /// </remarks>
    private static readonly HashSet<byte> Class1TypeIds = new()
    {
        0x9A, // 测风塔采集数据
        0x9B, // 短期预测
        0x9C, // 超短期预测
        0x9D, // 天气预报
        0xA1  // 光伏气象站采集数据
    };

    /// <summary>
    /// 判断指定TypeId是否为1级数据
    /// </summary>
    /// <param name="typeId">TypeId (0x95-0xA8)</param>
    /// <returns>true表示1级数据，false表示2级数据</returns>
    public static bool IsClass1Data(byte typeId)
    {
        return Class1TypeIds.Contains(typeId);
    }

    /// <summary>
    /// 判断指定TypeId是否为2级数据
    /// </summary>
    /// <param name="typeId">TypeId (0x95-0xA8)</param>
    /// <returns>true表示2级数据，false表示1级数据</returns>
    public static bool IsClass2Data(byte typeId)
    {
        return !IsClass1Data(typeId);
    }

    /// <summary>
    /// 根据报告类型代码获取TypeId
    /// </summary>
    /// <param name="reportTypeCode">报告类型代码（如 "EFJ_FARM_INFO"）</param>
    /// <returns>对应的TypeId，如果未找到返回null</returns>
    public static byte? GetTypeIdByReportType(string reportTypeCode)
    {
        return reportTypeCode switch
        {
            "EFJ_FARM_INFO" => 0x95,
            "EFJ_FARM_UNIT_INFO" => 0x96,
            "EFJ_FARM_UNIT_RUN_STATE" => 0x97,
            "EFJ_FARM_RUN_CAP" => 0x98,
            "EFJ_WIND_TOWER_INFO" => 0x99,
            "EFJ_FIVE_WIND_TOWER" => 0x9A,      // 1级数据
            "EFJ_DQ_RESULT_UP" => 0x9B,          // 1级数据
            "EFJ_CDQ_RESULT_UP" => 0x9C,         // 1级数据
            "EFJ_DQ_PLAN_UP" => 0xA6,
            "EFJ_NWP_UP" => 0x9D,                // 1级数据
            "EFJ_OTHER_UP" => 0x9E,
            "EFJ_FIF_THEORY_POWER" => 0x9F,
            "EGF_GF_INFO" => 0xA4,
            "EGF_GF_QXZ_INFO" => 0xA0,
            "EGF_GF_UNIT_INFO" => 0xA3,
            "EGF_GF_UNIT_RUN_STATE" => 0xA2,
            "EGF_FIVE_GF_QXZ" => 0xA1,           // 1级数据
            "EFJ_REALTIME" => 0xA7,
            "EGF_REALTIME" => 0xA8,
            _ => null
        };
    }

    /// <summary>
    /// 获取TypeId的描述名称
    /// </summary>
    public static string GetTypeIdDescription(byte typeId)
    {
        return typeId switch
        {
            0x95 => "风电场基础信息表",
            0x96 => "风电机组信息表",
            0x97 => "风机运行表",
            0x98 => "单风场所有风机运行表",
            0x99 => "测风塔信息表",
            0x9A => "测风塔采集数据表 [1级]",
            0x9B => "场站上报短期预测 [1级]",
            0x9C => "场站上报超短期预测 [1级]",
            0x9D => "场站上报天气预报 [1级]",
            0x9E => "场站上报其他信息",
            0x9F => "场站上报理论功率",
            0xA0 => "光伏气象站信息表",
            0xA1 => "气象站采集数据表 [1级]",
            0xA2 => "逆变器运行表",
            0xA3 => "光伏逆变器信息表",
            0xA4 => "光伏电站基础信息表",
            0xA6 => "场站上报日前计划",
            0xA7 => "风电场实时数据",
            0xA8 => "光伏电站实时数据",
            _ => $"未知TypeId (0x{typeId:X2})"
        };
    }
}
