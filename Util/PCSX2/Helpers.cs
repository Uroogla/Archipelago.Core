using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Archipelago.Core.Util.PCSX2
{
    public class Helpers
    {
        public static ulong GetEEmemOffset()
        {
            PCSX2MemoryHelper memoryHelper = new PCSX2MemoryHelper();
            var eeromAddress = memoryHelper.FindEEromAddress();
            return (ulong)eeromAddress;
        }
    }
}
