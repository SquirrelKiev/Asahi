using April.Config;
using Asahi.Database;
using Asahi.Database.Models.April;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Asahi.Modules.April;

[Inject(ServiceLifetime.Singleton)]
public class DelayedExecuteService(DiscordSocketClient client, IDbService dbService, AprilUtility aprilUtility, ILogger<DelayedExecuteService> logger)
{
    public Task? timerTask;

    public void StartBackgroundTask()
    {
        timerTask ??= Task.Run(TimerTask);
    }

    /// <remarks>Should only be one of these running!</remarks>
    private async Task TimerTask()
    {
        logger.LogTrace("away we go");
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        while (await timer.WaitForNextTickAsync())
        {
            try
            {
                await OnFinishWaitingForTick();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Unhandled exception in TimerTask! {message}", ex.Message);
            }
        }
    }

    private async Task OnFinishWaitingForTick()
    {
        var now = DateTimeOffset.UtcNow;

        await using var context = dbService.GetDbContext();
        var actionsDb = await GetActionsBeforeTime(context, now);


        if (actionsDb.Count == 0)
        {
            return;
        }

        logger.LogTrace("{count} actions", actionsDb.Count);


        Dictionary<string, UserData> userDatas = [];

        foreach (var actionDb in actionsDb)
        {
            try
            {
                var guild = client.GetGuild(actionDb.GuildId);

                if (guild == null)
                    throw new NullReferenceException();

                var channel = guild.GetTextChannel(actionDb.ChannelId);
                var user = guild.GetUser(actionDb.UserId);

                if (user == null || channel == null)
                    throw new NullReferenceException();
                
                var key = $"{actionDb.GuildId}-{actionDb.UserId}";
                if (!userDatas.TryGetValue(key, out var userData))
                {
                    userData = await context.GetAprilUserData(actionDb.GuildId, actionDb.UserId);
                    userDatas.Add(key, userData);
                }

                var actions = JsonConvert.DeserializeObject<List<RewardActionContainer>>(actionDb.ActionJson);

                if (actions == null)
                    throw new NullReferenceException($"{nameof(actions)} is null!");

                await aprilUtility.ExecuteRewardActions(actions, context, userData, user, channel);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "something broke while trying to execute a delayed action: {error}", ex.Message);
            }
            finally
            {
                context.DelayedActions.Remove(actionDb);
            }
        }

        await context.SaveChangesAsync();
    }

    private async Task<List<DelayedAction>> GetActionsBeforeTime(BotDbContext context, DateTimeOffset time)
    {
        return await context.DelayedActions.Where(x => x.WhenToExecute < time).ToListAsync();
    }
}
