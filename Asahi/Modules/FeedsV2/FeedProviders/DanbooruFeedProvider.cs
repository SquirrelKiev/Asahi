using System.Diagnostics;
using System.Runtime.CompilerServices;
using Asahi.Modules.Models;

namespace Asahi.Modules.FeedsV2.FeedProviders
{
    public class DanbooruFeedProvider(IDanbooruApi danbooruApi, DanbooruUtility danbooruUtility) : IFeedProvider
    {
        public string? FeedSource { get; private set; }
        public string DefaultFeedTitle { get; private set; } = "Danbooru";
        public ArticleIdScope ArticleIdScope => ArticleIdScope.FeedSource | ArticleIdScope.ChannelForPoll;

        private DanbooruPost[]? posts;

        public async Task<bool> Initialize(string feedSource, object? continuationToken = null,
            CancellationToken cancellationToken = default)
        {
            FeedSource = feedSource;
            var regexMatch = CompiledRegex.DanbooruFeedRegex().Match(feedSource);
            var tags = regexMatch.Groups["tags"].Value;

            var req = await danbooruApi.GetPosts(tags, cancellationToken);

            if (!req.IsSuccessful)
            {
                throw req.Error;
            }

            posts = req.Content;

            DefaultFeedTitle = string.IsNullOrWhiteSpace(tags) ? "Danbooru Feed" : $"Danbooru: {tags}".Truncate(64, false);

            return true;
        }

        public object? GetContinuationToken()
        {
            return null;
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
