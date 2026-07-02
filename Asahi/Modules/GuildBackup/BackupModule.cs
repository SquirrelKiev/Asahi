using System.Collections.Concurrent;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Text;
using Discord.Interactions;
using Newtonsoft.Json;

namespace Asahi.Modules.GuildBackup;

[Group("backup", "Commands relating to backing up guild related things.")]
[DefaultMemberPermissions(GuildPermission.ManageGuild)]
[CommandContextType(InteractionContextType.Guild)]
[IntegrationType(ApplicationIntegrationType.GuildInstall)]
public class BackupModule(BotConfig config, IHttpClientFactory httpClientFactory) : BotModule
{
    [RequireBotPermission(GuildPermission.BanMembers)]
    [SlashCommand("save", "Backs up the guild as a zip.")]
    public async Task BackupSlash()
    {
        await DeferAsync();

        using var client = httpClientFactory.CreateClient();
        
        // bans
        var banJsonList = new List<string>();

        var previousUserId = 0ul;
        while (true)
        {
            var banJson = await GetJsonTextFromDiscord(client, $"https://discord.com/api/v10/guilds/{Context.Guild.Id}/bans?limit=1000&after={previousUserId}");

            var obj = JsonConvert.DeserializeObject<List<PartialBanObject>>(banJson);
            if (obj == null || obj.Count == 0)
            {
                break;
            }

            banJsonList.Add(banJson);

            var userId = obj.Last().User.Id;
            previousUserId = userId;
        }

        var guildJson = await GetJsonTextFromDiscord(client, $"{DiscordConfig.APIUrl}guilds/{Context.Guild.Id}");
        var channelsJson = await GetJsonTextFromDiscord(client, $"{DiscordConfig.APIUrl}guilds/{Context.Guild.Id}/channels");
        var activeThreadsJson = await GetJsonTextFromDiscord(client, $"{DiscordConfig.APIUrl}guilds/{Context.Guild.Id}/threads/active");
        var onboardingJson = await GetJsonTextFromDiscord(client, $"{DiscordConfig.APIUrl}guilds/{Context.Guild.Id}/onboarding");
        
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = 12 // arbitrary number
        };
        ConcurrentDictionary<GuildEmote, MemoryStream> emotes = [];
        await Parallel.ForEachAsync(Context.Guild.Emotes, parallelOptions,  async (emote, token) =>
        {
            await using var emoteStream = await GetStreamFromDiscord(client, emote.Url, token);

            var newStream = new MemoryStream();
            await emoteStream.CopyToAsync(newStream, token);
            newStream.Position = 0;

            emotes[emote] = newStream;
        });
        
        ConcurrentDictionary<ICustomSticker, MemoryStream> stickers = [];
        await Parallel.ForEachAsync(Context.Guild.Stickers, parallelOptions, async (sticker, token) =>
        {
            await using var stickerStream = await GetStreamFromDiscord(client, sticker.GetStickerUrl(), token);

            var newStream = new MemoryStream();
            await stickerStream.CopyToAsync(newStream, token);
            newStream.Position = 0;

            stickers[sticker] = newStream;
        });
        
        ConcurrentDictionary<IRole, MemoryStream> roleIcons = [];
        await Parallel.ForEachAsync(Context.Guild.Roles, parallelOptions, async (role, token) =>
        {
            var iconUrl = role.GetIconUrl();
            if (iconUrl == null)
                return;

            await using var iconStream = await GetStreamFromDiscord(client, iconUrl, token);

            var newStream = new MemoryStream();
            await iconStream.CopyToAsync(newStream, token);
            newStream.Position = 0;

            roleIcons[role] = newStream;
        });
        
        using var stream = new MemoryStream();

