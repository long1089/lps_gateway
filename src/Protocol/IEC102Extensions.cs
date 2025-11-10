using System;
using System.Text;

namespace Gateway.Protocol.IEC102
{
    // 类型标识（TYP）
    public enum TypeId : byte
    {
        // 控制/确认
        FileEndCtrl            = 0x90,
        FileRetransmitCtrl     = 0x91,
        FileTooLongCtrl        = 0x92,
        FileNameInvalidCtrl    = 0x93,
        FrameTooLongCtrl       = 0x94,

        // 文件数据
        EFJ_FARM_INFO                 = 0x95, // 1
        EFJ_FARM_UNIT_INFO            = 0x96, // 2
        EFJ_FARM_UNIT_RUN_STATE       = 0x97, // 3
        EFJ_FARM_RUN_CAP              = 0x98, // 4
        EFJ_WIND_TOWER_INFO           = 0x99, // 5
        EFJ_FIVE_WIND_TOWER           = 0x9A, // 6
        EFJ_DQ_RESULT_UP              = 0x9B, // 7
        EFJ_CDQ_RESULT_UP             = 0x9C, // 8
        EFJ_NWP_UP                    = 0x9D, // 10
        EFJ_OTHER_UP                  = 0x9E, // 11
        EFJ_FIF_THEORY_POWER          = 0x9F, // 12
        EGF_GF_QXZ_INFO               = 0xA0, // 14
        EGF_FIVE_GF_QXZ               = 0xA1, // 17
        EGF_GF_UNIT_RUN_STATE         = 0xA2, // 16
        EGF_GF_UNIT_INFO              = 0xA3, // 15
        EGF_GF_INFO                   = 0xA4, // 13
        EFJ_DQ_PLAN_UP                = 0xA6, // 9
        EFJ_REALTIME                  = 0xA7, // 18
        EGF_REALTIME                  = 0xA8, // 19

        // 扩展指令
        TimeSync                      = 0x8B, // 时间同步（CP56Time2a）
        FileRequest                   = 0x8D, // 文件点播
        FileCancel                    = 0x8E, // 取消点播
    }

    public static class Cot
    {
        public const byte Act         = 0x06;
        public const byte ActConfirm  = 0x07;
        public const byte ActTerm     = 0x0A;
        public const byte SegmentLast = 0x07;
        public const byte SegmentMore = 0x08;
        public const byte OkSameLen   = 0x0B;
        public const byte BadDiffLen  = 0x0C;
        public const byte RetransmitM = 0x0D;
        public const byte RetransmitS = 0x0E;
        public const byte FileTooLongM= 0x0F;
        public const byte FileTooLongS= 0x10;
        public const byte NameBadM    = 0x11;
        public const byte NameBadS    = 0x12;
        public const byte FrameTooLongM=0x13;
        public const byte FrameTooLongS=0x14;
    }

    public static class Iec102Frame
    {
        public const byte StartFixed   = 0x10;
        public const byte StartVar     = 0x68;
        public const byte End          = 0x16;
        public const byte VSQ          = 0x01;
        public const ushort LinkAddr   = 0xFFFF;
        public const ushort CommAddr   = 0xFFFF;
        public const byte RecordAddr   = 0x00;

        // 构造可变帧（含 ASDU）
        public static byte[] BuildVariableFrame(
            byte control,
            TypeId typeId,
            byte cot,
            ReadOnlySpan<byte> data) // 仅 ASDU Data，不含 TYP/VSQ/COT/Addr
        {
            int asduLen = 1 + 1 + 1 + 2 + 1 + data.Length;
            int userLen = 1 + 2 + asduLen;

            var buf = new byte[4 + userLen + 2]; // 68 LL LL 68 .. CHK 16
            int p = 0;
            buf[p++] = StartVar;
            buf[p++] = (byte)(userLen & 0xFF);
            buf[p++] = (byte)((userLen >> 8) & 0xFF);
            buf[p++] = StartVar;

            int chkStart = p;
            buf[p++] = control;
            buf[p++] = (byte)(LinkAddr & 0xFF);
            buf[p++] = (byte)((LinkAddr >> 8) & 0xFF);

            buf[p++] = (byte)typeId;
            buf[p++] = VSQ;
            buf[p++] = cot;
            buf[p++] = (byte)(CommAddr & 0xFF);
            buf[p++] = (byte)((CommAddr >> 8) & 0xFF);
            buf[p++] = RecordAddr;

            data.CopyTo(buf.AsSpan(p));
            p += data.Length;

            byte chk = 0x00;
            for (int i = chkStart; i < p; i++) chk += buf[i];
            buf[p++] = chk;
            buf[p++] = End;
            return buf;
        }

