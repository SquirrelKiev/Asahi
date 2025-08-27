using System.Diagnostics;

namespace Asahi.Modules.FeedsV2.FeedProviders
{
    /// <remarks>Feed source should be <code>dummy:</code></remarks>
    public class DummyFeedProvider : IFeedProvider
    {
        private static int initializeCallCount = 0;
        
        public string? FeedSource { get; private set; }
        public string DefaultFeedTitle
        {
            get => "Dummy feed";
        }
        
        public Task<bool> Initialize(string feedSource, CancellationToken cancellationToken = default)
        {
            Debug.Assert(feedSource == "dummy:");

            FeedSource = feedSource;

            initializeCallCount++;
            return Task.FromResult(true);
        }

        public IEnumerable<int> ListArticleIds()
        {
            return Enumerable.Range(Math.Max(0, initializeCallCount-5), initializeCallCount);
        }

        public IAsyncEnumerable<MessageContents> GetArticleMessageContent(int articleId, Color embedColor, string? feedTitle, CancellationToken cancellationToken = default) =>
            GetArticleMessageContentSync(articleId, embedColor, feedTitle).ToAsyncEnumerable();

        private IEnumerable<MessageContents> GetArticleMessageContentSync(int articleId, Color embedColor, string? feedTitle)
        {
            yield return new MessageContents(new EmbedBuilder().WithColor(embedColor)
                .WithDescription($"This is a message for article {articleId}.").WithFooter(feedTitle ?? DefaultFeedTitle).WithCurrentTimestamp());
        }
    }
}
