using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Archipelago.Core.Models
{
    public class LocationCompletedEventArgs : EventArgs
    {
        public Location CompletedLocation { get; set; }
        public LocationCompletedEventArgs(Location location)
        {
            CompletedLocation = location;
        }
    }
}
