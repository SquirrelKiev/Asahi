using Asahi.Database;
using Asahi.Database.Models;
using Discord.Interactions;

namespace Asahi.Modules.LazyConfig;

[CommandContextType(InteractionContextType.Guild)]
[DefaultMemberPermissions(GuildPermission.ManageGuild)]
// [Group("config", "Configuration commands.")]
public class LazyConfigModule(DbService dbService) : BotModule
{
    [SlashCommand("prefix", "Gets/sets the bot prefix.")]
    public async Task SetPrefix(
        [MinLength(1)] [MaxLength(GuildConfig.MaxPrefixLength)] [Summary(description: "The new prefix.")] string prefix = "")
    {
        await DeferAsync();

        await using var context = dbService.GetDbContext();

        var config = await context.GetGuildConfig(Context.Guild.Id);

        if (string.IsNullOrWhiteSpace(prefix))
        {
            await FollowupAsync($"Prefix is `{config.Prefix}`");
            return;
        }

        config.Prefix = prefix;

        await context.SaveChangesAsync();

        await FollowupAsync($"Changed prefix to `{prefix}`");
    }
}