        // 构造“文件片段”ASDU 的 Data：FileName(64B) + Content(≤512B)
        public static byte[] BuildFileSegmentData(string fileName, ReadOnlySpan<byte> content)
        {
            if (content.Length > 512) throw new ArgumentOutOfRangeException(nameof(content), "Content segment must be ≤ 512 bytes.");
            Span<byte> name = stackalloc byte[64];
            name.Clear();
            var src = Encoding.ASCII.GetBytes(fileName);
            if (src.Length > 64) throw new ArgumentOutOfRangeException(nameof(fileName), "FileName must be ≤ 64 bytes.");
            src.AsSpan().CopyTo(name);

            byte[] data = new byte[64 + content.Length];
            name.CopyTo(data);
            content.CopyTo(data.AsSpan(64));
            return data;
        }

        // CP56Time2a（UTC）
        public static byte[] BuildCp56Time2a(DateTime utc)
        {
            var dt = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
            ushort ms = (ushort)(dt.Millisecond + dt.Second * 1000);
            byte[] data = new byte[7];
            data[0] = (byte)(ms & 0xFF);
            data[1] = (byte)((ms >> 8) & 0xFF);
            data[2] = (byte)dt.Minute;
            data[3] = (byte)dt.Hour;
            data[4] = (byte)dt.Day;
            data[5] = (byte)dt.Month;
            data[6] = (byte)(dt.Year - 2000);
            return data;
        }

        // 扩展指令：文件点播 Data（mode bit0: 0=latest,1=range; bit1:1=compressed）
        public static byte[] BuildFileRequestData(byte reportTypeCode, byte mode, ReadOnlySpan<byte> cp56StartOrRef, ReadOnlySpan<byte> cp56End = default)
        {
            if (reportTypeCode is < 1 or > 19) throw new ArgumentOutOfRangeException(nameof(reportTypeCode));
            var list = new System.Collections.Generic.List<byte>(1 + 1 + 7 + 7);
            list.Add(reportTypeCode);
            list.Add(mode);
            if (cp56StartOrRef.Length != 7) throw new ArgumentException("CP56Time2a required.");
            list.AddRange(cp56StartOrRef.ToArray());
            if ((mode & 0x01) == 0x01)
            {
                if (cp56End.Length != 7) throw new ArgumentException("End CP56Time2a required when range mode is set.");
                list.AddRange(cp56End.ToArray());
            }
            return list.ToArray();
        }
    }

    public static class ControlField
    {
        // 主站下行：PRM=1，FCB/FCV 控制，FC=0/3/9/10/11
        public static byte BuildMaster(bool fcb, bool fcv, byte fc /*0..15*/)
        {
            byte c = 0;
            c |= 1 << 6;             // PRM=1
            if (fcb) c |= 1 << 5;    // FCB
            if (fcv) c |= 1 << 4;    // FCV
            c |= (byte)(fc & 0x0F);  // FC
            return c;
        }

        // 子站上行：PRM=0；ACD/DFC/FC
        public static byte BuildSlave(bool acd, bool dfc, byte fc /*0..15*/)
        {
            byte c = 0;
            if (acd) c |= 1 << 5; // ACD
            if (dfc) c |= 1 << 4; // DFC
            c |= (byte)(fc & 0x0F);
            return c;
        }
    }
}