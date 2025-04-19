using BrickVault.Decompressors;
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
        }

        [DllImport(@"oo2core_8_win64.dll")]
        private static extern int OodleLZ_Decompress(byte[] buffer, long bufferSize, byte[] outputBuffer, long outputBufferSize,
            uint a, uint b, ulong c, uint d, uint e, uint f, uint g, uint h, uint i, uint threadModule);

        public static int Oodle(byte[] compressedData, int compressedSize, byte[] decompressedData, int decompressedSize)
        { // Entirely stateful - Cannot parallelise
            return OodleLZ_Decompress(compressedData, compressedSize, decompressedData, decompressedSize, 0, 0, 0, 0, 0, 0, 0, 0, 0, 3);
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
    }
}
