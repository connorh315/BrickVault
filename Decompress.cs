using BrickVault.Decompressors;
using LzhamWrapper;
using LzhamWrapper.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace BrickVault
{
    internal class Decompress
    {
        static Dictionary<string, DecompressorPool> pools = new();

        static Decompress()
        {
            pools.Add("DFLT", new DecompressorPool());
            pools.Add("LZ2K", new DecompressorPool());

            if (File.Exists("oo2core_8_win64.dll") && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                CanDecompressOodle = true;
            }
        }

        public static bool CanDecompressOodle { get; private set; } = false;

        [DllImport(@"oo2core_8_win64.dll")]
        private static extern int OodleLZ_Decompress(byte[] buffer, long bufferSize, byte[] outputBuffer, long outputBufferSize,
            uint a, uint b, ulong c, uint d, uint e, uint f, uint g, uint h, uint i, uint threadModule);

        public static int Oodle(byte[] compressedData, int compressedSize, byte[] decompressedData, int decompressedSize)
        { // Entirely stateful - Cannot parallelise
            if (!CanDecompressOodle)
                throw new Exception("Could not extract: oo2core_8_win64.dll file missing or platform is not Windows!");

            return OodleLZ_Decompress(compressedData, compressedSize, decompressedData, decompressedSize, 0, 0, 0, 0, 0, 0, 0, 0, 0, 3);
        }

        public static int LZHAM(byte[] compressedData, int compressedSize, byte[] decompressedData, int decompressedSize)
        {
            DecompressionParameters parameters = new DecompressionParameters();
            int i = 0;
            int j = 0;
            int z = 0;

            parameters.Flags |= LzhamWrapper.Enums.DecompressionFlag.ReadZlibStream;
            parameters.UpdateRate = LzhamWrapper.Enums.TableUpdateRate.Default;
            parameters.DictionarySize = 15;

            var handle = LzhamInterop.DecompressInit(parameters);

            int totalWritten = 0;
            int writeOffset = 0;

            int inputRemaining = compressedSize;
            int readOffset = 0;

            do
            {
                DecompressStatus decompressionStatus;
                do
                {
                    int remaining = inputRemaining;
                    int outSize = decompressedSize - totalWritten;
                    decompressionStatus = LzhamInterop.Decompress(handle, compressedData, ref remaining, readOffset, decompressedData, ref outSize, writeOffset, true);
                    if (!(decompressionStatus == DecompressStatus.HasMoreOutput
                          || decompressionStatus == DecompressStatus.NeedsMoreInput
                          || decompressionStatus == DecompressStatus.NotFinished
                          || (decompressionStatus == DecompressStatus.Success))
                        )
                    {
                        throw new InvalidOperationException($"Unexpected Decompress Status Code {decompressionStatus}");
                    }
                    int written = outSize;
                    int read = remaining;
                    totalWritten += written;
                    writeOffset += written;
                    inputRemaining = inputRemaining - read;
                    readOffset += read;
                } while (decompressionStatus == DecompressStatus.HasMoreOutput && totalWritten < decompressedSize);
            } while (totalWritten < decompressedSize && inputRemaining > 0);

            return totalWritten;

            //int usableComp = compressedSize;
            //int usableDecomp = decompressedSize;

            //MemoryStream input = new MemoryStream(compressedData, 0, compressedSize);

            //LzhamStream s = new LzhamStream(input, parameters);

            //Stream output = File.OpenWrite(@"A:\output");

            //s.CopyTo(output);

            //var handle = LzhamInterop.DecompressInit(parameters);
            //LzhamInterop.Decompress(handle, compressedData, ref usableComp, 0, decompressedData, ref usableDecomp, 0, true);

            //File.WriteAllBytes(@"A:\output", decompressedData);

            //for (i = 0; ; i++)
            //{
            //    parameters.Flags = 0;
            //    if (i == 0) { }
            //    else if (i == 1)
            //        parameters.Flags |= LzhamWrapper.Enums.DecompressionFlag.ReadZlibStream;
            //    else break;

            //    for (j = LzhamConsts.MaxDictionarySizeLog2X86; j >= LzhamConsts.MinDictionarySizeLog2; j--)
            //    {
            //        parameters.DictionarySize = (uint)j;

            //        for (z = 0; ; z++)
            //        {
            //            if (z == 0) parameters.UpdateRate = LzhamWrapper.Enums.TableUpdateRate.Default;
            //            else if (z == 1) parameters.UpdateRate = LzhamWrapper.Enums.TableUpdateRate.Fastest;
            //            else if (z == 2) parameters.UpdateRate = LzhamWrapper.Enums.TableUpdateRate.Slowest;
            //            else break;

            //            int usableComp = compressedSize;
            //            int usableDecomp = decompressedSize;

            //            Array.Clear(decompressedData);

            //            var handle = LzhamInterop.DecompressInit(parameters);
            //            LzhamInterop.Decompress(handle, compressedData, ref usableComp, 0, decompressedData, ref usableDecomp, 0, true);

            //            if (decompressedData[0] != 0)
            //            {
            //                File.WriteAllBytes(@"A:\output", decompressedData);
            //                Console.WriteLine();
            //            }

            //            if (usableDecomp > 0)
            //            {
            //                Console.WriteLine();
            //            }
            //        }
            //    }
            //}

            //int outBufSize = 0;
            //uint adler32 = 0;
            //var test = new LzhamStream(new MemoryStream(compressedData, 0, compressedSize), parameters);
            //Lzham.DecompressMemory();
            //test.CopyTo(new MemoryStream(decompressedSize));
            return decompressedSize;
        }

        public static int ZIPX(byte[] compressedData, int compressedSize, byte[] decompressedData, int decompressedSize)
        { // Stateless setup, not really much of a need to create a pool for.
            RC4.Apply(compressedData, BitConverter.GetBytes(compressedSize), compressedSize, decompressedData);
            return decompressedSize;
        }

        public static int RFPK(byte[] compressedData, int compressedSize, byte[] decompressedData, int decompressedSize)
        { // Stateless setup, not really much of a need to create a pool for.
            RefPack.Decompress(compressedData, compressedSize, decompressedData, decompressedSize);
            return decompressedSize;
        }

        public static int LZ2K(byte[] compressedData, int compressedSize, byte[] decompressedData, int decompressedSize)
        {
            var decompressor = pools["LZ2K"].Rent();
            if (decompressor == null) 
                decompressor = new LZ2K();

            int output = decompressor.Decompress(compressedData, compressedSize, decompressedData, decompressedSize);
            pools["LZ2K"].Return(decompressor);
            //int output = (int)Decompressors.LZ2K.Unlz2k(compressedData, compressedSize, decompressedData, decompressedSize);
            return output;
        }

        public static int DFLT(byte[] compressedData, int compressedSize, byte[] decompressedData, int decompressedSize)
        {
            var decompressor = pools["DFLT"].Rent();
            if (decompressor == null)
                decompressor = new DFLT();

            int output = decompressor.Decompress(compressedData, compressedSize, decompressedData, decompressedSize);
            pools["DFLT"].Return(decompressor);
            //int output = (int)Decompressors.DFLT.UnDFLT(compressedData, compressedSize, decompressedData, decompressedSize);
            return output;
        }

        public static int RNC(byte[] compressedData, int compressedSize, byte[] decompressedData, int decompressedSize)
        {
            Stream input = new MemoryStream();
            input.Write(new byte[] { 0x52, 0x4e, 0x43, 0x02 });
            byte[] comp = BitConverter.GetBytes(compressedSize - 6);
            byte[] decomp = BitConverter.GetBytes(decompressedSize);

            Array.Reverse(comp);
            Array.Reverse(decomp);

            input.Write(decomp);
            input.Write(comp);
            input.Write(compressedData, 0, compressedSize);
            byte[] chunk = new byte[12 + compressedSize];
            input.Position = 0;
            input.Read(chunk);
            //Stream output = new MemoryStream();
            //byte[] output = new RncDecoder().Decompress(chunk);
            //var result = Rnc.ReadRnc(input, output);
            //if (result != RncStatus.Ok) return -1;
            //output.Position = 0;
            //output.Read(decompressedData, 0, decompressedSize);
            return decompressedSize;
        }
    }
}
