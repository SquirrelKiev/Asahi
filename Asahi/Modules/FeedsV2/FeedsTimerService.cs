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
    FeedsStateTracker feedsStateTracker)
{
    private Task? timerTask;

    public void StartBackgroundTask(CancellationToken token)
    {
        timerTask ??= Task.Run(() => TimerTask(token), token);
    }

    /// <remarks>Should only be one of these running!</remarks>
    private async Task TimerTask(CancellationToken cancellationToken)
    {
        try
        {
            logger.LogTrace("RSS timer task started");

            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));

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
}
