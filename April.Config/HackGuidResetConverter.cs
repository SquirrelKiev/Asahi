using Newtonsoft.Json;

namespace April.Config;

/// <summary>
/// hack solution for deep cloning stuff. intended for the config editor.
/// </summary>
public class HackGuidResetConverter : JsonConverter
{
    // GARBAGE but works - good thing we're singlethreaded :tomfoolery:
    // could probably just pass in the new converter but
    public static bool shouldResetGuid = false;

    public override bool CanConvert(Type objectType)
    {
        return objectType == typeof(Guid);
    }

    public override object ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    {
        return Guid.NewGuid();
    }

    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        serializer.Serialize(writer, value);
    }
    
    public override bool CanRead => shouldResetGuid;
    public override bool CanWrite => false;
}