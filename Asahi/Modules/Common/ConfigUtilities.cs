using Asahi.Database;
using Microsoft.EntityFrameworkCore;

namespace Asahi.Modules;

public static class ConfigUtilities
{
    public static async Task<bool> CommonConfig(IInteractionContext botContext, IDbContextFactory<BotDbContext> dbService,
        Func<BotDbContext, EmbedBuilder, Task<ConfigChangeResult>> updateAction, bool ephemeral = false)
    {
        await botContext.Interaction.DeferAsync(ephemeral);

        await using var context = await dbService.CreateDbContextAsync();

        var embedBuilder = new EmbedBuilder();
        var message = await updateAction(context, embedBuilder);

        if (message.wasSuccess && message.shouldSave)
            await context.SaveChangesAsync();

        var embeds = CreateEmbeds(await botContext.Guild.GetCurrentUserAsync(), embedBuilder, message);

        await botContext.Interaction.FollowupAsync(embeds: embeds, ephemeral: ephemeral);
        return message.wasSuccess;
    }

    public static Embed[] CreateEmbeds(IGuildUser botUser, EmbedBuilder embedBuilder, ConfigChangeResult message)
    {
        var embeds = message.extraEmbeds;

        var roleColor = QuotingHelpers.GetUserRoleColorWithFallback(botUser, Color.Green);

        if (!message.onlyExtraEmbeds)
        {
            embeds = embeds.Prepend(
                embedBuilder
                    .WithDescription(message.message)
                    .WithOptionalColor(embedBuilder.Color.HasValue ? Color.Default : message.wasSuccess ? roleColor : Color.Red)
                    .Build()
            ).ToArray();
        }

        return embeds;
    }
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

public class ConfigException(string message) : Exception(message);
