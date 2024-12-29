using BrickVault.Decompressors;

namespace BrickVault.Types
{
    public abstract class DATFile
    {
        internal RawFile file { get; set; }

        public ArchiveFile[] Files { get; set; }

        internal uint trailerOffset;
        internal uint trailerSize;

        internal DATFile(RawFile file, uint trailerOffset, uint trailerSize) 
        { 
            this.file = file; 
            this.trailerOffset = trailerOffset;
            this.trailerSize = trailerSize;

            Read();
        }

        internal byte[] compressedShare = new byte[131072];
        internal byte[] decompressedShare = new byte[524288];

        public static DATFile Open(string fileLocation)
        {
            RawFile file = new RawFile(File.Open(fileLocation, FileMode.Open));

            uint trailerOffset = file.ReadUInt();
            if ((trailerOffset & 0x80000000) != 0)
            {
                trailerOffset ^= 0xffffffff;
                trailerOffset <<= 8;
                trailerOffset += 0x100;
            }

            uint trailerSize = file.ReadUInt();

            file.Seek(trailerOffset + 4, SeekOrigin.Begin); // 4 bytes being skipped over here is just trailer length again

            int magicBytes1 = file.ReadInt();
            uint magicBytes2 = file.ReadUInt();

            uint datVersion, fileCount;

            if (magicBytes2 != 0x3443432e && magicBytes2 != 0x2e434334 && magicBytes1 != 0x3443432e && magicBytes1 != 0x2e434334)
            {
                datVersion = (uint)Math.Abs(magicBytes1);
            }
            else
            {
                datVersion = (uint)Math.Abs(file.ReadInt(true));
            }

            Console.WriteLine(trailerOffset);

            DATFile result = null;

            switch (datVersion)
            {
                case 12:
                    return new DAT_v12(file, trailerOffset, trailerSize);
                case 13:
                    return new DAT_v13(file, trailerOffset, trailerSize);
            }

            return result;
        }

        internal abstract void Read();

        public virtual void ExtractFile(ArchiveFile extract, Stream write)
        {
            file.Seek(extract.Offset, SeekOrigin.Begin);

            if (extract.DecompressedSize != extract.CompressedSize)
            {
                int totalDecompressed = 0;
                while (totalDecompressed < extract.DecompressedSize)
                {
                    string comType = file.ReadString(4);
                    int compressedSize = file.ReadInt();
                    int decompressedSize = file.ReadInt();
                    file.ReadInto(compressedShare, compressedSize);
                    switch (comType)
                    {
                        case "OODL":
                            Decompress.Oodle(compressedShare, compressedSize, decompressedShare, decompressedSize);
                            break;
                        case "ZIPX":
                            Decompress.ZIPX(compressedShare, compressedSize, decompressedShare, decompressedSize);
                            break;
                        case "LZ2K": // Reversed compressed and uncompressed size...
                            file.Seek(file.Position - compressedSize + decompressedSize, SeekOrigin.Begin); // need to move the seek header back to the correct position as it will have "over-read" the data
                            (decompressedSize, compressedSize) = (compressedSize, decompressedSize);
                            if (compressedSize == decompressedSize)
                            {
                                Array.Copy(compressedShare, 0, decompressedShare, 0, compressedSize);
                                break;
                            }
                            try
                            {
                                Decompress.LZ2K(compressedShare, compressedSize, decompressedShare, decompressedSize);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine("LZ2K extract Failed");
                                return;
                            }
                            break;
                        default:
                            Console.WriteLine("Unknown compression type: {0}", comType);
                            break;
                    }

                    write.Write(decompressedShare, 0, decompressedSize);
                    totalDecompressed += decompressedSize;
                }
            }
            else
            {
                if (extract.DecompressedSize > decompressedShare.Length)
                {
                    decompressedShare = new byte[extract.DecompressedSize];
                }
                file.ReadInto(decompressedShare, (int)extract.DecompressedSize);
                write.Write(decompressedShare, 0, (int)extract.DecompressedSize);
            }
        }

        private string PreparePath(string outputLocation, ArchiveFile file)
        {
            string path = Path.Join(outputLocation, file.Path.ToUpper());
            Directory.CreateDirectory(Path.GetDirectoryName(path));

            return path;
        }

        public virtual void ExtractFiles(ArchiveFile[] files, string outputLocation)
        {
            foreach (ArchiveFile file in files)
            {
                string path = PreparePath(outputLocation, file);

                using (FileStream outputFile = File.OpenWrite(path))
                {
                    ExtractFile(file, outputFile);
                }
            }
        }

        public virtual void ExtractAll(string outputLocation)
        {
            for (int i = 0; i < Files.Length; i++)
            {
                ArchiveFile file = Files[i];

                string path = PreparePath(outputLocation, file);

                using (FileStream outputFile = File.OpenWrite(path))
                {
                    ExtractFile(file, outputFile);
                }

                Console.WriteLine($"Progress: {i + 1} / {Files.Length}");
            }
        }

        public virtual void TestExtract()
        {
            for (int i = 0; i < Files.Length; i++)
            {
                ArchiveFile file = Files[i];

                ExtractFile(file, new MemoryStream());
                //Console.WriteLine($"Progress: {i + 1} / {Files.Length}");
            }
        }
    }
}
