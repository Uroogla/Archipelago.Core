using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Archipelago.Core.Util.GPS
{
    public class MapChangedEventArgs : EventArgs
    {
        public int OldMapId { get; }
        public string OldMapName { get; }
        public int NewMapId { get; }
        public string NewMapName { get; }

        public MapChangedEventArgs(int oldMapId, string oldMapName, int newMapId, string newMapName)
        {
            OldMapId = oldMapId;
            OldMapName = oldMapName;
            NewMapId = newMapId;
            NewMapName = newMapName;
        }
    }
}
