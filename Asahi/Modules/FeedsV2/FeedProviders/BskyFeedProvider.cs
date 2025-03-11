using CodeHollow.FeedReader;

namespace Asahi.Modules.FeedsV2.FeedProviders
{
    // TODO: Rewrite to use the API so it can support replies
    public class BskyFeedProvider(HttpClient client) : RssFeedProvider(client)
    {
        public override string DefaultFeedTitle
        {
            get
            {
                if (genericFeed == null) return "BlueSky Feed";

                return genericFeed.Title;
            }
        }

        protected override MessageContents ArticleToMessageContents(FeedItem genericItem, Color embedColor, string? feedTitle)
        {
            return new MessageContents(genericItem.Link);
        }
    }
}
