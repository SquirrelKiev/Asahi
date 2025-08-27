using System.Diagnostics;
using System.Runtime.CompilerServices;
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

        public async Task<bool> Initialize(string feedSource, CancellationToken cancellationToken = default)
        {
            FeedSource = feedSource;
            
            var uri = new Uri(FeedSource);

            httpClient.MaxResponseContentBufferSize = 8000000;
            using var req = await httpClient.GetAsync(uri, cancellationToken);
            var json = await req.Content.ReadAsStringAsync(cancellationToken);

            // TODO: Validate
            posts = JsonConvert.DeserializeObject<DanbooruPost[]>(json);

            var query = HttpUtility.ParseQueryString(uri.Query);

            var tags = query["tags"];
            DefaultFeedTitle = tags == null ? "Danbooru Feed" : $"Danbooru: {tags}".Truncate(64, false);

            return true;
        }

        public IEnumerable<int> ListArticleIds()
        {
            Debug.Assert(posts != null);

            return posts.Select(x => x.Id);
        }

        public async IAsyncEnumerable<MessageContents> GetArticleMessageContent(int articleId, Color embedColor,
            string? feedTitle, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            Debug.Assert(posts != null);
            
            var post = posts.First(x => x.Id == articleId);

            yield return new MessageContents(await danbooruUtility.GetComponent(post, embedColor, feedTitle ?? DefaultFeedTitle, cancellationToken: cancellationToken));
        }
    }
}
