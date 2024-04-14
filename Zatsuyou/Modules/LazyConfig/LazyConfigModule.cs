using Discord.Interactions;
using Zatsuyou.Database;
using Zatsuyou.Database.Models;

namespace Zatsuyou.Modules.LazyConfig;

// [Group("config", "Configuration commands.")]
public partial class LazyConfigModule(DbService dbService) : BotModule
{
    [SlashCommand("prefix", "Gets/sets the bot prefix.")]
    [InteractionsModCommand]
    public async Task SetPrefix([MinLength(1),MaxLength(GuildConfig.MaxPrefixLength)]string prefix = "")
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