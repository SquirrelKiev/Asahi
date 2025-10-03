namespace Asahi.BotEmoteManagement;

public interface IEmoteSpecification;

/// <summary>
/// Represents a standard Unicode emote (emoji).
/// </summary>
/// <param name="UnicodeEmote">The Unicode character(s) for the emote.</param>
/// <remarks>
/// The <see cref="ReflectionBasedBotEmoteManagerService{TEmoteSpecification,TEmoteModel}"/> will resolve this
/// into a <see cref="Discord.Emoji"/> object.
/// </remarks>
public record UnicodeEmoteSpecification(string UnicodeEmote) : IEmoteSpecification
{
    public UnicodeEmoteSpecification() : this(string.Empty)
    {
    }
}

/// <summary>
/// Represents a custom emote that is hosted outside the application's emotes (e.g. on a Discord server).
/// </summary>
/// <param name="EmoteName">The name of the custom emote (without colons).</param>
/// <param name="EmoteId">The ID of the custom emote.</param>
/// <param name="IsAnimated">Whether the emote is animated or not.</param>
/// <remarks>
/// The <see cref="ReflectionBasedBotEmoteManagerService{TEmoteSpecification,TEmoteModel}"/> will resolve this
/// into a <see cref="Discord.Emote"/> object using the provided details.
/// </remarks>
public record ExternalCustomEmoteSpecification(string EmoteName, ulong EmoteId, bool IsAnimated) : IEmoteSpecification
{
    public ExternalCustomEmoteSpecification() : this(string.Empty, 0ul, false)
    {
    }
}

/// <summary>
/// Represents a custom emote that is managed internally by the bot.
/// The bot will upload and synchronize this emote to the application's emotes.
/// </summary>
/// <param name="EmoteKey">
/// A unique key for the internal emote. This key is used by the respective InternalEmoteSource to find the emote.
/// The key will also be used as the emote name on Discord.
/// </param>
/// <seealso cref="FileSystemInternalEmoteSource"/>
/// <remarks>
/// The <see cref="ReflectionBasedBotEmoteManagerService{TEmoteSpecification,TEmoteModel}"/> expects a corresponding
/// image file (e.g. EmoteKey.png, EmoteKey.gif) to exist in the configured internal emotes folder.
/// It will manage uploading, updating (if the image file's contents change), and deleting this emote
/// from the application's registered emotes on Discord. It then resolves this to a
/// <see cref="Discord.Emote"/> object.
/// </remarks>
public record InternalCustomEmoteSpecification(string EmoteKey) : IEmoteSpecification
{
    public InternalCustomEmoteSpecification() : this(string.Empty)
    {
    }
}