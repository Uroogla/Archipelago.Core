using Archipelago.Core.Util;
using Archipelago.Core.Util.PlatformMemory;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Archipelago.Core.Helpers
{
    public class GameClient : IGameClient
    {
        public GameClient(string exeName)
        {
            ProcessName = exeName;
            OldProcId = 0;
        }
        public bool IsConnected { get; set; }
        public int ProcId { get { return PlatformMemory.GetProcIdFromExe(ProcessName); } set { } }
        private int OldProcId { get; set; }
        public string ProcessName { get; set; }

        public bool Connect()
        {
            Log.Verbose($"Connecting to {ProcessName}");
            var pid = 0;
            try
            {
                pid = ProcId;
            }
            catch (Exception ex)
            {
                // Error is logged elsewhere, so no need to print it again here.
            }
            if (pid == 0)
            {
                Log.Error($"{ProcessName} not found.");
                IsConnected = false;
            }
            else if (OldProcId != 0 && OldProcId != pid)
            {
                Log.Error($"Either a second copy of the program was opened or the process ID has changed.");
                IsConnected = false;
            }
            else IsConnected = true;
            OldProcId = pid;
            return IsConnected;
        }
    }
}
