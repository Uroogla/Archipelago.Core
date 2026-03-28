using Archipelago.Core.Util;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Archipelago.Core.GameClients
{
    public class GenericGameClient : IGameClient
    {
        public GenericGameClient(string exeName)
        {
            ProcessName = exeName;
            OldProcId = 0;
        }
        public bool IsConnected { get; set; }
        public int ProcId { get { return Memory.GetProcIdFromExe(ProcessName); } set { } }
        private int OldProcId { get; set; }
        public string ProcessName { get; set; }

        public bool Connect()
        {
            Log.Verbose($"Connecting to {ProcessName}");
            var pid = ProcId;
            if (pid == 0)
            {
                Log.Error($"{ProcessName} not found.");
                IsConnected = false;
            }
            if (OldProcId != 0 && OldProcId != pid)
            {
                Log.Error($"Either a second copy of the program was opened/r/nor the process ID has changed.");
                IsConnected = false;
            }
            else IsConnected = true;
            OldProcId = pid;
            return IsConnected;
        }
    }
}
