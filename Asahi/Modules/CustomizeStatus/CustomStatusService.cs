using Asahi.Database;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace Asahi.Modules.CustomizeStatus;

[Inject(ServiceLifetime.Singleton)]
public class CustomStatusService(ILogger<CustomStatusService> logger, DbService dbService, DiscordSocketClient client)
{
    public async Task UpdateStatus()
    {
        await using var context = dbService.GetDbContext();

        var botWideConfig = await context.GetBotWideConfig();

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
                await client.SetActivityAsync(new Game(botWideConfig.BotActivity, botWideConfig.ActivityType));
                break;
            case ActivityType.Streaming:
                await client.SetActivityAsync(new StreamingGame(botWideConfig.BotActivity,
                    botWideConfig.ActivityStreamingUrl));
                break;
            case ActivityType.CustomStatus:
                await client.SetActivityAsync(new CustomStatusGame(botWideConfig.BotActivity));
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        logger.LogInformation("Set activity to {activityType}, with contents {activityContents}", botWideConfig.ActivityType, botWideConfig.BotActivity);
    }
}