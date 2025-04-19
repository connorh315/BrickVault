using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace BrickVault.Types
{
    internal class DAT_v07 : DATFile
    {
        public override uint Version() => 7;

        public DAT_v07(RawFile file, uint trailerOffset, uint trailerSize) : base(file, trailerOffset, trailerSize)
        {

        }

        struct SegmentData
        {
            public short previousIndex = 0;
            public short nextIndex = 0;
            public string segment = "";
            public short quickParentIndex;
            public short fileIndex;

            public SegmentData() { }
        }

        internal override void Read()
        {
            file.Seek(trailerOffset + 4, SeekOrigin.Begin);

            uint fileCount = file.ReadUInt();

            Files = new ArchiveFile[fileCount];

            for (int i = 0; i < fileCount; i++)
            {
                long fileOffset = file.ReadUInt();

                uint compressedSize = file.ReadUInt();
                uint decompressedSize = file.ReadUInt();

                uint compressionType = file.ReadUInt();

                decompressedSize &= 0x7fffffff;
                fileOffset = (fileOffset << 8) + (compressionType >> 24);

                compressionType &= 0xff;

                Files[i] = new ArchiveFile
                {
                    Offset = fileOffset,
                    CompressedSize = compressedSize,
                    DecompressedSize = decompressedSize,
                    CompressionType = (uint)compressionType
                };
            }

            uint pathsCount = file.ReadUInt();

            SegmentData[] segments = new SegmentData[pathsCount];

            long pathsOffset = file.Position + (pathsCount * 12) + 4; // 12 bytes for each path "entry", +4 for the size of the names block

            for (int i = 0; i < pathsCount; i++)
            {
                short next = file.ReadShort(); // This actually represents one of two things, if +ve then it is not a complete path and the value is the next segment, if -ve then the unsigned value represents the fileIndex, useful when the actual fileIndex has not been populated.
                short prev = file.ReadShort();
                int segOffset = file.ReadInt();
                short quickParentIndex = file.ReadShort();
                short fileIndex = file.ReadShort(); // Sometimes exists (LDI_WIIU), sometimes doesn't (LJW_PC_GAME0-...)

                string segmentName = "";
                if (segOffset >= 0)
                {
                    long originalLocation = file.Position;
                    file.Seek(pathsOffset + segOffset, SeekOrigin.Begin);
                    segmentName = file.ReadNullString();
                    file.Seek(originalLocation, SeekOrigin.Begin);
                }

                segments[i] = new SegmentData
                {
                    nextIndex = next,
                    previousIndex = prev,
                    segment = segmentName,
                    quickParentIndex = quickParentIndex,
                    fileIndex = fileIndex
                };
            }

            string[] constructionPaths = new string[segments.Length];

            string[] fullPaths = new string[fileCount];

            for (int i = 1; i < segments.Length; i++) // i = 0 is just root directory
            {
                SegmentData seg = segments[i];
                constructionPaths[i] = constructionPaths[seg.quickParentIndex] + seg.segment + (seg.nextIndex > 0 ? '\\' : "");

                if (seg.nextIndex <= 0)
                { // Sometimes fileIndex is not populated (i.e. LJW_PC_GAME0) 
                    Files[Math.Abs(seg.nextIndex)].Path = '\\' + constructionPaths[i];
                }
            }
        }
    }
}
