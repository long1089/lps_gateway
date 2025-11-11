namespace LpsGateway.Lib60870;

/// <summary>
/// IEC-102 传输原因码（COT - Cause of Transmission）常量
/// </summary>
public static class CauseOfTransmission
{
    /// <summary>
    /// 文件传输结束（最后一帧）
    /// </summary>
    public const byte FileTransferComplete = 0x07;

    /// <summary>
    /// 文件传输中间帧（还有后续帧）
    /// </summary>
    public const byte FileTransferInProgress = 0x09;

    /// <summary>
    /// 对账：主站确认接收
    /// </summary>
    public const byte ReconciliationFromMaster = 0x0A;

    /// <summary>
    /// 对账：子站确认接收
    /// </summary>
    public const byte ReconciliationFromSlave = 0x0B;

    /// <summary>
    /// 对账：主站重新确认
    /// </summary>
    public const byte ReconciliationReconfirm = 0x0C;

    /// <summary>
    /// 文件传输错误（主站发送）
    /// </summary>
    public const byte FileTransferError = 0x10;

    /// <summary>
    /// 文件传输错误确认（子站响应）
    /// </summary>
    public const byte FileTransferErrorAck = 0x11;

    /// <summary>
    /// 文件名格式错误
    /// </summary>
    public const byte InvalidFileNameFormat = 0x12;

    /// <summary>
    /// 单帧数据过长
    /// </summary>
    public const byte FrameTooLong = 0x13;

    /// <summary>
    /// 判断是否为文件传输相关的COT
    /// </summary>
    public static bool IsFileTransferCot(byte cot)
    {
        return cot == FileTransferComplete || 
               cot == FileTransferInProgress ||
               cot == ReconciliationFromMaster ||
               cot == ReconciliationFromSlave ||
               cot == ReconciliationReconfirm ||
               cot == FileTransferError ||
               cot == FileTransferErrorAck ||
               cot == InvalidFileNameFormat ||
               cot == FrameTooLong;
    }

    /// <summary>
    /// 获取COT的描述
    /// </summary>
    public static string GetDescription(byte cot)
    {
        return cot switch
        {
            FileTransferComplete => "文件传输结束",
            FileTransferInProgress => "文件传输中",
            ReconciliationFromMaster => "对账（主站）",
            ReconciliationFromSlave => "对账（子站）",
            ReconciliationReconfirm => "对账重确认",
            FileTransferError => "文件传输错误",
            FileTransferErrorAck => "文件传输错误确认",
            InvalidFileNameFormat => "文件名格式错误",
            FrameTooLong => "单帧数据过长",
            _ => $"COT=0x{cot:X2}"
        };
    }
}
