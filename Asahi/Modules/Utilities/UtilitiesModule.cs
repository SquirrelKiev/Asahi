﻿using System.Globalization;
using Asahi.Modules.BirthdayRoles;
using Discord.Interactions;
using Discord.WebSocket;
using Humanizer;
using Microsoft.Extensions.Logging;
using NodaTime;
using NodaTime.TimeZones;

namespace Asahi.Modules.Utilities;

[CommandContextType(InteractionContextType.Guild, InteractionContextType.BotDm, InteractionContextType.PrivateChannel)]
[IntegrationType(ApplicationIntegrationType.GuildInstall, ApplicationIntegrationType.UserInstall)]
public class UtilitiesModule(IClock clock, ILogger<UtilitiesModule> logger) : BotModule
{
    [CommandContextType(InteractionContextType.Guild)]
    [IntegrationType(ApplicationIntegrationType.GuildInstall)]
    [DefaultMemberPermissions(GuildPermission.ManageRoles)]
    [SlashCommand("hide-channel", "Hides a channel from a list of users.")]
    public async Task HideChannelSlash(
        [Summary(description: "The channel to hide.")] IGuildChannel channel,
        [Summary(description: "The members to hide it from. IDs, separated by spaces.")]
            string rawMembers
    )
    {
        await DeferAsync();

        var memberList = rawMembers.Split(' ');

        List<string> failedParse = [];
        List<string> failedFindMember = [];
        int count = 0;
        foreach (var memberIdStr in memberList)
        {
            if (!ulong.TryParse(memberIdStr, out var memberId))
            {
                failedParse.Add(memberIdStr);
                continue;
            }

            var member = await Context.Guild.GetUserAsync(memberId);

            if (member == null)
            {
                failedFindMember.Add(memberIdStr);
                continue;
            }

            var overwrite =
                channel.GetPermissionOverwrite(member) ?? OverwritePermissions.InheritAll;
            overwrite = overwrite.Modify(viewChannel: PermValue.Deny);
            await channel.AddPermissionOverwriteAsync(member, overwrite);
            count++;
        }

        string body = $"Updated for {count} members.";
        if (failedParse.Count != 0)
            body += $"Failed to parse IDs {failedParse.Humanize()}. ";
        if (failedFindMember.Count != 0)
            body += $"Failed to find users {failedFindMember.Humanize()}. ";
        await FollowupAsync(body, allowedMentions: AllowedMentions.None);
    }

    [CommandContextType(InteractionContextType.Guild)]
    [IntegrationType(ApplicationIntegrationType.GuildInstall)]
    [DefaultMemberPermissions(GuildPermission.ManageRoles)]
    [SlashCommand("unhide-channel", "Unhides a channel from a list of users.")]
    public async Task UnhideChannelSlash(
        [Summary(description: "The channel to unhide.")] IGuildChannel channel,
        [Summary(description: "The members to unhide it from. IDs, separated by spaces.")]
            string rawMembers
    )
    {
        await DeferAsync();

        var memberList = rawMembers.Split(' ');

        List<string> failedParse = [];
        List<string> failedFindMember = [];
        int count = 0;
        foreach (var memberIdStr in memberList)
        {
            if (!ulong.TryParse(memberIdStr, out var memberId))
            {
                failedParse.Add(memberIdStr);
                continue;
            }

            var member = await Context.Guild.GetUserAsync(memberId);

            if (member == null)
            {
                failedFindMember.Add(memberIdStr);
                continue;
            }

            var overwrite =
                channel.GetPermissionOverwrite(member) ?? OverwritePermissions.InheritAll;
            overwrite = overwrite.Modify(viewChannel: PermValue.Allow);
            await channel.AddPermissionOverwriteAsync(member, overwrite);
            count++;
        }

        string body = $"Updated for {count} members.";
        if (failedParse.Count != 0)
            body += $"Failed to parse IDs {failedParse.Humanize()}. ";
        if (failedFindMember.Count != 0)
            body += $"Failed to find users {failedFindMember.Humanize()}. ";
        await FollowupAsync(body, allowedMentions: AllowedMentions.None);
    }

