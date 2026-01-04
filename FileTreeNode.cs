using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BrickVault
{
    public class FileTreeNode // To move to eventually
    {
        internal ushort ParentIndex;
        internal ushort FileIndex;
        internal ushort FinalChild;
        internal ushort PreviousSibling;
        internal long PathCRC;

        private string segment;
        public string Segment
        {
            get => segment;
            set
            {
                segment = value.ToLower();
            }
        }

        public string SegmentUpper => Segment.ToUpper();

        public FileTree FileTree;

        public NewArchiveFile File;

        public FileTreeNode? Parent => ((ParentIndex == 0 || ParentIndex == 0xffff /* LJW_PC */) && string.IsNullOrEmpty(Segment)) ? null : FileTree.Nodes[ParentIndex];

        public bool HasChildren => FinalChild != 0;

        public string Path => BuildPathString();

        public string BuildPathString()
        {
            GetPathShape(out int count, out int totalLen);
            var rented = ArrayPool<char>.Shared.Rent(totalLen);
            try
            {
                int written = FillPathInto(new Span<char>(rented, 0, totalLen), count);
                return new string(rented, 0, written);
            }
            finally
            {
                ArrayPool<char>.Shared.Return(rented);
            }
        }

        // High-performance: caller provides destination buffer (reusable across many calls)
        public bool TryBuildPath(Span<char> destination, out int written)
        {
            GetPathShape(out int count, out int totalLen);
            if (destination.Length < totalLen)
            {
                written = 0;
                return false;
            }
            written = FillPathInto(destination, count);
            return true;
        }

        // Enumerate segments root → leaf (no allocations); handy for custom joins
        public IEnumerable<string> EnumerateSegments()
        {
            // collect into temporary stack (indices), then yield in order
            int count = 0;
            for (var n = this; n != null; n = n.Parent) count++;
            var stack = new FileTreeNode[count];
            for (var n = this; n != null; n = n.Parent) stack[--count] = n;
            for (int i = 0; i < stack.Length; i++) yield return stack[i].Segment;
        }

        private void GetPathShape(out int count, out int totalLen)
        {
            count = 0; totalLen = 0;

            for (var n = this; n != null; n = n.Parent)
            {
                if (n.Parent == null) // stop before root
                    break;

                count++;
                totalLen += n.Segment?.Length ?? 0;
            }

            totalLen += Math.Max(0, count - 1); // separators
        }

        private int FillPathInto(Span<char> dest, int count)
        {
            // Use the already known count instead of recalculating
            var chain = new FileTreeNode[count];
            var n = this;
            for (int i = count - 1; i >= 0; i--)
            {
                chain[i] = n!;
                n = n!.Parent;
            }

            int pos = 0;
            char sep = '\\';
            for (int i = 0; i < chain.Length; i++)
            {
                if (i > 0) dest[pos++] = sep;
                var seg = chain[i].Segment.AsSpan();
                seg.CopyTo(dest.Slice(pos));
                pos += seg.Length;
            }
            return pos;
        }
    }
}
