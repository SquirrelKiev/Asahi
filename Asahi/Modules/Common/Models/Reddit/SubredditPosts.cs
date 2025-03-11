#nullable disable

using Newtonsoft.Json;

namespace Asahi.Modules.Models
{
    [Serializable]
    public class SubredditPosts
    {
        [JsonProperty("data")]
        public PostData Data { get; set; }
    }
}