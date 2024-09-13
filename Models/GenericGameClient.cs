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
        private string ExeName { get; set; }
        public GenericGameClient(string exeName)
        {
            ExeName = exeName;
        }
        public bool IsConnected { get; set; }
        public int ProcId { get { return Memory.GetProcIdFromExe(ExeName); } set { } }

        public bool Connect()
        {
            Console.WriteLine($"Connecting to {ExeName}");
            var pid = ProcId;
            if (pid == 0)
            {
                Console.WriteLine($"{ExeName} not found.");
                Console.WriteLine("Press any key to exit.");
                Console.Read();
                System.Environment.Exit(0);
                return false;
            }
            return true;
        }
    }
}
