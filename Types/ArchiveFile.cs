using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BrickVault.Types
{
    public class ArchiveFile
    {
        public long Offset;
        public uint CompressedSize;
        public uint DecompressedSize;
        public uint CompressionType;

        public string Path;
    }
}
