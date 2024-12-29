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
        [DllImport(@"oo2core_8_win64.dll")]
        private static extern int OodleLZ_Decompress(byte[] buffer, long bufferSize, byte[] outputBuffer, long outputBufferSize,
            uint a, uint b, ulong c, uint d, uint e, uint f, uint g, uint h, uint i, uint threadModule);

        public static int Oodle(byte[] compressedData, int compressedSize, byte[] decompressedData, int decompressedSize)
        {
            return OodleLZ_Decompress(compressedData, compressedSize, decompressedData, decompressedSize, 0, 0, 0, 0, 0, 0, 0, 0, 0, 3);
        }

        public static int ZIPX(byte[] compressedData, int compressedSize, byte[] decompressedData, int decompressedSize)
        {
            RC4.Apply(compressedData, BitConverter.GetBytes(compressedSize), compressedSize, decompressedData);
            return decompressedSize;
        }

        public static int LZ2K(byte[] compressedData, int compressedSize, byte[] decompressedData, int decompressedSize)
        {
            MemoryStream compressedStream = new MemoryStream(compressedData, 0, compressedSize);
            MemoryStream decompressedStream = new MemoryStream(decompressedSize);
            int output = (int)Decompressors.LZ2K.Unlz2k(compressedStream, compressedSize, decompressedStream, decompressedSize);
            decompressedStream.Position = 0;
            decompressedStream.Read(decompressedData, 0, output);
            return output;
        }
    }
}
