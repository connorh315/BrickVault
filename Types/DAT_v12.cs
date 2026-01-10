namespace BrickVault.Types
{
    internal class DAT_v12 : DAT_v11
    {
        public override DATVersion Version => DATVersion.V12;

        public DAT_v12(RawFile file, long trailerOffset, uint trailerSize) : base(file, trailerOffset, trailerSize) 
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

            file.Seek(segmentsOffset + segmentsSize, SeekOrigin.Begin);

            FileTree = new FileTree(segmentsCount);
            Files = new NewArchiveFile[fileCount];

            for (int i = 0; i < segmentsCount; i++)
            {
                var archiveFile = new NewArchiveFile();
                var node = new FileTreeNode();
                node.FileTree = FileTree;
                FileTree.Nodes[i] = node;

                node.FinalChild = file.ReadUShort(true);
                node.PreviousSibling = file.ReadUShort(true);

                int nameOffset = file.ReadInt(true);

                node.ParentIndex = file.ReadUShort(true);
                node.FileIndex = file.ReadUShort(true);

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
                    //if (node.Parent == null)
                    //    node.PathCRC = CRC_FNV_OFFSET_64;
                    //else
                    //    node.PathCRC = CalculateSegmentCRC(node.Segment, node.Parent!.PathCRC);
                }

                file.Seek(previousPosition, SeekOrigin.Begin);
            }

            FileTree.Root = FileTree.Nodes[0];

            file.Seek(4, SeekOrigin.Current); // padding
            file.Seek(4, SeekOrigin.Current); // archive version repeat
            file.Seek(4, SeekOrigin.Current); // file count repeat

            for (int i = 0; i < fileCount; i++)
            {
                long fileOffset;
                if (minorVersion < 2)
                {
                    fileOffset = file.ReadUInt(true);
                }
                else
                {
                    fileOffset = file.ReadLong(true);
                }

                uint compressedSize = file.ReadUInt(true);
                uint decompressedSize = file.ReadUInt(true);

                long compressionType = 0;
                compressionType = decompressedSize;
                decompressedSize &= 0x7fffffff;
                compressionType >>= 31;

                Files[i].SetFileData(fileOffset, compressedSize, decompressedSize, (byte)compressionType);
            }

            //long[] crcs = new long[fileCount];
            //Dictionary<long, int> crcLookup = new Dictionary<long, int>();

            //for (int i = 0; i < fileCount; i++)
            //{
            //    long val = file.ReadLong(true);
            //    crcs[i] = val;
            //    crcLookup[val] = i;
            //}


            //for (int i = 0; i < fileCount; i++)
            //{
            //    string path = paths[i];

            //    long crc = CRC_FNV_OFFSET_64;
            //    foreach (char character in path.Substring(1).ToUpper())
            //    {
            //        crc ^= character;
            //        crc *= CRC_FNV_PRIME_64;
            //    }

            //    if (crcLookup.ContainsKey(crc))
            //    {
            //        int index = crcLookup[crc];
            //        Files[index].Path = path;
            //    }
            //    else
            //    {
            //        Console.WriteLine("Could not find CRC for file: {0}", path);
            //    }
            //}
        }
    }
}
