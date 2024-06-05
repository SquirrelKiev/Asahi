using System.Globalization;
using Asahi.Database;
using Asahi.Database.Models;
using Asahi.Modules.Highlights;
using Discord.Interactions;
using Discord.WebSocket;
using Humanizer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NodaTime;
using NodaTime.Text;
using static Asahi.Modules.BirthdayRoles.UserFacingBirthdayConfigModule;

namespace Asahi.Modules.BirthdayRoles;

// dumb I have to do this
public class UserFacingBirthdayConfigModule(DbService dbService,
    BirthdayTimerService bts,
    IClock clock,
    ILogger<BirthdayConfigModule> logger) : BotModule
{
    public enum Months
    {
        January = 1,
        February = 2,
        March = 3,
        April = 4,
        May = 5,
        June = 6,
        July = 7,
        August = 8,
        September = 9,
        October = 10,
        November = 11,
        December = 12
    }


    // User facing birthday-setting command
    [SlashCommand("birthday", "Sets your birthday.", true)]
    public async Task UserFacingSetBirthdaySlash([Summary(description: "The day of the month your birthday is on.")] int day,
        [Summary(description: "The month your birthday is in.")] Months month,
        [Summary(description: "Your timezone.")][Autocomplete(typeof(TimeZoneAutocomplete))] string timeZone,
        [Summary(description: "The name/ID of the config.")] string? name = null)
    {
        await CommonBirthdayConfig(name, Context.Guild.Id, async x =>
        {
            var user = await Context.Guild.GetUserAsync(Context.User.Id);

            if (user.RoleIds.Any(y => x.Config.DeniedRoles.Contains(y)) ||
                (x.Config.AllowedRoles.Any() && !user.RoleIds.Any(y => x.Config.AllowedRoles.Contains(y))))
            {
                return new ConfigChangeResult(false,
                    x.Config.DeniedForReasonPermissionsText.Replace(BirthdayConfig.UsernamePlaceholder,
                        user.DisplayName));
            }

            AnnualDate date;
            try
            {
                date = new AnnualDate((int)month, day);
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
                return new ConfigChangeResult(false,
                    x.Config.DeniedForReasonEditWindowText.Replace(BirthdayConfig.UsernamePlaceholder, user.DisplayName));
            }

            var localDate = date.InYear(now.Year);
            if (localDate.CompareTo(now.LocalDateTime.Date) < 0)
            {
                localDate = date.InYear(now.Year + 1);
            }

            var dateTime = localDate.AtStartOfDayInZone(tz).ToInstant();

            var eb = new EmbedBuilder()
                .WithTitle(x.Config.EmbedTitleText.Replace(BirthdayConfig.UsernamePlaceholder, user.DisplayName))
                .WithDescription(
                    x.Config.EmbedDescriptionText.Replace(BirthdayConfig.UsernamePlaceholder, user.DisplayName))
                .WithFooter(x.Config.EmbedFooterText.Replace(BirthdayConfig.UsernamePlaceholder, user.DisplayName))
                .WithOptionalColor(await QuotingHelpers.GetQuoteEmbedColor(x.Config.EmbedColorSource,
                    new Color(x.Config.FallbackEmbedColor), user,
                    (DiscordSocketClient)Context.Client))
                .WithThumbnailUrl(user.GetDisplayAvatarUrl())
                .AddField(x.Config.DisplayName.Titleize(), $"<t:{dateTime.ToUnixTimeSeconds()}:f>");

            return new ConfigChangeResult(true, string.Empty, [eb.Build()], true);
        });
    }

    private Task<bool> CommonConfig(Func<BotDbContext, EmbedBuilder, Task<ConfigChangeResult>> updateAction)
    {
        return ConfigUtilities.CommonConfig(Context, dbService, updateAction);
    }

    private Task<bool> CommonBirthdayConfig(string? name, ulong guildId, Func<BirthdayConfigModule.ConfigContext, Task<ConfigChangeResult>> updateAction,
        Func<IQueryable<BirthdayConfig>, IQueryable<BirthdayConfig>>? modifyQuery = null)
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

            modifyQuery ??= x => x;

            var query = modifyQuery(context.BirthdayConfigs);

            config ??= await query.FirstOrDefaultAsync(x => x.GuildId == Context.Guild.Id && x.Name == name);

            if (config == null)
            {
                return new ConfigChangeResult(false, "Could not find config.");
            }

            return await updateAction(new BirthdayConfigModule.ConfigContext(context, config, eb));
        });
    }
}

