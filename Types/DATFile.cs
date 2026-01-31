using System.Buffers;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;

namespace BrickVault.Types
{
    public abstract class DATFile
    {
        public enum DATVersion
        {
            V1 = 1,
            V2,
            V3,
            V4,
            V5,
            V6,
            V7,
            V8,
            V9,
            V10,
            V11,
            V12,
            V13,
            V1X
        }

        public abstract DATVersion Version { get; }

        private static string StripSlashFromPath(string path)
        {
            if (path.StartsWith("/")) return path.Substring(1);

            return path;
        }

        internal static long CRC_FNV_OFFSET_64 = -3750763034362895579;
        internal static long CRC_FNV_PRIME_64 = 1099511628211;

        internal static uint CRC_FNV_OFFSET_32 = 2166136261;
        internal static uint CRC_FNV_PRIME_32 = 0x199933;

        public static uint CalculateCRC32(string path)
        {
            uint crc = CRC_FNV_OFFSET_32;
            foreach (char character in StripSlashFromPath(path).ToUpper())
            {
                crc ^= character;
                crc *= CRC_FNV_PRIME_32;
            }

            return crc;
        }

        internal static long CalculateCRC64(string path)
        {
            long crc = CRC_FNV_OFFSET_64;
            foreach (char character in path)
            {
                crc ^= character;
                crc *= CRC_FNV_PRIME_64;
            }

            return crc;
        }

        internal static long CalculateSegmentCRC(string segment, long offset)
        {
            if (offset != CRC_FNV_OFFSET_64)
            {
                offset ^= '\\';
                offset *= CRC_FNV_PRIME_64;
            }

            foreach (char character in segment)
            {
                offset ^= character;
                offset *= CRC_FNV_PRIME_64;
            }

            return offset;
        }

        public ArchiveFile[] Files { get; set; }

        public FileTree FileTree { get; set; }

        public string FileLocation;

        public string FileName;

        public uint DecompressedSize { get; internal set; }

        internal long trailerOffset;
        internal uint trailerSize;

        internal DATFile(RawFile file, long trailerOffset, uint trailerSize)
        {
            FileLocation = file.FileLocation;
            FileName = Path.GetFileNameWithoutExtension(file.FileLocation);
            this.trailerOffset = trailerOffset;
            this.trailerSize = trailerSize;

            Read(file);
        }

        public static DATFile Open(string fileLocation)
        {
            using (RawFile file = new RawFile(fileLocation))
            {
                if (!fileLocation.ToLower().EndsWith(".DAT") && !fileLocation.ToLower().EndsWith(".DAT2"))
                {
                    long lhpOffset = file.ReadLong();
                    uint lhpTrailerSize = file.ReadUInt();
                    if (lhpOffset < 0) return null;
                    file.Seek(lhpOffset, SeekOrigin.Begin);
                    if (file.ReadInt() == -1)
                    {
                        Console.WriteLine($"Archive Name: {Path.GetFileName(fileLocation)}");
                        Console.WriteLine($"Trailer offset: {lhpOffset}");
                        Console.WriteLine($"Archive version: LHPC");

                        return new DAT_v01X(file, lhpOffset, lhpTrailerSize);
                    }
                    else
                    {
                        file.Seek(0, SeekOrigin.Begin);
                    }
                }

                uint trailerOffset = file.ReadUInt();
                if ((trailerOffset & 0x80000000) != 0)
                {
                    trailerOffset ^= 0xffffffff;
                    trailerOffset <<= 8;
                    trailerOffset += 0x100;
                }

                if (trailerOffset > file.fileStream.Length) return null;

                uint trailerSize = file.ReadUInt();

                file.Seek(trailerOffset, SeekOrigin.Begin);
                
                if (file.fileStream.Length >= 0x100000000)
                {
                    file.Seek(0x100000000, SeekOrigin.Current);
                }

                using (RawFile header = file.CreateMemoryFile(trailerSize))
                {
                    int determinant = header.ReadInt();

                    int magicBytes1 = header.ReadInt();
                    uint magicBytes2 = header.ReadUInt();

                    uint datVersion, fileCount;

                    if (determinant < 0 && determinant > -20) // Covers the versions
                    {
                        datVersion = (uint)Math.Abs(determinant);
                    }
                    else
                    {
                        if (magicBytes2 != 0x3443432e && magicBytes2 != 0x2e434334 && magicBytes1 != 0x3443432e && magicBytes1 != 0x2e434334)
                        {
                            datVersion = (uint)Math.Abs(magicBytes1);
                        }
                        else
                        {
                            datVersion = (uint)Math.Abs(header.ReadInt(true));
                        }
                    }

                    Console.WriteLine($"Archive Name: {Path.GetFileName(fileLocation)}");
                    Console.WriteLine($"Trailer offset: {trailerOffset}");
                    Console.WriteLine($"Archive version: {datVersion}");

                    DATFile result = null;

                    switch (datVersion)
                    {
                        case 1:
                            return new DAT_v01(header, trailerOffset, trailerSize);
                        case 2:
                        case 3: // LIJ2, 
                            return new DAT_v03(header, trailerOffset, trailerSize);
                        case 4: // LB2, LHP2, LPOTC, LHP1
                            return new DAT_v04(header, trailerOffset, trailerSize);
                        case 5: // LHO, LMSH, LOTR, LCU
                            return new DAT_v05(header, trailerOffset, trailerSize);
                        case 6: // LB3
                            //return new DAT_v06(file, trailerOffset, trailerSize);
                        case 7: // LDI, LJW
                            return new DAT_v07(header, trailerOffset, trailerSize);
                        case 8: // LMA, TFA
                            return new DAT_v08(header, trailerOffset, trailerSize);
                        case 11: // Worlds 
                            return new DAT_v11(header, trailerOffset, trailerSize);
                        case 12: // LMSH2, LM2VG, LIN, LDCSV, LNI
                            return new DAT_v12(header, trailerOffset, trailerSize);
                        case 13: // TSS
                            return new DAT_v13(header, trailerOffset, trailerSize);
                        default:
                            Console.WriteLine($"Unknown DAT version {datVersion}");
                            return null;
                    }
                }
            }
        }

