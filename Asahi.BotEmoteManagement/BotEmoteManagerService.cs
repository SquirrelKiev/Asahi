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

public abstract class BotEmoteManagerService<TEmoteSpecification, TEmoteModel>(
    IDiscordClient discordClient,
    BotEmoteManagerConfig config)
    where TEmoteModel : class, new()
{
    /// <summary>
    /// Gets the model containing the resolved emotes.
    /// </summary>
    /// <remarks>Will be invalid if <see cref="Initialize"/> is not called.</remarks>
    public TEmoteModel Emotes { get; private set; } = new();

    // TODO: Replace with source gen
    /// <summary>
    /// Resolves and synchronizes emotes with Discord.
    /// </summary>
    /// <param name="emotesSpecification">The populated specification defining the emotes.</param>
    /// <param name="internalEmoteTracking">A persisted list for tracking the state of internal custom emotes.</param>
    /// <exception cref="FileNotFoundException">Thrown if an image file for an internal emote is not found.</exception>
    public async Task Initialize(TEmoteSpecification emotesSpecification,
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
                        existingEntryWithKey.PropertyInfos.Add(specPropertyInfo);
                    }
                    else
                    {
                        internalEmoteSpecs.Add(
                            new InternalCustomEmoteToPropertyMapping([specPropertyInfo], internalEmoteSpec));
                    }

                    resolvedEmote = null;
                    break;
                default:
                    resolvedEmote = null;
                    break;
            }

            if (resolvedEmote != null)
            {
                typeof(TEmoteModel).GetProperty(specPropertyInfo.Name)?.SetValue(Emotes, resolvedEmote);
            }
        }

        var files = Directory.GetFiles(config.InternalEmoteImagesFolder);
        foreach (var emoteSpec in internalEmoteSpecs.Where(emoteSpec =>
                     files.All(x => Path.GetFileNameWithoutExtension(x) != emoteSpec.EmoteSpecification.EmoteKey)))
        {
            throw new FileNotFoundException("A corresponding image file for an internal emote was not found.",
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
        
        if(emotesToAdd.Count != 0)
        {
            foreach (var emoteToAdd in emotesToAdd)
            {
                await using var fileStream =
                    new FileStream(
                        files.First(x => Path.GetFileNameWithoutExtension(x) == emoteToAdd.EmoteSpecification.EmoteKey),
                        FileMode.Open);

                var existingEmote = existingEmotes.FirstOrDefault(x => x.Name == emoteToAdd.EmoteSpecification.EmoteKey);
                if (existingEmote != null)
                {
                    await discordClient.DeleteApplicationEmoteAsync(existingEmote.Id);
                    
                    // debug
                    // internalEmoteTracking.Add(new InternalCustomEmoteTracking
                    // {
                    //     EmoteKey = emoteToAdd.EmoteSpecification.EmoteKey,
                    //     EmoteId = existingEmote.Id,
                    //     IsAnimated = existingEmote.Animated,
                    //     Sha256Hash = await SHA256.HashDataAsync(fileStream)
                    // });
                    //
                    // continue;
                }
                
                var emote = await discordClient.CreateApplicationEmoteAsync(emoteToAdd.EmoteSpecification.EmoteKey,
                    new Image(fileStream));

                fileStream.Position = 0;

                internalEmoteTracking.Add(new InternalCustomEmoteTracking
                {
                    EmoteKey = emoteToAdd.EmoteSpecification.EmoteKey,
                    EmoteId = emote.Id,
                    IsAnimated = emote.Animated,
                    Sha256Hash = await SHA256.HashDataAsync(fileStream)
                });
            }
        }

        // doing this after all the emotesToRemove logic, so I don't have to worry about missing files or anything
        var emotesWithIncorrectHashes =
            internalEmoteTracking.ToAsyncEnumerable().WhereAwait(async x =>
            {
                if (emotesToAdd.Any(y => y.EmoteSpecification.EmoteKey == x.EmoteKey))
                    return false;

                await using var fileStream =
                    new FileStream(
                        files.First(y => Path.GetFileNameWithoutExtension(y) == x.EmoteKey),
                        FileMode.Open);

                var newHash = await SHA256.HashDataAsync(fileStream);
                return newHash.SequenceEqual(x.Sha256Hash) == false;
            });

        foreach (var emoteWithIncorrectHash in await emotesWithIncorrectHashes.ToArrayAsync())
        {
            await discordClient.DeleteApplicationEmoteAsync(emoteWithIncorrectHash.EmoteId);

            internalEmoteTracking.Remove(emoteWithIncorrectHash);

            await using var fileStream =
                new FileStream(
                    files.First(x => Path.GetFileNameWithoutExtension(x) == emoteWithIncorrectHash.EmoteKey),
                    FileMode.Open);

            var emote = await discordClient.CreateApplicationEmoteAsync(emoteWithIncorrectHash.EmoteKey,
                new Image(fileStream));

            fileStream.Position = 0;

            internalEmoteTracking.Add(new InternalCustomEmoteTracking
            {
                EmoteKey = emoteWithIncorrectHash.EmoteKey,
                EmoteId = emote.Id,
                IsAnimated = emote.Animated,
                Sha256Hash = await SHA256.HashDataAsync(fileStream)
            });
        }

        // TODO: Add explicit handling for unknown emotes (emotes that exist on Discord but we're not aware of)

        foreach (var spec in internalEmoteSpecs)
        {
            foreach (var internalEmote in internalEmoteTracking)
            {
                if (internalEmote.EmoteKey != spec.EmoteSpecification.EmoteKey)
                    continue;

                foreach (var property in spec.PropertyInfos)
                {
                    property.SetValue(Emotes, new Emote(internalEmote.EmoteId, internalEmote.EmoteKey, internalEmote.IsAnimated));
                }
            }
        }
    }

    private record InternalCustomEmoteToPropertyMapping(
        List<PropertyInfo> PropertyInfos,
        InternalCustomEmoteSpecification EmoteSpecification);
}

/// <summary>
/// Configuration for the <see cref="BotEmoteManagerService{TEmoteSpecification, TEmoteModel}"/>.
/// </summary>
public record BotEmoteManagerConfig
{
    /// <summary>
    /// The folder path for internal emote images.
    /// Defaults to "InternalEmotes" relative to the application base directory.
    /// </summary>
    public string InternalEmoteImagesFolder { get; init; } =
        Path.GetRelativePath(AppContext.BaseDirectory, "InternalEmotes");
}