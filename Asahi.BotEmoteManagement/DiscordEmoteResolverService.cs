using System.Diagnostics;
using Discord;

namespace Asahi.BotEmoteManagement;

public class DiscordEmoteResolverService(IDiscordClient discordClient, IInternalEmoteSource internalEmoteSource) : IEmoteResolver
{
    public async Task<Dictionary<string, IEmote>> ResolveAsync(
        IReadOnlyList<IEmoteResolver.EmoteSpecMapping> mappings,
        IList<InternalCustomEmoteTracking> internalEmoteTracking
    )
    {
        var internalSpecs = mappings
            .Where(x => x.EmoteSpecification is InternalCustomEmoteSpecification)
            .Select(x => (InternalCustomEmoteSpecification)x.EmoteSpecification)
            .Distinct()
            .ToList();

        var availableEmoteKeys = internalEmoteSource.GetAvailableEmoteKeys().ToList();

        var emoteSpecsWithoutMatchingImageData = internalSpecs.Where(emoteSpec =>
            !availableEmoteKeys.Contains(emoteSpec.EmoteKey));

        foreach (var emoteSpec in emoteSpecsWithoutMatchingImageData)
        {
            throw new FileNotFoundException("Image data for the emote key specified was not found.",
                emoteSpec.EmoteKey);
        }

        var existingEmotes = await discordClient.GetApplicationEmotesAsync();

        PruneStaleTrackedEmotes(internalEmoteTracking, existingEmotes);

        await RemoveObsoleteEmotesAsync(internalEmoteTracking, internalSpecs);
        var emotesToAdd = await AddMissingEmotesAsync(internalEmoteTracking, internalSpecs, existingEmotes);

        await UpdateChangedEmotesAsync(internalEmoteTracking, emotesToAdd);

        var resolvedEmotes = new Dictionary<string, IEmote>(mappings.Count);
        
        // TODO: Add explicit handling for unknown emotes (emotes that exist on Discord but are not internally tracked)
        
        ResolveEmotes(mappings, internalEmoteTracking, ref resolvedEmotes);

        Debug.Assert(mappings.Count == resolvedEmotes.Count);

        return resolvedEmotes;
    }

    private static void PruneStaleTrackedEmotes(IList<InternalCustomEmoteTracking> internalEmoteTracking,
        IReadOnlyCollection<Emote> existingEmotes)
    {
        foreach (var emote in internalEmoteTracking.Where(x => existingEmotes.All(y => y.Id != x.EmoteId)).ToList())
        {
            internalEmoteTracking.Remove(emote);
        }
    }

