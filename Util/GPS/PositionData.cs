using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Archipelago.Core.Util.GPS
{
    public struct PositionData
    {
        public int MapId { get; set; }
        public string MapName { get; set; }
        public string Region { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
    }
}
