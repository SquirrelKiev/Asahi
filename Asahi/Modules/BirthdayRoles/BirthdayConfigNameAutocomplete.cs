using Asahi.Database;
using Discord.Interactions;
using Microsoft.EntityFrameworkCore;

namespace Asahi.Modules.BirthdayRoles;

public class BirthdayConfigNameAutocomplete : AutocompleteHandler
{
    public override async Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context, IAutocompleteInteraction autocompleteInteraction,
        IParameterInfo parameter, IServiceProvider services)
    {
        if (context.Guild == null)
            return AutocompletionResult.FromSuccess();

        var autocompletes = new List<AutocompleteResult>();
        await using var dbContext = services.GetRequiredService<BotDbContext>();

        var boards = dbContext.BirthdayConfigs.Where(x => x.GuildId == context.Guild.Id &&
                                                          x.Name.StartsWith((string)autocompleteInteraction.Data.Current.Value))
            .OrderBy(x => x.Name).Take(25);

        foreach (var board in await boards.ToArrayAsync())
        {
            autocompletes.Add(new AutocompleteResult(board.Name, board.Name));
        }

        return AutocompletionResult.FromSuccess(autocompletes);
    }
}