[Group("birthday-config", "Commands relating to birthday management.")]
[DefaultMemberPermissions(GuildPermission.ManageGuild)]
public class BirthdayConfigModule(
    DbService dbService,
    BirthdayTimerService bts,
    IClock clock,
    ILogger<BirthdayConfigModule> logger) : BotModule
{
    [SlashCommand("create", "Creates a birthday config.")]
    public async Task CreateBirthdayConfigSlash([Summary(description: "The name/ID of the config.")] string name,
        [Summary(description: "The role to assign if its a user's birthday.")] IRole role)
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

    [SlashCommand("get", "Gets the config.")]
    public async Task GetConfigSlash([Summary(description: "The name/ID of the config.")] string? name = null)
    {
        await CommonBirthdayConfig(name, Context.Guild.Id, async x => 
            new ConfigChangeResult(true, 
                $"```json\n{JsonConvert.SerializeObject(x.Config, Formatting.Indented)}\n```", [], shouldSave: false));
    }

    [SlashCommand("set-user", "Sets a user's birthday.")]
    public async Task SetUserBirthdaySlash([Summary(description: "The user to change the birthday of.")] IGuildUser user, 
        [Summary(description: "The day of the month their birthday is on.")] int day,
        [Summary(description: "The month their birthday is in.")] Months month,
        [Summary(description: "Their timezone.")][Autocomplete(typeof(TimeZoneAutocomplete))] string timeZone,
        [Summary(description: "The name/ID of the config.")] string? name = null)
    {
        await CommonBirthdayConfig(name, Context.Guild.Id, async x =>
        {
            AnnualDate date;
            try
            {
                date = new AnnualDate((int)month, day);
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

    [SlashCommand("rm-user", "Removes a user's birthday entry.")]
    public async Task RemoveBirthdayUserSlash([Summary(description: "The user to remove the entry of.")] IGuildUser user,
        [Summary(description: "The name/ID of the config.")] string? name = null)
    {
        await CommonBirthdayConfig(name, Context.Guild.Id, async x =>
        {
            var birthday = await x.Context.GetBirthday(x.Config, user.Id);

            if (birthday == null)
                return new ConfigChangeResult(false, "No entry found for that user anyway.");

            x.Context.Remove(birthday);

            return new ConfigChangeResult(true, $"Removed birthday for user <@{user.Id}>.");
        });
    }

    [SlashCommand("get-user", "Gets a user's set birthday.")]
    public async Task GetBirthdayUserSlash([Summary(description: "The user to get the entry of.")] IGuildUser user,
        [Summary(description: "The name/ID of the config.")]
        string? name = null)
    {
        await CommonBirthdayConfig(name, Context.Guild.Id, async x =>
        {
            var birthday = await x.Context.GetBirthday(x.Config, user.Id);

            if (birthday == null)
                return new ConfigChangeResult(false, "No entry for that user found.");

            return new ConfigChangeResult(true,
                $"```json\n{JsonConvert.SerializeObject(birthday, Formatting.Indented)}\n```", [], shouldSave: false);
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

    [SlashCommand("add-filtered-role", "Dictates whether a user can add/change their birthday. Does not affect existing entries.")]
    public async Task AddRoleToListSlash([Summary(description: "The role to add.")] IRole role,
        [Summary(description: "The list to add the role to.")] AllowBlockList list, 
        [Summary(description: "The name/ID of the config.")] string? name = null)
    {
        await CommonBirthdayConfig(name, Context.Guild.Id, async x =>
        {
            List<ulong> roles; 

            switch (list)
            {
                case AllowBlockList.BlockList:
                    roles = x.Config.DeniedRoles;
                    break;
                case AllowBlockList.AllowList:
                    roles = x.Config.AllowedRoles;
                    break;
                default:
                    throw new NotSupportedException();
            }

            if (roles.Contains(role.Id))
                return new ConfigChangeResult(false, $"Role already in {list.Humanize()}.");

            roles.Add(role.Id);

            return new ConfigChangeResult(true, $"Added <@&{role.Id}> to {list.Humanize()}.");
        });
    }

    [SlashCommand("rm-filtered-role", "Removes a role from the allowlist or blocklist.")]
    public async Task RemoveRoleFromListSlash([Summary(description: "The role to remove.")] IRole role,
        [Summary(description: "The list to remove the role from.")] AllowBlockList list, 
        [Summary(description: "The name/ID of the config.")] string? name = null)
    {
        await CommonBirthdayConfig(name, Context.Guild.Id, async x =>
        {
            List<ulong> roles;

            switch (list)
            {
                case AllowBlockList.BlockList:
                    roles = x.Config.DeniedRoles;
                    break;
                case AllowBlockList.AllowList:
                    roles = x.Config.AllowedRoles;
                    break;
                default:
                    throw new NotSupportedException();
            }

            if (!roles.Contains(role.Id))
                return new ConfigChangeResult(false, $"Role not found in {list.Humanize()}.");

            roles.Remove(role.Id);

            return new ConfigChangeResult(true, $"Removed <@&{role.Id}> from {list.Humanize()}.");
        });
    }


#if DEBUG
    [SlashCommand("debug-test-date", "[DEBUG] Runs a birthday check for the specified date (DD/MM/YYYY) at the specified time (UTC).")]
    public async Task DebugRunForDateSlash(string date, string time)
    {
        await DeferAsync();

        var pattern = LocalDatePattern.CreateWithInvariantCulture("dd/MM/yyyy");
        var pattern2 = LocalTimePattern.GeneralIso;

        var parseResult = pattern.Parse(date);
        var parseResult2 = pattern2.Parse(time);

        if (!parseResult.Success)
        {
            throw parseResult.Exception;
        }
        if (!parseResult2.Success)
        {
            throw parseResult2.Exception;
        }

        var currentMoment = parseResult.Value.At(parseResult2.Value).InUtc();

        await bts.CheckForBirthdays(currentMoment.ToInstant());

        await FollowupAsync("Done, check logs for more info.");
    }
#endif

    private Task<bool> CommonConfig(Func<BotDbContext, EmbedBuilder, Task<ConfigChangeResult>> updateAction)
    {
        return ConfigUtilities.CommonConfig(Context, dbService, updateAction);
    }

    public record ConfigContext(BotDbContext Context, BirthdayConfig Config, EmbedBuilder EmbedBuilder);

    private Task<bool> CommonBirthdayConfig(string? name, ulong guildId, Func<ConfigContext, Task<ConfigChangeResult>> updateAction, 
        Func<IQueryable<BirthdayConfig>, IQueryable<BirthdayConfig>>? modifyQuery = null)
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

            modifyQuery ??= x => x;

            var query = modifyQuery(context.BirthdayConfigs);

            config ??= await query.FirstOrDefaultAsync(x => x.GuildId == Context.Guild.Id && x.Name == name);

            if (config == null)
            {
                return new ConfigChangeResult(false, "Could not find config.");
            }

            return await updateAction(new ConfigContext(context, config, eb));
        });
    }
}