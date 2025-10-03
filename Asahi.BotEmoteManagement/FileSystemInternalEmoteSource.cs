using System.Security.Cryptography;

namespace Asahi.BotEmoteManagement;

/// <summary>
/// An <see cref="IInternalEmoteSource"/> that retrieves emote images from the file system.
/// The <see cref="InternalCustomEmoteSpecification"/> Key is the filename without the extension.
/// </summary>
public class FileSystemInternalEmoteSource : IInternalEmoteSource
{
    // private readonly string[] emoteImageDirectories;
    private readonly Lazy<IReadOnlyDictionary<string, string>> emoteToKeyPathMap;

    /// <summary>
    /// Creates an instance of <see cref="FileSystemInternalEmoteSource"/>.
    /// </summary>
    /// <param name="emoteImageDirectories">The directories to search for emotes in.
    /// The <see cref="InternalCustomEmoteSpecification"/> Key is the filename without the extension.</param>
    /// <remarks>The higher the directory in the array, the higher the priority it has.
    /// For example, if the array is <c>[DirectoryA, DirectoryB]</c>, and both directories contain the emote <c>FooBar</c>,
    /// <c>DirectoryA</c>'s <c>FooBar</c> will take priority and be used.</remarks>
    public FileSystemInternalEmoteSource(string[] emoteImageDirectories)
    {
        // this.emoteImageDirectories = emoteImageDirectories;

        emoteToKeyPathMap = new Lazy<IReadOnlyDictionary<string, string>>(() =>
        {
            foreach (var folder in emoteImageDirectories)
            {
                if (!Directory.Exists(folder))
                {
                    // not sure if this is a good approach
                    Directory.CreateDirectory(folder);
                }
            }

            return emoteImageDirectories.SelectMany(Directory.EnumerateFiles)
                .GroupBy(Path.GetFileNameWithoutExtension)
                .ToDictionary(g => g.Key!, g => g.First());
        });
    }

    private string GetPathForKey(string emoteKey)
    {
        if (emoteToKeyPathMap.Value.TryGetValue(emoteKey, out var path))
        {
            return path;
        }

        throw new FileNotFoundException("A corresponding image file for an internal emote was not found.", emoteKey);
    }

    public IEnumerable<string> GetAvailableEmoteKeys() => emoteToKeyPathMap.Value.Keys;

    public Stream GetEmoteDataStream(string emoteKey)
    {
        var path = GetPathForKey(emoteKey);
        return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
    }

    public async Task<byte[]> GetEmoteDataIdentifierAsync(string emoteKey)
    {
        await using var fileStream = GetEmoteDataStream(emoteKey);
        return await SHA256.HashDataAsync(fileStream);
    }
}