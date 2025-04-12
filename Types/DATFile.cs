using System.Runtime.CompilerServices;

namespace BrickVault.Types
{
    public abstract class DATFile
    {
        internal static long CRC_FNV_OFFSET_64 = -3750763034362895579;
        internal static long CRC_FNV_PRIME_64 = 1099511628211;

        internal static uint CRC_FNV_OFFSET_32 = 2166136261;
        internal static uint CRC_FNV_PRIME_32 = 0x199933;

        internal RawFile file { get; set; }

        public ArchiveFile[] Files { get; set; }

        public abstract uint Version();

        public uint DecompressedSize { get; internal set; }

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
            RawFile file = new RawFile(fileLocation);

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
                case 11:
                    return new DAT_v11(file, trailerOffset, trailerSize);
                case 12:
                    return new DAT_v12(file, trailerOffset, trailerSize);
                case 13:
                    return new DAT_v13(file, trailerOffset, trailerSize);
                default:
                    Console.WriteLine($"Unknown DAT version {datVersion}");
                    return null;
            }

            return result;
        }

        internal abstract void Read();

        internal void Extract(ArchiveFile extract, Stream write, RawFile file)
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
                    long pos = file.Position;
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
                            file.Seek(pos + decompressedSize, SeekOrigin.Begin); // need to move the seek header back to the correct position as it will have "over-read" the data
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
                        case "DFLT":
                            Decompress.DFLT(compressedShare, compressedSize, decompressedShare, decompressedSize);
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

        public virtual void ExtractFile(ArchiveFile extract, Stream write)
        {
            Extract(extract, write, file);
        }

        private string PreparePath(string outputLocation, ArchiveFile file)
        {
            string path = Path.Join(outputLocation, file.Path.ToUpper());
            path = path.Replace('\\', Path.DirectorySeparatorChar);
            Directory.CreateDirectory(Path.GetDirectoryName(path));

            return path;
        }

        public virtual void ExtractFiles(ArchiveFile[] files, string outputLocation, ThreadedExtractionCtx? threaded = null)
        {
            if (threaded == null)
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
            else
            {
                threaded.TotalThreads = Math.Min(16, threaded.TotalThreads);

                int perThread = threaded.Total / threaded.TotalThreads;
                int position = 0;
                for (int i = 1; i <= threaded.TotalThreads; i++)
                {
                    int start = position;
                    int end = i == threaded.TotalThreads ? threaded.Total : position + perThread;
                    new Thread((object? args) =>
                    {
                        var (start, end) = ((int, int))args!;
                        ExtractByThread(files, start, end, outputLocation, threaded);
                    }).Start((start, end));
                    position = end;
                }
            }
        }

        private void ExtractByThread(ArchiveFile[] files, int start, int end, string outputLocation, ThreadedExtractionCtx threaded)
        {
            RawFile threadView = file.CreateView();

            for (int i = start; i < end; i++)
            {
                ArchiveFile file = files[i];
                
                string path = PreparePath(outputLocation, file);

                using (FileStream outputFile = File.OpenWrite(path))
                {
                    Extract(file, outputFile, threadView);

                    threaded.Increment();
                }
            }
        }
        
        public virtual void ExtractAll(string outputLocation, ThreadedExtractionCtx? threaded = null)
        {
            if (threaded == null)
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
            else
            {
                ExtractFiles(Files, outputLocation, threaded);
            }
        }

        public virtual void TestExtract()
        {
            MemoryStream stream = new MemoryStream();

            for (int i = 0; i < Files.Length; i++)
            {
                ArchiveFile file = Files[i];

                ExtractFile(file, stream);
                stream.Position = 0;
                Console.WriteLine($"Progress: {i + 1} / {Files.Length}");
            }
        }
    }
}
