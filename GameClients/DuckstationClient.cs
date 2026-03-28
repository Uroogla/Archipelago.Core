using Archipelago.Core;
using Archipelago.Core.Util;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Archipelago.Core.GameClients
{
    public class DuckstationClient : IGameClient
    {
        public DuckstationClient()
        {
            ProcessName = "duckstation-qt-x64-ReleaseLTCG";
            ProcId = Memory.GetProcIdFromExe(ProcessName);
        }
        public bool IsConnected { get; set; }
        public int ProcId { get; set; }
        private int OldProcId { get; set; }
        public string ProcessName { get; set; }

        public bool Connect()
        {
            ProcessName = "duckstation-qt-x64-ReleaseLTCG";
            if (ProcId == 0)
            {
                Log.Verbose($"Connecting to {ProcessName}");
            }
            try
            {
                ProcId = Memory.GetProcIdFromExe(ProcessName);
            }
            catch
            {
                Log.Error($"{ProcessName} not found.");
                return false;
            }
            
            if (ProcId == 0)
            {
                Log.Error($"{ProcessName} not found.");
                return false;
            }
            if (OldProcId != 0 && OldProcId != ProcId)
            {
                Log.Error($"Either a second copy of the program was opened\r\nor the process ID has changed.");
                return false;
            }
            OldProcId = ProcId;
            return true;
        }
    }
}
