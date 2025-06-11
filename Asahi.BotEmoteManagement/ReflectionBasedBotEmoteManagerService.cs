using System.Reflection;
using Discord;

namespace Asahi.BotEmoteManagement;

/// <inheritdoc/>
public class ReflectionBasedBotEmoteManagerService<TEmoteSpecification, TEmoteModel>(
    IDiscordClient discordClient,
    IInternalEmoteSource internalEmoteSource) : BaseBotEmoteManagerService<TEmoteSpecification, TEmoteModel>(discordClient, internalEmoteSource) where TEmoteModel : class, new()
{
    protected override List<EmoteMapping> DiscoverEmotes(TEmoteSpecification emoteSpec)
    {
        var discoveredEmotes = new List<EmoteMapping>();

        // ReSharper disable once LoopCanBeConvertedToQuery
        foreach (var specPropertyInfo in typeof(TEmoteSpecification).GetProperties()
                     .Where(x => x.PropertyType == typeof(IEmoteSpecification)))
        {
            var spec = (specPropertyInfo.GetValue(emoteSpec) as IEmoteSpecification)!;

            var modelProp = typeof(TEmoteModel).GetProperty(specPropertyInfo.Name);
            if (modelProp == null) continue;

            var discovered = new EmoteMapping(
                spec,
                emote => modelProp.SetValue(Emotes, emote)
            );
            discoveredEmotes.Add(discovered);
        }

        return discoveredEmotes;

    }
}
