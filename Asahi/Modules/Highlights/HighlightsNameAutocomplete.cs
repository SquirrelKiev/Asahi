using Asahi.Database;
using Discord.Interactions;
using Microsoft.EntityFrameworkCore;

namespace Asahi.Modules.Highlights;

public class HighlightsNameAutocomplete : AutocompleteHandler
{
    public override async Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context, IAutocompleteInteraction autocompleteInteraction,
        IParameterInfo parameter, IServiceProvider services)
    {
        if (context.Guild == null)
            return AutocompletionResult.FromSuccess();

        var autocompletes = new List<AutocompleteResult>();
        var dbService = services.GetRequiredService<DbService>();

        await using var dbContext = dbService.GetDbContext();

        var boards = dbContext.HighlightBoards.Where(x => x.GuildId == context.Guild.Id && 
                                                          x.Name.StartsWith((string)autocompleteInteraction.Data.Current.Value)).Take(25);

        foreach (var board in await boards.ToArrayAsync())
        {
            autocompletes.Add(new AutocompleteResult(board.Name, board.Name));
        }

        return AutocompletionResult.FromSuccess(autocompletes);
    }
}