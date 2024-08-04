using Newtonsoft.Json;

#nullable disable

namespace Asahi.Modules.RssAtomFeed.Models
{
    [Serializable]
    public class SubredditPosts
    {
        [JsonProperty("data")]
        public PostData Data { get; set; }
    }
}