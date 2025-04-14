using System.Collections.Concurrent;
using April.Config.Editor.Veldrid.ImageSharp;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Veldrid;

namespace April.Config.Editor;

public sealed class ImageLoader : IDisposable
{
    public class TextureInfo
    {
        public Texture texture;
        public TextureView textureView;
        public IntPtr imguiTexturePtr;
    }

    public ImageLoader(GraphicsDevice graphicsDevice, ImGuiController controller)
    {
        this.graphicsDevice = graphicsDevice;
        this.controller = controller;

        GetOrDownloadImage(notFoundUrl);
        GetOrDownloadImage(loadingUrl);
    }

    private readonly ImGuiController controller;
    private readonly GraphicsDevice graphicsDevice;

    private readonly ConcurrentDictionary<string, TextureInfo?> textureCache = new();
    private readonly ConcurrentDictionary<string, Task<TextureInfo?>> downloadTasks = new();
    private readonly CancellationTokenSource cancellationTokenSource = new();
    
    public const string notFoundUrl = "https://files.catbox.moe/jr5jhy.png";
    public const string loadingUrl = "https://files.catbox.moe/c5hh9f.png";

    public bool IsLoadingImage(string imageUrl)
    {
        return downloadTasks.ContainsKey(imageUrl);
    }

    public TextureInfo? GetOrDownloadImage(string imageUrl)
    {
        if (!downloadTasks.ContainsKey(imageUrl) && textureCache.TryGetValue(imageUrl, out var cachedTexture))
        {
            return cachedTexture;
        }

        if (!downloadTasks.TryGetValue(imageUrl, out var task))
        {
            task = Task.Run(() => DownloadAndLoadTextureAsync(imageUrl, cancellationTokenSource.Token), cancellationTokenSource.Token);
            downloadTasks.TryAdd(imageUrl, task);
        }

        if (!task.IsCompleted) return null;

        var texture = task.Result;
        downloadTasks.TryRemove(imageUrl, out _);
        return texture;
    }

    public async Task<TextureInfo?> DownloadAndLoadTextureAsync(string imageUrl, CancellationToken ct)
    {
        try
        {
            if (textureCache.TryGetValue(imageUrl, out var cachedTexture))
            {
                return cachedTexture;
            }

            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("ONKGacha (discord.gg/oshinoko)");
            var getReq = await httpClient.GetAsync(imageUrl, ct);

            if (!getReq.IsSuccessStatusCode)
            {
                throw new BadImageFormatException("Failed to get image.");
            }

            var imageData = await getReq.Content.ReadAsByteArrayAsync(ct);

            using var image = Image.Load<Rgba32>(imageData);

            var texture =
                new ImageSharpTexture(image).CreateDeviceTexture(graphicsDevice, graphicsDevice.ResourceFactory);
            var textureView = graphicsDevice.ResourceFactory.CreateTextureView(texture);

            var textureInfo = new TextureInfo()
            {
                texture = texture,
                textureView = textureView,
                imguiTexturePtr = controller.GetOrCreateImGuiBinding(graphicsDevice.ResourceFactory, textureView)
            };

            if (!textureCache.TryAdd(imageUrl, textureInfo))
                throw new Exception("Key already exists!");

            return textureInfo;
        }
        catch (Exception)
        {
            if (!textureCache.TryAdd(imageUrl, null))
                throw new Exception("Key already exists!");

            return null;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        cancellationTokenSource.Cancel();

        foreach (var texKvp in textureCache)
        {
            var textureInfo = texKvp.Value;

            if (textureInfo != null)
                DestroyTexture(textureInfo);
        }
    }

    private void DestroyTexture(TextureInfo textureInfo)
    {
        controller.DestroyImGuiBinding(textureInfo.textureView);
        textureInfo.textureView.Dispose();
        textureInfo.texture.Dispose();
    }
}