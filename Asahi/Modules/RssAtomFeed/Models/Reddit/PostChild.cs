using Newtonsoft.Json;

#nullable disable

namespace Asahi.Modules.RssAtomFeed.Models
{
    [Serializable]
    public class PostChild
    {
        [JsonProperty("data")]
        public Post Data { get; set; }
    }
}
