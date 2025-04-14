using System.Collections.Concurrent;
using Asahi.Database;
using Newtonsoft.Json;
using April.Config;

namespace Asahi.Modules.April;

[Inject(ServiceLifetime.Singleton)]
public class AprilConfigService(IDbService dbService)
{
    private ConcurrentDictionary<ulong, ConfigFile> cachedConfigs = [];

    // should this be a ValueTask?
    public async Task<ConfigFile?> GetConfig(ulong guildId, BotDbContext context)
    {
        if (cachedConfigs.TryGetValue(guildId, out var file))
        {
            return file;
        }

        var contents = await context.GetGuildConfig(guildId);

        if (contents.AprilConfig == null)
            return null;

        var config = JsonConvert.DeserializeObject<ConfigFile>(contents.AprilConfig);
        if (config == null) return null;

        cachedConfigs.TryAdd(guildId, config);

        return config;
    }

    public async Task<string?> SetConfig(ulong guildId, string contents)
    {
        ConfigFile file;

        try
        {
            file = JsonConvert.DeserializeObject<ConfigFile>(contents) ?? throw new NullReferenceException("contents was null!");
        }
        catch (Exception ex)
        {
            return $"Failed to deserialize config!\nException below:\n```\n{ex}\n```";
        }

        await using var context = dbService.GetDbContext();

        var config = await context.GetGuildConfig(guildId);

        config.AprilConfig = contents;

        await context.SaveChangesAsync();

        cachedConfigs[guildId] = file;

        return null;
    }
}
