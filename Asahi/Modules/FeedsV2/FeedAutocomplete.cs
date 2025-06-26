using System.Linq.Expressions;
using Asahi.Database;
using Asahi.Database.Models.Rss;
using Discord.Interactions;
using Microsoft.EntityFrameworkCore;

namespace Asahi.Modules.FeedsV2;

public class FeedAutocomplete : AutocompleteHandler
{
    public override async Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context,
        IAutocompleteInteraction autocompleteInteraction,
        IParameterInfo parameter, IServiceProvider services)
    {
        if (context.Guild == null)
            return AutocompletionResult.FromSuccess();

        await using var dbContext = services.GetRequiredService<BotDbContext>();
        var state = services.GetRequiredService<FeedsStateTracker>();

        var input = (string)autocompleteInteraction.Data.Current.Value;
        
        // format: "#channel-name search term" or "#channel-id search term"
        List<ulong>? channelFilter = null;
        var searchTerm = input;
        
        var channels = await context.Guild.GetTextChannelsAsync();
        if (!string.IsNullOrEmpty(input))
        {
            if (input.StartsWith('#'))
            {
                var parts = input.Split(' ', 2);
                var channelMention = parts[0][1..];
                
                var filteredChannels = channels.Where(c => 
                    c.Name.Contains(channelMention, StringComparison.OrdinalIgnoreCase) || c.Id.ToString() == channelMention);
                
                channelFilter = filteredChannels.Select(x => x.Id).ToList();
                searchTerm = parts.Length > 1 ? parts[1] : "";
            }
        }

        var query = dbContext.RssFeedListeners.Where(x => x.GuildId == context.Guild.Id);
        
        if (channelFilter != null && channelFilter.Count != 0)
        {
            query = query.Where(x => channelFilter.Contains(x.ChannelId));
        }

        var channelId = autocompleteInteraction.ChannelId.GetValueOrDefault();
        var feeds = await query.OrderByDescending(FeedOrderSelector(channelId)).Take(25).ToListAsync();

        return AutocompletionResult.FromSuccess(
            feeds.Where(x => string.IsNullOrEmpty(searchTerm) ||
                             x.FeedUrl.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                             (x.FeedTitle ?? state.GetBestDefaultFeedTitle(x.FeedUrl)).Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                             x.Id.ToString() == searchTerm)
                .Select(x => CreateAutocompleteResult(x, state, channels)))
            ;
    }

    private static Expression<Func<FeedListener, uint>> FeedOrderSelector(ulong channelId)
    {
        return x => x.Id + (x.ChannelId == channelId ? 0xFFFFFFFF : 0);
    }

    private static AutocompleteResult CreateAutocompleteResult(FeedListener x, FeedsStateTracker state, IReadOnlyCollection<ITextChannel> channels)
    {
        var disabledPrefix = x.Enabled ? "" : "(DISABLED) ";
        var suffix = $" (#{(channels.FirstOrDefault(y => y.Id == x.ChannelId)?.Name ?? x.ChannelId.ToString()).Truncate(32, false)}) ({x.Id})";
        
        var fixedLength = disabledPrefix.Length + suffix.Length;
        var availableForTitle = Math.Max(1, 100 - fixedLength);
        
        var truncatedTitle = state.GetBestDefaultFeedTitle(x.FeedUrl).Truncate(availableForTitle, false);

        var res = $"{disabledPrefix}{truncatedTitle}{suffix}";
        
        return new AutocompleteResult(res, x.Id);
    }
}
