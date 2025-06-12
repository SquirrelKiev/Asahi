using Discord;

namespace Asahi.BotEmoteManagement;

public interface IEmoteResolver
{
    public record EmoteSpecMapping(string PropertyName, IEmoteSpecification EmoteSpecification);

    public Task<Dictionary<string, IEmote>> ResolveAsync(
        IReadOnlyList<EmoteSpecMapping> mappings,
        IList<InternalCustomEmoteTracking> internalEmoteTracking
    );
}