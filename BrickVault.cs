using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BrickVault
{
    public class BrickVault
    {
        public static DATFile Open(string fileLocation)
        {
            return DATFile.Open(fileLocation);
        }
    }
}
