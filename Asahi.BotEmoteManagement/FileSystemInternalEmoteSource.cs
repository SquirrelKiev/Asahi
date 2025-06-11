using System.Security.Cryptography;

namespace Asahi.BotEmoteManagement;

public class FileSystemInternalEmoteSource : IInternalEmoteSource
{
    private readonly string emoteImagesFolder;
    private readonly Lazy<IReadOnlyDictionary<string, string>> emoteToKeyPathMap;

    public FileSystemInternalEmoteSource(string emoteImagesFolder)
    {
        this.emoteImagesFolder = emoteImagesFolder;

        emoteToKeyPathMap = new Lazy<IReadOnlyDictionary<string, string>>(() =>
        {
            if (!Directory.Exists(emoteImagesFolder))
            {
                // not sure if this is a good approach
                Directory.CreateDirectory(emoteImagesFolder);
            }
            
            return Directory.EnumerateFiles(emoteImagesFolder)
                .GroupBy(path => Path.GetFileNameWithoutExtension(path) ?? string.Empty)
                .ToDictionary(g => g.Key, g => g.First());
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