using Newtonsoft.Json;

namespace April.Config
{
    [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore)]
    public class GachaMessage
    {
        public string? content = null;

        public List<GachaEmbed>? embeds;
    }

    [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore)]
    public class GachaEmbed
    {
        public GachaEmbedAuthor? author;

        public string? title;

        public string? description;

        public string? url;

        public uint? color;

        public List<GachaEmbedField>? fields;

        public GachaEmbedMedia? image;

        public GachaEmbedMedia? thumbnail;

        public GachaEmbedFooter? footer;

        public DateTime? timestamp;
    }

    [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore)]
    public class GachaEmbedAuthor
    {
        public string? name;

        public string? url;

        [JsonProperty("icon_url")]
        public string? iconUrl;
    }

    [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore)]
    public class GachaEmbedMedia
    {
        public string? url;
    }

    [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore)]
    public class GachaEmbedFooter
    {
        public string? text;

        [JsonProperty("icon_url")]
        public string? iconUrl;
    }

    [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore)]
    public class GachaEmbedField
    {
        public string name = "";

        public string value = "";

        public bool inline = false;
    }
}
