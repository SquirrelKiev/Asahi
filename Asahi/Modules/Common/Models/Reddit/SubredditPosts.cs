#nullable disable

using Newtonsoft.Json;

namespace Asahi.Modules.Models
{
    [Serializable]
    public class SubredditPosts
    {
        [JsonProperty("kind")]
        public string Kind { get; set; }
        
        [JsonProperty("data")]
        public PostData Data { get; set; }
    }
}
