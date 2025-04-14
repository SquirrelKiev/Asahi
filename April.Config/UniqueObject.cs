using Newtonsoft.Json;

namespace April.Config;

public abstract class UniqueObject
{
    [JsonProperty("guid"), JsonConverter(typeof(HackGuidResetConverter))]
    public Guid Guid { get; private set; } = Guid.NewGuid();

    //public override int GetHashCode()
    //{
    //    return guid.GetHashCode();
    //}

    //public override bool Equals(object? obj)
    //{
    //    return guid.Equals(obj);
    //}
}