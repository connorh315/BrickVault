using System.Runtime.CompilerServices;
using System.Text;

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

        public string FileLocation => file.FileLocation;

        public uint DecompressedSize { get; internal set; }

        internal long trailerOffset;
        internal uint trailerSize;

        internal DATFile(RawFile file, long trailerOffset, uint trailerSize)
        {
            this.file = file;
            this.trailerOffset = trailerOffset;
            this.trailerSize = trailerSize;

            Read();
        }

        public static DATFile Open(string fileLocation)
        {
            RawFile file = new RawFile(fileLocation);

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
                    Console.WriteLine($"Archive version: LHP");

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
            int determinant = file.ReadInt();

            int magicBytes1 = file.ReadInt();
            uint magicBytes2 = file.ReadUInt();

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
                    datVersion = (uint)Math.Abs(file.ReadInt(true));
                }
            }

            Console.WriteLine($"Archive Name: {Path.GetFileName(fileLocation)}");
            Console.WriteLine($"Trailer offset: {trailerOffset}");
            Console.WriteLine($"Archive version: {datVersion}");

            DATFile result = null;

            switch (datVersion)
            {
                case 1:
                    return new DAT_v01(file, trailerOffset, trailerSize);
                case 2:
                case 3: // LIJ2, 
                case 4: // LB2, LHP2, LPOTC, LHP1
                    return new DAT_v04(file, trailerOffset, trailerSize);
                case 5: // LHO, LMSH, LOTR, LCU
                    return new DAT_v05(file, trailerOffset, trailerSize);
                case 6: // LB3
                    //return new DAT_v06(file, trailerOffset, trailerSize);
                case 7: // LDI, LJW
                    return new DAT_v07(file, trailerOffset, trailerSize);
                case 8: // LMA, TFA
                    return new DAT_v08(file, trailerOffset, trailerSize);
                case 11: // Worlds 
                    return new DAT_v11(file, trailerOffset, trailerSize);
                case 12: // LMSH2, LM2VG, LIN, LDCSV, LNI
                    return new DAT_v12(file, trailerOffset, trailerSize);
                case 13: // TSS
                    return new DAT_v13(file, trailerOffset, trailerSize);
                default:
                    Console.WriteLine($"Unknown DAT version {datVersion}");
                    return null;
            }

            return result;
        }

        internal abstract void Read();

        internal void Extract(ArchiveFile extract, Stream write, RawFile file, byte[] compressedShare, byte[] decompressedShare)
        {
            file.Seek(extract.Offset, SeekOrigin.Begin);

            if (extract.DecompressedSize != extract.CompressedSize || extract.CompressionType != 0) // (extraction.compressionType != 0 is for LMSH_GAME1 where 2 files, despite being "compressed" outputs a file as the same size as the comp. input")
            {
                int totalDecompressed = 0;
                while (totalDecompressed < extract.DecompressedSize)
                {
                    int decompressedSize = 0;
                    if (Version() == 1111)
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

        internal byte[] compressedShare = new byte[131072*64];
        internal byte[] decompressedShare = new byte[524288*64]; // TODO: Write some better constants
        public virtual void ExtractFile(ArchiveFile extract, Stream write)
        {
            if (compressedShare.Length < extract.CompressedSize)
                compressedShare = new byte[extract.CompressedSize];
            if (decompressedShare.Length < extract.DecompressedSize)
                decompressedShare = new byte[extract.DecompressedSize];

            Extract(extract, write, file, compressedShare, decompressedShare);
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
                int counter = 0;
                foreach (ArchiveFile file in files)
                {
                    counter++;
                    string path = PreparePath(outputLocation, file);

                    using (FileStream outputFile = File.OpenWrite(path))
                    {
                        ExtractFile(file, outputFile);
                    }

                    Console.WriteLine($"Progress: {counter} / {Files.Length}");
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
            byte[] compressedShare = new byte[0x40000*100];
            byte[] decompressedShare = new byte[524288*100];

            RawFile threadView = file.CreateView();

            for (int i = start; i < end; i++)
            {
                ArchiveFile file = files[i];
                
                string path = PreparePath(outputLocation, file);

                if (threaded.Cancel.IsCancellationRequested) return;

                using (FileStream outputFile = File.OpenWrite(path))
                {
                    if (compressedShare.Length < file.CompressedSize)
                        compressedShare = new byte[file.CompressedSize];
                    if (decompressedShare.Length < file.DecompressedSize)
                        decompressedShare = new byte[file.DecompressedSize];

                    Extract(file, outputFile, threadView, compressedShare, decompressedShare);

                    if (threaded.DisplayOutput)
                    {
                        Console.WriteLine($"Extracted: {file.Path}"); // not safe to output progress/total here as may be multiple DATs being extracted
                    }

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

                    Console.WriteLine($"Extracted {i + 1} / {Files.Length}: {file.Path}");
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
