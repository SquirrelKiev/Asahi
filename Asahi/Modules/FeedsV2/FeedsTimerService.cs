using System.Web;
using Asahi.Database;
using Asahi.Database.Models.Rss;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Asahi.Modules.FeedsV2;

[Inject(ServiceLifetime.Singleton)]
public class FeedsTimerService(
    ILogger<FeedsTimerService> logger,
    FeedsProcessorService feedsProcessor,
    IDbContextFactory<BotDbContext> dbService,
    IFeedsStateTracker feedsStateTracker)
{
    private Task? timerTask;

    public void StartBackgroundTask(CancellationToken token)
    {
        timerTask ??= Task.Run(() => TimerTask(token), token);
    }

    /// <remarks>Should only be one of these running!</remarks>
    private async Task TimerTask(CancellationToken cancellationToken)
    {
        await MigrateOldMessages();
        
        try
        {
            logger.LogTrace("RSS timer task started");

            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(2));

            do
            {
                try
                {
                    var feeds = await GetFeeds();

                    await feedsProcessor.PollFeeds(feedsStateTracker, feeds);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Unhandled exception in TimerTask! {message}", ex.Message);
                }
            } while (await timer.WaitForNextTickAsync(cancellationToken));
        }
        catch (TaskCanceledException)
        {
            // throw;
        }
        catch (OperationCanceledException)
        {
            // throw;
        }
        catch (Exception e)
        {
            logger.LogCritical(
                e,
                "Unhandled exception in TimerTask! Except much worse because this was outside of the loop!!"
            );
        }
    }

    public async Task<FeedListener[]> GetFeeds()
    {
        await using var context = await dbService.CreateDbContextAsync();

        return await context.RssFeedListeners.ToArrayAsync();
    }

    private async Task MigrateOldMessages()
    {
        await using var context = await dbService.CreateDbContextAsync();

        var outdatedFeeds = await context
            .RssFeedListeners.Where(x => x.FeedUrl.StartsWith("https://danbooru.donmai.us/posts.json"))
            .ToListAsync();

        if (outdatedFeeds.Count == 0)
            return;

        logger.LogInformation(
            "Migrating {outdatedMessageCount} feed sources to new danbooru feed source schema.",
            outdatedFeeds.Count);

        foreach (var feed in outdatedFeeds)
        {
            try
            {
                var oldFeedUrl = feed.FeedUrl;
                logger.LogTrace("Migrating feed {feedSource} ({feedId}).", oldFeedUrl, feed.Id);
                
                var uri = new Uri(feed.FeedUrl);

                var query = HttpUtility.ParseQueryString(uri.Query);

                var tags = query["tags"];

                if (tags == null)
                {
                    logger.LogError("Attempted to migrate feed {feedSource} ({feedId}), but no tags could be found. Please manually review.", oldFeedUrl, feed.Id);
                    break;
                }

                feed.FeedUrl = $"danbooru: {tags.Trim()}";

                if (!CompiledRegex.DanbooruFeedRegex().IsMatch(feed.FeedUrl))
                {
                    logger.LogError("Attempted to migrate feed {feedId} from {oldFeedSource} to {newFeedSource}, but failed to pass the regex. Please manually review.", feed.Id, oldFeedUrl, feed.FeedUrl);
                    feed.FeedUrl = oldFeedUrl;
                    break;
                }
            }
            catch (Exception e)
            {
                logger.LogError(e, "Failed to migrate feed {feedSource} (ID {feedId})!", feed.FeedUrl, feed.Id);
            }
        }

        await context.SaveChangesAsync();
    }
}
