using Asahi.Modules.FeedsV2.FeedProviders;

namespace Asahi.Modules.FeedsV2
{
    public class DefaultFeedProviderFactory(IHttpClientFactory httpClientFactory, DanbooruUtility danbooruUtility, IRedditApi redditApi) : IFeedProviderFactory
    {
        public IFeedProvider? GetFeedProvider(string feedSource)
        {
            if (feedSource == "dummy:")
                return new DummyFeedProvider();

            if (feedSource.StartsWith("https://danbooru.donmai.us/posts.json"))
                return new DanbooruFeedProvider(httpClientFactory.CreateClient(), danbooruUtility);
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
    }
}
