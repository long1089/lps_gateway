using System;
using System.Collections.Generic;

namespace LPSGateway.Lib60870
{
    /// <summary>
    /// ASDU (Application Service Data Unit) manager for IEC-102
    /// Handles TYPE IDs 0x90-0xA8
    /// </summary>
    public class AsduManager
    {
        public class Asdu
        {
            public byte TypeId { get; set; }
            public byte CauseOfTransmission { get; set; }
            public ushort CommonAddress { get; set; }
            public byte[] Data { get; set; } = Array.Empty<byte>();
        }

        /// <summary>
        /// Parse ASDU from frame data
        /// </summary>
        public static Asdu ParseAsdu(byte[] data)
        {
            if (data.Length < 4)
                throw new ArgumentException("Data too short for ASDU");

            var asdu = new Asdu
            {
                TypeId = data[0],
                CauseOfTransmission = data[1],
                CommonAddress = BitConverter.ToUInt16(data, 2)
            };

            if (data.Length > 4)
            {
                asdu.Data = new byte[data.Length - 4];
                Array.Copy(data, 4, asdu.Data, 0, asdu.Data.Length);
            }

            return asdu;
        }

        /// <summary>
        /// Build ASDU data structure
        /// </summary>
        public static byte[] BuildAsdu(byte typeId, byte causeOfTransmission, ushort commonAddress, byte[] data)
        {
            var result = new List<byte>
            {
                typeId,
                causeOfTransmission
            };
            
            result.AddRange(BitConverter.GetBytes(commonAddress));
            
            if (data != null && data.Length > 0)
            {
                result.AddRange(data);
            }

            return result.ToArray();
        }

        /// <summary>
        /// Check if TYPE ID is in the expected range for E-file transfers (0x90-0xA8)
        /// </summary>
        public static bool IsEFileTypeId(byte typeId)
        {
            return typeId >= 0x90 && typeId <= 0xA8;
        }
    }
}
