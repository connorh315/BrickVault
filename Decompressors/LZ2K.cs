// Original code (written in cpp) found here: https://github.com/pianistrevor/unlz2k/blob/main/unlz2k/unlz2k.cpp

namespace BrickVault.Decompressors
{
    internal class LZ2K
    {
        private const int MaxChunkSize = 0x40000;
        private const uint Lz2kHeader = 0x4C5A324B;

        private byte[] compressedFile = new byte[MaxChunkSize];
        private int tmpSrcOffs;
        private int tmpSrcSize;
        private int tmpDestSize;
        private uint bitstream;
        private byte lastByteRead;
        private byte previousBitAlign;
        private ushort chunksWithCurrentSetupLeft;
        private uint readOffset;
        private int literalsToCopy;
        private byte[] tmpChunk = new byte[8192];
        private byte[] smallByteDict = new byte[20];
        private byte[] largeByteDict = new byte[510];
        private ushort[] smallWordDict = new ushort[256];
        private ushort[] parallelDict0 = new ushort[1024];
        private ushort[] parallelDict1 = new ushort[1024];
        private ushort[] largeWordDict = new ushort[4096];

        public static long Unlz2k(byte[] compressed, int compressedSize, byte[] decompressed, int decompressedSize)
        {
            return new LZ2K().Unlz2kChunk(compressed, compressedSize, decompressed, decompressedSize);
        }

        private int Unlz2kChunk(byte[] compressed, int compressedSize, byte[] decompressed, int decompressedSize)
        {
            if (decompressed.Length == 0)
                return 0;

            Array.Copy(compressed, 0, compressedFile, 0, compressedSize);
            compressedFile = compressed;
            tmpSrcOffs = 0;
            tmpSrcSize = compressed.Length;
            bitstream = 0;
            lastByteRead = 0;
            previousBitAlign = 0;
            chunksWithCurrentSetupLeft = 0;
            readOffset = 0;
            literalsToCopy = 0;
            int bytesLeft = decompressedSize;
            int bytesWritten = 0;

            LoadIntoBitstream(32);

            while (bytesLeft > 0)
            {
                int chunkSize = Math.Min(bytesLeft, 8192);
                ReadAndDecrypt(chunkSize, tmpChunk);
                //dest.Write(tmpChunk, 0, chunkSize);
                Array.Copy(tmpChunk, 0, decompressed, bytesWritten, chunkSize);
                bytesWritten += chunkSize;
                bytesLeft -= chunkSize;
            }

            return bytesWritten;
        }

        private void LoadIntoBitstream(byte bits)
        {
            bitstream <<= bits;
            if (bits > previousBitAlign)
            {
                do
                {
                    bits -= previousBitAlign;
                    bitstream |= (uint)(lastByteRead << bits);
                    lastByteRead = tmpSrcOffs < tmpSrcSize ? compressedFile[tmpSrcOffs++] : (byte)0;
                    previousBitAlign = 8;
                } while (bits > previousBitAlign);
            }
            previousBitAlign -= bits;
            bitstream |= (uint)(lastByteRead >> previousBitAlign);
        }

        private void ReadAndDecrypt(int chunkSize, byte[] outBuffer)
        {
            int outputOffs = 0;
            literalsToCopy--;

            while (literalsToCopy >= 0)
            {
                outBuffer[outputOffs++] = outBuffer[readOffset++];
                readOffset &= 0x1FFF;
                if (outputOffs == chunkSize)
                    return;
                literalsToCopy--;
            }

            while (outputOffs < chunkSize)
            {
                uint tmpVal = DecodeBitstream();
                if (tmpVal <= 255)
                {
                    outBuffer[outputOffs++] = (byte)tmpVal;
                    if (outputOffs == chunkSize)
                        return;
                }
                else
                {
                    uint lbOffs = DecodeBitstreamForLiterals();
                    readOffset = (uint)(outputOffs - (int)lbOffs - 1) & 0x1FFF; // TODO: Possibly should be (int)?
                    literalsToCopy = (int)tmpVal - 254;
                    while (literalsToCopy >= 0)
                    {
                        outBuffer[outputOffs++] = outBuffer[readOffset++];
                        readOffset &= 0x1FFF;
                        if (outputOffs == chunkSize)
                            return;
                        literalsToCopy--;
                    }
                }
            }

            if (outputOffs > chunkSize)
            {
                throw new InvalidOperationException("Read further than the given length.");
            }
        }

        private uint ReadUInt32(Stream src, bool bigEndian)
        {
            Span<byte> buffer = stackalloc byte[4];
            src.Read(buffer);

            if (bigEndian)
            {
                return (uint)(buffer[0] << 24 | buffer[1] << 16 | buffer[2] << 8 | buffer[3]);
            }
            else
            {
                return (uint)(buffer[3] << 24 | buffer[2] << 16 | buffer[1] << 8 | buffer[0]);
            }
        }

