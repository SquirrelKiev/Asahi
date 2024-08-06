using System.Text;
using Asahi.Database;
using Asahi.Database.Models;
using Asahi.Modules.BotManagement;
using Discord.Interactions;
using Discord.WebSocket;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace Asahi.Modules.CustomizeStatus;

[Group("bot", "Commands relating to configuring the bot.")]
public class BotManagementModule(DbService dbService, CustomStatusService css, BotConfig config,
    InteractiveService interactive, DiscordSocketClient client) : BotModule
{
    [TrustedMember(TrustedId.TrustedUserPerms.StatusPerms)]
    [SlashCommand("toggle-activity", "Toggles the bot activity.")]
    public async Task ToggleBotActivitySlash([Summary(description: "Whether the bot should have a status or not.")] bool isActive)
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

        await FollowupAsync($"{config.LoadingEmote} Setting status on bot... (May take a minute depending on current rate-limits)");

        await css.UpdateStatus();

        await ModifyOriginalResponseAsync(new MessageContents("Successfully set activity."));
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
    public async Task AddTrustedIdSlash([Summary(description: "The user ID of the user.")] string idStr,
        [MaxLength(TrustedId.CommentMaxLength), Summary(description: "A note to put beside the user.")] string comment,
        [Summary(description: "Should the user have permission to use Wolfram?")]
        bool wolframPerms,
        [Summary(description: "Should the user have permission to add or remove other trusted users?")]
        bool trustedUserPerms,
        [Summary(description: "Should the user have permission to change the bot's status/profile?")]
        bool statusPerms,
        [Summary(description: "Should the user have permission to view the guilds the bot is in?")]
        bool guildManagementPerms)
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

        if (wolframPerms) permissionFlags |= TrustedId.TrustedUserPerms.WolframPerms;
        if (trustedUserPerms) permissionFlags |= TrustedId.TrustedUserPerms.TrustedUserEditPerms;
        if (statusPerms) permissionFlags |= TrustedId.TrustedUserPerms.StatusPerms;
        if (guildManagementPerms) permissionFlags |= TrustedId.TrustedUserPerms.BotGuildManagementPerms;

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
    public async Task RemoveTrustedIdSlash([Summary(description: "The user ID of the user.")] string idStr)
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

    [TrustedMember(TrustedId.TrustedUserPerms.BotGuildManagementPerms)]
    [SlashCommand("list-guilds", "Lists the guilds the bot is currently in.")]
    public async Task ListGuildsSlash()
    {
        await DeferAsync();

        await using var context = dbService.GetDbContext();

        var botWideConfig = await context.GetBotWideConfig(Context.Client.CurrentUser.Id);

        List<PageBuilder> pages = [];

        foreach (var guild in client.Guilds)
        {
            await guild.DownloadUsersAsync();

            var eb = new PageBuilder()
                .WithOptionalColor(QuotingHelpers.GetUserRoleColorWithFallback(guild.CurrentUser, Color.Default));

            var trustedIds = botWideConfig.TrustedIds.Select(x => x.Id);
            foreach (var managerUserId in config.ManagerUserIds)
            {
                if (botWideConfig.TrustedIds.All(x => x.Id != managerUserId))
                {
                    trustedIds = trustedIds.Append(managerUserId);
                }
            }

            var trustedMembers = guild.Users.Where(x => trustedIds.Any(y => y == x.Id))
                .Select(x => $"* {x.Mention} ({x.Username}#{x.Discriminator}) - In trusted list")
                .Aggregate(new StringBuilder(), (x, y) => x.AppendLine(y)).ToString();

            if (trustedMembers.Length == 0)
            {
                trustedMembers = "None!";
            }

            var rssFeedsCount = await context.RssFeedListeners.Where(x => x.GuildId == guild.Id).CountAsync();

            eb.WithAuthor("Server Info");
            eb.WithTitle(guild.Name);
            eb.WithThumbnailUrl(guild.IconUrl);
            eb.WithFields([
                new EmbedFieldBuilder().WithName("Id").WithValue(guild.Id),
                new EmbedFieldBuilder().WithName("Owner").WithValue($"{guild.Owner.Mention} ({guild.Owner.Username}#{guild.Owner.Discriminator})"),
                new EmbedFieldBuilder().WithName("Members").WithValue(guild.MemberCount),
                new EmbedFieldBuilder().WithName("RSS Feeds").WithValue($"{rssFeedsCount} feed(s)"),
                new EmbedFieldBuilder().WithName("Known Members").WithValue(trustedMembers),
            ]);

            pages.Add(eb);
        }

        var paginator = new StaticPaginatorBuilder()
            .WithOptions(
            [
                new PaginatorButton("<", PaginatorAction.Backward, ButtonStyle.Secondary),
                new PaginatorButton("Jump", PaginatorAction.Jump, ButtonStyle.Secondary),
                new PaginatorButton(">", PaginatorAction.Forward, ButtonStyle.Secondary),
                new PaginatorButton(ModulePrefixes.RED_BUTTON, null, "X", ButtonStyle.Danger),
            ])
            .WithActionOnCancellation(ActionOnStop.DeleteMessage)
            .WithActionOnTimeout(ActionOnStop.DisableInput)
            .WithUsers(Context.User)
            .WithFooter(PaginatorFooter.PageNumber)
            .WithPages(pages);

        await interactive.SendPaginatorAsync(paginator.Build(), Context.Interaction, TimeSpan.FromMinutes(2), InteractionResponseType.DeferredChannelMessageWithSource);
    }
}