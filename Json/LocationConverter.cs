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
    internal class LocationConverter : JsonConverter<ILocation>
    {
        public override bool CanWrite => true;

        public override ILocation ReadJson(JsonReader reader, Type objectType, ILocation existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return null;

            var jObject = JObject.Load(reader);

            // Check the CheckType value to determine which class to deserialize to
            if (jObject.TryGetValue("CheckType", out var checkTypeToken))
            {
                var checkType = checkTypeToken.ToObject<LocationCheckType>();

                // If CheckType is AND or OR, deserialize as CompositeLocation
                if (checkType == LocationCheckType.AND || checkType == LocationCheckType.OR)
                {
                    return jObject.ToObject<CompositeLocation>(serializer);
                }
                else
                {
                    // Otherwise, deserialize as regular Location
                    return jObject.ToObject<Location>(serializer);
                }
            }

            // Fallback to Location if CheckType is missing
            return jObject.ToObject<Location>(serializer);
        }
        public override void WriteJson(JsonWriter writer, ILocation value, JsonSerializer serializer)
        {
            switch (value)
            {
                case CompositeLocation composite:
                    serializer.Serialize(writer, composite);
                    break;
                case Location location:
                    serializer.Serialize(writer, location);
                    break;
                default:
                    throw new JsonSerializationException($"Unexpected ILocation type: {value.GetType()}");
            }
        }
    }
        
    
}
