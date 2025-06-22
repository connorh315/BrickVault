using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace BrickVault.Types
{
    internal class DAT_v01X : DATFile
    {
        public override uint Version() => 1111;

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
        string searchLocation = "";
#endif

        internal override void Read()
        {
            file.Seek(trailerOffset + 4, SeekOrigin.Begin);

            Dictionary<uint, string> dict = new();
            Dictionary<uint, string> dict2 = new();

            foreach (var list in Directory.EnumerateFiles(searchLocation, "*.list"))
            {
                string[] lhp_list = File.ReadAllLines(list);
                foreach (var path in lhp_list)
                {
                    if (path.StartsWith('#')) continue;

                    AddToDictionary(dict, path);
                }
            }

            string[] paths = File.ReadAllLines(@"C:\Users\Connor\Desktop\LHP_PC_GAME.list");
            foreach (var path in paths)
            {
                AddToDictionary(dict2, path);
            }

            string[] paths2 = File.ReadAllLines(@"C:\Users\Connor\Desktop\gamedictionary.txt");
            foreach (var path in paths2)
            {
                AddToDictionary(dict2, path);
            }

            string[] dictionary = File.ReadAllLines(@"C:\Users\Connor\Desktop\dictionary.txt");
            foreach (var path in dictionary)
            {
                AddToDictionary(dict2, path);
            }

            string[] otherdict = File.ReadAllLines(@"C:\Users\Connor\Downloads\hashes2.txt");
            foreach (var path in otherdict)
            {
                AddToDictionary(dict2, path);
            }

            //string[] samples = File.ReadAllLines(@"C:\Program Files (x86)\Steam\steamapps\common\LHPCR\Years 1-4\AUDIO\LEVELSFX.TXT");

            //foreach (var sample in samples)
            //{
            //    if (sample.StartsWith("SAMPLE: "))
            //    {
            //        var split = sample.Split(" ");

            //        AddToDictionary(dict2, split[1]);
            //    }
            //}

            //string[] opened = File.ReadAllLines(@"C:\Program Files (x86)\Steam\steamapps\common\LHPCR\Years 1-4\NUFILESTAT-OPENED.TXT");
            //foreach (var path in opened)
            //{
            //    AddToDictionary(dict2, path);
            //}

            //string[] opened2 = File.ReadAllLines(@"C:\Program Files (x86)\Steam\steamapps\common\LHPCR\Years 1-4\TRACKEDFILES.TXT");
            //foreach (var path in opened2)
            //{
            //    AddToDictionary(dict2, path);
            //}

            //string[] opened3 = File.ReadAllLines(@"C:\Program Files (x86)\Steam\steamapps\common\LHPCR\Years 1-4\extract\harry1\00004320.fra");
            //foreach (var path in opened3)
            //{
            //    AddToDictionary(dict2, path);
            //}

            //string prefix = "";
            //string prefix2 = "";
            //string[] areas = File.ReadAllLines(@"C:\Program Files (x86)\Steam\steamapps\common\LHPCR\Years 1-4\LEVELS\AREAS.TXT");
            //foreach (var path in areas)
            //{
            //    string trim = path.Trim();
            //    if (trim.StartsWith("dir"))
            //    {
            //        var split = trim.Split('"');
            //        prefix = $"LEVELS\\{split[1]}\\";
            //    }
            //    else if (trim.StartsWith("file"))
            //    {
            //        var split = trim.Split('"');
            //        prefix2 = split[1];
            //    }
            //    else if (trim.StartsWith("level"))
            //    {
            //        var split = trim.Split('"');
            //        AddToDictionary(dict2, prefix + $"{split[1]}\\{split[1]}.sfx");
            //        AddToDictionary(dict2, prefix + $"{split[1]}\\{split[1]}.lut.bin");
            //        AddToDictionary(dict2, prefix + $"legosets.txt");
            //    }
            //}

            //string[] levels = File.ReadAllLines(@"C:\Program Files (x86)\Steam\steamapps\common\LHPCR\Years 1-4\LEVELS\LEVELS.TXT");
            //foreach (var path in levels)
            //{
            //    string trim = path.Trim();
            //    if (trim.StartsWith("dir"))
            //    {
            //        var split = trim.Split('"');
            //        prefix = $"LEVELS\\{split[1]}\\";
            //    }
            //    else if (trim.StartsWith("file"))
            //    {
            //        var split = trim.Split('"');
            //        prefix2 = split[1];
            //        AddToDictionary(dict, prefix + $"{split[1]}.sfx");
            //        AddToDictionary(dict, prefix + $"{split[1]}.lut.bin");
            //        AddToDictionary(dict, prefix + $"legosets.txt");
            //    }
            //}

            //string[] gizcut = File.ReadAllLines(@"C:\Program Files (x86)\Steam\steamapps\common\LHPCR\Years 1-4\CUT\GIZCUTSCENES.TXT");
            //foreach (var path in gizcut)
            //{
            //    string trim = path.Trim();
            //    if (trim.StartsWith("dir"))
            //    {
            //        var split = trim.Split('"');
            //        prefix = $"CUT\\{split[1]}\\";
            //    }
            //    else if (trim.StartsWith("file"))
            //    {
            //        var split = trim.Split('"');
            //        prefix2 = split[1];
            //        AddToDictionary(dict2, prefix + $"{split[1]}.txt");
            //    }
            //}

            //string[] audio = File.ReadAllLines(@"C:\Program Files (x86)\Steam\steamapps\common\LHPCR\Years 1-4\AUDIO\AUDIO.CFG");
            //prefix = "";
            //foreach (var untrimmed in audio)
            //{
            //    var line = untrimmed.Trim().Replace(";", "").Trim();

            //    if (line.StartsWith("BASEPATH"))
            //    {
            //        prefix = line.Split(" ")[1];
            //    }
            //    else if (line.StartsWith("Sample"))
            //    {
            //        var split = line.Split("fname ");
            //        if (split.Length < 2) continue;
            //        var removeSpeech = split[1].Split('"');

            //        AddToDictionary(dict2, prefix + removeSpeech[1] + ".wav");
            //    }
            //}

            //string[] music = File.ReadAllLines(@"C:\Program Files (x86)\Steam\steamapps\common\LHPCR\Years 1-4\AUDIO\MUSIC.CFG");
            //prefix = "";
            //foreach (var line in music)
            //{
            //    if (line.StartsWith("PATH"))
            //    {
            //        prefix = line.Split('"')[1].Substring(1);
            //    }
            //    else
            //    {
            //        var split = line.Split('"');
            //        if (split.Length <= 1) continue;
            //        AddToDictionary(dict2, prefix + split[1] + ".ogg");
            //    }
            //}

            uint fileCount = file.ReadUInt();

            Files = new ArchiveFile[fileCount];

            for (int i = 0; i < fileCount; i++)
            {
                long fileOffset = file.ReadLong();

                file.Seek(4, SeekOrigin.Current); // 0x12345678

                uint compressedSize = file.ReadUInt();
                uint decompressedSize = file.ReadUInt();

                uint compressionType = file.ReadUInt();

                Files[i] = new ArchiveFile
                {
                    Offset = fileOffset,
                    CompressedSize = compressedSize,
                    DecompressedSize = decompressedSize,
                    CompressionType = (uint)compressionType,
                    Path = $"\\{i}.unk"
                };
            }

            int accounted = 0;

            List<(uint, string)> structure = new();

            for (int i = 0; i < fileCount; i++)
            {
                uint crc = file.ReadUInt();

                if (dict.ContainsKey(crc))
                {
                    //Console.WriteLine($"Valid file: {dict[crc]}");
                    Files[i].Path = "\\" + dict[crc].ToLower();
                    structure.Add((crc, dict[crc].ToLower()));
                    dict.Remove(crc);
                    accounted++;
                }
                else if (dict2.ContainsKey(crc))
                {
                    Console.WriteLine($"Valid file: {dict2[crc]}");
                    Files[i].Path = "\\" + dict2[crc].ToLower();
                    structure.Add((crc, dict2[crc].ToLower()));
                    dict2.Remove(crc);
                    accounted++;
                }
                else
                {
                    structure.Add((crc, ""));
                    //Files[i].Path = $"\\unknown\\{crc:x8}.unk";
                }
            }
            Console.WriteLine($"Accounted for {accounted} / {fileCount}"); // 3907, 3931, 4052
        }
    }
}