        public static void BuildFromFolder(DATBuildSettings settings, BuildProgress progress = null)
        {
            if (progress == null)
            {
                progress = new BuildProgress();
            }

            if (settings.Version == DATVersion.V11)
            {
                DAT_v11.Build(settings, progress);
                return;
            }

            throw new NotImplementedException("Cannot build this archive version - No creator implemented!");
        }

        internal abstract void Read(RawFile file);

        internal void Extract(ArchiveFile extract, Stream write, RawFile file, byte[] compressedShare, byte[] decompressedShare)
        {
            file.Seek(extract.Offset, SeekOrigin.Begin);

            if (extract.DecompressedSize != extract.CompressedSize || extract.CompressionType != 0) // (extraction.compressionType != 0 is for LMSH_GAME1 where 2 files, despite being "compressed" outputs a file as the same size as the comp. input")
            {
                int totalDecompressed = 0;
                while (totalDecompressed < extract.DecompressedSize)
                {
                    int decompressedSize = 0;
                    if (Version == DATVersion.V1X)
                    {
                        file.ReadInto(compressedShare, (int)extract.CompressedSize);
                        decompressedSize = Decompress.LZHAM(compressedShare, (int)extract.CompressedSize, decompressedShare, (int)extract.DecompressedSize);
                    }
                    else
                    {
                        string comType = file.ReadString(4);
                        int compressedSize = file.ReadInt();
                        decompressedSize = file.ReadInt();

                        if (comType.StartsWith("RNC"))
                        {
                            file.Seek(file.Position - 8, SeekOrigin.Begin);
                            decompressedSize = file.ReadInt(true);
                            compressedSize = file.ReadInt(true) + 6;
                        }
                        else if (comType == "LZ2K")
                        {
                            (decompressedSize, compressedSize) = (compressedSize, decompressedSize);
                        }

                        if (compressedSize != decompressedSize || (comType == "ZIPX"))
                        { // Sometimes at the end of a collection of chunks, the final chunk might be stored raw, despite previous chunks being compressed - This accounts for that scenario
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
                                             //file.Seek(pos + decompressedSize, SeekOrigin.Begin); // need to move the seek header back to the correct position as it will have "over-read" the data

                                    //if (compressedSize == decompressedSize)
                                    //{
                                    //    Array.Copy(compressedShare, 0, decompressedShare, 0, compressedSize);
                                    //    break;
                                    //}
                                    Decompress.LZ2K(compressedShare, compressedSize, decompressedShare, decompressedSize);
                                    break;
                                case "DFLT":
                                    Decompress.DFLT(compressedShare, compressedSize, decompressedShare, decompressedSize);
                                    break;
                                case "RFPK":
                                    Decompress.RFPK(compressedShare, compressedSize, decompressedShare, decompressedSize);
                                    break;
                                case "RNC\x02":
                                    Decompress.RNC(compressedShare, compressedSize, decompressedShare, decompressedSize);
                                    break;
                                default:
                                    Console.WriteLine("Unknown compression type: {0}", comType);
                                    break;
                            }
                        }
                        else
                        {
                            file.ReadInto(decompressedShare, decompressedSize);
                        }
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

        private static byte[] RentBuffer(uint size)
        {
            return ArrayPool<byte>.Shared.Rent((int)size);
        }

        private static void ReturnBuffer(byte[] array)
        {
            ArrayPool<byte>.Shared.Return(array, clearArray: false);
        }

        private static byte[] SwapBuffer(byte[] original, uint newSize)
        {
            ReturnBuffer(original);
            return RentBuffer(newSize);
        }

        /// <summary>
        /// Useful for a one-off extraction.
        /// Use ExtractFiles() if extracting multiple files for better memory-efficiency
        /// </summary>
        /// <param name="extract"></param>
        /// <param name="datFile"></param>
        /// <param name="write"></param>
        public virtual void ExtractFile(ArchiveFile extract, RawFile datFile, Stream write)
        {
            byte[] comp = RentBuffer(extract.CompressedSize);
            byte[] decomp = RentBuffer(extract.DecompressedSize);
            try
            {
                Extract(extract, write, datFile, comp, decomp);
            }
            finally
            {
                ReturnBuffer(comp);
                ReturnBuffer(decomp);
            }
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
            if (string.IsNullOrEmpty(outputLocation) && !string.IsNullOrEmpty(FileLocation))
            {
                outputLocation = Path.GetDirectoryName(FileLocation);
            }

            if (threaded == null)
            {
                uint maxComp = 0, maxDecomp = 0;

                foreach (var f in files) // work out buffer sizes
                {
                    maxComp = Math.Max(maxComp, f.CompressedSize);
                    maxDecomp = Math.Max(maxDecomp, f.DecompressedSize);
                }

                byte[] comp = RentBuffer(maxComp);
                byte[] decomp = RentBuffer(maxDecomp);

                try
                {
                    int counter = 0;
                    using (RawFile datFile = new RawFile(FileLocation))
                    {
                        foreach (ArchiveFile file in files)
                        {
                            counter++;
                            string path = PreparePath(outputLocation, file);

                            using (FileStream outputFile = File.Create(path))
                            {
                                Extract(file, outputFile, datFile, comp, decomp);
                            }

                            Console.WriteLine($"Progress: {counter} / {files.Length}");
                        }
                    }
                }
                finally
                {
                    ReturnBuffer(comp);
                    ReturnBuffer(decomp);
                }
            }
            else
            {
                int totalFiles = files.Length;

                threaded.TotalThreads = Math.Min(16, threaded.TotalThreads);

                int perThread = totalFiles / threaded.TotalThreads;
                int position = 0;
                for (int i = 1; i <= threaded.TotalThreads; i++)
                {
                    int start = position;
                    int end = i == threaded.TotalThreads ? totalFiles : position + perThread;
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
            byte[] comp = RentBuffer(1 << 20);     // 1 MB start
            byte[] decomp = RentBuffer(4 << 20);   // 4 MB start

            try
            {
                using (RawFile threadView = new RawFile(FileLocation))
                {
                    for (int i = start; i < end; i++)
                    {
                        ArchiveFile file = files[i];
                
                        string path = PreparePath(outputLocation, file);

                        if (threaded.Cancel.IsCancellationRequested) return;

                        using (FileStream outputFile = File.Create(path))
                        {
                            if (comp.Length < file.CompressedSize)
                                comp = SwapBuffer(comp, file.CompressedSize);
                            if (decomp.Length < file.DecompressedSize)
                                decomp = SwapBuffer(decomp, file.DecompressedSize);

                            Extract(file, outputFile, threadView, comp, decomp);

                            if (threaded.DisplayOutput)
                            {
                                Console.WriteLine($"Extracted: {file.Path}"); // not safe to output progress/total here as may be multiple DATs being extracted
                            }

                            threaded.Increment();
                        }
                    }
                }
            }
            finally
            {
                ReturnBuffer(comp);
                ReturnBuffer(decomp);
            }
        }
        
        public virtual void ExtractAll(string outputLocation, ThreadedExtractionCtx? threaded = null)
        {
            if (threaded == null)
            {
                byte[] comp = RentBuffer(8 * 1024 * 1024);   // 8 MB
                byte[] decomp = RentBuffer(16 * 1024 * 1024); // 16 MB

                try
                {
                    using (RawFile datFile = new RawFile(FileLocation))
                    {
                        for (int i = 0; i < Files.Length; i++)
                        {
                            ArchiveFile file = Files[i];

                            string path = PreparePath(outputLocation, file);

                            if (comp.Length < file.CompressedSize)
                                comp = SwapBuffer(comp, file.CompressedSize);
                            if (decomp.Length < file.DecompressedSize)
                                decomp = SwapBuffer(decomp, file.DecompressedSize);

                            using (FileStream outputFile = File.Create(path))
                            {
                                Extract(file, outputFile, datFile, comp, decomp);
                            }

                            Console.WriteLine($"Extracted {i + 1} / {Files.Length}: {file.Path}");
                        }
                    }
                }
                finally
                {
                    ReturnBuffer(comp);
                    ReturnBuffer(decomp);
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

            using (RawFile datFile = new RawFile(FileLocation))
            {
                for (int i = 0; i < Files.Length; i++)
                {
                    ArchiveFile file = Files[i];

                    ExtractFile(file, datFile, stream);
                    stream.Position = 0;
                    Console.WriteLine($"Progress: {i + 1} / {Files.Length}");
                }
            }
        }
    }
}
