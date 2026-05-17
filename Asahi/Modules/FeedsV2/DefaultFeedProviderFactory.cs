using System.Net;
using System.Web;
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

        public string? NormalizeFeedSource(string feedSource)
        {
            if (feedSource == "dummy:")
                return "dummy:";

            var danbooruRegex = CompiledRegex.DanbooruFeedRegex().Match(feedSource);
            if (danbooruRegex.Success)
                return $"danbooru: {danbooruRegex.Groups["tags"].Value.Trim()}";
            
            var redditRegex = CompiledRegex.RedditFeedRegex().Match(feedSource);
            if (redditRegex.Success)
                return $"reddit: r/{redditRegex.Groups["subreddit"].Value.Trim()}";

            if (Uri.TryCreate(feedSource, UriKind.Absolute, out var uri))
            {
                if (uri is { Host: "danbooru.donmai.us", AbsolutePath: "/posts" or "/posts.json" })
                {
                    var query = HttpUtility.ParseQueryString(uri.Query);

                    var tags = query.Get("tags");

                    if (tags != null)
                    {
                        return $"danbooru: {tags.Trim().ToLowerInvariant()}";
                    }
                }
                else if (uri.Host is "reddit.com" or "www.reddit.com" or "old.reddit.com")
                {
                    var match = CompiledRegex.RedditSubredditPathRegex().Match(uri.AbsolutePath);

                    if (match.Success)
                    {
                        return $"reddit: r/{match.Groups[1].Value.ToLowerInvariant()}";
                    }
                }
            }

            if (feedSource.StartsWith("http://") || feedSource.StartsWith("https://"))
                return feedSource;
                
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
