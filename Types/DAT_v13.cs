namespace BrickVault.Types
{
    internal class DAT_v13 : DAT_v12
    {
        public override uint Version() => 13;

        public DAT_v13(RawFile file, long trailerOffset, uint trailerSize) : base(file, trailerOffset, trailerSize)
        {

        }

        internal override void Read()
        {
            file.Seek(trailerOffset + 16, SeekOrigin.Begin);

            uint minorVersion = file.ReadUInt(true);
            uint fileCount = file.ReadUInt(true);

            uint segmentsCount = file.ReadUInt(true);
            uint segmentsSize = file.ReadUInt(true);

            long segmentsOffset = trailerOffset + 32;

            file.Seek(segmentsOffset + segmentsSize + 4, SeekOrigin.Begin);

            string[] folders = new string[segmentsCount];
            string[] paths = new string[fileCount];

            int nodeId = 0;

            for (int i = 0; i < segmentsCount; i++)
            {
                int nameOffset = file.ReadInt(true);
                ushort folderId = file.ReadUShort(true);

                ushort orderId = 0;
                if (minorVersion >= 2)
                {
                    orderId = file.ReadUShort(true);
                }

                short someId = file.ReadShort(true);
                short fileId = file.ReadShort(true);

                long previousPosition = file.Position;

                if (nameOffset != -1)
                {
                    file.Seek(segmentsOffset + nameOffset, SeekOrigin.Begin);
                    string segment = file.ReadNullString();
                    if (i == segmentsCount - 1)
                    {
                        fileId = 1;
                    }

                    string pathName = folders[folderId] + "\\" + segment;

                    if (fileId != 0)
                    {
                        paths[orderId] = pathName;
                        nodeId++;
                    }
                    else
                    {
                        folders[i] = pathName;
                    }
                }

                file.Seek(previousPosition, SeekOrigin.Begin);
            }

            file.Seek(4, SeekOrigin.Current); // archive version repeat
            file.Seek(4, SeekOrigin.Current); // file count repeat

            Files = new ArchiveFile[fileCount];

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
                compressionType = fileOffset;
                compressionType >>= 56;
                fileOffset &= 0xffffffffffffff;

                Files[i] = new ArchiveFile
                {
                    Offset = fileOffset,
                    CompressedSize = compressedSize,
                    DecompressedSize = decompressedSize,
                    CompressionType = (uint)compressionType,
                    Path = paths[i]
                };
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

            //using (var output = new StreamWriter(@"A:\og.txt"))
            //{
            //    foreach (var file in Files)
            //    {
            //        output.WriteLine(file.Path);
            //    }
            //}
        }
    }
}
