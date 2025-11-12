namespace LpsGateway.Lib60870;

/// <summary>
/// IEC-102 传输原因码（COT - Cause of Transmission）常量
/// </summary>
/// <remarks>
/// 根据IEC-102协议规范：
/// - 文件传输使用 COT=0x07（最后一帧）和 COT=0x08（非最后一帧）
/// - 对账确认使用 COT=0x0A（主站）、0x0B（子站）、0x0C（重传准备）
/// - 错误处理使用 COT=0x0F（文件过长）、0x11（格式错误）、0x13（单帧过长）
/// - 错误确认使用 COT=0x10、0x12、0x14
/// </remarks>
public static class CauseOfTransmission
{
    /// <summary>
    /// 文件传输结束（最后一帧）
    /// COT=0x07
    /// </summary>
    public const byte FileTransferComplete = 0x07;

    /// <summary>
    /// 文件传输中（非最后一帧）
    /// COT=0x08（协议规范）
    /// </summary>
    public const byte FileTransferInProgress = 0x08;

    /// <summary>
    /// 对账：主站确认接收
    /// COT=0x0A
    /// </summary>
    public const byte ReconciliationFromMaster = 0x0A;

    /// <summary>
    /// 对账：子站确认接收（文件传送成功）
    /// COT=0x0B
    /// </summary>
    public const byte ReconciliationFromSlave = 0x0B;

    /// <summary>
    /// 对账：准备重新传输（长度不匹配时）
    /// COT=0x0C
    /// </summary>
    public const byte ReconciliationReconfirm = 0x0C;
    
    /// <summary>
    /// 通知重传（主站发送）
    /// COT=0x0D
    /// </summary>
    public const byte RetransmitNotification = 0x0D;
    
    /// <summary>
    /// 通知重传确认（子站响应）
    /// COT=0x0E
    /// </summary>
    public const byte RetransmitNotificationAck = 0x0E;

    /// <summary>
    /// 文件过长错误（主站发送，＞512×40字节）
    /// TYP=0x92, COT=0x0F
    /// </summary>
    public const byte FileTooLongError = 0x0F;
    
    /// <summary>
    /// 文件过长确认（子站响应）
    /// COT=0x10
    /// </summary>
    public const byte FileTooLongAck = 0x10;

    /// <summary>
    /// 文件名格式错误（主站发送）
    /// TYP=0x93, COT=0x11
    /// </summary>
    public const byte InvalidFileNameFormat = 0x11;
    
    /// <summary>
    /// 文件名格式错误确认（子站响应）
    /// COT=0x12
    /// </summary>
    public const byte InvalidFileNameFormatAck = 0x12;

    /// <summary>
    /// 单帧数据过长错误（主站发送，＞512字节）
    /// TYP=0x94, COT=0x13
    /// </summary>
    public const byte FrameTooLongError = 0x13;
    
    /// <summary>
    /// 单帧数据过长确认（子站响应）
    /// COT=0x14
    /// </summary>
    public const byte FrameTooLongAck = 0x14;

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
               cot == RetransmitNotification ||
               cot == RetransmitNotificationAck ||
               cot == FileTooLongError ||
               cot == FileTooLongAck ||
               cot == InvalidFileNameFormat ||
               cot == InvalidFileNameFormatAck ||
               cot == FrameTooLongError ||
               cot == FrameTooLongAck;
    }

    /// <summary>
    /// 获取COT的描述
    /// </summary>
    public static string GetDescription(byte cot)
    {
        return cot switch
        {
            FileTransferComplete => "文件传输结束 (COT=0x07)",
            FileTransferInProgress => "文件未传输结束 (COT=0x08)",
            ReconciliationFromMaster => "对账：主站确认接收 (COT=0x0A)",
            ReconciliationFromSlave => "对账：子站确认文件传送成功 (COT=0x0B)",
            ReconciliationReconfirm => "对账：准备重新传输 (COT=0x0C)",
            RetransmitNotification => "通知重传 (COT=0x0D)",
            RetransmitNotificationAck => "通知重传确认 (COT=0x0E)",
            FileTooLongError => "文件过长错误 (COT=0x0F)",
            FileTooLongAck => "文件过长确认 (COT=0x10)",
            InvalidFileNameFormat => "文件名格式错误 (COT=0x11)",
            InvalidFileNameFormatAck => "文件名格式错误确认 (COT=0x12)",
            FrameTooLongError => "单帧数据过长 (COT=0x13)",
            FrameTooLongAck => "单帧数据过长确认 (COT=0x14)",
            _ => $"未知COT (0x{cot:X2})"
        };
    }
}
