using System.Diagnostics;
using Asahi.Database.Models.Rss;
using Asahi.Modules.FeedsV2.FeedProviders;
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
        foreach (var feed in feeds.GroupBy(x => x.FeedUrl)
                     .Where(x => x.Any(y => y is { Enabled: true, ForcedDisable: false })))
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

        logger.LogTrace("Finished processing all feeds.");
    }

    // HACK: debugging stuff, remove once reddit has stopped spamming channels
    private readonly Dictionary<string, List<string>?> previousArticleIds = [];

    private async Task ProcessFeed(string feedSource, FeedListener[] listeners, FeedsStateTracker stateTracker,
        CancellationToken cancellationToken = default)
    {
        var feedProvider = feedProviderFactory.GetFeedProvider(feedSource);

        if (feedProvider == null)
            return;

        var continuationToken = stateTracker.GetFeedSourceContinuationToken(feedSource);
        
        if (!await feedProvider.Initialize(feedSource, continuationToken, cancellationToken: cancellationToken))
        {
            logger.LogWarning("Failed to initialize feed {feedSource}.", feedSource);
            return;
        }
        
        stateTracker.SetFeedSourceContinuationToken(feedSource, feedProvider.GetContinuationToken());

        stateTracker.UpdateDefaultFeedTitleCache(feedSource, feedProvider.DefaultFeedTitle);

        if (TryCacheInitialArticlesIfNecessary(feedSource, feedProvider, stateTracker))
        {
            return;
        }

        // HACK: debugging stuff, remove once reddit has stopped spamming channels
        List<string>? newArticleIds = null;
        List<int>? previousSeenArticleIds = null;
        RedditFeedProvider? rfp = feedProvider as RedditFeedProvider;
        if (rfp != null)
        {
            newArticleIds = rfp.ListArticleRedditIds().ToList();
            previousSeenArticleIds = stateTracker.GetSeenArticleIds(feedSource)?.ToList();

            if (feedProvider.ListArticleIds().Except(stateTracker.GetSeenArticleIds(feedSource) ?? []).Count() > 5)
            {
                logger.LogError(
                    "R/DEBUG: more than 5 articles are unseen, assuming stuff has blown up. feed is {feed}. seen ids are {seenIds}. feed json is {json}",
                    feedSource, stateTracker.GetSeenArticleIds(feedSource), rfp.Json);
            }
        }

        foreach (var articleId in feedProvider.ListArticleIds())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!stateTracker.IsNewArticle(feedSource, articleId))
            {
                logger.LogTrace("Already seen article {articleId} for feed {feedSource}.", articleId, feedSource);
                continue;
            }

            foreach (var listener in listeners)
            {
                // Part of me wants to check the cancellation token here, but I feel it's important to let the messages send no matter what
                try
                {
                    if (!listener.Enabled)
                        continue;

                    logger.LogTrace("Processing listener {listenerId}.", listener.Id);

                    Debug.Assert(listener.FeedUrl == feedSource);

                    var articleMessages = feedProvider.GetArticleMessageContent(articleId,
                        await colorProvider.GetEmbedColor(listener.GuildId), listener.FeedTitle,
                        CancellationToken.None);

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
        }

        if (feedProvider.ListArticleIds().Any())
        {
            stateTracker.PruneMissingArticles(feedProvider);
        }
        else
        {
            logger.LogTrace("Skipping pruning for {feedSource} due to empty article list.", feedSource);
        }

        // TODO: remove all this hack stuff
        // HACK: debugging stuff, remove once reddit has stopped spamming channels
        if (rfp != null)
        {
            var newSeenArticleIds = stateTracker.GetSeenArticleIds(feedSource)?.ToList();
            
            var idDiffs = previousSeenArticleIds?.Except(newSeenArticleIds ?? []).ToList();
            if (idDiffs?.Count > 5)
            {
                logger.LogError(
                    "R/DEBUG: more than 5 articles have been now marked as unseen in one go, assuming stuff has blown up. feed is {feed}. seen ids are {seenIds}. old article ids are {previousIds}, new ids are {newIds}. diff is {idDiffs}. feed json is {json}",
                    feedSource, stateTracker.GetSeenArticleIds(feedSource), previousArticleIds.GetValueOrDefault(feedSource),
                    newArticleIds, idDiffs, rfp.Json);
            }
            else
            {
                logger.LogTrace(
                    "R/DEBUG: feed is {feed}. seen ids are {seenIds}. old article ids are {previousIds}, new ids are {newIds}. diff is {idDiffs}. feed json is {json}",
                    feedSource, newSeenArticleIds, previousArticleIds.GetValueOrDefault(feedSource),
                    newArticleIds, idDiffs, rfp.Json);
            }

            previousArticleIds[feedSource] = newArticleIds;
        }

        logger.LogTrace("Finished processing feed {feedSource}.", feedSource);
    }

    /// <summary>
    /// If this is the first time seeing the feed source, this will cache the initially returned articles from it so they aren't sent multiple times.
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
