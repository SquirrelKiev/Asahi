using Asahi.Database;
using Discord.Interactions;
using Microsoft.EntityFrameworkCore;

namespace Asahi.Modules.Highlights;

public class HighlightsThresholdAutocomplete : AutocompleteHandler
{
    public override async Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context, IAutocompleteInteraction autocompleteInteraction,
        IParameterInfo parameter, IServiceProvider services)
    {
        if (context.Guild == null)
            return AutocompletionResult.FromSuccess();

        //var logger = services.GetRequiredService<ILogger<HighlightsThresholdAutocomplete>>();
        var dbService = services.GetRequiredService<DbService>();

        var boardOption = autocompleteInteraction.Data.Options.FirstOrDefault(x => x.Name == "name");

        if (boardOption == null)
            return AutocompletionResult.FromSuccess();

        await using var dbContext = dbService.GetDbContext();

        var board = await dbContext.HighlightBoards.Include(highlightBoard => highlightBoard.Thresholds)
            .FirstOrDefaultAsync(x => x.GuildId == context.Guild.Id && x.Name == (string)boardOption.Value);

        if (board == null)
            return AutocompletionResult.FromSuccess();

        var results = new List<AutocompleteResult>();
        foreach (var threshold in board.Thresholds)
        {
            var name = (await context.Guild.GetChannelAsync(threshold.OverrideId))?.Name;
            name = name == null ? (await context.Client.GetGuildAsync(threshold.OverrideId)).Name : $"#{name}";

            results.Add(new AutocompleteResult($"{name}", threshold.OverrideId.ToString()));
        }

        var userInput = (string)autocompleteInteraction.Data.Current.Value;
        return AutocompletionResult.FromSuccess(results.Where(x => 
            x.Name.StartsWith(userInput) || ((string)x.Value).StartsWith(userInput)).Take(25));
    }
}
