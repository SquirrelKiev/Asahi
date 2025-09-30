using System.Diagnostics;
using Asahi.Modules.Models;
using Newtonsoft.Json;

namespace Asahi.Modules.FeedsV2.FeedProviders
{
    public class RedditFeedProvider(IRedditApi redditApi) : IFeedProvider
    {
        public string? FeedSource { get; private set; }

        public string DefaultFeedTitle { get; private set; } = "Reddit Feed";

        // HACK: for debugging. remove later
        public string? Json { get; private set; }

        private List<PostChild>? posts;

        private RedditContinuationToken? lastReceivedPostPreviousRun;

        public async Task<bool> Initialize(string feedSource, object? continuationToken = null,
            CancellationToken cancellationToken = default)
        {
            FeedSource = feedSource;

            var regex = CompiledRegex.RedditFeedRegex().Match(feedSource);

            var feedType = regex.Groups["type"].Value;

            if (feedType != "r")
            {
                return false;
            }

            var subreddit = regex.Groups["subreddit"].Value;

            DefaultFeedTitle = $"r/{subreddit}";

            var lastReceivedPost = continuationToken as RedditContinuationToken?;
            lastReceivedPostPreviousRun = lastReceivedPost;

            var res = await redditApi.GetSubredditPostsRaw(subreddit, before: lastReceivedPost?.LastReceivedId,
                cancellationToken: cancellationToken);
            if (!res.IsSuccessful)
                return false;

            var deserialized = JsonConvert.DeserializeObject<SubredditPosts>(res.Content)!;

            if (deserialized.Kind != "Listing")
                return false;
            
            posts = deserialized.Data.Children.Where(x =>
                    lastReceivedPostPreviousRun == null ||
                    x.Data.CreatedUTC > lastReceivedPostPreviousRun.Value.LastReceivedTimestamp)
                .ToList();

            Json = res.Content;

            return true;
        }

        public object? GetContinuationToken()
        {
            Debug.Assert(posts != null);

            var lastReceivedPost = posts.FirstOrDefault();

            if (lastReceivedPost != null)
                return new RedditContinuationToken(lastReceivedPost.Data.Name, lastReceivedPost.Data.CreatedUTC);
            else
                return lastReceivedPostPreviousRun;
        }

        public IEnumerable<int> ListArticleIds()
        {
            Debug.Assert(posts != null);

            return ListArticleRedditIds().Select(x => x.GetHashCode());
        }

        public IEnumerable<string> ListArticleRedditIds()
        {
            Debug.Assert(posts != null);

            return posts.Select(x => x.Data.Name);
        }

        public IAsyncEnumerable<MessageContents> GetArticleMessageContent(int articleId, Color embedColor,
            string? feedTitle, CancellationToken cancellationToken = default)
        {
            Debug.Assert(posts != null);

            var post = posts.First(x => x.Data.Name.GetHashCode() == articleId);

            IEnumerable<MessageContents> contents = [GetArticleMessageContent(post.Data)];

            return contents.ToAsyncEnumerable();
        }

        private static MessageContents GetArticleMessageContent(Post post)
        {
            if (post.Spoiler)
                return new MessageContents($"|| https://www.rxddit.com{post.Permalink} ||");
            else
                return new MessageContents($"https://www.rxddit.com{post.Permalink}");
        }

        // tracking timestamp to get around a reddit API bug where it randomly decides to send old JSON sometimes.
        private readonly record struct RedditContinuationToken(string LastReceivedId, ulong LastReceivedTimestamp);
    }
}