    private async Task RemoveObsoleteEmotesAsync(IList<InternalCustomEmoteTracking> internalEmoteTracking,
        List<InternalCustomEmoteSpecification> internalEmoteMappings)
    {
        var emotesToRemove = internalEmoteTracking.Where(x =>
                internalEmoteMappings.All(y => y.EmoteKey != x.EmoteKey))
            .ToList();

        foreach (var emoteToRemove in emotesToRemove)
        {
            await discordClient.DeleteApplicationEmoteAsync(emoteToRemove.EmoteId);

            internalEmoteTracking.Remove(emoteToRemove);
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="internalEmoteTracking"></param>
    /// <param name="internalEmoteMappings"></param>
    /// <param name="existingEmotes"></param>
    /// <returns>The emotes that were added.</returns>
    private async Task<List<InternalCustomEmoteSpecification>> AddMissingEmotesAsync(
        IList<InternalCustomEmoteTracking> internalEmoteTracking,
        List<InternalCustomEmoteSpecification> internalEmoteMappings,
        IReadOnlyCollection<Emote> existingEmotes)
    {
        var emotesToAdd = internalEmoteMappings.Where(x =>
                internalEmoteTracking.All(y => y.EmoteKey != x.EmoteKey))
            .ToList();

        if (emotesToAdd.Count == 0) return emotesToAdd;

        foreach (var emoteToAdd in emotesToAdd)
        {
            await using var stream = internalEmoteSource.GetEmoteDataStream(emoteToAdd.EmoteKey);

            var existingEmote =
                existingEmotes.FirstOrDefault(x => x.Name == emoteToAdd.EmoteKey);
            if (existingEmote != null)
            {
                await discordClient.DeleteApplicationEmoteAsync(existingEmote.Id);

                // debug
                // internalEmoteTracking.Add(new InternalCustomEmoteTracking
                // {
                //     EmoteKey = emoteToAdd.EmoteSpecification.EmoteKey,
                //     EmoteId = existingEmote.Id,
                //     IsAnimated = existingEmote.Animated,
                //     EmoteDataIdentifier = await internalEmoteSource.GetEmoteDataIdentifierAsync(emoteToAdd.EmoteSpecification.EmoteKey)
                // });
                //
                // continue;
            }

            var emote = await discordClient.CreateApplicationEmoteAsync(emoteToAdd.EmoteKey,
                new Image(stream));

            stream.Position = 0;

            internalEmoteTracking.Add(new InternalCustomEmoteTracking
            {
                EmoteKey = emoteToAdd.EmoteKey,
                EmoteId = emote.Id,
                IsAnimated = emote.Animated,
                EmoteDataIdentifier =
                    await internalEmoteSource.GetEmoteDataIdentifierAsync(emoteToAdd.EmoteKey)
            });
        }

        return emotesToAdd;
    }

    private async Task UpdateChangedEmotesAsync(IList<InternalCustomEmoteTracking> internalEmoteTracking,
        List<InternalCustomEmoteSpecification> emotesToAdd)
    {
        var emotesWithIncorrectIdentifiers =
            internalEmoteTracking.ToAsyncEnumerable().WhereAwait(async x =>
            {
                if (emotesToAdd.Any(y => y.EmoteKey == x.EmoteKey))
                    return false;

                var newIdentifier = await internalEmoteSource.GetEmoteDataIdentifierAsync(x.EmoteKey);
                return newIdentifier.SequenceEqual(x.EmoteDataIdentifier) == false;
            });

        foreach (var emoteWithIncorrectHash in await emotesWithIncorrectIdentifiers.ToArrayAsync())
        {
            await discordClient.DeleteApplicationEmoteAsync(emoteWithIncorrectHash.EmoteId);

            internalEmoteTracking.Remove(emoteWithIncorrectHash);

            await using var stream = internalEmoteSource.GetEmoteDataStream(emoteWithIncorrectHash.EmoteKey);

            var emote = await discordClient.CreateApplicationEmoteAsync(emoteWithIncorrectHash.EmoteKey,
                new Image(stream));

            internalEmoteTracking.Add(new InternalCustomEmoteTracking
            {
                EmoteKey = emoteWithIncorrectHash.EmoteKey,
                EmoteId = emote.Id,
                IsAnimated = emote.Animated,
                EmoteDataIdentifier =
                    await internalEmoteSource.GetEmoteDataIdentifierAsync(emoteWithIncorrectHash.EmoteKey)
            });
        }
    }

    private static void ResolveEmotes(
        IReadOnlyList<IEmoteResolver.EmoteSpecMapping> mappings,
        IList<InternalCustomEmoteTracking> internalEmoteTracking,
        ref Dictionary<string, IEmote> resolvedEmotes)
    {
        foreach (var mapping in mappings)
        {
            switch (mapping.EmoteSpecification)
            {
                case UnicodeEmoteSpecification u:
                    resolvedEmotes[mapping.PropertyName] = new Emoji(u.UnicodeEmote);
                    break;
                case ExternalCustomEmoteSpecification e:
                    resolvedEmotes[mapping.PropertyName] = new Emote(e.EmoteId, e.EmoteName,
                        e.IsAnimated);
                    break;
                case InternalCustomEmoteSpecification i:
                    var existingEntry =
                        internalEmoteTracking.First(x => x.EmoteKey == i.EmoteKey);
                    
                    resolvedEmotes[mapping.PropertyName] = 
                        new Emote(existingEntry.EmoteId, existingEntry.EmoteKey, existingEntry.IsAnimated);
                    break;
            }
        }
    }
}