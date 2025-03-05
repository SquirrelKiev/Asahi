using System.Xml.Linq;
using Asahi.Database.Models.Rss;
using CodeHollow.FeedReader;

namespace Asahi.Modules.RssAtomFeed;

public class BskyMessageGenerator(FeedItem[] feedItems) : IEmbedGeneratorAsync
{
    public IAsyncEnumerable<MessageContents> GenerateFeedItemMessages(
        FeedListener feedListener,
        HashSet<int> seenArticles,
        HashSet<int> processedArticles,
        Color embedColor,
        bool shouldCreateEmbeds
    ) => GenerateFeedItemMessagesSync(feedListener, seenArticles, processedArticles, embedColor, shouldCreateEmbeds).ToAsyncEnumerable();
    
    public IEnumerable<MessageContents> GenerateFeedItemMessagesSync(
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

            yield return new MessageContents(feedItem.Link);
        }
    }
}
