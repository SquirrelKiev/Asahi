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

        var dbService = services.GetRequiredService<DbService>();

        var boardOption = autocompleteInteraction.Data.Options.FirstOrDefault(x => x.Name == "name");

        if (boardOption == null)
            return AutocompletionResult.FromSuccess();

        await using var dbContext = dbService.GetDbContext();

        var board = await dbContext.HighlightBoards.Include(highlightBoard => highlightBoard.EmoteAliases)
            .FirstOrDefaultAsync(x => x.GuildId == context.Guild.Id && x.Name == (string)boardOption.Value);

        if (board == null)
            return AutocompletionResult.FromSuccess();

        return AutocompletionResult.FromSuccess(
            board.EmoteAliases.Where(x => x.EmoteName.StartsWith((string)boardOption.Value))
                .Select(emote => new AutocompleteResult(emote.EmoteName, emote.EmoteName)).Take(25));
    }
}