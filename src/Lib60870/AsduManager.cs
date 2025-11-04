using System.Text;

namespace LpsGateway.Lib60870;

public class AsduData
{
    public byte TypeId { get; set; }
    public byte CauseOfTransmission { get; set; }
    public ushort CommonAddr { get; set; }
    public byte[] Payload { get; set; } = Array.Empty<byte>();
}

public static class AsduManager
{
    public static AsduData ParseAsdu(byte[] data)
    {
        if (data.Length < 6)
        {
            throw new ArgumentException("ASDU data too short");
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

    public static bool IsLastFrame(byte causeOfTransmission)
    {
        return causeOfTransmission == 0x07;
    }
}
