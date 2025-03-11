#nullable disable

using Newtonsoft.Json;

namespace Asahi.Modules.Models
{
    [Serializable]
    public class PostChild
    {
        [JsonProperty("data")]
        public Post Data { get; set; }
    }
}
