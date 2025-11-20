using System.Net;
using Asahi.Modules.FeedsV2.FeedProviders;

namespace Asahi.Modules.FeedsV2
{
    public class DefaultFeedProviderFactory(IHttpClientFactory httpClientFactory, DanbooruUtility danbooruUtility, IRedditApi redditApi, IDanbooruApi danbooruApi) : IFeedProviderFactory
    {
        public IFeedProvider? GetFeedProvider(string feedSource)
        {
            if (feedSource == "dummy:")
                return new DummyFeedProvider();

            if (CompiledRegex.DanbooruFeedRegex().IsMatch(feedSource))
                return new DanbooruFeedProvider(danbooruApi, danbooruUtility);
            if (feedSource.StartsWith("https://nyaa.si"))
                return new NyaaFeedProvider(httpClientFactory.CreateClient("rss"));
            if (CompiledRegex.BskyPostRegex().IsMatch(feedSource) || CompiledRegex.OpenRssBskyPostRegex().IsMatch(feedSource))
                return new BskyFeedProvider(httpClientFactory.CreateClient("rss"));
            if (CompiledRegex.RedditFeedRegex().IsMatch(feedSource))
                return new RedditFeedProvider(redditApi);

            // TODO: SSRF bad!
            if (feedSource.StartsWith("http://") || feedSource.StartsWith("https://"))
                return new RssFeedProvider(httpClientFactory.CreateClient("rss"));
                
            return null;
        }

        public string? GetHyperlinkableFeedUrl(string feedSource)
        {
            var danbooruMatch = CompiledRegex.DanbooruFeedRegex().Match(feedSource);
            if (danbooruMatch.Success)
            {
                var tags = danbooruMatch.Groups["tags"].Value;
                return $"https://danbooru.donmai.us/posts?tags={WebUtility.UrlEncode(tags)}";
            }

            var redditMatch = CompiledRegex.RedditFeedRegex().Match(feedSource);
            if (redditMatch.Success)
            {
                var type = redditMatch.Groups["type"].Value;
                var subreddit = redditMatch.Groups["subreddit"].Value;

                return $"https://reddit.com/{type}/{subreddit}/new";
            }

            return null;
        }
    }
}
