using Asahi.Database;
using Serilog.Context;
using System.Text.RegularExpressions;

namespace Asahi.Modules;

public static partial class ConfigUtilities
{
    public static async Task<bool> CommonConfig(IInteractionContext botContext, DbService dbService,
        Func<BotDbContext, EmbedBuilder, Task<ConfigChangeResult>> updateAction)
    {
        await botContext.Interaction.DeferAsync();

        await using var context = dbService.GetDbContext();

        var embedBuilder = new EmbedBuilder();
        var message = await updateAction(context, embedBuilder);

        if (message.wasSuccess && message.shouldSave)
            await context.SaveChangesAsync();

        var embeds = await CreateEmbeds(await botContext.Guild.GetUserAsync(botContext.Client.CurrentUser.Id), embedBuilder, message);

        await botContext.Interaction.FollowupAsync(embeds: embeds);
        return message.wasSuccess;
    }

    public static async Task<Embed[]> CreateEmbeds(IGuildUser botUser, EmbedBuilder embedBuilder, ConfigChangeResult message)
    {
        var embeds = message.extraEmbeds;

        var roleColor = QuotingHelpers.GetUserRoleColorWithFallback(botUser, Color.Green);

        if (!message.onlyExtraEmbeds)
        {
            embeds = embeds.Prepend(
                embedBuilder
                    .WithDescription(message.message)
                    .WithColor(message.wasSuccess ? roleColor : Color.Red)
                    .Build()
            ).ToArray();
        }

        return embeds;
    }

    [GeneratedRegex(@"^[\w-]+$")]
    public static partial Regex IsValidId();
}

public struct ConfigChangeResult(bool wasSuccess, string message, Embed[] extraEmbeds, bool onlyExtraEmbeds = false, bool shouldSave = true)
{
    public bool wasSuccess = wasSuccess;
    public string message = message;
    public Embed[] extraEmbeds = extraEmbeds;
    public bool onlyExtraEmbeds = onlyExtraEmbeds;
    public bool shouldSave = shouldSave;

    public ConfigChangeResult(bool wasSuccess, string message) : this(wasSuccess, message, [])
    {
    }
}