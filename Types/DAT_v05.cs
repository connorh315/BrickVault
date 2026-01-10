using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace BrickVault.Types
{
    internal class DAT_v05 : DATFile
    {
        public override DATVersion Version => DATVersion.V5;

        public DAT_v05(RawFile file, long trailerOffset, uint trailerSize) : base(file, trailerOffset, trailerSize)
        {

        }

        internal override void Read(RawFile file)
        {
            file.Seek(4, SeekOrigin.Begin);

            uint fileCount = file.ReadUInt();

            Files = new NewArchiveFile[fileCount];

            for (int i = 0; i < fileCount; i++)
            {
                var archiveFile = new NewArchiveFile();

                long fileOffset = file.ReadUInt();

                uint compressedSize = file.ReadUInt();
                uint decompressedSize = file.ReadUInt();

                uint compressionType = file.ReadUInt();

                decompressedSize &= 0x7fffffff;
                fileOffset = (fileOffset << 8) + (compressionType >> 24);

                compressionType &= 0xff;

                archiveFile.SetFileData(fileOffset, compressedSize, decompressedSize, (byte)compressionType);

                Files[i] = archiveFile;
            }

            uint pathsCount = file.ReadUInt();
            FileTree = new FileTree(pathsCount);
            long pathsOffset = file.Position + (pathsCount * 12) + 4; // 12 bytes for each path "entry", +4 for the size of the names block

            for (int i = 0; i < pathsCount; i++)
            {
                var node = new FileTreeNode();
                node.FileTree = FileTree;
                FileTree.Nodes[i] = node;

                short read = file.ReadShort(); // This actually represents one of two things, if +ve then it is not a complete path and the value is the next segment, if -ve then the unsigned value represents the fileIndex, useful when the actual fileIndex has not been populated.
                node.FinalChild = (ushort)Math.Max((short)0, read);
                node.PreviousSibling = file.ReadUShort();
                int segOffset = file.ReadInt();
                node.ParentIndex = file.ReadUShort(); 
                node.FileIndex = file.ReadUShort();

                if (read <= 0)
                { // Fix up. Ridiculous file version (read note below)
                    node.FileIndex = (ushort)Math.Abs(read);
                    var archiveFile = ((NewArchiveFile)Files[node.FileIndex]);
                    archiveFile.Node = node;
                    node.File = archiveFile;
                }
                else
                {
                    node.FileIndex = 0;
                }


                string segmentName = "";
                if (segOffset >= 0)
                {
                    long originalLocation = file.Position;
                    file.Seek(pathsOffset + segOffset, SeekOrigin.Begin);
                    segmentName = file.ReadNullString();
                    file.Seek(originalLocation, SeekOrigin.Begin);
                    node.Segment = segmentName;
                }
            }

            canUseQuickLookup = canUseQuickLookup & !nonsenseQuickLookup;

            string[] constructionPaths = new string[segments.Length];

            List<string> test = new List<string>();

            string filePath = "";
            for (int i = 1; i < segments.Length; i++) // i = 0 is just root directory
            {
                SegmentData seg = segments[i];
                if (canUseQuickLookup)
                {
                    constructionPaths[i] = constructionPaths[seg.quickParentIndex] + seg.segment + (seg.nextIndex > 0 ? '\\' : "");

                    if (seg.nextIndex <= 0)
                    { // Sometimes fileIndex is not populated (i.e. LJW_PC_GAME0) 
                        Files[Math.Abs(seg.nextIndex)].Path = '\\' + constructionPaths[i];
                    }
                }
                else
                {
                    if (seg.previousIndex != 0)
                    {
                        filePath = test[seg.previousIndex - 1];
                    }

                    test.Add(filePath);

                    filePath += '\\' + seg.segment;

                    if (seg.nextIndex <= 0)
                    {
                        Files[Math.Abs(seg.nextIndex)].Path = filePath;
                    }

                }
            }
        }
    }
}
