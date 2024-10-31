using Asahi.Database.Models.Rss;

namespace Asahi.Modules.RssAtomFeed
{
    public interface IEmbedGenerator
    {
        /// <summary>
        /// Returns an IEnumerable of all the embeds for that feed's items.
        /// </summary>
        /// <param name="feedListener">The listener.</param>
        /// <param name="seenArticles">The previously seen articles from the last run. Will not be edited.</param>
        /// <param name="processedArticles">The current work in progress articles that have been processed. Will be edited.</param>
        /// <param name="embedColor">The color to use for the embed.</param>
        /// <param name="shouldCreateEmbeds">Whether embeds should be created or not.
        /// Primarily for when the url has not been seen before, to generate the initial list of IDs to ignore for the next time round.</param>
        /// <returns></returns>
        public IEnumerable<MessageContents> GenerateFeedItemMessages(
            FeedListener feedListener,
            HashSet<int> seenArticles,
            HashSet<int> processedArticles,
            Color embedColor,
            bool shouldCreateEmbeds
        );
    }
}
