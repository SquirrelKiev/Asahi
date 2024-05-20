using System.Globalization;
using Asahi.Database;
using Asahi.Database.Models;
using Discord.Interactions;
using Discord.WebSocket;
using Humanizer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NodaTime;
using NodaTime.Text;

namespace Asahi.Modules.BirthdayRoles;

[Group("birthday-config", "Commands relating to birthday management.")]
public class BirthdayConfigModule(DbService dbService, BirthdayTimerService bts, IClock clock, ILogger<BirthdayConfigModule> logger) : BotModule
{
    [SlashCommand("create", "Creates a birthday config.")]
    public async Task CreateBirthdayConfigSlash([Summary(description: "The name/ID of the config.")] string name, IRole role)
    {
        await CommonConfig(async (context, eb) =>
        {
            name = name.ToLowerInvariant();

            if (!ConfigUtilities.IsValidId().IsMatch(name))
            {
                return new ConfigChangeResult(false, $"`{name}` is not valid.");
            }

            if (await context.BirthdayConfigs.AnyAsync(x => x.GuildId == Context.Guild.Id && x.Name == name))
            {
                return new ConfigChangeResult(false, "A config with name already exists.");
            }

            context.Add(new BirthdayConfig()
            {
                Name = name,
                BirthdayRole = role.Id,
                GuildId = Context.Guild.Id
            });

            return new ConfigChangeResult(true, "Birthday config added!");
        });
    }

