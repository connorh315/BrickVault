using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace BrickVault
{
    public class NewArchiveFile : ArchiveFile
    {
        public override string Path { get => Node.Path; set => base.Path = value; }

        public FileTreeNode Node;
    }
}
