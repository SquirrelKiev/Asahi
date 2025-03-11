#nullable disable

using Newtonsoft.Json;

namespace Asahi.Modules.Models
{
    [Serializable]
    public class PostData
    {
        [JsonProperty("children")]
        public List<PostChild> Children { get; set; }

        [JsonProperty("facets")]
        public object Facets { get; set; }
    }
}
