namespace Asahi.BotEmoteManagement;

public interface IInternalEmoteSource
{
    /// <summary>
    /// Gets a collection of all available emote keys from the source.
    /// </summary>
    /// <returns>A collection of emote keys.</returns>
    IEnumerable<string> GetAvailableEmoteKeys();
    
    /// <summary>
    /// Opens a stream for the image data associated with the given emote key.
    /// </summary>
    /// <param name="emoteKey">The key identifying the emote.</param>
    /// <returns>A stream of the emote's image data.</returns>
    Stream GetEmoteDataStream(string emoteKey);

    /// <summary>
    /// Computes an identifier for the given emote's image data.
    /// Usually just a SHA256 hash of the emote's image data.
    /// </summary>
    /// <param name="emoteKey">The key identifying the emote.</param>
    /// <returns>A byte array representing the emote image's identifier.</returns>
    Task<byte[]> GetEmoteDataIdentifierAsync(string emoteKey);
}