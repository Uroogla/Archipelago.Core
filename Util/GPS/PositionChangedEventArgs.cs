using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Archipelago.Core.Util.GPS.GPSHandler;

namespace Archipelago.Core.Util.GPS
{
    public class PositionChangedEventArgs : EventArgs
    {
        public float OldX { get; }
        public float OldY { get; }
        public float OldZ { get; }
        public float NewX { get; }
        public float NewY { get; }
        public float NewZ { get; }

        public PositionChangedEventArgs(float oldX, float oldY, float oldZ, float newX, float newY, float newZ)
        {
            OldX = oldX;
            OldY = oldY;
            OldZ = oldZ;
            NewX = newX;
            NewY = newY;
            NewZ = newZ;
        }
    }
}
