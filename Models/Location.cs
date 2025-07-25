
using Archipelago.Core.Json;
using Archipelago.Core.Util;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Archipelago.Core.Models
{
    public class Location : ILocation
    {
        [JsonConverter(typeof(HexToULongConverter))]
        public ulong Address { get; set; }
        public int AddressBit { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public int Id { get; set; }
        public NibblePosition NibblePosition { get; set; }
        public LocationCheckType CheckType { get; set; }
        public string CheckValue { get; set; }
        public LocationCheckCompareType CompareType { get; set; }
        public string RangeStartValue { get; set; }
        public string RangeEndValue { get; set; }

        public bool Check()
        {
            return Helpers.CheckLocation(this);
        }
    }
}
