using BrickVault.Decompressors;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BrickVault
{
    public class DecompressorPool
    {
        private readonly ConcurrentBag<Decompressor> pool = new();

        public Decompressor? Rent() => pool.TryTake(out var result) ? result : null;

        public void Return(Decompressor decompressor)
        {
            decompressor.Reset();

            pool.Add(decompressor);
        }
    }
}
