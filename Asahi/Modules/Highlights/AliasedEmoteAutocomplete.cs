using Asahi.Database;
using Discord.Interactions;
using Microsoft.EntityFrameworkCore;

namespace Asahi.Modules.Highlights;

public class AliasedEmoteAutocomplete : AutocompleteHandler
{
    public override async Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context, IAutocompleteInteraction autocompleteInteraction,
        IParameterInfo parameter, IServiceProvider services)
    {
        if (context.Guild == null)
            return AutocompletionResult.FromSuccess();

        var dbService = services.GetRequiredService<IDbService>();

        await using var dbContext = dbService.GetDbContext();

        var aliases = await dbContext.EmoteAliases.Where(x => x.GuildId == context.Guild.Id).ToArrayAsync();

        return AutocompletionResult.FromSuccess(
            aliases.Where(x => x.EmoteName.StartsWith((string)autocompleteInteraction.Data.Current.Value))
                .Select(emote => new AutocompleteResult(emote.EmoteName, emote.EmoteName)).Take(25));
    }
}