    [SlashCommand("time", "Gets the time in that timezone.")]
    public async Task GetTimeSlash(
        [Autocomplete<TimeZoneAutocomplete>]
        [Summary(description: "The timezone to get the time in.")]
            string timeZone
    )
    {
        await DeferAsync();
        if (
            !TzdbDateTimeZoneSource.Default.CanonicalIdMap.TryGetValue(
                timeZone,
                out string? canonicalTimeZone
            )
        )
        {
            await FollowupAsync("No timezone found.");
            return;
        }

        var tz = TzdbDateTimeZoneSource.Default.ForId(canonicalTimeZone);

        var now = clock.GetCurrentInstant();
        // could use the Discord provided culture, but it starts mixing and matching languages then and looks weird
        var culture = new CultureInfo("en-US");
        var time = now.InZone(tz);

        await FollowupAsync(
            $"The time in `{canonicalTimeZone}` is "
                + $"`{time.LocalDateTime.Date.ToString("D", culture)}, {time.LocalDateTime.TimeOfDay.ToString("T", culture)}`."
        );
    }

    [SlashCommand("quote", "Quote a message")]
    public async Task QuoteSlash([Summary(description: "A link to the message to quote.")] string messageLink)
    {
        await DeferAsync();

        var messageProcessed = CompiledRegex.MessageLinkRegex().Match(messageLink);

        if (!messageProcessed.Success)
        {
            await FollowupAsync(embeds: ConfigUtilities.CreateEmbeds(await Context.Guild.GetCurrentUserAsync(), new EmbedBuilder(),
                new ConfigChangeResult(false, "Message link provided could not be parsed.")));
            return;
        }

        var guildId = ulong.Parse(messageProcessed.Groups["guild"].Value);
        var channelId = ulong.Parse(messageProcessed.Groups["channel"].Value);
        var messageId = ulong.Parse(messageProcessed.Groups["message"].Value);

        var guild = await Context.Client.GetGuildAsync(guildId);

        var executorGuildUser = await guild.GetUserAsync(Context.User.Id);
        if (executorGuildUser == null)
        {
            await FollowupAsync(embeds: ConfigUtilities.CreateEmbeds(await Context.Guild.GetCurrentUserAsync(), new EmbedBuilder(),
                new ConfigChangeResult(false, "No permission.")));
            return;
        }

        var channel = await guild.GetTextChannelAsync(channelId);
        var perms = executorGuildUser.GetPermissions(channel);
        if (!perms.ViewChannel || !perms.ReadMessageHistory)
        {
            await FollowupAsync(embeds: ConfigUtilities.CreateEmbeds(await Context.Guild.GetCurrentUserAsync(), new EmbedBuilder(),
                new ConfigChangeResult(false, "No permission.")));
            return;
        }

        if (channel is SocketThreadChannel { Type: ThreadType.PrivateThread } threadChannel)
        {
            _ = await threadChannel.GetUsersAsync();
            if (threadChannel.GetUser(Context.User.Id) == null)
            {
                await FollowupAsync(embeds: ConfigUtilities.CreateEmbeds(await Context.Guild.GetCurrentUserAsync(), new EmbedBuilder(),
                    new ConfigChangeResult(false, "No permission.")));
                return;
            }
        }
        var message = await channel.GetMessageAsync(messageId);

        var author = await guild.GetUserAsync(message.Author.Id);

        var quoteMessages = QuotingHelpers.QuoteMessage(message, QuotingHelpers.GetUserRoleColorWithFallback(author, Color.Green), logger,
            true, []);

        foreach (var quoteMessage in quoteMessages)
        {
            await FollowupAsync(quoteMessage);
        }
    }
}
