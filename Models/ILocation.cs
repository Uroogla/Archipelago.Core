using Archipelago.Core.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Archipelago.Core.Models
{
    public interface ILocation
    {
        int Id { get; set; }
        string Name { get; set; }
        string Category { get; set; }
        public LocationCheckType CheckType { get; set; }
        public bool Check();
    }
}
