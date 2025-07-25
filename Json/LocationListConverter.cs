using Archipelago.Core.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Archipelago.Core.Json
{
    public class LocationListConverter : JsonConverter<List<ILocation>>
    {
        private readonly LocationConverter _locationConverter = new LocationConverter();

        public override List<ILocation> ReadJson(JsonReader reader, Type objectType, List<ILocation> existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return null;

            var locations = new List<ILocation>();
            var array = JArray.Load(reader);

            foreach (var item in array)
            {
                // Use the LocationConverter to handle each item
                using var itemReader = item.CreateReader();
                var location = _locationConverter.ReadJson(itemReader, typeof(ILocation), null, false, serializer);
                locations.Add(location);
            }

            return locations;
        }

        public override void WriteJson(JsonWriter writer, List<ILocation> value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
                return;
            }

            writer.WriteStartArray();
            foreach (var location in value)
            {
                _locationConverter.WriteJson(writer, location, serializer);
            }
            writer.WriteEndArray();
        }
    }
}


