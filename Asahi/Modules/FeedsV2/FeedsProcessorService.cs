using System.Diagnostics;
using Asahi.Database.Models.Rss;
using Microsoft.Extensions.Logging;

namespace Asahi.Modules.FeedsV2;

[Inject(ServiceLifetime.Singleton)]
public class FeedsProcessorService(
    IFeedProviderFactory feedProviderFactory,
    IColorProviderService colorProvider,
    IFeedMessageDispatcher messageDispatcher,
    ILogger<FeedsProcessorService> logger)
{
    public async Task PollFeeds(FeedsStateTracker stateTracker, FeedListener[] feeds)
    {
        // might be good to parallelize this, but i need to figure out some solution to avoid rate-limits
        // TODO: separate feed requests and feed processing and message dispatching into separate tasks, so each can run separately
        foreach (var feed in feeds.GroupBy(x => x.FeedUrl).Where(x => x.Any(y => y.Enabled)))
        {
            try
            {
                var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                await ProcessFeed(feed.Key, feed.ToArray(), stateTracker, cts.Token);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to process feed {feed}!", feed.Key);
            }
        }

        stateTracker.PruneMissingFeeds(feeds.Select(x => x.FeedUrl));
        
        // logger.LogTrace("Finished processing all feeds.");
    }

    private async Task ProcessFeed(string feedSource, FeedListener[] listeners, FeedsStateTracker stateTracker, CancellationToken cancellationToken = default)
    {
        var feedProvider = feedProviderFactory.GetFeedProvider(feedSource);

        if (feedProvider == null)
            return;

        if (!await feedProvider.Initialize(feedSource, cancellationToken))
        {
            return;
        }
        
        stateTracker.UpdateDefaultFeedTitleCache(feedSource, feedProvider.DefaultFeedTitle);

        if (TryCacheInitialArticlesIfNecessary(feedSource, feedProvider, stateTracker))
        {
            return;
        }

        foreach (var articleId in feedProvider.ListArticleIds())
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            if (!stateTracker.IsNewArticle(feedSource, articleId))
            {
                // logger.LogTrace("Already seen article {articleId} for feed {feedSource}.", articleId, feedSource);
                continue;
            }

            foreach (var listener in listeners)
            {
                // Part of me wants to check the cancellation token here, but I feel it's important to let the messages send no matter what
                try
                {
                    if(!listener.Enabled)
                        continue;
                    
                    // logger.LogTrace("Processing listener {listenerId}.", listener.Id);

                    Debug.Assert(listener.FeedUrl == feedSource);

                    var articleMessages = feedProvider.GetArticleMessageContent(articleId,
                        await colorProvider.GetEmbedColor(listener.GuildId), listener.FeedTitle, CancellationToken.None);

                    await messageDispatcher.SendMessages(listener, articleMessages);
                }
                catch (Exception e)
                {
                    logger.LogWarning(e,
                        "Failed to dispatch message to listener {listener}. Guild ID {guildId}, Channel ID {channelId}, feed source {feedSource}",
                        listener.Id, listener.GuildId, listener.ChannelId, listener.FeedUrl);
                }
            }

            stateTracker.MarkArticleAsRead(feedSource, articleId);
            stateTracker.PruneMissingArticles(feedProvider);
        }
        
        // logger.LogTrace("Finished processing feed {feedSource}.", feedSource);
    }

    /// <summary>
    /// If this is the first time seeing the feed source, this will cache the initial returned articles from it so they aren't sent multiple times.
    /// </summary>
    /// <returns>Whether it cached anything or not.</returns>
    public bool TryCacheInitialArticlesIfNecessary(string feedSource, IFeedProvider feedProvider,
        FeedsStateTracker stateTracker)
    {
        if (!stateTracker.IsFirstTimeSeeingFeedSource(feedSource))
        {
            return false;
        }

        logger.LogTrace("First time seeing source {feedSource}.", feedSource);

        foreach (var articleId in feedProvider.ListArticleIds())
        {
            stateTracker.MarkArticleAsRead(feedSource, articleId);
        }

        return true;
    }
}
