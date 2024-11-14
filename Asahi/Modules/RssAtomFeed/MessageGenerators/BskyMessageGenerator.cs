using System.Xml.Linq;
using Asahi.Database.Models.Rss;
using CodeHollow.FeedReader;

namespace Asahi.Modules.RssAtomFeed;

public class BskyMessageGenerator(FeedItem[] feedItems) : IEmbedGenerator
{
    public IEnumerable<MessageContents> GenerateFeedItemMessages(
        FeedListener feedListener,
        HashSet<int> seenArticles,
        HashSet<int> processedArticles,
        Color embedColor,
        bool shouldCreateEmbeds
    )
    {
        foreach (var feedItem in feedItems)
        {
            processedArticles.Add(feedItem.Id.GetHashCode(StringComparison.Ordinal));

            if (seenArticles.Contains(feedItem.Id.GetHashCode(StringComparison.Ordinal)))
                continue;
            if (!shouldCreateEmbeds)
                continue;

            yield return GenerateFeedItemEmbed(feedItem);
        }
    }

    public MessageContents GenerateFeedItemEmbed(FeedItem genericItem)
    {
        return new MessageContents(genericItem.Link.Replace("https://bsky.app/", "https://bskye.app/"));
    }
}
