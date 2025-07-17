using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Archipelago.Core.Models
{
    public class GameState
    {
        public GameState()
        {
            CompletedLocations = new List<Location>();
            ReceivedItems = new List<Item>();
        }
        
        public List<Location> CompletedLocations { get; set; }
        public List<Item> ReceivedItems { get; set; }
        public int LastCheckedIndex { get; set; }
    }
}
