using Asahi.Database;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace Asahi.Modules.BotManagement;

[Inject(ServiceLifetime.Singleton)]
public class CustomStatusService(ILogger<CustomStatusService> logger, DbService dbService, DiscordSocketClient client)
{
    private int currentActivityId;

    private Task? timerTask;

    /// <summary>
    /// Starts the background task that refreshes the custom status.
    /// </summary>
    /// <param name="cancellationToken">Indicates that the task should stop refreshing.</param>
    public void StartBackgroundTask(CancellationToken cancellationToken)
    {
        timerTask ??= Task.Run(() => TimerTask(cancellationToken), cancellationToken);
    }

    /// <remarks>Should only be one of these running!</remarks>
    private async Task TimerTask(CancellationToken cancellationToken)
    {
        logger.LogTrace("Highlights timer task started");

        try
        {
            await UpdateStatus();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception in TimerTask! {message}", ex.Message);
        }

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(15));
        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            try
            {
                await UpdateStatus();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unhandled exception in TimerTask! {message}", ex.Message);
            }
        }
    }

    public async Task UpdateStatus()
    {
        await using var context = dbService.GetDbContext();

        var botWideConfig = await context.GetBotWideConfig(client.CurrentUser.Id);

        if (botWideConfig.BotActivities.Length == 0)
        {
            await client.SetActivityAsync(null);
            return;
        }

        var activity = botWideConfig.BotActivities[currentActivityId % botWideConfig.BotActivities.Length];

        await client.SetStatusAsync(botWideConfig.UserStatus);

        if (!botWideConfig.ShouldHaveActivity)
        {
            await client.SetActivityAsync(null);
            return;
        }

        await client.SetStatusAsync(botWideConfig.UserStatus);

        switch (botWideConfig.ActivityType)
        {
            case ActivityType.Playing:
            case ActivityType.Listening:
            case ActivityType.Watching:
            case ActivityType.Competing:
                await client.SetActivityAsync(new Game(activity, botWideConfig.ActivityType));
                break;
            case ActivityType.Streaming:
                await client.SetActivityAsync(new StreamingGame(activity,
                    botWideConfig.ActivityStreamingUrl));
                break;
            case ActivityType.CustomStatus:
                await client.SetActivityAsync(new CustomStatusGame(activity));
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        logger.LogInformation("Set activity to {activityType}, with contents {activityContents}", botWideConfig.ActivityType, activity);

        currentActivityId = (currentActivityId + 1) % botWideConfig.BotActivities.Length;
    }
}