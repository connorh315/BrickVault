using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading;
using System.Threading.Tasks;

namespace BrickVault.Types
{
    internal class FileSegmentStruct // To move to eventually
    {
        public ushort ParentIndex;
        public short FileIndex;
        public short FinalChild;
        public short PreviousSibling;
        public string CurrentPath;
    }

    internal class DAT_v11 : DATFile
    {
        public override DATVersion Version => DATVersion.V11;

        public DAT_v11(RawFile file, long trailerOffset, uint trailerSize) : base(file, trailerOffset, trailerSize)
        {

        }

        internal override void Read(RawFile file)
        {
            file.Seek(trailerOffset + 16, SeekOrigin.Begin);

            uint minorVersion = file.ReadUInt(true);
            uint fileCount = file.ReadUInt(true);

            uint segmentsCount = file.ReadUInt(true);
            uint segmentsSize = file.ReadUInt(true);

            long segmentsOffset = trailerOffset + 32;

            file.Seek(segmentsOffset + segmentsSize + 4, SeekOrigin.Begin);

            string[] folders = new string[segmentsCount];
            string[] paths = new string[segmentsCount];

            int nodeId = 0;

            for (int i = 0; i < segmentsCount; i++)
            {
                int nameOffset = file.ReadInt(true);
                ushort folderId = file.ReadUShort(true);

                short orderId = 0;
                if (minorVersion >= 2)
                {
                    orderId = file.ReadShort(true);
                }

                short unkId = file.ReadShort(true);
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
                        paths[nodeId] = pathName;
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
                compressionType = decompressedSize;
                decompressedSize &= 0x7fffffff;
                compressionType >>= 31;

                Files[i] = new ArchiveFile
                {
                    Offset = fileOffset,
                    CompressedSize = compressedSize,
                    DecompressedSize = decompressedSize,
                    CompressionType = (uint)compressionType
                };
            }

            long[] crcs = new long[fileCount];
            Dictionary<long, int> crcLookup = new Dictionary<long, int>();

            for (int i = 0; i < fileCount; i++)
            {
                uint val = file.ReadUInt(true);
                crcs[i] = val;
                crcLookup[val] = i;
            }

            Dictionary<string, uint> collisions = new();

            uint collisionCount = file.ReadUInt(true);
            file.Seek(4, SeekOrigin.Current); // Size of CRC collision block

            List<string> collisionsCaught = new();

            for (int i = 0; i < collisionCount; i++)
            {
                string overridePath = file.ReadNullString();
                collisionsCaught.Add(overridePath);
                if (overridePath.Length % 2 == 0) file.ReadByte(); // string is aligned to a 2-byte boundary, so the original parser likely read 2-bytes at a time and then checked if either of them were zero, so there's one byte of padding if the string is even-length
                short id = file.ReadShort(true);
                Files[i].Path = overridePath;
            }

            List<uint> collisionCrcs = new();

            foreach (var coll in collisionsCaught)
            {
                uint crc = CRC_FNV_OFFSET_32;
                foreach (char character in coll.Substring(1).ToUpper())
                {
                    crc ^= character;
                    crc *= CRC_FNV_PRIME_32;
                }

                collisionCrcs.Add(crc);
            }

            for (int i = 0; i < fileCount; i++)
            {
                string path = paths[i];

                uint crc = CRC_FNV_OFFSET_32;
                foreach (char character in path.Substring(1).ToUpper())
                {
                    crc ^= character;
                    crc *= CRC_FNV_PRIME_32;
                }

                if (crcLookup.ContainsKey(crc))
                {
                    int index = crcLookup[crc];
                    Files[index].Path = path;
                }
                else if (!collisionsCaught.Contains(path.Substring(1)))
                {
                    Console.WriteLine("Could not find CRC for file: {0}", path);
                }
            }

            for (int i = 0; i < fileCount; i++)
            {
                if (Files[i].Path == null )
                {
                    Console.WriteLine($"Id: {i} empty");
                }
            }
        }

        private static readonly List<string> ClashableModPaths = new()
        {
            @"STUFF\TEXT\TEXT.CSV"
        };

