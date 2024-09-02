using System;
using System.Globalization;
using Asahi.Modules.BirthdayRoles;
using Discord.Interactions;
using Humanizer;
using NodaTime;
using NodaTime.TimeZones;

namespace Asahi.Modules.Utilities;

public class UtilitiesModule(IClock clock) : BotModule
{
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
}
