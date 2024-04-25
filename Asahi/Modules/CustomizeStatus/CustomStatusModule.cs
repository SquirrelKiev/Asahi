using Asahi.Database;
using Asahi.Database.Models;
using Discord.Interactions;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Serilog.Core;

namespace Asahi.Modules.CustomizeStatus;

[TrustedMember]
[Group("bot", "Commands relating to configuring the bot.")]
public class CustomStatusModule(DbService dbService, CustomStatusService css, ILogger<CustomStatusModule> logger) : BotModule
{
    [SlashCommand("toggle-activity", "Toggles the bot activity.")]
    public async Task ToggleBotActivitySlash(bool isActive)
    {
        await DeferAsync();

        await using var context = dbService.GetDbContext();

        var botWideConfig = await context.GetBotWideConfig();

        botWideConfig.ShouldHaveActivity = isActive;

        await context.SaveChangesAsync();

        await css.UpdateStatus();

        await FollowupAsync("Toggled.");
    }

    [SlashCommand("activity", "Sets the bot's current activity.")]
    public async Task SetBotStatusSlash(
        [Summary(description: "The activity type the bot should have.")]
        ActivityType activityType,
        [MaxLength(128)]
        [Summary(description: "The activity text.")]
        string activity,
        [Summary(description: $"Streaming URL. This will only need to be set if the activity type is {nameof(ActivityType.Streaming)}.")]
        string streamingUrl = "")
    {
        await DeferAsync();

        await using var context = dbService.GetDbContext();

        var botWideConfig = await context.GetBotWideConfig();

        botWideConfig.ShouldHaveActivity = true;
        botWideConfig.ActivityType = activityType;
        botWideConfig.BotActivity = activity;
        if (!string.IsNullOrWhiteSpace(streamingUrl))
            botWideConfig.ActivityStreamingUrl = streamingUrl;

        await context.SaveChangesAsync();

        await css.UpdateStatus();

        await FollowupAsync("Successfully set activity.");
    }

    [SlashCommand("status", "Sets the bot's status.")]
    public async Task SetBotStatusSlash([Summary(description: "The status.")] UserStatus status)
    {
        await DeferAsync();

        await using var context = dbService.GetDbContext();

        var botWideConfig = await context.GetBotWideConfig();

        botWideConfig.UserStatus = status;

        await context.SaveChangesAsync();

        await css.UpdateStatus();

        await FollowupAsync($"Successfully set status to {status}.");
    }

    [SlashCommand("add-trusted-id", "Adds a user to the trusted user list. This is a dangerous permission to grant.")]
    public async Task AddTrustedIdSlash(string idStr, [MaxLength(TrustedId.CommentMaxLength)] string comment)
    {
        await DeferAsync();

        if (!ulong.TryParse(idStr, out var id))
        {
            await FollowupAsync("Not valid.");
            return;
        }

        await using var context = dbService.GetDbContext();

        var botWideConfig = await context.GetBotWideConfig();

        if (botWideConfig.TrustedIds.Any(x => x.Id == id))
        {
            await FollowupAsync("ID already exists.");
            return;
        }
        botWideConfig.TrustedIds.Add(new TrustedId(id, comment));

        await context.SaveChangesAsync();

        await FollowupAsync("Added ID.");
    }

    [SlashCommand("rm-trusted-id", "Removes a user from the trusted user list.")]
    public async Task RemoveTrustedIdSlash(string idStr)
    {
        await DeferAsync();

        if (!ulong.TryParse(idStr, out var id))
        {
            await FollowupAsync("Not valid.");
            return;
        }

        await using var context = dbService.GetDbContext();

        var botWideConfig = await context.GetBotWideConfig();

        var trusted = botWideConfig.TrustedIds.FirstOrDefault(x => x.Id == id);
        if (trusted == null)
        {
            await FollowupAsync("ID doesn't exist, can't remove.");
            return;
        }

        botWideConfig.TrustedIds.Remove(trusted);

        await context.SaveChangesAsync();

        await FollowupAsync("Removed ID.");
    }

    [SlashCommand("list-trusted-ids", "Lists the trusted IDs.")]
    public async Task ListTrustedIdsSlash()
    {
        await DeferAsync();

        await using var context = dbService.GetDbContext();

        var botWideConfig = await context.GetBotWideConfig();

        await FollowupAsync($"```json\n{JsonConvert.SerializeObject(botWideConfig.TrustedIds, Formatting.Indented)}\n```");
    }
}