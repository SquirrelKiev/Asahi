using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace April.Config
{
    public class PoolConditionContainerConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(PoolConditionContainer);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
        {
            var jsonObject = JObject.Load(reader);
            var conditionType = jsonObject[nameof(PoolConditionContainer.conditionType)]?.ToObject<PoolConditionContainer.ConditionType>();

            if (conditionType == null)
            {
                throw new JsonSerializationException(nameof(conditionType));
            }

            var conditionContainer = new PoolConditionContainer(conditionType.Value);

            serializer.Populate(jsonObject[nameof(PoolConditionContainer.data)]!.CreateReader(), conditionContainer.data);

            return conditionContainer;
        }

        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        {
            throw new NotImplementedException("Unused.");
        }

        public override bool CanWrite => false;
    }
}