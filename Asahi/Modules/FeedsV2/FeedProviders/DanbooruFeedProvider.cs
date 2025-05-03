using System.Diagnostics;
using System.Web;
using Asahi.Modules.Models;
using Newtonsoft.Json;

namespace Asahi.Modules.FeedsV2.FeedProviders
{
    public class DanbooruFeedProvider(HttpClient httpClient, DanbooruUtility danbooruUtility) : IFeedProvider
    {
        public string? FeedSource { get; private set; }
        public string DefaultFeedTitle { get; private set; } = "Danbooru";

        private DanbooruPost[]? posts;

        public async Task<bool> Initialize(string feedSource)
        {
            FeedSource = feedSource;
            
            var uri = new Uri(FeedSource);

            httpClient.MaxResponseContentBufferSize = 8000000;
            using var req = await httpClient.GetAsync(uri);
            var json = await req.Content.ReadAsStringAsync();

            // TODO: Validate
            posts = JsonConvert.DeserializeObject<DanbooruPost[]>(json!);

            var query = HttpUtility.ParseQueryString(uri.Query);

            var tags = query["tags"];
            DefaultFeedTitle = tags == null ? "Danbooru Feed" : $"Danbooru: {tags}";

            return true;
        }

        public IEnumerable<int> ListArticleIds()
        {
            Debug.Assert(posts != null);

            return posts.Select(x => x.Id);
        }

        public IAsyncEnumerable<MessageContents> GetArticleMessageContent(int articleId, Color embedColor,
            string? feedTitle)
        {
            Debug.Assert(posts != null);
            
            var post = posts.First(x => x.Id == articleId);

            return danbooruUtility.GetEmbeds(post, embedColor, feedTitle ?? DefaultFeedTitle);
        }
    }
}
