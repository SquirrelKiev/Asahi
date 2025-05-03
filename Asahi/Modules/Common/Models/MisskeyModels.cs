namespace Asahi.Modules.Models;

using System.Collections.Generic;
using Newtonsoft.Json;

public class MisskeyNote
{
    [JsonProperty("files")]
    public required MisskeyFile[] Files { get; set; }
}

public class MisskeyFile
{
    [JsonProperty("url")]
    public required string Url { get; set; }

    [JsonProperty("isSensitive")]
    public bool IsSensitive { get; set; }

    [JsonProperty("properties")]
    public required MisskeyImageProperties Properties { get; set; }
}

public class MisskeyImageProperties
{
    [JsonProperty("width")]
    public int Width { get; set; }

    [JsonProperty("height")]
    public int Height { get; set; }
}
