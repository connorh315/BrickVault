using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BrickVault.Decompressors
{
    public abstract class Decompressor
    {
        /// <summary>
        /// Called by a thread to decompress using the given decompressor
        /// </summary>
        /// <param name="input">Compressed bytes</param>
        /// <param name="inputLength">Compressed byte length (NOTE: May not be input.Length as large buffers may be reused)</param>
        /// <param name="output">Output location</param>
        /// <param name="outputLength">Output length (NOTE: May not be output.Length as large buffers may be reused)</param>
        /// <returns></returns>
        public abstract int Decompress(byte[] input, int inputLength, byte[] output, int outputLength);

        /// <summary>
        /// Resets the decompressor state to be reused.
        /// </summary>
        public virtual void Reset() { }
    }
}
