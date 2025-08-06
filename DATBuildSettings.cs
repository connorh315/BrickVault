using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static BrickVault.Types.DATFile;

namespace BrickVault
{
    public class DATBuildSettings
    {
        public DATBuildSettings() { }

        public string BuilderID { get; set; }

        public string OutputFileLocation { get; set; }

        public string InputFolderLocation { get; set; }

        public DATVersion Version { get; set; }

        public bool ShouldCreateHDR = false;

        public bool IsMod { get; private set; } = false;
        public string ModCreator { get; private set; }
        public string ModName { get; private set; }
        public string ModVersion { get; private set; }
        public void SetupAsMod(string creator, string name, string version)
        {
            IsMod = true;
            ModCreator = creator;
            ModName = name;
            ModVersion = version;
        }
    }
}
