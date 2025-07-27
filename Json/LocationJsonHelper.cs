using Archipelago.Core.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Archipelago.Core.Json
{
    public class LocationJsonHelper
    {
        private static LocationJsonHelper _instance;
        public static LocationJsonHelper Instance => _instance ??= new LocationJsonHelper();
        private readonly JsonSerializerSettings _settings = new JsonSerializerSettings()
        {
            Converters = { new LocationConverter() },
            Formatting = Formatting.Indented 
        };

        public LocationJsonHelper()
        {
        }

        public string SerializeLocation(ILocation location)
        {
            return JsonConvert.SerializeObject(location, _settings);
        }

        public string SerializeLocations(List<ILocation> locations)
        {
            return JsonConvert.SerializeObject(locations, _settings);
        }

        public ILocation DeserializeLocation(string json)
        {
            return JsonConvert.DeserializeObject<ILocation>(json, _settings);
        }

        public T DeserializeLocation<T>(string json) where T : ILocation
        {
            return JsonConvert.DeserializeObject<T>(json, _settings);
        }

        public List<ILocation> DeserializeLocations(string json)
        {
            return JsonConvert.DeserializeObject<List<ILocation>>(json, _settings);
        }
    }
}
