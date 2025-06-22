using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BrickVault.Decompressors
{
    internal class RNCContext
    {
        public byte[] Input;
        public int InputLen;
        public byte[] Output;
        public int OutputLen;

        public int PackedSize;

        public int InputOffset;

        public ushort UnpackedCRC;
        public ushort PackedCRC;

        public byte[] mem1 = new byte[0xffff];
        public byte[] Decoded = new byte[0xffff];

        public ushort UnpackedCRCReal = 0;

        public int Mem1Pointer = 0xFFFD;

        public ushort MatchCount = 0;
        public ushort MatchOffset = 0;
        public ushort EncKey = 0;

        public ushort BitCount = 0;
        public uint BitBuffer = 0;
        public uint ProcessedSize = 0;

        public int OutputOffset = 0;

        public int DictSize = 0x8000;              // or 0x8000 depending on mode
        public int WindowIndex;                    // equivalent to "window - decoded"

        public RNCContext(byte[] compressedChunk, int compressedChunkSize, byte[] decompressedChunk, int decompressedChunkSize)
        {
            Input = compressedChunk;
            InputLen = compressedChunkSize;
            Output = decompressedChunk;
            OutputLen = decompressedChunkSize;
            PackedSize = InputLen - 13;
        }
    }

    public static class RNC
    {
        private static readonly ushort[] CrcTable =
        {
            0x0000, 0xC0C1, 0xC181, 0x0140, 0xC301, 0x03C0, 0x0280, 0xC241,
            0xC601, 0x06C0, 0x0780, 0xC741, 0x0500, 0xC5C1, 0xC481, 0x0440,
            0xCC01, 0x0CC0, 0x0D80, 0xCD41, 0x0F00, 0xCFC1, 0xCE81, 0x0E40,
            0x0A00, 0xCAC1, 0xCB81, 0x0B40, 0xC901, 0x09C0, 0x0880, 0xC841,
            0xD801, 0x18C0, 0x1980, 0xD941, 0x1B00, 0xDBC1, 0xDA81, 0x1A40,
            0x1E00, 0xDEC1, 0xDF81, 0x1F40, 0xDD01, 0x1DC0, 0x1C80, 0xDC41,
            0x1400, 0xD4C1, 0xD581, 0x1540, 0xD701, 0x17C0, 0x1680, 0xD641,
            0xD201, 0x12C0, 0x1380, 0xD341, 0x1100, 0xD1C1, 0xD081, 0x1040,
            0xF001, 0x30C0, 0x3180, 0xF141, 0x3300, 0xF3C1, 0xF281, 0x3240,
            0x3600, 0xF6C1, 0xF781, 0x3740, 0xF501, 0x35C0, 0x3480, 0xF441,
            0x3C00, 0xFCC1, 0xFD81, 0x3D40, 0xFF01, 0x3FC0, 0x3E80, 0xFE41,
            0xFA01, 0x3AC0, 0x3B80, 0xFB41, 0x3900, 0xF9C1, 0xF881, 0x3840,
            0x2800, 0xE8C1, 0xE981, 0x2940, 0xEB01, 0x2BC0, 0x2A80, 0xEA41,
            0xEE01, 0x2EC0, 0x2F80, 0xEF41, 0x2D00, 0xEDC1, 0xEC81, 0x2C40,
            0xE401, 0x24C0, 0x2580, 0xE541, 0x2700, 0xE7C1, 0xE681, 0x2640,
            0x2200, 0xE2C1, 0xE381, 0x2340, 0xE101, 0x21C0, 0x2080, 0xE041,
            0xA001, 0x60C0, 0x6180, 0xA141, 0x6300, 0xA3C1, 0xA281, 0x6240,
            0x6600, 0xA6C1, 0xA781, 0x6740, 0xA501, 0x65C0, 0x6480, 0xA441,
            0x6C00, 0xACC1, 0xAD81, 0x6D40, 0xAF01, 0x6FC0, 0x6E80, 0xAE41,
            0xAA01, 0x6AC0, 0x6B80, 0xAB41, 0x6900, 0xA9C1, 0xA881, 0x6840,
            0x7800, 0xB8C1, 0xB981, 0x7940, 0xBB01, 0x7BC0, 0x7A80, 0xBA41,
            0xBE01, 0x7EC0, 0x7F80, 0xBF41, 0x7D00, 0xBDC1, 0xBC81, 0x7C40,
            0xB401, 0x74C0, 0x7580, 0xB541, 0x7700, 0xB7C1, 0xB681, 0x7640,
            0x7200, 0xB2C1, 0xB381, 0x7340, 0xB101, 0x71C0, 0x7080, 0xB041,
            0x5000, 0x90C1, 0x9181, 0x5140, 0x9301, 0x53C0, 0x5280, 0x9241,
            0x9601, 0x56C0, 0x5780, 0x9741, 0x5500, 0x95C1, 0x9481, 0x5440,
            0x9C01, 0x5CC0, 0x5D80, 0x9D41, 0x5F00, 0x9FC1, 0x9E81, 0x5E40,
            0x5A00, 0x9AC1, 0x9B81, 0x5B40, 0x9901, 0x59C0, 0x5880, 0x9841,
            0x8801, 0x48C0, 0x4980, 0x8941, 0x4B00, 0x8BC1, 0x8A81, 0x4A40,
            0x4E00, 0x8EC1, 0x8F81, 0x4F40, 0x8D01, 0x4DC0, 0x4C80, 0x8C41,
            0x4400, 0x84C1, 0x8581, 0x4540, 0x8701, 0x47C0, 0x4680, 0x8641,
            0x8201, 0x42C0, 0x4380, 0x8341, 0x4100, 0x81C1, 0x8081, 0x4040
        };

        private static void Unpack(RNCContext ctx)
        {
            ctx.UnpackedCRC = ReadWordBE(ctx.Input, ref ctx.InputOffset);
            ctx.PackedCRC = ReadWordBE(ctx.Input, ref ctx.InputOffset);

            ReadByte(ctx.Input, ref ctx.InputOffset); // leeway
            ReadByte(ctx.Input, ref ctx.InputOffset); // chunks_count

            if (CrcBlock(ctx.Input, ctx.InputOffset, ctx.PackedSize) != ctx.PackedCRC)
            {
                throw new Exception("CRC does not match - Bad input data?");
            }

            InputBitsM2(ctx, 1);
            InputBitsM2(ctx, 1); // encryption required

            ctx.WindowIndex = ctx.DictSize;

            UnpackDataM2(ctx);
        }

        public static void Decompress(byte[] compressedChunk, int compressedChunkSize, byte[] decompressedChunk, int decompressedChunkSize)
        {
            RNCContext ctx = new RNCContext(compressedChunk, compressedChunkSize, decompressedChunk, decompressedChunkSize);

            Unpack(ctx);
        }

        private static int UnpackDataM2(RNCContext ctx)
        {
            while (ctx.ProcessedSize < ctx.OutputLen)
            {
                while (true)
                {
                    if (InputBitsM2(ctx, 1) == 0)
                    {
                        WriteDecodedByte(ctx, (byte)((ctx.EncKey ^ ReadSourceByte(ctx)) & 0xFF));
                        ctx.EncKey = Ror16(ctx.EncKey);
                        ctx.ProcessedSize++;
                    }
                    else
                    {
                        if (InputBitsM2(ctx, 1) != 0)
                        {
                            if (InputBitsM2(ctx, 1) != 0)
                            {
                                if (InputBitsM2(ctx, 1) != 0)
                                {
                                    ctx.MatchCount = (ushort)(ReadSourceByte(ctx) + 8);

                                    if (ctx.MatchCount == 8)
                                    {
                                        InputBitsM2(ctx, 1); // discard
                                        break;
                                    }
                                }
                                else
                                {
                                    ctx.MatchCount = 3;
                                }

                                DecodeMatchOffset(ctx);
                            }
                            else
                            {
                                ctx.MatchCount = 2;
                                ctx.MatchOffset = (ushort)(ReadSourceByte(ctx) + 1);
                            }

                            ctx.ProcessedSize += ctx.MatchCount;

                            while (ctx.MatchCount-- > 0)
                                WriteDecodedByte(ctx, ctx.Decoded[ctx.WindowIndex-ctx.MatchOffset]);
                        }
                        else
                        {
                            DecodeMatchCount(ctx);

                            if (ctx.MatchCount != 9)
                            {
                                DecodeMatchOffset(ctx);
                                ctx.ProcessedSize += ctx.MatchCount;

                                while (ctx.MatchCount-- > 0)
                                    WriteDecodedByte(ctx, ctx.Decoded[ctx.WindowIndex - ctx.MatchOffset]);
                            }
                            else
                            {
                                uint dataLength = (InputBitsM2(ctx, 4) << 2) + 12;
                                ctx.ProcessedSize += (uint)dataLength;

                                while (dataLength-- > 0)
                                    WriteDecodedByte(ctx, (byte)((ctx.EncKey ^ ReadSourceByte(ctx)) & 0xFF));

                                ctx.EncKey = Ror16(ctx.EncKey);
                            }
                        }
                    }
                }
            }

            int start = ctx.DictSize;
            int length = ctx.WindowIndex - start;

            if (length > 0)
                Buffer.BlockCopy(ctx.Decoded, start, ctx.Output, ctx.OutputOffset, length);

            return 0;
        }

        private static void DecodeMatchCount(RNCContext ctx)
        {
            ctx.MatchCount = (ushort)(InputBitsM2(ctx, 1) + 4);

            if (InputBitsM2(ctx, 1) == 1)
            {
                ctx.MatchCount = (ushort)(((ctx.MatchCount - 1) << 1) + InputBitsM2(ctx, 1));
            }
        }

        private static void DecodeMatchOffset(RNCContext ctx)
        {
            ctx.MatchOffset = 0;

            if (InputBitsM2(ctx, 1) == 1)
            {
                ctx.MatchOffset = (ushort)InputBitsM2(ctx, 1);

                if (InputBitsM2(ctx, 1) == 1)
                {
                    ctx.MatchOffset = (ushort)(((ctx.MatchOffset << 1) | (ushort)InputBitsM2(ctx, 1)) | 4);

                    if (InputBitsM2(ctx, 1) == 0)
                    {
                        ctx.MatchOffset = (ushort)((ctx.MatchOffset << 1) | (ushort)InputBitsM2(ctx, 1));
                    }
                }
                else if (ctx.MatchOffset == 0)
                {
                    ctx.MatchOffset = (ushort)(InputBitsM2(ctx, 1) + 2);
                }
            }

            ctx.MatchOffset = (ushort)(((ctx.MatchOffset << 8) | ReadSourceByte(ctx)) + 1);
        }

        private static void WriteDecodedByte(RNCContext ctx, byte b)
        {
            // If we've filled the decoded buffer (0xFFFF limit), flush to output and slide the window
            if (ctx.WindowIndex == 0xFFFF)
            {
                int flushStart = ctx.DictSize;
                int flushLength = 0xFFFF - ctx.DictSize;

                // Write decoded[dict_size .. 0xFFFF] to output
                Buffer.BlockCopy(ctx.Decoded, flushStart, ctx.Output, ctx.OutputOffset, flushLength);
                ctx.OutputOffset += flushLength;

                // Slide last dict_size bytes to front of decoded buffer
                Buffer.BlockCopy(ctx.Decoded, ctx.WindowIndex - ctx.DictSize, ctx.Decoded, 0, ctx.DictSize);

                // Reset window index
                ctx.WindowIndex = ctx.DictSize;
            }

            // Write the new decoded byte
            ctx.Decoded[ctx.WindowIndex++] = b;

            // Update unpacked CRC
            ctx.UnpackedCRCReal = (ushort)(
                CrcTable[(ctx.UnpackedCRCReal ^ b) & 0xFF] ^ (ctx.UnpackedCRCReal >> 8)
            );
        }

        public static ushort Ror16(ushort x)
        {
            if ((x & 1) != 0)
                return (ushort)(0x8000 | (x >> 1));
            else
                return (ushort)(x >> 1);
        }

        private static uint InputBitsM2(RNCContext ctx, short count)
        {
            uint bits = 0;

            while (count-- > 0)
            {
                if (ctx.BitCount == 0)
                {
                    ctx.BitBuffer = ReadSourceByte(ctx);
                    ctx.BitCount = 8;
                }

                bits <<= 1;

                if ((ctx.BitBuffer & 0x80) != 0)
                    bits |= 1;

                ctx.BitBuffer <<= 1;
                ctx.BitCount--;
            }

            return bits;
        }

        private static byte ReadSourceByte(RNCContext ctx)
        {
            // Trigger a refill if we're at the simulated pointer address 0xFFFD
            if (ctx.Mem1Pointer >= 0xFFFD)
            {
                int leftSize = ctx.InputLen - ctx.InputOffset;

                int sizeToRead = Math.Min(leftSize, 0xFFFD);

                // Refill the start of mem1
                Array.Copy(ctx.Input, ctx.InputOffset, ctx.mem1, 0, sizeToRead);
                ctx.InputOffset += sizeToRead;

                // Simulate extra 2 bytes read (lookahead safety)
                int remainder = leftSize - sizeToRead;
                if (remainder > 2) remainder = 2;

                if (remainder > 0)
                {
                    Array.Copy(ctx.Input, ctx.InputOffset, ctx.mem1, sizeToRead, remainder);
                    ctx.InputOffset += remainder;
                    ctx.InputOffset -= remainder; // rewind like original code
                }

                ctx.Mem1Pointer = 0; // reset position in buffer
            }

            return ctx.mem1[ctx.Mem1Pointer++];
        }

        private static ushort CrcBlock(byte[] buf, int offset, int size)
        {
            ushort crc = 0;

            for (int i = 0; i < size; i++)
            {
                crc ^= buf[offset++];
                crc = (ushort)((crc >> 8) ^ CrcTable[crc & 0xFF]);
            }

            return crc;
        }

        public static byte PeekByte(byte[] buf, int offset)
        {
            return buf[offset];
        }

        public static byte ReadByte(byte[] buf, ref int offset)
        {
            return buf[offset++];
        }

        public static void WriteByte(byte[] buf, ref int offset, byte b)
        {
            buf[offset++] = b;
        }

        public static ushort PeekWordBE(byte[] buf, int offset)
        {
            byte b1 = PeekByte(buf, offset);
            byte b2 = PeekByte(buf, offset + 1);
            return (ushort)((b1 << 8) | b2);
        }

        public static ushort ReadWordBE(byte[] buf, ref int offset)
        {
            byte b1 = ReadByte(buf, ref offset);
            byte b2 = ReadByte(buf, ref offset);
            return (ushort)((b1 << 8) | b2);
        }

        public static void WriteWordBE(byte[] buf, ref int offset, ushort val)
        {
            WriteByte(buf, ref offset, (byte)((val >> 8) & 0xFF));
            WriteByte(buf, ref offset, (byte)(val & 0xFF));
        }

        public static uint PeekDwordBE(byte[] buf, int offset)
        {
            ushort w1 = PeekWordBE(buf, offset);
            ushort w2 = PeekWordBE(buf, offset + 2);
            return ((uint)w1 << 16) | w2;
        }

        public static uint ReadDwordBE(byte[] buf, ref int offset)
        {
            ushort w1 = ReadWordBE(buf, ref offset);
            ushort w2 = ReadWordBE(buf, ref offset);
            return ((uint)w1 << 16) | w2;
        }

        public static void WriteDwordBE(byte[] buf, ref int offset, uint val)
        {
            WriteWordBE(buf, ref offset, (ushort)(val >> 16));
            WriteWordBE(buf, ref offset, (ushort)(val & 0xFFFF));
        }

        public static void ReadBuf(byte[] dest, byte[] source, ref int offset, int size)
        {
            Array.Copy(source, offset, dest, 0, size);
            offset += size;
        }

        public static void WriteBuf(byte[] dest, ref int offset, byte[] source, int size)
        {
            Array.Copy(source, 0, dest, offset, size);
            offset += size;
        }
    }
}
