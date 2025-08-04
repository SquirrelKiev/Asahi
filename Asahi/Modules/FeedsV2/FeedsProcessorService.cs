using System.Collections.Concurrent;
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
        foreach (var feed in feeds.GroupBy(x => x.FeedUrl).Where(x => x.Any(y => y.Enabled)))
        {
            try
            {
                await ProcessFeed(feed.Key, feed.ToArray(), stateTracker);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to process feed {feed}!", feed.Key);
            }
        }

        stateTracker.PruneMissingFeeds(feeds.Select(x => x.FeedUrl));
    }

    private async Task ProcessFeed(string feedSource, FeedListener[] listeners, FeedsStateTracker stateTracker)
    {
        var feedProvider = feedProviderFactory.GetFeedProvider(feedSource);

        if (feedProvider == null)
            return;

        await feedProvider.Initialize(feedSource);
        
        stateTracker.UpdateDefaultFeedTitleCache(feedSource, feedProvider.DefaultFeedTitle);

        if (TryCacheInitialArticlesIfNecessary(feedSource, feedProvider, stateTracker))
        {
            return;
        }

        foreach (var articleId in feedProvider.ListArticleIds())
        {
            if (!stateTracker.IsNewArticle(feedSource, articleId))
            {
                // logger.LogTrace("Already seen article {articleId} for feed {feedSource}.", articleId, feedSource);
                continue;
            }

            foreach (var listener in listeners)
            {
                try
                {
                    if(!listener.Enabled)
                        continue;
                    
                    // logger.LogTrace("Processing listener {listenerId}.", listener.Id);

                    Debug.Assert(listener.FeedUrl == feedSource);

                    var articleMessages = feedProvider.GetArticleMessageContent(articleId,
                        await colorProvider.GetEmbedColor(listener.GuildId), listener.FeedTitle);

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
