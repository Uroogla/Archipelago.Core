using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Archipelago.Core.Util.Duckstation
{
    public class Helpers
    {
        public static ulong GetEEmemOffset()
        {
            DuckstationMemoryHelper memoryHelper = new DuckstationMemoryHelper();
            var eeromAddress = memoryHelper.FindEEromAddress();
            return (ulong)eeromAddress;
        }
    }
}
