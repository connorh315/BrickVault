using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BrickVault.Types
{
    internal class DAT_v08 : DATFile
    {
        public override DATVersion Version => DATVersion.V8;

        public DAT_v08(RawFile file, long trailerOffset, uint trailerSize) : base(file, trailerOffset, trailerSize)
        {

        }

        internal override void Read(RawFile file)
        {
            file.Seek(16, SeekOrigin.Begin);

            uint minorVersion = file.ReadUInt(true);
            uint fileCount = file.ReadUInt(true);

            uint segmentsCount = file.ReadUInt(true);
            uint segmentsSize = file.ReadUInt(true);

            long segmentsOffset = 32;

            file.Seek(segmentsOffset + segmentsSize + 4, SeekOrigin.Begin);

            file.Seek(segmentsOffset + segmentsSize, SeekOrigin.Begin);

            FileTree = new FileTree(segmentsCount);
            Files = new NewArchiveFile[fileCount];

            for (int i = 0; i < segmentsCount; i++)
            {
                var archiveFile = new NewArchiveFile();
                var node = new FileTreeNode();
                node.FileTree = FileTree;
                FileTree.Nodes[i] = node;

                short identifier = file.ReadShort(true);
                //node.FinalChild = file.ReadUShort(true);
                node.PreviousSibling = file.ReadUShort(true);

                int nameOffset = file.ReadInt(true);

                node.ParentIndex = file.ReadUShort(true);
                if (minorVersion >= 2)
                {
                    node.FileIndex = file.ReadUShort(true);
                    if (identifier >= 0)
                    {
                        node.FinalChild = (ushort)identifier;
                    }
                    //Console.WriteLine($"{identifier} - {node.PreviousSibling} - {nameOffset} - {node.ParentIndex} - {node.FileIndex}");
                }
                else
                {
                    if (identifier < 0)
                    {
                        node.FileIndex = (ushort)Math.Abs(identifier);
                    }
                    else
                    {
                        node.FinalChild = (ushort)identifier;
                    }
                }


                long previousPosition = file.Position;

                if (nameOffset != -1)
                {
                    file.Seek(segmentsOffset + nameOffset, SeekOrigin.Begin);
                    node.Segment = file.ReadNullString();
                }

                if (node.FinalChild == 0 || node.FileIndex != 0)
                {
                    archiveFile.Node = node;
                    node.File = archiveFile;
                    Files[node.FileIndex] = archiveFile;
                    if (node.Parent == null)
                        node.PathCRC = CRC_FNV_OFFSET_64;
                    else
                        node.PathCRC = CalculateSegmentCRC(node.Segment, node.Parent!.PathCRC);
                }

                file.Seek(previousPosition, SeekOrigin.Begin);
            }

            FileTree.Root = FileTree.Nodes[0];

            file.Seek(4, SeekOrigin.Current); // 0 padding
            file.Seek(4, SeekOrigin.Current); // archive version
            file.Seek(4, SeekOrigin.Current); // file count

            Console.WriteLine();

            //Files = new ArchiveFile[fileCount];

            for (int i = 0; i < fileCount; i++)
            {
                long fileOffset = file.ReadUInt(true);

                uint compressedSize = file.ReadUInt(true);
                uint decompressedSize = file.ReadUInt(true);

                uint compressionType = file.ReadUInt(true);

                decompressedSize &= 0x7fffffff;
                fileOffset = (fileOffset << 8) + (compressionType & 0x00ffffff);

                compressionType >>= 24;

                Files[i].SetFileData(fileOffset, compressedSize, decompressedSize, (byte)compressionType);

                //Files[i] = new ArchiveFile
                //{
                //    Offset = fileOffset,
                //    CompressedSize = compressedSize,
                //    DecompressedSize = decompressedSize,
                //    CompressionType = (uint)compressionType
                //};
            }

            //string[] folders = new string[segmentsCount];
            //string[] paths = new string[segmentsCount];

            //int nodeId = 0;

            //for (int i = 0; i < segmentsCount; i++)
            //{
            //    int nameOffset = file.ReadInt(true);
            //    ushort folderId = file.ReadUShort(true);

            //    short orderId = 0;
            //    if (minorVersion >= 2)
            //    {
            //        orderId = file.ReadShort(true);
            //    }

            //    short unkId = file.ReadShort(true);
            //    short fileId = file.ReadShort(true);

            //    long previousPosition = file.Position;

            //    if (nameOffset != -1)
            //    {
            //        file.Seek(segmentsOffset + nameOffset, SeekOrigin.Begin);
            //        string segment = file.ReadNullString();
            //        if (i == segmentsCount - 1)
            //        {
            //            fileId = 1;
            //        }

            //        string pathName = folders[folderId] + "\\" + segment;

            //        if (fileId != 0)
            //        {
            //            paths[nodeId] = pathName;
            //            nodeId++;
            //        }
            //        else
            //        {
            //            folders[i] = pathName;
            //        }
            //    }

            //    file.Seek(previousPosition, SeekOrigin.Begin);
            //}

            //file.Seek(4, SeekOrigin.Current); // archive version repeat
            //file.Seek(4, SeekOrigin.Current); // file count repeat



            //long[] crcs = new long[fileCount];
            //Dictionary<long, int> crcLookup = new Dictionary<long, int>();

            //for (int i = 0; i < fileCount; i++)
            //{
            //    uint val = file.ReadUInt(true);
            //    crcs[i] = val;
            //    crcLookup[val] = i;
            //}

            //Dictionary<string, uint> collisions = new();

            //uint collisionCount = file.ReadUInt(true);
            //file.Seek(4, SeekOrigin.Current); // Size of CRC collision block

            //List<string> collisionsCaught = new();

            //for (int i = 0; i < collisionCount; i++)
            //{
            //    string overridePath = file.ReadNullString();
            //    collisionsCaught.Add(overridePath);
            //    if (overridePath.Length % 2 == 0) file.ReadByte(); // string is aligned to a 2-byte boundary, so the original parser likely read 2-bytes at a time and then checked if either of them were zero, so there's one byte of padding if the string is even-length
            //    short id = file.ReadShort(true);
            //    Files[i].Path = overridePath;
            //}

            //for (int i = 0; i < fileCount; i++)
            //{
            //    string path = paths[i];

            //    uint crc = CRC_FNV_OFFSET_32;
            //    foreach (char character in path.Substring(1).ToUpper())
            //    {
            //        crc ^= character;
            //        crc *= CRC_FNV_PRIME_32;
            //    }

            //    if (crcLookup.ContainsKey(crc))
            //    {
            //        int index = crcLookup[crc];
            //        Files[index].Path = path;
            //    }
            //    else if (!collisionsCaught.Contains(path.Substring(1)))
            //    {
            //        Console.WriteLine("Could not find CRC for file: {0}", path);
            //    }
            //}

            //for (int i = 0; i < fileCount; i++)
            //{
            //    if (Files[i].Path == null)
            //    {
            //        Console.WriteLine($"Id: {i} empty");
            //    }
            //}
        }
    }
}