        private uint DecodeBitstream()
        {
            if (chunksWithCurrentSetupLeft == 0)
            {
                chunksWithCurrentSetupLeft = (ushort)(bitstream >> 16);
                LoadIntoBitstream(16);
                FillSmallDicts(19, 5, 3);
                FillLargeDicts();
                FillSmallDicts(14, 4, (byte)0xff); // TODO: Made change here, check it
            }

            chunksWithCurrentSetupLeft--;
            ushort tmpVal = largeWordDict[bitstream >> 20];
            if (tmpVal >= 510)
            {
                uint mask = 0x80000;
                do
                {
                    if ((bitstream & mask) == 0)
                    {
                        tmpVal = parallelDict0[tmpVal];
                    }
                    else
                    {
                        tmpVal = parallelDict1[tmpVal];
                    }
                    mask >>= 1;
                } while (tmpVal >= 510);
            }

            byte bits = largeByteDict[tmpVal];
            LoadIntoBitstream(bits);
            return tmpVal;
        }

        private uint DecodeBitstreamForLiterals()
        {
            byte tmpOffs = (byte)(bitstream >> 24);
            ushort tmpVal = smallWordDict[tmpOffs];
            if (tmpVal >= 14)
            {
                uint mask = 0x800000;
                do
                {
                    if ((bitstream & mask) == 0)
                    {
                        tmpVal = parallelDict0[tmpVal];
                    }
                    else
                    {
                        tmpVal = parallelDict1[tmpVal];
                    }
                    mask >>= 1;
                } while (tmpVal >= 14);
            }

            byte bits = smallByteDict[tmpVal];
            LoadIntoBitstream(bits);

            if (tmpVal == 0)
                return 0;

            if (tmpVal == 1)
                return 1; /* KEY CHANGE HERE: Original code used "return 2" however this led to the output files not being correct */

            tmpVal--;

            uint tmpBitstream = bitstream >> (32 - tmpVal);
            LoadIntoBitstream((byte)tmpVal);
            return tmpBitstream + (1u << tmpVal);
        }

        private void FillSmallDicts(byte length, byte bits, byte specialInd)
        {
            uint tmpVal = bitstream >> (32 - bits);
            LoadIntoBitstream(bits);

            if (tmpVal == 0)
            {
                tmpVal = bitstream >> (32 - bits);
                LoadIntoBitstream(bits);
                if (length > 0)
                {
                    Array.Fill(smallByteDict, (byte)0, 0, length);
                }

                for (int i = 0; i < 256; ++i)
                {
                    smallWordDict[i] = (ushort)tmpVal;
                }
                return;
            }

            byte tmpVal2 = 0;
            while (tmpVal2 < tmpVal)
            {
                byte tmpByte = (byte)(bitstream >> 29);
                byte currentBits = 3;

                if (tmpByte == 7)
                {
                    uint mask = 0x10000000;
                    if ((bitstream & mask) == 0)
                    {
                        currentBits = 4;
                    }
                    else
                    {
                        byte counter = 0;
                        do
                        {
                            mask >>= 1;
                            counter++;
                        } while ((bitstream & mask) != 0);
                        currentBits = (byte)(counter + 4);
                        tmpByte += counter;
                    }
                }

                LoadIntoBitstream(currentBits);
                smallByteDict[tmpVal2++] = tmpByte;

                if (tmpVal2 == specialInd)
                {
                    byte specialLen = (byte)(bitstream >> 30);
                    LoadIntoBitstream(2);

                    if (specialLen > 0)
                    {
                        Array.Fill(smallByteDict, (byte)0, tmpVal2, specialLen);
                        tmpVal2 += specialLen;
                    }
                }
            }

            if (tmpVal2 < length)
            {
                Array.Fill(smallByteDict, (byte)0, tmpVal2, length - tmpVal2);
            }

            FillWordsUsingBytes(length, smallByteDict, 8, smallWordDict);
        }

