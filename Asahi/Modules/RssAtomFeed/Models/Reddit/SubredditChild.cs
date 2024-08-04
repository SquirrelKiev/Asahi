using Newtonsoft.Json;

#nullable disable

namespace Asahi.Modules.RssAtomFeed.Models
{
    [Serializable]
    public class SubredditChild
    {
        [JsonProperty("data")]
        public Subreddit Data { get; set; }
    }
}
