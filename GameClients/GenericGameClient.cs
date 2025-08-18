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
        }
        public bool IsConnected { get; set; }
        public int ProcId { get { return Memory.GetProcIdFromExe(ProcessName); } set { } }
        public string ProcessName { get; set; }

        public bool Connect()
        {
            Log.Information($"Connecting to {ProcessName}");
            var pid = ProcId;
            if (pid == 0)
            {
                Log.Warning($"{ProcessName} not found.");
                IsConnected = false;
            }
            else IsConnected = true;
            return IsConnected;
        }
    }
}
