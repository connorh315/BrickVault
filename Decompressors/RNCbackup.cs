using System;
using System.IO;

namespace BrickVault.Decompressors;

public class Rncbackup
{
    private byte[] input;
    private byte[] output;
    private int inputOffset;
    private int outputOffset;

    private const int RNC_SIGN = 0x524E43;
    private const int RNC_HEADER_SIZE = 0x12;
    private const int MAX_BUF_SIZE = 0x1E00000;

    private ushort encKey;
    private ushort unpackedCrc;
    private ushort packedCrc;
    private ushort unpackedCrcReal;

    private byte[] mem1;
    private byte[] decoded;
    private int bitCount;
    private uint bitBuffer;
    private int dictSize;
    private int method;
    private int processedSize;
    private int inputSize;
    private int packedSize;
    private int matchOffset;
    private int matchCount;
    private int windowPtr;

    private struct HuffmanNode
    {
        public int BitDepth;
        public int Code;
    }

    private HuffmanNode[] rawTable = new HuffmanNode[16];
    private HuffmanNode[] lenTable = new HuffmanNode[16];
    private HuffmanNode[] posTable = new HuffmanNode[16];

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

    public byte[] Decompress(byte[] data)
    {
        input = data;
        inputOffset = 0;
        output = new byte[MAX_BUF_SIZE];
        outputOffset = 0;
        DoUnpack();
        byte[] result = new byte[outputOffset];
        Array.Copy(output, result, outputOffset);
        return result;
    }

    private void DoUnpack()
    {
        if (PeekDwordBE(input, inputOffset) >> 8 != RNC_SIGN)
            throw new InvalidDataException("Invalid RNC header.");

        method = (int)(ReadDwordBE() & 3);
        inputSize = (int)ReadDwordBE();
        packedSize = (int)ReadDwordBE();
        unpackedCrc = ReadWordBE();
        packedCrc = ReadWordBE();
        ReadByte(); // leeway
        ReadByte(); // chunks count

        if (CrcBlock(input, inputOffset, packedSize) != packedCrc)
            throw new InvalidDataException("Packed CRC check failed.");

        mem1 = new byte[0xFFFF];
        decoded = new byte[0xFFFF * 2];
        dictSize = 0xFFFF;
        unpackedCrcReal = 0;
        bitCount = 0;
        bitBuffer = 0;
        processedSize = 0;
        windowPtr = dictSize;

        if (InputBits(1) != 0 || InputBits(1) != 0)
            throw new InvalidDataException("Encrypted file requires a key.");

        switch (method)
        {
            case 1: UnpackDataM1(); break;
            case 2: UnpackDataM2(); break;
            default: throw new InvalidDataException("Unsupported compression method.");
        }

        if (unpackedCrc != unpackedCrcReal)
            throw new InvalidDataException("Unpacked CRC check failed.");
    }

    private void UnpackDataM2()
    {
        while (processedSize < inputSize)
        {
            while (true)
            {
                if (InputBits(1) == 0)
                {
                    byte b = (byte)(encKey ^ ReadSourceByte());
                    WriteDecodedByte(b);
                    RorW(ref encKey);
                    processedSize++;
                }
                else
                {
                    if (InputBits(1) != 0)
                    {
                        if (InputBits(1) != 0)
                        {
                            if (InputBits(1) != 0)
                            {
                                matchCount = ReadSourceByte() + 8;
                                if (matchCount == 8)
                                {
                                    InputBits(1);
                                    break;
                                }
                            }
                            else matchCount = 3;
                            DecodeMatchOffset();
                        }
                        else
                        {
                            matchCount = 2;
                            matchOffset = ReadSourceByte() + 1;
                        }

                        processedSize += matchCount;
                        while (matchCount-- > 0)
                            WriteDecodedByte(decoded[windowPtr - matchOffset]);
                    }
                    else
                    {
                        DecodeMatchCount();
                        if (matchCount != 9)
                        {
                            DecodeMatchOffset();
                            processedSize += matchCount;
                            while (matchCount-- > 0)
                                WriteDecodedByte(decoded[windowPtr - matchOffset]);
                        }
                        else
                        {
                            int len = (InputBits(4) << 2) + 12;
                            processedSize += len;
                            while (len-- > 0)
                                WriteDecodedByte((byte)(encKey ^ ReadSourceByte()));
                            RorW(ref encKey);
                        }
                    }
                }
            }
        }

        Array.Copy(decoded, dictSize, output, outputOffset, windowPtr - dictSize);
        outputOffset += windowPtr - dictSize;
    }

    private void DecodeMatchCount()
    {
        matchCount = InputBits(1) + 4;
        if (InputBits(1) != 0)
            matchCount = ((matchCount - 1) << 1) + InputBits(1);
    }

