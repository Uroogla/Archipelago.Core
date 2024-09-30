using Archipelago.Core.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Archipelago.Core.Models
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
            Console.WriteLine($"Connecting to {ProcessName}");
            var pid = ProcId;
            if (pid == 0)
            {
                Console.WriteLine($"{ProcessName} not found.");
                Console.WriteLine("Press any key to exit.");
                Console.Read();
                System.Environment.Exit(0);
                return false;
            }
            return true;
        }
    }
}
