using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace BrickVault.Types
{
    internal class DAT_v01X : DATFile
    {
        public override DATVersion Version => DATVersion.V1X;

        public DAT_v01X(RawFile file, long trailerOffset, uint trailerSize) : base(file, trailerOffset, trailerSize)
        {

        }

        private uint CalculateHash(string path)
        {
            uint crc = CRC_FNV_OFFSET_32;
            foreach (char character in path.ToUpper())
            {
                crc ^= character;
                crc *= CRC_FNV_PRIME_32;
            }

            return crc;
        }

        private void AddToDictionary(Dictionary<uint, string> dict, string path, bool block=false)
        {
            path = path.ToLower();
            uint crc = CalculateHash(path);
            if (!dict.ContainsKey(crc))
                dict.Add(crc, path);

            uint crc2 = CalculateHash("__PATCH__\\" + path);
            if (!dict.ContainsKey(crc2))
                dict.Add(crc2, "__PATCH__\\" + path);

            //if (block) return;

            //if (path.EndsWith(".pak") || path.EndsWith(".as"))
            //{
            //    path = path.Replace("_cbs.pak", "").Replace("_an3.pak", "").Replace("_an3_ps3.pak", "").Replace("_an3_ps4.pak", "").Replace("_an3_wii.pak", "").Replace("_cbs_ps4.pak", "").Replace(".as", "");
            //    AddToDictionary(dict, path + "_an3.pak", true);
            //    AddToDictionary(dict, path + "_an3_ps3.pak", true);
            //    AddToDictionary(dict, path + "_an3_ps4.pak", true);
            //    AddToDictionary(dict, path + "_an3_wii.pak", true);
            //    AddToDictionary(dict, path + "_cbs.pak", true);
            //    AddToDictionary(dict, path + "_cbs_ps4.pak", true);
            //    AddToDictionary(dict, path + "_ran.pak", true);
            //}
            //else if (path.EndsWith("_ai.led"))
            //{
            //    AddToDictionary(dict, path.Replace("_ai.led", "_ai_edmesh.led"));
            //}
        }

#if DEBUG
        string searchLocation = @"G:\";
#else
        string searchLocation = Directory.GetCurrentDirectory();
#endif

        internal override void Read(RawFile file)
        {
            file.Seek(trailerOffset + 4, SeekOrigin.Begin);

            Dictionary<uint, string> dict = new();
            Dictionary<uint, string> dict2 = new();

            foreach (var list in Directory.EnumerateFiles(searchLocation, "*.list"))
            {
                if (list.Contains("builds.list")) continue;

                string[] lhp_list = File.ReadAllLines(list);
                foreach (var path in lhp_list)
                {
                    if (path.StartsWith('#')) continue;

                    AddToDictionary(dict, path);
                }
            }

            uint fileCount = file.ReadUInt();

            Files = new NewArchiveFile[fileCount];

            for (int i = 0; i < fileCount; i++)
            {
                var archiveFile = new NewArchiveFile();

                long fileOffset = file.ReadLong();

                file.Seek(4, SeekOrigin.Current); // 0x12345678

                uint compressedSize = file.ReadUInt();
                uint decompressedSize = file.ReadUInt();

                uint compressionType = file.ReadUInt();

                archiveFile.SetFileData(fileOffset, compressedSize, decompressedSize, (byte)compressionType);

                Files[i] = archiveFile;
            }

            int accounted = 0;

            FileTree = new FileTree(ushort.MaxValue); // remind me to think of something better than this at some point.

            FileTreeNode root = new FileTreeNode();
            root.FileTree = FileTree;
            FileTree.Nodes[0] = root;

            for (int i = 0; i < fileCount; i++)
            {
                uint crc = file.ReadUInt();

                if (dict.ContainsKey(crc))
                {
                    FileTreeNode fileNode = BuildPathNodes(FileTree, dict[crc]);

                    fileNode.File = (NewArchiveFile)Files[i];
                    fileNode.File.Node = fileNode;


                    //Console.WriteLine($"Valid file: {dict[crc]}");
                    //Files[i].Path = "\\" + dict[crc].ToLower();
                    //structure.Add((crc, dict[crc].ToLower()));
                    //dict.Remove(crc);
                    accounted++;
                }
                else if (dict2.ContainsKey(crc))
                {
                    //Console.WriteLine($"Valid file: {dict2[crc]}");
                    //Files[i].Path = "\\" + dict2[crc].ToLower();
                    //structure.Add((crc, dict2[crc].ToLower()));
                    //dict2.Remove(crc);
                    accounted++;
                }
                else
                {
                    FileTreeNode fileNode = BuildPathNodes(FileTree, $"\\unknown\\{crc:x8}.unk");
                    fileNode.File = (NewArchiveFile)Files[i];
                    fileNode.File.Node = fileNode;
                    //Files[i].Path = ;
                }
            }
            Console.WriteLine($"Accounted for {accounted} / {fileCount}"); // 3907, 3931, 4052
        }

        private ushort nextNodePointer = 1;

        private FileTreeNode BuildPathNodes(FileTree fileTree, string path)
        {
            string[] segments = path.Split('\\');
            ushort parentIndex = 0;
            for (int i = 0; i < segments.Length; i++)
            {
                FileTreeNode parent = fileTree.Nodes[parentIndex];

                string segment = segments[i];
                if (segment.Length == 0) continue;

                ushort childIndex = parent.HasChild(segment);

                if (childIndex == 0)
                {
                    FileTreeNode node = new FileTreeNode();
                    node.FileTree = fileTree;
                    node.Segment = segment;
                    node.ParentIndex = parentIndex;

                    fileTree.Nodes[nextNodePointer] = node;
                    childIndex = nextNodePointer++;

                    ushort parentFinalChild = parent.FinalChild;
                    parent.FinalChild = childIndex;
                    node.PreviousSibling = parentFinalChild;
                }

                parentIndex = childIndex;
            }

            return fileTree.Nodes[parentIndex];
        }
    }
}
