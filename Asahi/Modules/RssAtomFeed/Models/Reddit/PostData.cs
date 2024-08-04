using Newtonsoft.Json;

#nullable disable

namespace Asahi.Modules.RssAtomFeed.Models
{
    [Serializable]
    public class PostData
    {
        [JsonProperty("children")]
        public List<PostChild> Children { get; set; }

        [JsonProperty("facets")]
        public object Facets { get; set; }  // TODO - Find out what this is.  It's used by Models.Search.GetSearch.  Comes up empty in tests even when include_facets is true.  --Kris
    }
}