        private void FillLargeDicts()
        {
            short tmpVal = (short)(bitstream >> 23);
            LoadIntoBitstream(9);

            if (tmpVal == 0)
            {
                tmpVal = (short)(bitstream >> 23);
                LoadIntoBitstream(9);
                Array.Fill(largeByteDict, (byte)0);

                for (int i = 0; i < 4096; i++)
                {
                    largeWordDict[i] = (ushort)tmpVal;
                }
                return;
            }

            ushort bytes = 0;
            if (tmpVal < 0)
            {
                Array.Fill(largeByteDict, (byte)0);
                FillWordsUsingBytes(510, largeByteDict, 12, largeWordDict);
                return;
            }

            while (bytes < tmpVal)
            {
                ushort tmpLen = (ushort)(bitstream >> 24);
                ushort tmpVal2 = smallWordDict[tmpLen];

                if (tmpVal2 >= 19)
                {
                    uint mask = 0x800000;
                    do
                    {
                        if ((bitstream & mask) == 0)
                        {
                            tmpVal2 = parallelDict0[tmpVal2];
                        }
                        else
                        {
                            tmpVal2 = parallelDict1[tmpVal2];
                        }
                        mask >>= 1;
                    } while (tmpVal2 >= 19);
                }

                byte bits = smallByteDict[tmpVal2];
                LoadIntoBitstream(bits);

                if (tmpVal2 > 2)
                {
                    tmpVal2 -= 2;
                    largeByteDict[bytes++] = (byte)tmpVal2;
                }
                else
                {
                    if (tmpVal2 == 0)
                        tmpLen = 1;
                    else if (tmpVal2 == 1)
                    {
                        tmpVal2 = (ushort)(bitstream >> 28);
                        LoadIntoBitstream(4);
                        tmpLen = (ushort)(tmpVal2 + 3);
                    }
                    else
                    {
                        tmpVal2 = (ushort)(bitstream >> 23);
                        LoadIntoBitstream(9);
                        tmpLen = (ushort)(tmpVal2 + 20);
                    }

                    if (tmpLen > 0)
                    {
                        Array.Fill(largeByteDict, (byte)0, bytes, tmpLen);
                        bytes += tmpLen;
                    }
                }
            }

            if (bytes < 510)
            {
                Array.Fill(largeByteDict, (byte)0, bytes, 510 - bytes);
            }

            FillWordsUsingBytes(510, largeByteDict, 12, largeWordDict);
        }

        private void FillWordsUsingBytes(ushort bytesLen, byte[] bytes, byte pivot, ushort[] words)
        {
            ushort[] srcDict = new ushort[17];
            ushort[] destDict = new ushort[18];
            destDict[1] = 0;

            for (int i = 0; i < bytesLen; i++)
            {
                byte tmp = bytes[i];
                srcDict[tmp]++;
            }

            byte shift = 14;
            int ind = 1;
            ushort low, high;

            while (ind <= 16)
            {
                low = (ushort)(srcDict[ind] << (shift + 1));
                high = (ushort)(srcDict[ind + 1] << shift);
                low += destDict[ind];
                ind += 4;
                high += low;

                destDict[ind - 3] = low;
                destDict[ind - 2] = high;

                low = (ushort)(srcDict[ind - 2] << (shift - 1));
                low += high;
                high = (ushort)(srcDict[ind - 1] << (shift - 2));
                high += low;

                destDict[ind - 1] = low;
                destDict[ind] = high;
                shift -= 4;
            }

            /// This seems to fire sometimes, however I've compared the outputs from the files that do fire here and it doesn't actually seem to be an issue (GAME.DAT - LM2VG)
            if (destDict[17] != 0)
            {
                throw new InvalidOperationException("Bad table");
            }

            shift = (byte)(pivot - 1);
            byte tmpVal = (byte)(16 - pivot);
            byte tmpValCopy = tmpVal;

            for (int i = 1; i <= pivot; i++)
            {
                destDict[i] >>= tmpValCopy;
                srcDict[i] = (ushort)(1 << shift--);
            }

            tmpValCopy--;
            for (int i = pivot + 1; i <= 16; i++)
            {
                srcDict[i] = (ushort)(1 << tmpValCopy--);
            }

            ushort comp1 = (ushort)(destDict[pivot + 1] >> (16 - pivot));
            if (comp1 != 0)
            {
                ushort comp2 = (ushort)(1 << pivot);
                if (comp1 != comp2)
                {
                    for (int i = comp1; i < comp2; i++)
                    {
                        words[i] = 0;
                    }
                }
            }

            if (bytesLen == 0)
            {
                return;
            }

            shift = (byte)(15 - pivot);
            ushort mask = (ushort)(1 << shift);
            ushort bytesLenCopy = bytesLen;

            for (int i = 0; i < bytesLen; i++)
            {
                byte tmpByte = bytes[i];
                if (tmpByte != 0)
                {
                    ushort destVal = destDict[tmpByte];
                    ushort srcVal = (ushort)(srcDict[tmpByte] + destVal);

                    if (tmpByte > pivot)
                    {
                        ushort[] dictPtr = words;
                        ushort tmpOffs = (ushort)(destVal >> tmpVal);
                        byte newLen = (byte)(tmpByte - pivot);

                        while (newLen > 0)
                        {
                            if (dictPtr[tmpOffs] == 0)
                            {
                                parallelDict0[bytesLenCopy] = 0;
                                parallelDict1[bytesLenCopy] = 0;
                                dictPtr[tmpOffs] = bytesLenCopy++;
                            }

                            tmpOffs = dictPtr[tmpOffs];

                            if ((destVal & mask) == 0)
                            {
                                dictPtr = parallelDict0;
                            }
                            else
                            {
                                dictPtr = parallelDict1;
                            }

                            destVal <<= 1;
                            newLen--;
                        }

                        dictPtr[tmpOffs] = (ushort)i;
                    }
                    else if (destVal < srcVal)
                    {
                        for (int j = destVal; j < srcVal; j++)
                        {
                            words[j] = (ushort)i;
                        }
                    }

                    destDict[tmpByte] = srcVal;
                }
            }
        }
    }
}
