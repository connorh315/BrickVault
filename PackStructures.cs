using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// This is a temporary file, to be moved eventually into better places once I've resolved better path building in the reader to reduce memory

namespace BrickVault
{
    internal class ArchivePackFile // To move to across the board eventually
    {
        public string Path;
        public uint CRC;
        public uint CompressedSize;
        public uint DecompressedSize;
        public uint CompressionType = 0;
        public long Offset;
    }

    public class PathNode
    {
        public string Name;
        public ushort FileIndex = 0;
        public ushort Index;
        public uint MinCRC = uint.MaxValue;
        public uint SecondCRC = uint.MaxValue;

        public List<PathNode> Children = new();

        public PathNode Parent;
        public PathNode? PreviousSibling
        {
            get
            {
                if (Parent == null)
                    return null;

                PathNode prev = null;
                foreach (var child in Parent.Children)
                {
                    if (child == this)
                    {
                        return prev;
                    }
                    prev = child;
                }

                return prev;
            }
        }

        public PathNode? FinalChild
        {
            get
            {
                if (Children.Count == 0) return null;

                return Children[Children.Count - 1];
            }
        }

        public PathNode(string name)
        {
            Name = name;
        }

        public void AddChild(PathNode child)
        {
            child.Parent = this;

            Children.Add(child);
            Children.Sort((a, b) =>
            {
                int crcCompare = b.MinCRC.CompareTo(a.MinCRC); // Descending CRC
                if (crcCompare != 0)
                    return crcCompare;

                Console.WriteLine($"Forcing hand on comparison between {a.Name} and {b.Name}");

                if (a.Name == "MINIFIG" && b.Name == "SMALL") return -1;

                if (Parent == null)
                {
                    return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase); // Descending alphabetical
                }
                else
                {
                    return string.Compare(b.Name, a.Name, StringComparison.OrdinalIgnoreCase); // Descending alphabetical
                }
            });
        }

        public bool HasChild(string name, out PathNode segment)
        {
            foreach (var child in Children)
            {
                if (child.Name == name)
                {
                    segment = child;
                    return true;
                }
            }

            segment = null;
            return false;
        }

        public void SetCRC(uint crc)
        {
            if (crc < MinCRC)
            {
                SecondCRC = MinCRC;
                MinCRC = crc;
                Parent?.SetCRC(crc);
            }
            else if (crc < SecondCRC)
            {
                SecondCRC = crc;
                Parent?.SetCRC(crc);
            }
        }
    }
}
