using Discord.Interactions;
using Humanizer;

namespace Asahi.Modules.Utilities;

public class UtilitiesModule : BotModule
{
    [DefaultMemberPermissions(GuildPermission.ManageRoles)]
    [SlashCommand("hide-channel", "Hides a channel from a list of users.")]
    public async Task HideChannelSlash([Summary(description: "The channel to hide.")] IGuildChannel channel,
        [Summary(description: "The members to hide it from. IDs, separated by spaces.")] string rawMembers)
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

            var overwrite = channel.GetPermissionOverwrite(member) ?? OverwritePermissions.InheritAll;
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
    public async Task UnhideChannelSlash([Summary(description: "The channel to unhide.")] IGuildChannel channel, 
        [Summary(description: "The members to unhide it from. IDs, separated by spaces.")] string rawMembers)
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

            var overwrite = channel.GetPermissionOverwrite(member) ?? OverwritePermissions.InheritAll;
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
}