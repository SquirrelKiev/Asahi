#nullable disable

using Newtonsoft.Json;

namespace Asahi.Modules.Models
{
    [Serializable]
    public class SubredditChild
    {
        [JsonProperty("data")]
        public Subreddit Data { get; set; }
    }
}