        await using (var zip = await ZipArchive.CreateAsync(stream, ZipArchiveMode.Create, true, Encoding.UTF8))
        {
            await WriteTextToZip(zip, guildJson, "guild.json");
            await WriteTextToZip(zip, channelsJson, "channels.json");
            await WriteTextToZip(zip, activeThreadsJson, "active-threads.json");
            for (var i = 0; i < banJsonList.Count; i++)
            {
                var json = banJsonList[i];
                await WriteTextToZip(zip, json, $"bans-pg{i}.json");
            }

            if(Context.Guild.IconId != null)
            {
                await using var guildIconStream = await GetStreamFromDiscord(client, Context.Guild.IconUrl);
                await WriteStreamToZip(zip, guildIconStream, $"guild-icon.{FormatToExtension(ImageFormat.Auto, Context.Guild.IconId)}");
            }
            
            if(Context.Guild.BannerId != null)
            {
                await using var guildBannerStream = await GetStreamFromDiscord(client, CDN.GetGuildBannerUrl(Context.Guild.Id, Context.Guild.BannerId, ImageFormat.Auto, 2048));
                await WriteStreamToZip(zip, guildBannerStream, $"guild-banner.{FormatToExtension(ImageFormat.Auto, Context.Guild.BannerId)}");
            }

            if (Context.Guild.DiscoverySplashId != null)
            {
                await using var guildDiscoverySplashStream = await GetStreamFromDiscord(client, $"{DiscordConfig.CDNUrl}/discovery-splashes/{Context.Guild.Id}/{Context.Guild.DiscoverySplashId}.png?size=2048");
                await WriteStreamToZip(zip, guildDiscoverySplashStream, $"guild-discovery-splash.{FormatToExtension(ImageFormat.Auto, Context.Guild.DiscoverySplashId)}");
            }
            
            if (Context.Guild.SplashUrl != null)
            {
                await using var guildSplashStream = await GetStreamFromDiscord(client, Context.Guild.SplashUrl);
                await WriteStreamToZip(zip, guildSplashStream, $"guild-splash.{FormatToExtension(ImageFormat.Auto, Context.Guild.SplashId)}");
            }

            foreach (var stickerKvp in stickers)
            {
                await WriteStreamToZip(zip, stickerKvp.Value, $"stickers/{stickerKvp.Key.Id}.{FormatToExtension(stickerKvp.Key.Format)}");
                stickerKvp.Value.Dispose();
            }
            
            foreach (var emoteKvp in emotes)
            {
                await WriteStreamToZip(zip, emoteKvp.Value, $"emotes/{emoteKvp.Key.Id}.{(emoteKvp.Key.Animated ? "gif" : "png")}");
                emoteKvp.Value.Dispose();
            }

            foreach (var roleKvp in roleIcons)
            {
                await WriteStreamToZip(zip, roleKvp.Value, $"role-icons/{roleKvp.Key.Id}.png");
                roleKvp.Value.Dispose();
            }
        }

        await FollowupWithFileAsync(stream, $"{Context.Guild.Id}.zip");
    }

    private static async Task WriteTextToZip(ZipArchive zip, string text, string path)
    {
        var entry = zip.CreateEntry(path, CompressionLevel.SmallestSize);
        await using var entryStream = await entry.OpenAsync();
        await entryStream.WriteAsync(Encoding.UTF8.GetBytes(text));
    }

    private static async Task WriteStreamToZip(ZipArchive zip, Stream stream, string path)
    {
        var entry = zip.CreateEntry(path, CompressionLevel.NoCompression);
        await using var entryStream = await entry.OpenAsync();
        await stream.CopyToAsync(entryStream);
    }

    private async Task<string> GetJsonTextFromDiscord(HttpClient client, string url)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, url);

        req.Headers.Authorization = new AuthenticationHeaderValue("Bot", config.BotToken);

        using var res = await client.SendAsync(req);

        res.EnsureSuccessStatusCode();
        var json = await res.Content.ReadAsStringAsync();

        return json;
    }

    private async Task<Stream> GetStreamFromDiscord(HttpClient client, string url, CancellationToken token = default)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, url);

        req.Headers.Authorization = new AuthenticationHeaderValue("Bot", config.BotToken);

        // disposing would dispose the stream (seems to be all this does anyway)
        var res = await client.SendAsync(req, token);

        res.EnsureSuccessStatusCode();
        var stream = await res.Content.ReadAsStreamAsync(token);

        return stream;
    }
    
    // copied from discord.net's CDN class
    private static string FormatToExtension(ImageFormat format, string imageId)
    {
        if (format == ImageFormat.Auto)
            format = imageId.StartsWith("a_") ? ImageFormat.Gif : ImageFormat.Png;
        return format switch
        {
            ImageFormat.Gif => "gif",
            ImageFormat.Jpeg => "jpeg",
            ImageFormat.Png => "png",
            ImageFormat.WebP => "webp",
            _ => throw new ArgumentException(nameof(format)),
        };
    }
    
    // copied from discord.net's CDN class
    private static string FormatToExtension(StickerFormatType format)
    {
        return format switch
        {
            StickerFormatType.None or StickerFormatType.Png or StickerFormatType.Apng => "png", // In the case of the Sticker endpoint, the sticker will be available as PNG if its format_type is PNG or APNG, and as Lottie if its format_type is LOTTIE.
            StickerFormatType.Lottie => "lottie",
            StickerFormatType.Gif => "gif",
            _ => throw new ArgumentException(nameof(format)),
        };
    }

    private record PartialBanObject
    {
        public PartialUserObject User { get; init; }
        public string Reason { get; init; }
    }

    private record PartialUserObject
    {
        public ulong Id { get; init; }
    }
}
