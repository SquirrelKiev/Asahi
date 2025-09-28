using System.Diagnostics;
using Asahi.Modules.Models;
using CodeHollow.FeedReader;

namespace Asahi.Modules.FeedsV2.FeedProviders
{
    public class RedditFeedProvider(IRedditApi redditApi) : IFeedProvider
    {
        public string? FeedSource { get; private set; }

        public string DefaultFeedTitle { get; private set; } = "Reddit Feed";

        // HACK: for debugging. remove later
        public string? Json { get; private set; }

        private SubredditPosts? posts;

        public async Task<bool> Initialize(string feedSource, CancellationToken cancellationToken = default)
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

            var res = await redditApi.GetSubredditPosts(subreddit, cancellationToken);
            if (!res.IsSuccessful)
                return false;

            posts = res.Content;
            if (res.RequestMessage?.Content != null)
                Json = await res.RequestMessage.Content.ReadAsStringAsync(cancellationToken);

            return posts.Kind == "Listing";
        }

        public IEnumerable<int> ListArticleIds()
        {
            Debug.Assert(posts != null);

            return ListArticleRedditIds().Select(x => x.GetHashCode());
        }

        public IEnumerable<string> ListArticleRedditIds()
        {
            Debug.Assert(posts != null);

            return posts.Data.Children.Select(x => x.Data.Id);
        }

        public IAsyncEnumerable<MessageContents> GetArticleMessageContent(int articleId, Color embedColor,
            string? feedTitle, CancellationToken cancellationToken = default)
        {
            Debug.Assert(posts != null);

            var post = posts.Data.Children.First(x => x.Data.Id.GetHashCode() == articleId);

            IEnumerable<MessageContents> contents = [GetArticleMessageContent(post.Data)];

            return contents.ToAsyncEnumerable();
        }

        private MessageContents GetArticleMessageContent(Post post)
        {
            if (post.Spoiler)
                return new MessageContents($"|| https://www.rxddit.com{post.Permalink} ||");
            else
                return new MessageContents($"https://www.rxddit.com{post.Permalink}");
        }
    }
}
