using System.Reflection;
using System.Security.Cryptography;
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
public class BotEmoteManagerService<TEmoteSpecification, TEmoteModel>(
    IDiscordClient discordClient,
    IInternalEmoteSource internalEmoteSource)
    where TEmoteModel : class, new()
{
    /// <summary>
    /// Gets the model containing the resolved emotes.
    /// </summary>
    /// <remarks>Will be invalid if <see cref="InitializeAsync"/> is not called.</remarks>
    public TEmoteModel Emotes { get; } = new();

    // TODO: Replace with source gen
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
        var internalEmoteSpecs = new List<InternalCustomEmoteToPropertyMapping>();

        foreach (var specPropertyInfo in typeof(TEmoteSpecification).GetProperties()
                     .Where(x => x.PropertyType == typeof(IEmoteSpecification)))
        {
            var spec = specPropertyInfo.GetValue(emotesSpecification) as IEmoteSpecification;

            IEmote? resolvedEmote;
            switch (spec)
            {
                case UnicodeEmoteSpecification unicodeSpec:
                    resolvedEmote = new Emoji(unicodeSpec.UnicodeEmote);
                    break;
                case ExternalCustomEmoteSpecification externalEmoteSpec:
                    resolvedEmote = new Emote(externalEmoteSpec.EmoteId, externalEmoteSpec.EmoteName,
                        externalEmoteSpec.IsAnimated);
                    break;
                case InternalCustomEmoteSpecification internalEmoteSpec:
                    var existingEntryWithKey = internalEmoteSpecs.FirstOrDefault(x =>
                        x.EmoteSpecification == internalEmoteSpec);
                    if (existingEntryWithKey != null)
                    {
                        existingEntryWithKey.ModelProperties.Add(typeof(TEmoteModel).GetProperty(specPropertyInfo.Name)!);
                    }
                    else
                    {
                        internalEmoteSpecs.Add(
                            new InternalCustomEmoteToPropertyMapping([typeof(TEmoteModel).GetProperty(specPropertyInfo.Name)!], internalEmoteSpec));
                    }

                    resolvedEmote = null;
                    break;
                case null:
                    throw new ArgumentNullException(specPropertyInfo.Name,
                        "Emote specification cannot have null values.");
                default:
                    throw new NotSupportedException($"Emote type {spec.GetType()} is not supported(?)");
            }

            if (resolvedEmote != null)
            {
                typeof(TEmoteModel).GetProperty(specPropertyInfo.Name)?.SetValue(Emotes, resolvedEmote);
            }
        }

        var availableEmoteKeys = internalEmoteSource.GetAvailableEmoteKeys().ToList();
        foreach (var emoteSpec in internalEmoteSpecs.Where(emoteSpec => 
                     !availableEmoteKeys.Contains(emoteSpec.EmoteSpecification.EmoteKey)))
        {
            throw new FileNotFoundException("Image data for the emote key specified was not found.",
                emoteSpec.EmoteSpecification.EmoteKey);
        }

        var existingEmotes = await discordClient.GetApplicationEmotesAsync();

        // in case things get out of sync for whatever reason, remove any emotes that don't exist on discord from the database
        foreach (var emote in internalEmoteTracking.Where(x => existingEmotes.All(y => y.Id != x.EmoteId)).ToList())
        {
            internalEmoteTracking.Remove(emote);
        }

        var emotesToAdd = internalEmoteSpecs.Where(x =>
                internalEmoteTracking.All(y => y.EmoteKey != x.EmoteSpecification.EmoteKey))
            .ToList();
        var emotesToRemove = internalEmoteTracking.Where(x =>
                internalEmoteSpecs.All(y => y.EmoteSpecification.EmoteKey != x.EmoteKey))
            .ToList();

        foreach (var emoteToRemove in emotesToRemove)
        {
            await discordClient.DeleteApplicationEmoteAsync(emoteToRemove.EmoteId);

            internalEmoteTracking.Remove(emoteToRemove);
        }

        if (emotesToAdd.Count != 0)
        {
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
                    EmoteDataIdentifier = await internalEmoteSource.GetEmoteDataIdentifierAsync(emoteToAdd.EmoteSpecification.EmoteKey)
                });
            }
        }

        // doing this after all the emotesToRemove logic, so I don't have to worry about missing files or anything
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
                EmoteDataIdentifier = await internalEmoteSource.GetEmoteDataIdentifierAsync(emoteWithIncorrectHash.EmoteKey)
            });
        }

        // TODO: Add explicit handling for unknown emotes (emotes that exist on Discord but we're not aware of)

        foreach (var spec in internalEmoteSpecs)
        {
            foreach (var internalEmote in internalEmoteTracking)
            {
                if (internalEmote.EmoteKey != spec.EmoteSpecification.EmoteKey)
                    continue;

                foreach (var property in spec.ModelProperties)
                {
                    property.SetValue(Emotes,
                        new Emote(internalEmote.EmoteId, internalEmote.EmoteKey, internalEmote.IsAnimated));
                }
            }
        }
    }

    private record InternalCustomEmoteToPropertyMapping(
        List<PropertyInfo> ModelProperties,
        InternalCustomEmoteSpecification EmoteSpecification);
}