        static List<PathNode> FlattenedPathNodes;
        private static void Flatten(PathNode node)
        {
            foreach (var child in node.Children)
            {
                FlattenedPathNodes.Add(child);
                child.Index = (ushort)(FlattenedPathNodes.Count - 1);
                Flatten(child);
            }
        }

        internal static void Build(DATBuildSettings settings, BuildProgress progress)
        {
            progress.Status = BuildStatus.ScanningFiles;

            var importFiles = Directory.GetFiles(settings.InputFolderLocation, "*.*", SearchOption.AllDirectories).OrderBy(f => f).ToList();

            RawFile datFile = RawFile.Create(settings.OutputFileLocation);

            datFile.Seek(0, SeekOrigin.Begin);

            if (settings.IsMod)
            {
                datFile.Seek(8, SeekOrigin.Begin);

                datFile.WriteString(BrickVault.PackerID);
                datFile.WriteUInt(BrickVault.PackerVersion , true);
                datFile.WriteString(settings.BuilderID, 1);

                datFile.WriteString(settings.ModName, 1);
                datFile.WriteString(settings.ModCreator, 1);
                datFile.WriteString(settings.ModVersion, 1);
            }

            if (datFile.Position < 0x108)
                datFile.Seek(0x108, SeekOrigin.Begin);

            progress.Status = BuildStatus.PackingFiles;
            progress.Current = 0;
            progress.Total = importFiles.Count;

            ArchivePackFile[] archiveFiles = new ArchivePackFile[importFiles.Count];
            for (int i = 0; i < importFiles.Count; i++)
            {
                string importFile = importFiles[i];

                uint fileSize = 0;
                long fileOffset;
                using (RawFile toImport = new RawFile($"{importFile}"))
                {
                    fileSize = (uint)toImport.fileStream.Length;

                    datFile.WriteUInt(fileSize);
                    datFile.WriteInt(0);
                    datFile.WriteUInt(fileSize);
                    datFile.WriteInt(0);
                    datFile.WriteInt(0);
                    datFile.WriteInt(0);
                    fileOffset = datFile.Position;
                    toImport.fileStream.CopyTo(datFile.fileStream);
                }

                string reducedPath = importFile.Replace(settings.InputFolderLocation, "").ToUpper().Replace('/', '\\');

                uint crc = CalculateCRC32(reducedPath);
                if (ClashableModPaths.Contains(reducedPath) && settings.IsMod) // Ensures that the file can be layered by the mod loader
                    crc = 0;

                ArchivePackFile archiveFile = new ArchivePackFile()
                {
                    Path = reducedPath,
                    CRC = crc,
                    CompressedSize = fileSize,
                    DecompressedSize = fileSize,
                    CompressionType = 0,
                    Offset = fileOffset
                };

                archiveFiles[i] = archiveFile;

                progress.Current++;

                progress.CancellationToken.ThrowIfCancellationRequested();
            }

            progress.Status = BuildStatus.WritingHeader;

            archiveFiles = archiveFiles.OrderBy(f => f.CRC).ToArray();

            PathNode root = new PathNode("ROOT");

            for (ushort i = 0; i < archiveFiles.Length; i++)
            {
                string[] split = archiveFiles[i].Path.Split('\\');

                PathNode prev = root;
                foreach (string segment in split)
                {
                    PathNode child;
                    if (!prev.HasChild(segment, out child))
                    {
                        child = new PathNode(segment);
                        prev.AddChild(child);
                    }
                    prev = child;
                }

                prev.FileIndex = i;
                prev.SetCRC(archiveFiles[i].CRC);
            }

            FlattenedPathNodes = new() { root };
            Flatten(root);

            string hdrFileLocation = settings.OutputFileLocation.Replace(".DAT", ".HDR");

            using (RawFile hdrFile = RawFile.Create(hdrFileLocation))
            {
                hdrFile.WriteInt(0); // header size (fixed later)

                hdrFile.WriteString(".CC40TAD");

                hdrFile.WriteInt(-11, true);
                hdrFile.WriteInt(2, true);

                hdrFile.WriteInt(archiveFiles.Length, true);
                hdrFile.WriteInt(FlattenedPathNodes.Count, true);

                long namesBlockSize = hdrFile.Position;
                hdrFile.WritePadding(4);
                long namesBlockStart = hdrFile.Position;

                using (RawFile segmentData = new RawFile(new MemoryStream()))
                {
                    foreach (var node in FlattenedPathNodes)
                    {
                        if (node.Parent == null)
                        {
                            segmentData.WriteUShort(node.FinalChild.Index, true);
                            segmentData.WriteUShort(0, true);
                            segmentData.WriteInt(-1, true);
                            segmentData.WriteUShort(0, true);
                            segmentData.WriteUShort(0, true);
                            continue;
                        }

                        segmentData.WriteUShort(node.FinalChild?.Index ?? 0, true);
                        segmentData.WriteUShort(node.PreviousSibling?.Index ?? 0, true);
                        segmentData.WriteInt((int)(hdrFile.Position - namesBlockStart), true);
                        segmentData.WriteUShort(node.Parent.Index, true);
                        segmentData.WriteUShort(node.FileIndex, true);

                        hdrFile.WriteString(node.Name.ToLower(), 2);
                    }

                    hdrFile.WriteShort(0); // padding

                    using (var hop = new RawFileHop(hdrFile, namesBlockSize))
                    {
                        hdrFile.WriteUInt(hop.BlockLength, true);
                    }

                    segmentData.Seek(0, SeekOrigin.Begin);
                    segmentData.fileStream.CopyTo(hdrFile.fileStream);
                }

                hdrFile.WritePadding(4);

                hdrFile.WriteInt(-11, true);
                hdrFile.WriteInt(archiveFiles.Length, true);

                foreach (var file in archiveFiles)
                {
                    hdrFile.WriteLong(file.Offset, true);
                    hdrFile.WriteUInt(file.CompressedSize, true);
                    hdrFile.WriteUInt(file.DecompressedSize | (file.CompressionType << 31), true);
                }

                foreach (var file in archiveFiles)
                {
                    hdrFile.WriteUInt(file.CRC, true);
                }

                hdrFile.WriteInt(ClashableModPaths.Count, true);

                long clashLength = hdrFile.Position;
                hdrFile.WritePadding(4);

                ushort clashIndex = 0;
                foreach (var path in ClashableModPaths)
                {
                    hdrFile.WriteString(path.ToLower(), path.Length % 2 == 0 ? 2 : 1);
                    hdrFile.WriteUShort(clashIndex++, true);
                }

                using (var hop = new RawFileHop(hdrFile, clashLength))
                {
                    hdrFile.WriteUInt(hop.BlockLength, true);
                }

                hdrFile.WriteString("additionalcontent/opus_beetlejuice", 1);
                hdrFile.WritePadding(16);

                hdrFile.WriteString("ROTV");
                hdrFile.WriteInt(archiveFiles.Length, true);
                hdrFile.WritePadding(16 * archiveFiles.Length);

                hdrFile.WriteString("ROTV");
                hdrFile.WriteInt(archiveFiles.Length, true);
                hdrFile.WritePadding(16 * archiveFiles.Length);

                hdrFile.WriteString("ROTV");
                hdrFile.WriteInt(archiveFiles.Length, true);
                hdrFile.WritePadding(archiveFiles.Length);

                hdrFile.WriteString("ROTV");
                hdrFile.WriteInt(archiveFiles.Length, true);
                foreach (var file in archiveFiles)
                {
                    hdrFile.WriteUInt(file.CompressedSize, true);
                }

                hdrFile.Seek(0, SeekOrigin.Begin);
                hdrFile.WriteUInt((uint)hdrFile.fileStream.Length - 4, true);

                uint headerOffset = (uint)((datFile.Position + 0xFF) & ~0xFFL); // Align to next 256-byte boundary

                hdrFile.Seek(0, SeekOrigin.Begin);
                datFile.Seek(headerOffset, SeekOrigin.Begin);
                hdrFile.fileStream.CopyTo(datFile.fileStream);

                headerOffset -= 0x100;
                headerOffset >>= 8;
                headerOffset ^= 0xffffffff;

                datFile.Seek(0, SeekOrigin.Begin);
                datFile.WriteUInt(headerOffset);
                datFile.WriteUInt((uint)hdrFile.fileStream.Length);
            }

            datFile.Dispose();

            if (!settings.ShouldCreateHDR)
            {
                File.Delete(hdrFileLocation);
            }

            progress.Status = BuildStatus.Done;
        }
    }
}