    [SlashCommand("remove-user", "Removes a user's birthday entry.")]
    public async Task RemoveBirthdayUserSlash(IGuildUser user, [Summary(description: "The name/ID of the config.")] string? name = null)
    {
        await CommonBirthdayConfig(name, Context.Guild.Id, async x =>
        {

        };
    }

    [SlashCommand("birthday", "Sets your birthday.", true)]
    public async Task UserFacingSetBirthdaySlash(int day, int month, string timeZone, [Summary(description: "The name/ID of the config.")] string? name = null)
    {
        await CommonBirthdayConfig(name, Context.Guild.Id, async x =>
        {
            var user = await Context.Guild.GetUserAsync(Context.User.Id);

            AnnualDate date;
            try
            {
                date = new AnnualDate(month, day);
            }
            catch (ArgumentOutOfRangeException ex)
            {
                return new ConfigChangeResult(false, $"Not a valid date. {ex.Message}");
            }

            var tz = DateTimeZoneProviders.Tzdb.GetZoneOrNull(timeZone);

            if (tz == null)
                return new ConfigChangeResult(false, $"`{timeZone}` is not a valid/supported timezone.");

            var now = clock.GetCurrentInstant().InUtc();

            var existingBirthday = await x.Context.SetBirthday(x.Config, user.Id, date, tz, now.LocalDateTime);

            if (x.Config.EditWindowSeconds != 0 &&
                existingBirthday.TimeCreatedUtc.PlusSeconds(x.Config.EditWindowSeconds) < now.LocalDateTime)
            {
                return new ConfigChangeResult(false, x.Config.DeniedText.Replace(BirthdayConfig.UsernamePlaceholder, user.DisplayName));
            }

            var localDate = date.InYear(now.Year);
            if (localDate.CompareTo(now.LocalDateTime.Date) < 0)
            {
                localDate = date.InYear(now.Year + 1);
            }

            var dateTime = localDate.AtStartOfDayInZone(tz).ToInstant();

            var eb = new EmbedBuilder()
                .WithTitle(x.Config.EmbedTitleText.Replace(BirthdayConfig.UsernamePlaceholder, user.DisplayName))
                .WithDescription(x.Config.EmbedDescriptionText.Replace(BirthdayConfig.UsernamePlaceholder, user.DisplayName))
                .WithFooter(x.Config.EmbedFooterText.Replace(BirthdayConfig.UsernamePlaceholder, user.DisplayName))
                .WithOptionalColor(await QuotingHelpers.GetQuoteEmbedColor(x.Config.EmbedColorSource, new Color(x.Config.FallbackEmbedColor), user,
                    (DiscordSocketClient)Context.Client))
                .WithThumbnailUrl(user.GetDisplayAvatarUrl())
                .AddField(x.Config.DisplayName.Titleize(), $"<t:{dateTime.ToUnixTimeSeconds()}:f>");

            return new ConfigChangeResult(true, string.Empty, [eb.Build()], true);
        });
    }

    [SlashCommand("set-user", "Sets a user's birthday.")]
    public async Task SetUserBirthdaySlash(IGuildUser user, int day, int month, string timeZone, [Summary(description: "The name/ID of the config.")] string? name = null)
    {
        await CommonBirthdayConfig(name, Context.Guild.Id, async x =>
        {
            AnnualDate date;
            try
            {
                date = new AnnualDate(month, day);
            }
            catch (ArgumentOutOfRangeException ex)
            {
                return new ConfigChangeResult(false, $"Not a valid date. {ex.Message}");
            }

            var tz = DateTimeZoneProviders.Tzdb.GetZoneOrNull(timeZone);

            if (tz == null)
                return new ConfigChangeResult(false, $"`{timeZone}` is not a valid/supported timezone.");

            await x.Context.SetBirthday(x.Config, user.Id, date, tz, clock.GetCurrentInstant().InUtc().LocalDateTime);

            return new ConfigChangeResult(true, 
                $"Set <@{user.Id}>'s birthday to the {date.Day.Ordinalize()} of {date.ToString("MMMM", CultureInfo.InvariantCulture)}. ({tz.Id})");
        });
    }

    [SlashCommand("set-default", "Sets the default config for the Guild.")]
    public async Task SetDefaultConfigSlash([Summary(description: "The name/ID of the config.")] string name)
    {
        await CommonBirthdayConfig(name, Context.Guild.Id, async x =>
        {
            var guildConfig = await x.Context.GetGuildConfig(Context.Guild.Id);

            guildConfig.DefaultBirthdayConfig = x.Config;

            return new ConfigChangeResult(true, $"Set default config to `{x.Config.Name}`.");
        });
    }

    [SlashCommand("debug-test-date", "[DEBUG] Runs a birthday check for the specified date (DD/MM/YYYY) at 12:00 UTC.")]
    public async Task DebugRunForDateSlash(string date)
    {
        await DeferAsync();

        var pattern = LocalDatePattern.CreateWithInvariantCulture("dd/MM/yyyy");

        var parseResult = pattern.Parse(date);

        if (!parseResult.Success)
        {
            throw parseResult.Exception;
        }

        var zone = DateTimeZone.Utc;
        var time = parseResult.Value.AtStartOfDayInZone(zone).PlusHours(12);

        await bts.CheckForBirthdays(time.ToInstant());

        await FollowupAsync("Done, check logs for more info.");
    }
    
    private Task<bool> CommonConfig(Func<BotDbContext, EmbedBuilder, Task<ConfigChangeResult>> updateAction)
    {
        return ConfigUtilities.CommonConfig(Context, dbService, updateAction);
    }

    public record ConfigContext(BotDbContext Context, BirthdayConfig Config, EmbedBuilder EmbedBuilder);

    private Task<bool> CommonBirthdayConfig(string? name, ulong guildId, Func<ConfigContext, Task<ConfigChangeResult>> updateAction)
    {
        return CommonConfig(async (context, eb) =>
        {
            BirthdayConfig? config = null;

            if (name == null)
            {
                config = (await context.GetGuildConfig(guildId, x => x.Include(y => y.DefaultBirthdayConfig))).DefaultBirthdayConfig;

                if (config == null)
                {
                    return new ConfigChangeResult(false, "No default config set. Please manually specify the config name.");
                }

                name = config.Name;
            }

            name = name.ToLowerInvariant();

            if (!ConfigUtilities.IsValidId().IsMatch(name))
            {
                return new ConfigChangeResult(false, $"`{name}` is not valid.");
            }

            eb.WithAuthor(name);

            config ??= await context.BirthdayConfigs.FirstOrDefaultAsync(x => x.GuildId == Context.Guild.Id && x.Name == name);

            if (config == null)
            {
                return new ConfigChangeResult(false, "Could not find config.");
            }

            return await updateAction(new ConfigContext(context, config, eb));
        });
    }
}