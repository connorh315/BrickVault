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
    internal class DAT_v03 : DATFile
    {
        public override DATVersion Version => DATVersion.V3;

        public DAT_v03(RawFile file, long trailerOffset, uint trailerSize) : base(file, trailerOffset, trailerSize)
        {

        }

        private static uint CalculateCRC(FileTreeNode node)
        {
            uint CRC_FNV_OFFSET_32 = 0x811C9DC5;
            uint CRC_FNV_PRIME_32 = 0x199933;

            uint crc = CRC_FNV_OFFSET_32;
            foreach (char character in node.Path.ToUpper())
            {
                crc ^= character;
                crc *= CRC_FNV_PRIME_32;
            }

            return crc;
        }

        private static int FindCRC(uint[] crcs, uint target)
        {
            for (int i = 0; i < crcs.Length; i++)
            {
                if (crcs[i] == target) return i;
            }

            return -1;

            // LB1 doesn't store them in order...
            //int left = 0;
            //int right = crcs.Length - 1;
            //while (left <= right)
            //{
            //    int mid = left + (right - left) / 2;
            //    if (crcs[mid] == target)
            //    {
            //        return mid;
            //    }
            //    else if (crcs[mid] < target)
            //    {
            //        left = mid + 1;
            //    }
            //    else
            //    {
            //        right = mid - 1;
            //    }
            //}
            //return -1; // Not found
        }

        internal override void Read(RawFile file)
        {
            file.Seek(4, SeekOrigin.Begin);

            uint fileCount = file.ReadUInt();

            Files = new ArchiveFile[fileCount];

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
            long pathsOffset = file.Position + (pathsCount * 8) + 4; // 12 bytes for each path "entry", +4 for the size of the names block

            Dictionary<uint, FileTreeNode> nodeLookup = new();

            for (int i = 0; i < pathsCount; i++)
            {
                var node = new FileTreeNode();
                node.FileTree = FileTree;
                FileTree.Nodes[i] = node;

                short read = file.ReadShort(); // This actually represents one of two things, if +ve then it is not a complete path and the value is the next segment, if -ve then the unsigned value represents the fileIndex, useful when the actual fileIndex has not been populated.
                node.FinalChild = (ushort)Math.Max((short)0, read);
                node.PreviousSibling = file.ReadUShort();
                int segOffset = file.ReadInt();

                if (read <= 0)
                {
                    // TCS just store the order that the files were added to the archive here, rather than the correct index into the file table.
                    //node.FileIndex = (ushort)(Math.Abs(read));
                    //var archiveFile = ((NewArchiveFile)Files[node.FileIndex]);
                    //archiveFile.Node = node;
                    //node.File = archiveFile;
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



            file.Seek(pathsOffset - 4, SeekOrigin.Begin);
            int namesBlockSize = file.ReadInt();
            file.Seek(namesBlockSize, SeekOrigin.Current);

            uint[] crcs = new uint[fileCount];
            for (int i = 0; i < fileCount; i++)
            {
                crcs[i] = file.ReadUInt();
            }

            // Parent Index + File Index not explicitly stored in version < 4. So needs to be calculated.
            Queue<ushort> nodes = new();
            nodes.Enqueue(0);


            while (nodes.Count != 0)
            {
                ushort parentIndex = nodes.Dequeue();
                FileTreeNode parent = FileTree.Nodes[parentIndex];

                ushort childIndex = parent.FinalChild;

                while (childIndex != 0)
                {
                    FileTreeNode child = FileTree.Nodes[childIndex];
                    child.ParentIndex = parentIndex;
                    nodes.Enqueue(childIndex);

                    childIndex = child.PreviousSibling;
                }

                if (parent.FinalChild == 0)
                { // See note above on TCS.
                    uint crc = CalculateCRC(parent);
                    int fileIndex = FindCRC(crcs, crc);
                    if (fileIndex >= 0)
                    {
                        var archiveFile = ((NewArchiveFile)Files[fileIndex]);
                        archiveFile.Node = parent;
                        parent.FileIndex = (ushort)fileIndex;
                        parent.File = archiveFile;
                    }
                    else
                    {
                        throw new Exception($"Could not find file index {parent.Path} in archive!");
                    }
                }
            }

            FileTree.Root = FileTree.Nodes[0];
        }
    }
}
