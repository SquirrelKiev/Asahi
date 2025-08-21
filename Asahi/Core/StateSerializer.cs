namespace Asahi;

public static class StateSerializer
{
    public static string SerializeObject<T>(T obj, string id)
    {
        ArgumentNullException.ThrowIfNull(obj, nameof(obj));
        
        return id + SerializeObject(obj);
    }

    public static string SerializeObject(object obj)
    {
        using MemoryStream ms = new();
        ProtoBuf.Serializer.Serialize(ms, obj);

        return Base2048Converter.Encode(ms.ToArray());
    }

    public static T? DeserializeObject<T>(string obj)
    {
        return DeserializeObject<T>(Base2048Converter.Decode(obj));
    }
    
    public static T? DeserializeObject<T>(byte[] obj)
    {
        using MemoryStream ms = new(obj);
        return ProtoBuf.Serializer.Deserialize<T>(ms);
    }
}