    private void DecodeMatchOffset()
    {
        matchOffset = 0;
        if (InputBits(1) != 0)
        {
            matchOffset = InputBits(1);
            if (InputBits(1) != 0)
            {
                matchOffset = ((matchOffset << 1) | InputBits(1)) | 4;
                if (InputBits(1) == 0)
                    matchOffset = (matchOffset << 1) | InputBits(1);
            }
            else if (matchOffset == 0)
                matchOffset = InputBits(1) + 2;
        }
        matchOffset = ((matchOffset << 8) | ReadSourceByte()) + 1;
    }

    private void WriteDecodedByte(byte b)
    {
        if (windowPtr >= decoded.Length)
        {
            Array.Copy(decoded, dictSize, output, outputOffset, dictSize);
            outputOffset += dictSize;
            Array.Copy(decoded, decoded.Length - dictSize, decoded, 0, dictSize);
            windowPtr = dictSize;
        }

        decoded[windowPtr++] = b;
        unpackedCrcReal = (ushort)((unpackedCrcReal >> 8) ^ CrcTable[(unpackedCrcReal ^ b) & 0xFF]);
    }

    private void UnpackDataM1()
    {
        while (processedSize < inputSize)
        {
            MakeHuffmanTable(rawTable);
            MakeHuffmanTable(lenTable);
            MakeHuffmanTable(posTable);

            int subchunks = InputBits(16);
            for (int i = 0; i < subchunks; i++)
            {
                int rawCount = DecodeHuffman(rawTable);
                processedSize += rawCount;

                for (int j = 0; j < rawCount; j++)
                    WriteDecodedByte((byte)(encKey ^ ReadSourceByte()));

                RorW(ref encKey);

                if (i < subchunks - 1)
                {
                    matchOffset = DecodeHuffman(lenTable) + 1;
                    matchCount = DecodeHuffman(posTable) + 2;
                    processedSize += matchCount;

                    for (int k = 0; k < matchCount; k++)
                        WriteDecodedByte(decoded[windowPtr - matchOffset]);
                }
            }
        }

        Array.Copy(decoded, dictSize, output, outputOffset, windowPtr - dictSize);
        outputOffset += windowPtr - dictSize;
    }

    private void MakeHuffmanTable(HuffmanNode[] table)
    {
        Array.Clear(table, 0, table.Length);
        int count = InputBits(5);
        for (int i = 0; i < count; i++)
            table[i].BitDepth = InputBits(4);

        int code = 0;
        for (int depth = 1; depth <= 16; depth++)
        {
            for (int i = 0; i < count; i++)
            {
                if (table[i].BitDepth == depth)
                {
                    table[i].Code = ReverseBits(code, depth);
                    code++;
                }
            }
            code <<= 1;
        }
    }

    private int DecodeHuffman(HuffmanNode[] table)
    {
        for (int i = 0; i < table.Length; i++)
        {
            if (table[i].BitDepth == 0)
                continue;

            int bits = PeekBits(table[i].BitDepth);
            if (bits == table[i].Code)
            {
                InputBits(table[i].BitDepth);
                return i < 2 ? i : ((1 << (i - 1)) | InputBits(i - 1));
            }
        }
        throw new InvalidDataException("Huffman decode failed.");
    }

    private int PeekBits(int count)
    {
        while (bitCount < count)
        {
            bitBuffer |= (uint)(ReadSourceByte() << (24 - bitCount));
            bitCount += 8;
        }
        return (int)(bitBuffer >> (32 - count));
    }

    private int InputBits(int count) // check
    {
        int result = 0;
        while (count-- > 0)
        {
            if (bitCount == 0)
            {
                bitBuffer = ReadSourceByte();
                bitCount = 8;
            }

            result <<= 1;
            if ((bitBuffer & 0x80) != 0)
                result |= 1;

            bitBuffer <<= 1;
            bitCount--;
        }
        return result;
    }

    private static int ReverseBits(int value, int count)
    {
        int result = 0;
        while (count-- > 0)
        {
            result = (result << 1) | (value & 1);
            value >>= 1;
        }
        return result;
    }

    private static void RorW(ref ushort x)
    {
        x = (ushort)((x >> 1) | ((x & 1) << 15));
    }

    private byte ReadByte() => input[inputOffset++];

    private ushort ReadWordBE() => (ushort)((ReadByte() << 8) | ReadByte());

    private uint ReadDwordBE() => (uint)((ReadWordBE() << 16) | ReadWordBE());

    private byte ReadSourceByte()
    {
        if (inputOffset >= input.Length)
            throw new EndOfStreamException();
        return input[inputOffset++];
    }

    private static uint PeekDwordBE(byte[] buf, int offset)
    {
        return (uint)((buf[offset] << 24) | (buf[offset + 1] << 16) | (buf[offset + 2] << 8) | buf[offset + 3]);
    }

    private ushort CrcBlock(byte[] buf, int offset, int size)
    {
        ushort crc = 0;
        for (int i = 0; i < size; i++)
        {
            crc ^= buf[offset++];
            crc = (ushort)((crc >> 8) ^ CrcTable[crc & 0xFF]);
        }
        return crc;
    }
}
