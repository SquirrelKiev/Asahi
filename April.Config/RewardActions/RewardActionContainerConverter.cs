using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace April.Config
{
    public class RewardActionContainerConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(RewardActionContainer);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
        {
            var jsonObject = JObject.Load(reader);
            var actionType = jsonObject[nameof(RewardActionContainer.actionType)]?.ToObject<RewardActionContainer.ActionType>();

            if (actionType == null)
            {
                throw new JsonSerializationException(nameof(actionType));
            }

            var actionContainer = new RewardActionContainer(actionType.Value);

            serializer.Populate(jsonObject[nameof(RewardActionContainer.data)]!.CreateReader(), actionContainer.data);

            return actionContainer;
        }

        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        {
            throw new NotImplementedException("Unused.");
        }

        public override bool CanWrite => false;
    }
}