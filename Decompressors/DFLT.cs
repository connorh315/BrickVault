using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BrickVault.Decompressors
{
    public class DFLT : Decompressor
    {
        DfltContext ctx;

        public DFLT()
        {
            ctx = new DfltContext();
        }

        internal static uint DecompressChunk(DfltContext ctx)
        {
            ctx.BitCount = 0;
            ctx.BitBuffer = 0;

            while (true)
            {
                // Ensure at least 1 bit for final block flag
                if (ctx.BitCount < 1)
                {
                    ctx.RefillBuffer();
                }

                // Read "final block" bit
                bool isFinalBlock = (ctx.BitBuffer & 1) != 0;
                ctx.BitBuffer >>= 1;
                ctx.BitCount--;

                if (ctx.BitCount < 2)
                {
                    ctx.RefillBuffer();
                }

                int blockType = (int)(ctx.BitBuffer & 0x3);
                ctx.BitBuffer >>= 2;
                ctx.BitCount -= 2;

                int result;
                switch (blockType)
                {
                    case 2: // Stored/raw block
                        result = DecodeStoredBlock(ctx);
                        break;

                    case 1: // Fixed Huffman
                        if (BuildHuffmanTable(ctx.LiteralTable, DfltTables.FixedLiteralLengths, 0x120) == 0)
                            return 0;

                        if (BuildHuffmanTable(ctx.DistanceTable, DfltTables.FixedDistanceLengths, 0x20) == 0)
                            return 0;

                        result = DecodeHuffmanCompressedBlock(ctx);
                        break;

                    case 0: // Dynamic Huffman
                        result = ParseDynamicHuffmanHeader(ctx);
                        if (result != 0)
                            result = DecodeHuffmanCompressedBlock(ctx);
                        break;

                    default:
                        return 0; // Reserved/invalid block type
                }

                if (result == 0)
                    return 0;

                if (isFinalBlock)
                    break;
            }

            return 1;
        }

        private static int ParseDynamicHuffmanHeader(DfltContext ctx)
        {
            byte[] clenOrder = new byte[19] {
                16, 17, 18,  0,  8,  7,  9,  6,
                10, 5, 11,  4, 12, 3, 13, 2,
                14, 1, 15
            };

            var codeLengths = ctx.CodeLengths;
            Array.Clear(codeLengths, 0, codeLengths.Length);

            if (ctx.BitCount < 5) ctx.RefillBuffer();
            ctx.BitCount -= 5;
            int numLiterals = (int)(ctx.BitBuffer & 0x1F) + 257;
            ctx.BitBuffer >>= 5;

            if (ctx.BitCount < 5) ctx.RefillBuffer();
            ctx.BitCount -= 5;
            int numDistances = (int)(ctx.BitBuffer & 0x1F) + 1;
            ctx.BitBuffer >>= 5;

            if (ctx.BitCount < 4) ctx.RefillBuffer();
            ctx.BitCount -= 4;
            int numClenCodes = (int)(ctx.BitBuffer & 0xF) + 4;
            ctx.BitBuffer >>= 4;

            // Read the code length code lengths
            for (int i = 0; i < numClenCodes; ++i)
            {
                if (ctx.BitCount < 3) ctx.RefillBuffer();
                codeLengths[clenOrder[i]] = (byte)(ctx.BitBuffer & 0x7);
                ctx.BitBuffer >>= 3;
                ctx.BitCount -= 3;
            }

            // Build the temporary dispatch Huffman table
            BuildHuffmanTable(ctx.DispatchTable, codeLengths, 19);

            int totalCodes = numLiterals + numDistances;
            int clenIndex = 0;
            uint bitBuffer = ctx.BitBuffer;
            int bitCount = ctx.BitCount;

            while (clenIndex < totalCodes)
            {
                if (bitCount < 0x10)
                {
                    ctx.BitBuffer = bitBuffer;
                    ctx.BitCount = bitCount;
                    ctx.RefillBuffer();
                    bitBuffer = ctx.BitBuffer;
                    bitCount = ctx.BitCount;
                }

                ushort sym = ctx.DispatchTable.FastLookup[bitBuffer & 0x1FF];
                if (sym != 0xFFFF)
                {
                    byte len = ctx.DispatchTable.BitLengths[sym];
                    bitBuffer >>= len;
                    bitCount -= len;
                    sym = ctx.DispatchTable.Symbols[sym];
                }
                else
                {
                    uint rev = bitBuffer;
                    rev = ((rev >> 1) & 0x5555) | ((rev & 0x5555) << 1);
                    rev = ((rev >> 2) & 0x3333) | ((rev & 0x3333) << 2);
                    rev = ((rev >> 4) & 0x0F0F) | ((rev & 0x0F0F) << 4);
                    rev = (rev >> 8) | ((rev & 0x00FF) << 8);

                    int len = 10;
                    while (ctx.DispatchTable.Thresholds[len] <= rev) ++len;

                    ushort baseVal = ctx.DispatchTable.Limits[len];
                    ushort offset = ctx.DispatchTable.Bases[len];
                    bitBuffer >>= len;
                    bitCount -= len;

                    int index = ((int)(rev >> (16 - len)) - baseVal) + offset;
                    sym = ctx.DispatchTable.Symbols[index];
                }

                if (sym < 16)
                {
                    ctx.CodeLengths[clenIndex++] = (byte)sym;
                }
                else
                {
                    int repeat = 0;
                    byte prev = (clenIndex > 0) ? ctx.CodeLengths[clenIndex - 1] : (byte)0;

                    if (sym == 16)
                    {
                        if (bitCount < 2)
                        {
                            ctx.BitBuffer = bitBuffer;
                            ctx.BitCount = bitCount;
                            ctx.RefillBuffer();
                            bitBuffer = ctx.BitBuffer;
                            bitCount = ctx.BitCount;
                        }
                        repeat = (int)(bitBuffer & 0x3) + 3;
                        bitBuffer >>= 2;
                        bitCount -= 2;
                    }
                    else if (sym == 17)
                    {
                        if (bitCount < 3)
                        {
                            ctx.BitBuffer = bitBuffer;
                            ctx.BitCount = bitCount;
                            ctx.RefillBuffer();
                            bitBuffer = ctx.BitBuffer;
                            bitCount = ctx.BitCount;
                        }
                        repeat = (int)(bitBuffer & 0x7) + 3;
                        bitBuffer >>= 3;
                        bitCount -= 3;
                        prev = 0;
                    }
                    else if (sym == 18)
                    {
                        if (bitCount < 7)
                        {
                            ctx.BitBuffer = bitBuffer;
                            ctx.BitCount = bitCount;
                            ctx.RefillBuffer();
                            bitBuffer = ctx.BitBuffer;
                            bitCount = ctx.BitCount;
                        }
                        repeat = (int)(bitBuffer & 0x7F) + 11;
                        bitBuffer >>= 7;
                        bitCount -= 7;
                        prev = 0;
                    }

                    for (int j = 0; j < repeat; j++)
                        ctx.CodeLengths[clenIndex++] = prev;
                }
            }

            ctx.BitCount = bitCount;
            ctx.BitBuffer = bitBuffer;

            // Build final literal and distance tables
            BuildHuffmanTable(ctx.LiteralTable, ctx.CodeLengths, numLiterals);
            BuildHuffmanTable(ctx.DistanceTable, ctx.CodeLengths.Skip(numLiterals).ToArray(), numDistances);

            return 1;
        }

        private static int DecodeHuffmanCompressedBlock(DfltContext ctx)
        {
            int loops = 0;
            while (true)
            {
                loops++;
                // Refill bit buffer if necessary
                if (ctx.BitCount < 0x10)
                {
                    ctx.RefillBuffer();
                }

                ushort sym = ctx.LiteralTable.Decode(ref ctx.BitBuffer, ref ctx.BitCount);

                if (sym < 256)
                {
                    ctx.WriteByte((byte)sym);
                    continue;
                }
                else if (sym == 256)
                {
                    break; // end of block
                }

                // --- Match length ---
                int matchLen = DfltTables.LengthBaseValues[sym - 256];
                int lenExtra = DfltTables.LengthExtraValues[sym - 256];

                if (ctx.BitCount < lenExtra)
                {
                    ctx.RefillBuffer();
                }

                if (lenExtra > 0)
                {
                    matchLen += (int)(ctx.BitBuffer & ((1 << lenExtra) - 1));
                    ctx.BitBuffer >>= lenExtra;
                    ctx.BitCount -= lenExtra;
                }

                // --- Distance symbol ---
                if (ctx.BitCount < 0x10)
                {
                    ctx.RefillBuffer();
                }

                sym = ctx.DistanceTable.Decode(ref ctx.BitBuffer, ref ctx.BitCount);
                int distBase = DfltTables.DistBaseValues[sym];
                int distExtra = DfltTables.DistExtraValues[sym];

                int distance = distBase;
                if (distExtra > 0)
                {
                    if (ctx.BitCount < distExtra)
                    {
                        ctx.RefillBuffer();
                    }
                    distance += (int)(ctx.BitBuffer & ((1 << distExtra) - 1));
                    ctx.BitBuffer >>= distExtra;
                    ctx.BitCount -= distExtra;
                }

                // Match copy
                int matchPos = ctx.OutputOffset - distance;
                if (matchPos < 0 || matchPos + matchLen > ctx.Output.Length)
                {
                    Console.Error.WriteLine($"Match out of bounds: {matchPos:X}");
                    return 0;
                }

                for (int i = 0; i < matchLen; i++)
                {
                    ctx.WriteByte(ctx.Output[matchPos + i]);
                }
            }

            return 1;
        }

        private static int BuildHuffmanTable(HuffmanTable table, byte[] codeLengths, int count)
        { // Originally undflt_func1
            int[] lengthCounts = new int[17];      // Code length histogram for lengths 1-16
            uint[] offsets = new uint[16];         // Next code value per bit length

            // Reset fast lookup table to 0xFFFF (invalid)
            for (int i = 0; i < table.FastLookup.Length; i++)
                table.FastLookup[i] = 0xFFFF;

            // Count number of codes for each bit length
            for (int i = 0; i < count; i++)
            {
                byte len = codeLengths[i];
                if (len <= 16)
                    lengthCounts[len]++;
            }

            lengthCounts[0] = 0; // Code length 0 is not used

            // Build base and limit values for each bit length
            int code = 0;
            int nextCode = 0;
            for (int len = 1; len <= 15; len++)
            {
                table.Bases[len] = (ushort)nextCode;
                table.Limits[len - 1] = (ushort)code;
                offsets[len - 1] = (uint)nextCode;

                nextCode += lengthCounts[len];
                code += lengthCounts[len];
                table.Thresholds[len - 1] = (uint)nextCode << (16 - len);
                nextCode *= 2;
            }

            // Sentinel
            if (table.Thresholds.Length >= 24)
                table.Thresholds[23] = 0x10000;

            // Assign actual symbols to slots
            for (int sym = 0; sym < count; sym++)
            {
                byte len = codeLengths[sym];
                if (len == 0) continue;

                uint codeVal = offsets[len - 1]++;
                ushort baseVal = table.Bases[len];
                ushort limitVal = table.Limits[len - 1];
                int symIdx = (int)(codeVal - baseVal + limitVal);

                table.BitLengths[symIdx] = len;
                table.Symbols[symIdx] = (ushort)sym;

                // Fill direct-lookup table if length <= 9
                if (len < 10)
                {
                    uint rev = codeVal;
                    rev = ((rev >> 1) & 0x5555) | ((rev & 0x5555) << 1);
                    rev = ((rev >> 2) & 0x3333) | ((rev & 0x3333) << 2);
                    rev = ((rev >> 4) & 0x0F0F) | ((rev & 0x0F0F) << 4);
                    rev = (rev >> 8) | ((rev & 0x00FF) << 8);
                    rev >>= (16 - len) & 0x1F;

                    for (int j = (int)rev; j < 512; j += (1 << len))
                    {
                        table.FastLookup[j] = (ushort)symIdx;
                    }
                }
            }

            return 1;
        }

        private static int DecodeStoredBlock(DfltContext ctx)
        {
            // Step 1: Align bit buffer to byte boundary
            int bitsToDiscard = ctx.BitCount & 7;
            if (bitsToDiscard > 0)
            {
                if (ctx.BitCount < bitsToDiscard)
                    ctx.RefillBuffer();

                ctx.BitBuffer >>= bitsToDiscard;
                ctx.BitCount -= bitsToDiscard;
            }

            // Step 2: Read 2 bytes for LEN field
            byte[] lenBytes = new byte[4];
            int lenBytesRead = 0;

            // Drain from bit buffer
            while (ctx.BitCount > 0)
            {
                lenBytes[lenBytesRead++] = (byte)(ctx.BitBuffer & 0xFF);
                ctx.BitBuffer >>= 8;
                ctx.BitCount -= 8;
            }

            // Read remaining bytes from input
            while (lenBytesRead < 2)
            {
                lenBytes[lenBytesRead++] = ctx.HasInput ? ctx.ReadByte() : (byte)0;
            }

            int len = (lenBytes[1] << 8) | lenBytes[0];

            // Step 4: Copy leftover bits if any
            if (lenBytesRead > 2)
            {
                int leftover = lenBytesRead - 2;
                for (int i = 0; i < leftover; i++)
                    ctx.WriteByte(lenBytes[2 + i]);
                len -= leftover;
            }

            // Step 5: Make sure enough input is available
            if (ctx.InputOffset + len > ctx.Input.Length)
                return 1;  // Not enough input

            // Step 6: Copy literal block
            Array.Copy(ctx.Input, ctx.InputOffset, ctx.Output, ctx.OutputOffset, len);
            ctx.InputOffset += len;
            ctx.OutputOffset += len;

            return 1;
        }

        public static long UnDFLT(byte[] compressed, int compressedSize, byte[] decompressed, int decompressedSize)
        {
            DfltContext ctx = new DfltContext()
            {
                Input = compressed,
                Output = decompressed
            };

            return DecompressChunk(ctx);
        }

        public override int Decompress(byte[] input, int inputLength, byte[] output, int outputLength)
        {
            ctx.Input = input;
            ctx.Output = output;
            ctx.InputLength = inputLength;
            ctx.OutputLength = outputLength;

            return (int)DecompressChunk(ctx);
        }

        public override void Reset()
        {
            ctx.Reset();
        }
    }

    public class HuffmanTable
    {
        public ushort[] FastLookup = new ushort[512];     // 0x200
        public byte[] BitLengths = new byte[768];         // 0x300
        public ushort[] Symbols = new ushort[768];        // 0x300
        public ushort[] Bases = new ushort[32];           // 0x20
        public ushort[] Limits = new ushort[32];          // 0x20
        public uint[] Thresholds = new uint[32];          // 0x20

        public ushort Decode(ref uint bitBuffer, ref int bitCount)
        {
            int index = (int)(bitBuffer & 0x1FF); // 9-bit fast lookup
            int symbolIndex = FastLookup[index];

            if (symbolIndex != 0xFFFF)
            {
                int bitLen = BitLengths[symbolIndex];  // Corrected field
                bitBuffer >>= bitLen;
                bitCount -= bitLen;
                return Symbols[symbolIndex];
            }

            // Slow path: bit-reversed lookup
            uint rev = bitBuffer;
            rev = ((rev >> 1) & 0x5555) | ((rev & 0x5555) << 1);
            rev = ((rev >> 2) & 0x3333) | ((rev & 0x3333) << 2);
            rev = ((rev >> 4) & 0x0F0F) | ((rev & 0x0F0F) << 4);
            rev = (rev >> 8) | ((rev & 0x00FF) << 8);

            int len = 10;
            while (len < Thresholds.Length && Thresholds[len - 1] <= rev)
                len++;

            if (len >= Thresholds.Length)
                throw new Exception(); // TODO: Remove (not sure why this is here)

            int baseVal = Limits[len - 1];
            int offset = Bases[len];
            bitBuffer >>= len;
            bitCount -= len;

            int finalIndex = ((int)(rev >> (16 - len)) - offset) + baseVal;
            return Symbols[finalIndex];

        }

        public void Reset()
        {
            Array.Clear(FastLookup);
            Array.Clear(BitLengths);
            Array.Clear(Symbols);
            Array.Clear(Bases);
            Array.Clear(Limits);
            Array.Clear(Thresholds);
        }
    }

    public class DfltContext
    {
        // Bitstream reader state
        public int BitCount;
        public uint BitBuffer;

        // Input and output spans
        public byte[] Input;
        public int InputLength;
        public int InputOffset;
        public byte[] Output;
        public int OutputLength;
        public int OutputOffset;

        // Internal decode pointer (equivalent to ptr)
        public int WritePtr => OutputOffset;

        // Utility to check how much input is left
        public bool HasInput => InputOffset < InputLength;

        public byte ReadByte()
        {
            if (!HasInput) return 0;
            return Input[InputOffset++];
        }

        public void WriteByte(byte b)
        {
            if (OutputOffset < OutputLength)
                Output[OutputOffset++] = b;
        }

        public void WriteBytes(ReadOnlySpan<byte> src)
        {
            Array.Copy(src.ToArray(), 0, Output, OutputOffset, src.Length);
            OutputOffset += src.Length;
        }

        public void RefillBuffer()
        {
            while (BitCount < 0x19)
            {
                BitBuffer |= (uint)(HasInput ? ReadByte() : 0) << BitCount;
                BitCount += 8;
            }
        }

        // Huffman table instances
        public HuffmanTable LiteralTable = new HuffmanTable();
        public HuffmanTable DistanceTable = new HuffmanTable();
        public HuffmanTable DispatchTable = new HuffmanTable();

        // Dispatcher table (shared by all)
        public ushort[] HuffmanDispatchTable = new ushort[512];

        // Temporary buffer for reading code lengths
        public byte[] CodeLengths = new byte[512];

        public void Reset()
        {
            BitCount = 0;
            BitBuffer = 0;
            InputOffset = 0;
            OutputOffset = 0;

            LiteralTable.Reset();
            DistanceTable.Reset();
            DispatchTable.Reset();

            Array.Clear(HuffmanDispatchTable);
            Array.Clear(CodeLengths);
        }
    }

    internal static class DfltTables
    {
        public static readonly byte[] FixedLiteralLengths = new byte[288];
        public static readonly byte[] FixedDistanceLengths = new byte[32];

        public static readonly int[] LengthBaseValues = new int[]
        {
            0, 3, 4, 5, 6, 7, 8, 9,
            10, 11, 13, 15, 17, 19, 23, 27,
            31, 35, 43, 51, 59, 67, 83, 99,
            115, 131, 163, 195, 227, 258, 0, 0
        };

        public static readonly int[] LengthExtraValues = new int[]
        {
            0, 0, 0, 0, 0, 0, 0, 0, 
            0, 1, 1, 1, 1, 2, 2, 2, 
            2, 3, 3, 3, 3, 4, 4, 4, 
            4, 5, 5, 5, 5, 0, 0, 0
        };

        public static readonly int[] DistBaseValues = new int[]
        {
            1, 2, 3, 4, 5, 7, 9, 13,
            17, 25, 33, 49, 65, 97, 129, 193,
            257, 385, 513, 769, 1025, 1537, 2049, 3073,
            4097, 6145, 8193, 12289, 16385, 24577, 0, 0, 0
        };

        public static readonly int[] DistExtraValues = new int[]
        {
            0, 0, 0, 0, 1, 1, 2, 2,
            3, 3, 4, 4, 5, 5, 6, 6,
            7, 7, 8, 8, 9, 9, 10, 10,
            11, 11, 12, 12, 13, 13
        };

        static DfltTables()
        {
            // Fixed literal/length codes (0–287)
            // 0–143 => 8 bits
            for (int i = 0; i <= 143; i++)
                FixedLiteralLengths[i] = 8;

            // 144–255 => 9 bits
            for (int i = 144; i <= 255; i++)
                FixedLiteralLengths[i] = 9;

            // 256–279 => 7 bits
            for (int i = 256; i <= 279; i++)
                FixedLiteralLengths[i] = 7;

            // 280–287 => 8 bits
            for (int i = 280; i <= 287; i++)
                FixedLiteralLengths[i] = 8;

            // Fixed distance codes (0–31), all 5 bits
            for (int i = 0; i < 32; i++)
                FixedDistanceLengths[i] = 5;
        }
    }
}