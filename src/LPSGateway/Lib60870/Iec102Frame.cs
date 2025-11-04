using System;
using System.Collections.Generic;
using System.Linq;

namespace LPSGateway.Lib60870
{
    /// <summary>
    /// IEC-102 frame parser and builder for 0x10 (fixed) and 0x68 (variable) frames
    /// </summary>
    public class Iec102Frame
    {
        public const byte StartFixed = 0x10;
        public const byte StartVariable = 0x68;
        public const byte End = 0x16;

        public byte[] ControlField { get; set; } = Array.Empty<byte>();
        public byte[] Address { get; set; } = Array.Empty<byte>();
        public byte[] Data { get; set; } = Array.Empty<byte>();
        public bool IsVariable { get; set; }

        public Iec102Frame()
        {
        }

        public Iec102Frame(byte[] controlField, byte[] address, byte[]? data = null)
        {
            ControlField = controlField ?? Array.Empty<byte>();
            Address = address ?? Array.Empty<byte>();
            Data = data ?? Array.Empty<byte>();
            IsVariable = data != null && data.Length > 0;
        }

        /// <summary>
        /// Build a fixed frame (0x10 format)
        /// </summary>
        public static byte[] BuildFixedFrame(byte controlField, byte[] address)
        {
            var frame = new List<byte> { StartFixed, controlField };
            frame.AddRange(address);
            byte checksum = CalculateChecksum(new[] { controlField }.Concat(address).ToArray());
            frame.Add(checksum);
            frame.Add(End);
            return frame.ToArray();
        }

        /// <summary>
        /// Build a variable frame (0x68 format)
        /// </summary>
        public static byte[] BuildVariableFrame(byte controlField, byte[] address, byte[] data)
        {
            var userDataLength = 1 + address.Length + data.Length; // control + address + data
            var frame = new List<byte> { StartVariable, (byte)userDataLength, (byte)userDataLength, StartVariable };
            frame.Add(controlField);
            frame.AddRange(address);
            frame.AddRange(data);
            
            byte checksum = CalculateChecksum(new[] { controlField }.Concat(address).Concat(data).ToArray());
            frame.Add(checksum);
            frame.Add(End);
            return frame.ToArray();
        }

        /// <summary>
        /// Try to parse frames from incoming byte stream
        /// </summary>
        public static List<Iec102Frame> TryParseFrames(byte[] buffer, ref int offset)
        {
            var frames = new List<Iec102Frame>();

            while (offset < buffer.Length)
            {
                var startByte = buffer[offset];
                
                if (startByte == StartFixed)
                {
                    // Fixed frame: 10 C A A CS 16 (minimum 6 bytes)
                    if (offset + 6 > buffer.Length)
                        break; // Need more data

                    var controlField = new[] { buffer[offset + 1] };
                    var address = new[] { buffer[offset + 2], buffer[offset + 3] };
                    var checksum = buffer[offset + 4];
                    var end = buffer[offset + 5];

                    if (end != End)
                    {
                        offset++;
                        continue;
                    }

                    var calculatedChecksum = CalculateChecksum(new[] { buffer[offset + 1], buffer[offset + 2], buffer[offset + 3] });
                    if (checksum != calculatedChecksum)
                    {
                        offset++;
                        continue;
                    }

                    frames.Add(new Iec102Frame
                    {
                        ControlField = controlField,
                        Address = address,
                        Data = Array.Empty<byte>(),
                        IsVariable = false
                    });

                    offset += 6;
                }
                else if (startByte == StartVariable)
                {
                    // Variable frame: 68 L L 68 C A A DATA CS 16
                    if (offset + 4 > buffer.Length)
                        break; // Need more data

                    var length1 = buffer[offset + 1];
                    var length2 = buffer[offset + 2];
                    var start2 = buffer[offset + 3];

                    if (length1 != length2 || start2 != StartVariable)
                    {
                        offset++;
                        continue;
                    }

                    var totalFrameLength = 6 + length1; // 68 L L 68 + length + CS 16
                    if (offset + totalFrameLength > buffer.Length)
                        break; // Need more data

                    var controlField = new[] { buffer[offset + 4] };
                    var address = new[] { buffer[offset + 5], buffer[offset + 6] };
                    var dataLength = length1 - 3; // minus control and address
                    var data = new byte[dataLength];
                    Array.Copy(buffer, offset + 7, data, 0, dataLength);

                    var checksum = buffer[offset + 4 + length1];
                    var end = buffer[offset + 5 + length1];

                    if (end != End)
                    {
                        offset++;
                        continue;
                    }

                    var checksumData = new byte[length1];
                    Array.Copy(buffer, offset + 4, checksumData, 0, length1);
                    var calculatedChecksum = CalculateChecksum(checksumData);
                    
                    if (checksum != calculatedChecksum)
                    {
                        offset++;
                        continue;
                    }

                    frames.Add(new Iec102Frame
                    {
                        ControlField = controlField,
                        Address = address,
                        Data = data,
                        IsVariable = true
                    });

                    offset += totalFrameLength;
                }
                else
                {
                    offset++;
                }
            }

            return frames;
        }

        /// <summary>
        /// Calculate IEC-102 checksum (sum of bytes modulo 256)
        /// </summary>
        private static byte CalculateChecksum(byte[] data)
        {
            int sum = 0;
            foreach (var b in data)
            {
                sum += b;
            }
            return (byte)(sum % 256);
        }
    }
}
