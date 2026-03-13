using Archipelago.Core;
using Archipelago.Core.Util;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
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
            return true;
        }
    }
}
