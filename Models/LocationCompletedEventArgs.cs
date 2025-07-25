using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Archipelago.Core.Models
{
    public class LocationCompletedEventArgs : EventArgs
    {
        public ILocation CompletedLocation { get; set; }
        public LocationCompletedEventArgs(ILocation location)
        {
            CompletedLocation = location;
        }
    }
}
