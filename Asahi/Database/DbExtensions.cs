using Asahi.Database.Models;
using Asahi.Database.Models.Rss;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace Asahi.Database;

public static class DbExtensions
{
    /// <param name="modifyFunc">Should only be used with Include(), if this filters anything then bad stuff will happen.</param>
    public static async Task<GuildConfig> GetGuildConfig(this BotDbContext context,
        ulong guildId,
        Func<IQueryable<GuildConfig>, IQueryable<GuildConfig>>? modifyFunc = null)
    {
        IQueryable<GuildConfig> configs = context.GuildConfigs;
        if (modifyFunc != null)
            configs = modifyFunc(context.GuildConfigs);

        var guildConfig = await configs.FirstOrDefaultAsync(x => x.GuildId == guildId);

        if (guildConfig != null) return guildConfig;

        guildConfig = new GuildConfig()
        {
            GuildId = guildId
        };

        context.Add(guildConfig);

        return guildConfig;
    }

    public static async Task<CustomCommand?> GetCustomCommand(this BotDbContext context, ulong guildId, string name)
    {
        var command = await context.CustomCommands.FirstOrDefaultAsync(x => x.GuildId == guildId && x.Name == name);

        return command;
    }

    public static async Task<BotWideConfig> GetBotWideConfig(this BotDbContext context, ulong botId)
    {
        var cfg = await context.BotWideConfig.Include(x => x.TrustedIds).FirstOrDefaultAsync(x => x.BotId == botId);
        if (cfg != null) return cfg;

        cfg = new BotWideConfig()
        {
            BotId = botId
        };

        context.Add(cfg);

        return cfg;
    }

    public static Task<BirthdayEntry?> GetBirthday(this BotDbContext context, BirthdayConfig config, ulong userId)
    {
        return context.Birthdays.FirstOrDefaultAsync(x => x.BirthdayConfig == config && x.UserId == userId);
    }

    public static async Task<BirthdayEntry> SetBirthday(this BotDbContext context, BirthdayConfig config, ulong userId, AnnualDate date, DateTimeZone tz, LocalDateTime now)
    {
        var birthday = await context.GetBirthday(config, userId);

        if (birthday == null)
        {
            birthday = new BirthdayEntry()
            {
                BirthdayConfig = config,
                BirthDayDate = date,
                TimeZone = tz.Id,
                UserId = userId,
                TimeCreatedUtc = now
            };

            context.Add(birthday);
        }
        else
        {
            birthday.BirthDayDate = date;
            birthday.TimeZone = tz.Id;
        }

        return birthday;
    }

    public static Task<RssFeedListener?> GetFeed(this BotDbContext context, uint id, ulong guildId)
    {
        return context.RssFeedListeners.FirstOrDefaultAsync(x => x.Id == id && x.GuildId == guildId);
    }
}