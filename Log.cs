using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BrickVault
{
    public static class Log
    {
        [Conditional("LOG_INFO")]
        public static void Info(string msg) => Console.WriteLine(msg);
    }
}
