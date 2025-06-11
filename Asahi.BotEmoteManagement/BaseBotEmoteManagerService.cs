using Discord;

namespace Asahi.BotEmoteManagement;

// TODO: Source generate TEmoteModel
/// <summary>
/// Manages bot emotes, primarily populating <see cref="TEmoteModel"/>
/// with the emotes specified in <typeparamref name="TEmoteSpecification"/>.
/// </summary>
/// <typeparam name="TEmoteSpecification">The type representing the emote config/specifications.
/// Expects an object with properties that are <see cref="IEmoteSpecification"/>s.
/// </typeparam>
/// <typeparam name="TEmoteModel">The type representing the model that holds the resolved emotes.
/// Expects an object with the exact same properties as <typeparamref name="TEmoteSpecification"/>,
/// just with <see cref="IEmote"/>s instead of <see cref="IEmoteSpecification"/>s.
/// </typeparam>
public abstract class BaseBotEmoteManagerService<TEmoteSpecification, TEmoteModel>(
    IDiscordClient discordClient,
    IInternalEmoteSource internalEmoteSource) where TEmoteModel : class, new()
{
    /// <summary>
    /// Gets the model containing the resolved emotes.
    /// </summary>
    /// <remarks>Will be invalid if <see cref="InitializeAsync"/> is not called.</remarks>
    public TEmoteModel Emotes { get; } = new();

    protected abstract List<EmoteMapping> DiscoverEmotes(TEmoteSpecification emoteSpec);

    /// <summary>
    /// Resolves and synchronizes emotes with Discord.
    /// </summary>
    /// <param name="emotesSpecification">The populated specification defining the emotes.</param>
    /// <param name="internalEmoteTracking">A persisted list for tracking the state of internal custom emotes.</param>
    /// <exception cref="FileNotFoundException">Thrown if an image file for an internal emote is not found.</exception>
    /// <exception cref="ArgumentNullException">Thrown if any spec properties on <see cref="TEmoteSpecification"/> are null.</exception>
    /// <exception cref="NotSupportedException">Thrown if any spec properties on <see cref="TEmoteSpecification"/>
    /// are not one of <see cref="UnicodeEmoteSpecification"/>, <see cref="ExternalCustomEmoteSpecification"/>,
    /// or <see cref="InternalCustomEmoteTracking"/>.</exception>
    public async Task InitializeAsync(TEmoteSpecification emotesSpecification,
        IList<InternalCustomEmoteTracking> internalEmoteTracking)
    {
        var discoveredEmotes = DiscoverEmotes(emotesSpecification);
        var internalEmoteMappings = ResolveSimpleEmotes(discoveredEmotes);

        var availableEmoteKeys = internalEmoteSource.GetAvailableEmoteKeys().ToList();

        var emoteSpecsWithoutMatchingImageData = internalEmoteMappings.Where(emoteSpec =>
            !availableEmoteKeys.Contains(emoteSpec.EmoteSpecification.EmoteKey));

        foreach (var emoteSpec in emoteSpecsWithoutMatchingImageData)
        {
            throw new FileNotFoundException("Image data for the emote key specified was not found.",
                emoteSpec.EmoteSpecification.EmoteKey);
        }

        var existingEmotes = await discordClient.GetApplicationEmotesAsync();

        PruneStaleTrackedEmotes(internalEmoteTracking, existingEmotes);

        await RemoveObsoleteEmotesAsync(internalEmoteTracking, internalEmoteMappings);
        var emotesToAdd = await AddMissingEmotesAsync(internalEmoteTracking, internalEmoteMappings, existingEmotes);

        await UpdateChangedEmotesAsync(internalEmoteTracking, emotesToAdd);

        // TODO: Add explicit handling for unknown emotes (emotes that exist on Discord but we're not aware of)

        BindInternalEmotesToModel(internalEmoteTracking, internalEmoteMappings);
    }

    private static List<InternalEmoteMapping> ResolveSimpleEmotes(List<EmoteMapping> discoveredEmotes)
    {
        var internalEmoteMappings = new List<InternalEmoteMapping>(discoveredEmotes.Count);

        foreach (var discoveredEmote in discoveredEmotes)
        {
            switch (discoveredEmote.EmoteSpecification)
            {
                case UnicodeEmoteSpecification unicode:
                    discoveredEmote.AssignAction(new Emoji(unicode.UnicodeEmote));
                    break;
                case ExternalCustomEmoteSpecification external:
                    discoveredEmote.AssignAction(new Emote(external.EmoteId, external.EmoteName, external.IsAnimated));
                    break;
                case InternalCustomEmoteSpecification internalEmote:
                    var existingEntry =
                        internalEmoteMappings.FirstOrDefault(x => x.EmoteSpecification.Equals(internalEmote));

                    if (existingEntry == null)
                    {
                        internalEmoteMappings.Add(new InternalEmoteMapping(internalEmote, discoveredEmote.AssignAction));
                    }
                    else
                    {
                        existingEntry.AssignAction += discoveredEmote.AssignAction;
                    }
                    break;
                default:
                    throw new NotSupportedException(
                        $"Emote type {discoveredEmote.EmoteSpecification.GetType()} is not supported(?)");
            }
        }

        return internalEmoteMappings;
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
        List<InternalEmoteMapping> internalEmoteMappings)
    {
        var emotesToRemove = internalEmoteTracking.Where(x =>
                internalEmoteMappings.All(y => y.EmoteSpecification.EmoteKey != x.EmoteKey))
            .ToList();

        foreach (var emoteToRemove in emotesToRemove)
        {
            await discordClient.DeleteApplicationEmoteAsync(emoteToRemove.EmoteId);

            internalEmoteTracking.Remove(emoteToRemove);
        }
    }

    private async Task<List<InternalEmoteMapping>> AddMissingEmotesAsync(
        IList<InternalCustomEmoteTracking> internalEmoteTracking,
        List<InternalEmoteMapping> internalEmoteMappings,
        IReadOnlyCollection<Emote> existingEmotes)
    {
        var emotesToAdd = internalEmoteMappings.Where(x =>
                internalEmoteTracking.All(y => y.EmoteKey != x.EmoteSpecification.EmoteKey))
            .ToList();

        if (emotesToAdd.Count == 0) return emotesToAdd;

        foreach (var emoteToAdd in emotesToAdd)
        {
            await using var stream = internalEmoteSource.GetEmoteDataStream(emoteToAdd.EmoteSpecification.EmoteKey);

            var existingEmote =
                existingEmotes.FirstOrDefault(x => x.Name == emoteToAdd.EmoteSpecification.EmoteKey);
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

            var emote = await discordClient.CreateApplicationEmoteAsync(emoteToAdd.EmoteSpecification.EmoteKey,
                new Image(stream));

            stream.Position = 0;

            internalEmoteTracking.Add(new InternalCustomEmoteTracking
            {
                EmoteKey = emoteToAdd.EmoteSpecification.EmoteKey,
                EmoteId = emote.Id,
                IsAnimated = emote.Animated,
                EmoteDataIdentifier =
                    await internalEmoteSource.GetEmoteDataIdentifierAsync(emoteToAdd.EmoteSpecification.EmoteKey)
            });
        }

        return emotesToAdd;
    }

    private async Task UpdateChangedEmotesAsync(IList<InternalCustomEmoteTracking> internalEmoteTracking,
        List<InternalEmoteMapping> emotesToAdd)
    {
        var emotesWithIncorrectIdentifiers =
            internalEmoteTracking.ToAsyncEnumerable().WhereAwait(async x =>
            {
                if (emotesToAdd.Any(y => y.EmoteSpecification.EmoteKey == x.EmoteKey))
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

    private static void BindInternalEmotesToModel(IList<InternalCustomEmoteTracking> internalEmoteTracking,
        List<InternalEmoteMapping> internalEmoteMappings)
    {
        foreach (var mapping in internalEmoteMappings)
        {
            var trackedEmote =
                internalEmoteTracking.FirstOrDefault(x => x.EmoteKey == mapping.EmoteSpecification.EmoteKey);

            if (trackedEmote == null)
                continue;

            var resolvedEmote = new Emote(trackedEmote.EmoteId, trackedEmote.EmoteKey, trackedEmote.IsAnimated);
            mapping.AssignAction(resolvedEmote);
        }
    }

    protected record EmoteMapping(IEmoteSpecification EmoteSpecification, Action<IEmote> AssignAction);
    protected class InternalEmoteMapping(InternalCustomEmoteSpecification emoteSpecification, Action<IEmote> assignAction)
    {
        public InternalCustomEmoteSpecification EmoteSpecification { get; init; } = emoteSpecification;
        public Action<IEmote> AssignAction { get; set; } = assignAction;
    }
}