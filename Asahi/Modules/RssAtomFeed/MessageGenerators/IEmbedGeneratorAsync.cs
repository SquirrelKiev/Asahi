using Asahi.Database.Models.Rss;

namespace Asahi.Modules.RssAtomFeed
{
    public interface IEmbedGeneratorAsync
    {
        /// <summary>
        /// Returns an IAsyncEnumerable of all the embeds for that feed's items.
        /// </summary>
        /// <param name="feedListener">The listener.</param>
        /// <param name="seenArticles">The previously seen articles from the last run. Will not be edited.</param>
        /// <param name="processedArticles">The current work in progress articles that have been processed. Will be edited.</param>
        /// <param name="embedColor">The color to use for the embed.</param>
        /// <returns></returns>
        public IAsyncEnumerable<MessageContents> GenerateFeedItemMessages(
            FeedListener feedListener,
            HashSet<int> seenArticles,
            HashSet<int> processedArticles,
            Color embedColor,
            bool shouldCreateEmbeds
        );
    }
}
