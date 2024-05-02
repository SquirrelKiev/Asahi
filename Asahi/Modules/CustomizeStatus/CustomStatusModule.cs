using Asahi.Database;
using Asahi.Database.Models;
using Discord.Interactions;
using Newtonsoft.Json;

namespace Asahi.Modules.CustomizeStatus;

[Group("bot", "Commands relating to configuring the bot.")]
public class CustomStatusModule(DbService dbService, CustomStatusService css) : BotModule
{
    [TrustedMember(TrustedId.TrustedUserPerms.StatusPerms)]
    [SlashCommand("toggle-activity", "Toggles the bot activity.")]
    public async Task ToggleBotActivitySlash(bool isActive)
    {
        await DeferAsync();

        await using var context = dbService.GetDbContext();

        var botWideConfig = await context.GetBotWideConfig(Context.Client.CurrentUser.Id);

        botWideConfig.ShouldHaveActivity = isActive;

        await context.SaveChangesAsync();

        await css.UpdateStatus();

        await FollowupAsync("Toggled.");
    }

    [TrustedMember(TrustedId.TrustedUserPerms.StatusPerms)]
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

        var botWideConfig = await context.GetBotWideConfig(Context.Client.CurrentUser.Id);

        botWideConfig.ShouldHaveActivity = true;
        botWideConfig.ActivityType = activityType;
        botWideConfig.BotActivity = activity;
        if (!string.IsNullOrWhiteSpace(streamingUrl))
            botWideConfig.ActivityStreamingUrl = streamingUrl;

        await context.SaveChangesAsync();

        await css.UpdateStatus();

        await FollowupAsync("Successfully set activity.");
    }

    [TrustedMember(TrustedId.TrustedUserPerms.StatusPerms)]
    [SlashCommand("status", "Sets the bot's status.")]
    public async Task SetBotStatusSlash([Summary(description: "The status.")] UserStatus status)
    {
        await DeferAsync();

        await using var context = dbService.GetDbContext();

        var botWideConfig = await context.GetBotWideConfig(Context.Client.CurrentUser.Id);

        botWideConfig.UserStatus = status;

        await context.SaveChangesAsync();

        await css.UpdateStatus();

        await FollowupAsync($"Successfully set status to {status}.");
    }

    [TrustedMember(TrustedId.TrustedUserPerms.TrustedUserEditPerms)]
    [SlashCommand("add-trusted-id", "Adds a user to the trusted user list. This is a dangerous permission to grant.")]
    public async Task AddTrustedIdSlash(string idStr, [MaxLength(TrustedId.CommentMaxLength)] string comment, 
        bool wolframPerms, bool trustedUserPerms, bool statusPerms)
    {
        await DeferAsync();

        if (!ulong.TryParse(idStr, out var id))
        {
            await FollowupAsync("Not valid.");
            return;
        }

        await using var context = dbService.GetDbContext();

        var botWideConfig = await context.GetBotWideConfig(Context.Client.CurrentUser.Id);

        if (botWideConfig.TrustedIds.Any(x => x.Id == id))
        {
            await FollowupAsync("ID already exists.");
            return;
        }

        var permissionFlags = TrustedId.TrustedUserPerms.None;

        if(wolframPerms) permissionFlags |= TrustedId.TrustedUserPerms.WolframPerms;
        if(trustedUserPerms) permissionFlags |= TrustedId.TrustedUserPerms.TrustedUserEditPerms;
        if(statusPerms) permissionFlags |= TrustedId.TrustedUserPerms.StatusPerms;

        // why does it break if I just add to the botWideConfig.TrustedIds list?? but only on the 2nd time??? wtf????
        // weird ass concurrency error, but it shouldn't be a concurrency issue as nothing will be getting modified
        // and the contents is there cuz if i json serialize it and log it, I get the correct results?
        // why is it cursed? why? im tearing my hair out here, this better not happen for anything else I swear
        context.Add(new TrustedId()
        {
            Id = id,
            Comment = comment,
            PermissionFlags = permissionFlags,
            BotWideConfig = botWideConfig,
        });

        await context.SaveChangesAsync();

        await FollowupAsync("Added ID.");
    }

    [TrustedMember(TrustedId.TrustedUserPerms.TrustedUserEditPerms)]
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

        var botWideConfig = await context.GetBotWideConfig(Context.Client.CurrentUser.Id);

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

    [TrustedMember(TrustedId.TrustedUserPerms.TrustedUserEditPerms)]
    [SlashCommand("list-trusted-ids", "Lists the trusted IDs.")]
    public async Task ListTrustedIdsSlash()
    {
        await DeferAsync();

        await using var context = dbService.GetDbContext();

        var botWideConfig = await context.GetBotWideConfig(Context.Client.CurrentUser.Id);

        await FollowupAsync($"```json\n{JsonConvert.SerializeObject(botWideConfig.TrustedIds, Formatting.Indented)}\n```");
    }
}