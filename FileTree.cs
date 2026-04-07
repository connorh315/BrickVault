using BrickVault.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BrickVault
{
    public class FileTree
    {
        public FileTreeNode[] Nodes;

        public FileTreeNode Root { get; set; }

        public FileTree(uint nodeCount)
        {
            Nodes = new FileTreeNode[nodeCount];
        }

        public FileTreeNode[] GetChildren(FileTreeNode node)
        {
            uint childCount = 0;
            if (node.FinalChild == 0) return Array.Empty<FileTreeNode>();

            for (ushort i = node.FinalChild; i != 0; i = Nodes[i].PreviousSibling)
            {
                childCount++;
            }

            FileTreeNode[] children = new FileTreeNode[childCount];
            int index = 0;
            for (ushort i = node.FinalChild; i != 0; i = Nodes[i].PreviousSibling)
            {
                children[index++] = Nodes[i];
            }

            return children;
        }

        public IEnumerable<FileTreeNode> EnumerateChildren(FileTreeNode node)
        {
            for (ushort i = node.FinalChild; i != 0; i = Nodes[i].PreviousSibling)
                yield return Nodes[i];      // yields in reverse order
        }

        public void SetFileTree(FileTreeNode[] nodes, FileTreeNode root = null)
        {
            Nodes = nodes;
            Root = root;
            if (root == null)
            {
                root = Nodes[0];
            }
        }

        public FileTreeNode GetNode(string path)
        {
            string[] segments = path.Split('\\');

            FileTreeNode parent = Root;

            for (int i = 0; i < segments.Length; i++)
            {
                var seg = segments[i];

                ushort child = parent.HasChild(seg);

                if (child == 0) return null;

                parent = Nodes[child];
            }

            return parent;
        }

        public NewArchiveFile GetFile(string path)
        {
            var node = GetNode(path);
            return node?.File;
        }

        public IEnumerable<NewArchiveFile> EnumerateFilesRecursive(FileTreeNode node)
        {
            if (node.File != null)
                yield return node.File;

            foreach (var child in EnumerateChildren(node))
            {
                foreach (var f in EnumerateFilesRecursive(child))
                    yield return f;
            }
        }
    }
}
