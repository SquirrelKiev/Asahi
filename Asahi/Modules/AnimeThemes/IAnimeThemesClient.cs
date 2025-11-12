using Refit;

namespace Asahi.Modules.AnimeThemes;

public interface ILegacyAnimeThemesClient
{
    [Get("/search")]
    public Task<SearchResponse> SearchAsync([Query][AliasAs("q")] string query, [Query(delimiter: "")] SearchQueryParams queryParams);

    // there's gotta be a better way that I'm not aware of
    public class SearchQueryParams
    {
        [AliasAs("include")]
        public Dictionary<string, string[]> Include { get; set; } = new()
        {
            {
                "[anime]",
                ["animethemes.animethemeentries.videos", "animethemes.song.artists", "images"]
            }
        };

        [AliasAs("fields")]
        public Dictionary<string, string[]> Fields { get; set; } = new()
        {
            {
                "[search]",
                ["anime"]
            }
        };
    }
}
