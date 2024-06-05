using System.Globalization;
using Asahi.Database;
using Asahi.Database.Models;
using Asahi.Modules.Highlights;
using Discord.Interactions;
using Discord.WebSocket;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Humanizer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NodaTime;
using NodaTime.Text;
using NodaTime.TimeZones;

namespace Asahi.Modules.BirthdayRoles;

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

// dumb I have to do this
public class UserFacingBirthdayConfigModule(DbService dbService,
    BirthdayTimerService bts,
    IClock clock,
    ILogger<BirthdayConfigModule> logger) : BotModule
{
    // User facing birthday-setting command
    [SlashCommand("birthday", "Sets your birthday.", true)]
    public async Task UserFacingSetBirthdaySlash([Summary(description: "The day of the month your birthday is on.")] int day,
        [Summary(description: "The month your birthday is in.")] Months month,
        [Summary(description: "Your timezone.")][Autocomplete(typeof(TimeZoneAutocomplete))] string timeZone,
        [Summary(description: BirthdayConfigModule.NameDescription), Autocomplete(typeof(BirthdayConfigNameAutocomplete))] string? name = null)
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

            if(!TzdbDateTimeZoneSource.Default.CanonicalIdMap.TryGetValue(timeZone, out string? canonicalTimeZone))
            {
                return new ConfigChangeResult(false, $"`{timeZone}` is not a valid/supported timezone.");
            }

            var tz = TzdbDateTimeZoneSource.Default.ForId(canonicalTimeZone);

            var now = clock.GetCurrentInstant().InUtc();

            var existingBirthday = await x.Context.SetBirthday(x.Config, user.Id, date, tz, now.LocalDateTime);

            if (x.Config.EditWindowSeconds != 0 &&
                existingBirthday.TimeCreatedUtc.PlusSeconds(x.Config.EditWindowSeconds) < now.LocalDateTime)
            {
                return new ConfigChangeResult(false,
                    x.Config.DeniedForReasonEditWindowText.Replace(BirthdayConfig.UsernamePlaceholder, user.DisplayName));
            }

            var eb = new EmbedBuilder()
                .WithTitle(x.Config.EmbedTitleText.Replace(BirthdayConfig.UsernamePlaceholder, user.DisplayName))
                .WithDescription(
                    x.Config.EmbedDescriptionText.Replace(BirthdayConfig.UsernamePlaceholder, user.DisplayName))
                .WithFooter(x.Config.EmbedFooterText.Replace(BirthdayConfig.UsernamePlaceholder, user.DisplayName))
                .WithOptionalColor(await QuotingHelpers.GetQuoteEmbedColor(x.Config.EmbedColorSource,
                    new Color(x.Config.FallbackEmbedColor), user,
                    (DiscordSocketClient)Context.Client))
                .WithThumbnailUrl(user.GetDisplayAvatarUrl())
                .AddField(x.Config.Name.Titleize(), $"{date.Day.Ordinalize()} {date.ToString("MMMM", CultureInfo.InvariantCulture)}");

            await Context.Channel.SendMessageAsync(embed: eb.Build());

            return new ConfigChangeResult(true, "Set successfully!");
        }, ephemeral: true);
    }

    private Task<bool> CommonConfig(Func<BotDbContext, EmbedBuilder, Task<ConfigChangeResult>> updateAction, bool ephemeral = false)
    {
        return ConfigUtilities.CommonConfig(Context, dbService, updateAction, ephemeral);
    }

    private Task<bool> CommonBirthdayConfig(string? name, ulong guildId, Func<BirthdayConfigModule.ConfigContext, Task<ConfigChangeResult>> updateAction,
        Func<IQueryable<BirthdayConfig>, IQueryable<BirthdayConfig>>? modifyQuery = null, bool ephemeral = false)
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
        }, ephemeral);
    }
}

[Group("birthday-config", "Commands relating to birthday management.")]
[DefaultMemberPermissions(GuildPermission.ManageGuild)]
public class BirthdayConfigModule(
    DbService dbService,
    BirthdayTimerService bts,
    IClock clock,
    InteractiveService interactive,
    ILogger<BirthdayConfigModule> logger) : BotModule
{
    public const string NameDescription = "The name/ID of the config.";

    [SlashCommand("create", "Creates a birthday config.")]
    public async Task CreateBirthdayConfigSlash([Summary(description: NameDescription)] string name,
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

            if ((await Context.Guild.GetUsersAsync()).Any(x => x.RoleIds.Contains(role.Id)))
                return new ConfigChangeResult(false, "This role must be empty before you can add it. " +
                                                     "Noting again that this role is the role to assign when its the user's birthday.");

            context.Add(new BirthdayConfig()
            {
                Name = name,
                BirthdayRole = role.Id,
                GuildId = Context.Guild.Id
            });

            return new ConfigChangeResult(true, "Birthday config added!");
        });
    }

    [SlashCommand("remove", "Removes the birthday config.")]
    public async Task RemoveConfigSlash([Summary(description: NameDescription), Autocomplete(typeof(BirthdayConfigNameAutocomplete))] string name)
    {
        await CommonBirthdayConfig(name, Context.Guild.Id, async context =>
        {
            var guildConfig = await context.Context
                .GetGuildConfig(Context.Guild.Id, x => x.Include(y => y.DefaultBirthdayConfig));

            if (guildConfig.DefaultBirthdayConfig?.Name == context.Config.Name)
                return new ConfigChangeResult(false, "Please clear the default config first.");

            context.Context.Remove(context.Config);

            return new ConfigChangeResult(true, "Removed config.");
        });
    }

    [SlashCommand("set-role", "Set the role to assign users when its their birthday.")]
    public async Task SetRoleSlash([Summary(description: "The role to assign if its a user's birthday.")] IRole role, [Summary(description: BirthdayConfigModule.NameDescription), Autocomplete(typeof(BirthdayConfigNameAutocomplete))] string? name = null)
    {
        await CommonBirthdayConfig(name, Context.Guild.Id, async context =>
        {
            if ((await Context.Guild.GetUsersAsync()).Any(x => x.RoleIds.Contains(role.Id)))
                return new ConfigChangeResult(false, "This role must be empty before you can add it. " +
                                                    "Noting again that this role is the role to assign when its the user's birthday.");

            context.Config.BirthdayRole = role.Id;

            return new ConfigChangeResult(true, $"Set the birthday role to <@&{role.Id}>. Noting that users wont be removed from the old role," +
                                                $"so make sure to clean that up.");
        });
    }

    [SlashCommand("get", "Gets the config.")]
    public async Task GetConfigSlash([Summary(description: BirthdayConfigModule.NameDescription), Autocomplete(typeof(BirthdayConfigNameAutocomplete))] string? name = null)
    {
        await CommonBirthdayConfig(name, Context.Guild.Id, x =>
            Task.FromResult(new ConfigChangeResult(true,
                $"```json\n{JsonConvert.SerializeObject(x.Config, Formatting.Indented)}\n```", [], shouldSave: false)));
    }

    [SlashCommand("set-user", "Sets a user's birthday.")]
    public async Task SetUserBirthdaySlash([Summary(description: "The user to change the birthday of.")] IGuildUser user,
        [Summary(description: "The day of the month their birthday is on.")] int day,
        [Summary(description: "The month their birthday is in.")] Months month,
        [Summary(description: "Their timezone.")][Autocomplete(typeof(TimeZoneAutocomplete))] string timeZone,
        [Summary(description: BirthdayConfigModule.NameDescription), Autocomplete(typeof(BirthdayConfigNameAutocomplete))] string? name = null)
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

            if (!TzdbDateTimeZoneSource.Default.CanonicalIdMap.TryGetValue(timeZone, out string? canonicalTimeZone))
            {
                return new ConfigChangeResult(false, $"`{timeZone}` is not a valid/supported timezone.");
            }

            var tz = TzdbDateTimeZoneSource.Default.ForId(canonicalTimeZone);

            await x.Context.SetBirthday(x.Config, user.Id, date, tz, clock.GetCurrentInstant().InUtc().LocalDateTime);

            return new ConfigChangeResult(true,
                $"Set <@{user.Id}>'s birthday to the {date.Day.Ordinalize()} of {date.ToString("MMMM", CultureInfo.InvariantCulture)}. ({tz.Id})");
        });
    }

    [SlashCommand("rm-user", "Removes a user's birthday entry.")]
    public async Task RemoveBirthdayUserSlash([Summary(description: "The user to remove the entry of.")] IGuildUser user,
        [Summary(description: BirthdayConfigModule.NameDescription), Autocomplete(typeof(BirthdayConfigNameAutocomplete))] string? name = null)
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
        [Summary(description: NameDescription)]
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
    public async Task SetDefaultConfigSlash([Summary(description: BirthdayConfigModule.NameDescription), Autocomplete(typeof(BirthdayConfigNameAutocomplete))] string? name = null)
    {
        await CommonConfig(async (context, eb) =>
        {
            var guildConfig = await context.GetGuildConfig(Context.Guild.Id, x => x.Include(y => y.DefaultBirthdayConfig));

            if (name == null)
            {
                guildConfig.DefaultBirthdayConfig = null;
                return new ConfigChangeResult(true, "Cleared default config.");
            }
            else
            {
                try
                {
                    var config = await ResolveConfig(context, name, Context.Guild.Id);
                    guildConfig.DefaultBirthdayConfig = config;

                    return new ConfigChangeResult(true, $"Set default config to `{config.Name}`.");
                }
                catch (ConfigException ex)
                {
                    return new ConfigChangeResult(false, ex.Message);
                }
            }
        });
    }

    [SlashCommand("add-filtered-role", "Dictates whether a user can add/change their birthday. Does not affect existing entries.")]
    public async Task AddRoleToListSlash([Summary(description: "The role to add.")] IRole role,
        [Summary(description: "The list to add the role to.")] AllowBlockList list,
        [Summary(description: BirthdayConfigModule.NameDescription), Autocomplete(typeof(BirthdayConfigNameAutocomplete))] string? name = null)
    {
        await CommonBirthdayConfig(name, Context.Guild.Id, x =>
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
                return Task.FromResult(new ConfigChangeResult(false, $"Role already in {list.Humanize()}."));

            roles.Add(role.Id);

            return Task.FromResult(new ConfigChangeResult(true, $"Added <@&{role.Id}> to {list.Humanize()}."));
        });
    }

    [SlashCommand("rm-filtered-role", "Removes a role from the allowlist or blocklist.")]
    public async Task RemoveRoleFromListSlash([Summary(description: "The role to remove.")] IRole role,
        [Summary(description: "The list to remove the role from.")] AllowBlockList list,
        [Summary(description: BirthdayConfigModule.NameDescription), Autocomplete(typeof(BirthdayConfigNameAutocomplete))] string? name = null)
    {
        await CommonBirthdayConfig(name, Context.Guild.Id, x =>
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
                return Task.FromResult(new ConfigChangeResult(false, $"Role not found in {list.Humanize()}."));

            roles.Remove(role.Id);

            return Task.FromResult(new ConfigChangeResult(true, $"Removed <@&{role.Id}> from {list.Humanize()}."));
        });
    }

    [SlashCommand("set-edit-window", "How long before a user can't edit their birthday in seconds. Set to 0 for infinite.")]
    public async Task SetEditWindowSlash(
        [Summary(description: "How long before a user can't edit their birthday in seconds. Set to 0 for infinite.")]
        [MinValue(0)]
        int editWindow,
        [Summary(description: BirthdayConfigModule.NameDescription),
         Autocomplete(typeof(BirthdayConfigNameAutocomplete))]
        string? name = null)
    {
        await CommonBirthdayConfig(name, Context.Guild.Id, context =>
        {
            if (editWindow < 0)
            {
                return Task.FromResult(
                    new ConfigChangeResult(false, "cant be below zero but how on earth did you even get it lower than zero tf"));
            }

            context.Config.EditWindowSeconds = editWindow;

            return Task.FromResult(new ConfigChangeResult(true, $"Set edit window for users to {editWindow}s{(editWindow == 0 ? " (infinite)" : "")}."));
        });
    }

    [SlashCommand("get-birthdays", "Lists all the users with a birthday set.")]
    public async Task GetBirthdaysSlash(
        [Summary(description: BirthdayConfigModule.NameDescription),
         Autocomplete(typeof(BirthdayConfigNameAutocomplete))]
        string? name = null)
    {
        await DeferAsync();

        await using var context = dbService.GetDbContext();

        var roleColor = QuotingHelpers.GetUserRoleColorWithFallback(await Context.Guild.GetCurrentUserAsync(), Color.Green);

        BirthdayConfig bday;
        try
        {
            bday = await ResolveConfig(context, name, Context.Guild.Id);
        }
        catch (ConfigException ex)
        {
            await FollowupAsync(embeds: ConfigUtilities.CreateEmbeds(await Context.Guild.GetCurrentUserAsync(),
                new EmbedBuilder(),
                new ConfigChangeResult(false, ex.Message)));
            return;
        }

        var bdays = await context.Birthdays.Where(x => x.BirthdayConfig == bday).ToArrayAsync();

        if (bdays.Length == 0)
        {
            await FollowupAsync(embeds: ConfigUtilities.CreateEmbeds(await Context.Guild.GetCurrentUserAsync(),
                new EmbedBuilder(),
                new ConfigChangeResult(false, "No one has a birthday :(")));
            return;
        }

        var pages = bdays
            .Select(x => $"* <@{x.UserId}> - `{x.BirthDayDate}` (`{x.TimeZone}`)")
            .Chunk(10).Select(x => new PageBuilder().WithColor(roleColor).WithDescription(string.Join('\n', x)));

        var paginator = new StaticPaginatorBuilder()
            .WithDefaultEmotes()
            .WithUsers(Context.User)
            .WithPages(pages);

        await interactive.SendPaginatorAsync(paginator.Build(), Context.Interaction, TimeSpan.FromMinutes(1), InteractionResponseType.DeferredChannelMessageWithSource);
    }

    public class BirthdayTextModal : IModal
    {
        public string Title => "Text";

        [InputLabel("Embed Title Text")]
        [ModalTextInput("embed_title_text", maxLength: BirthdayConfig.MaxStringLength)]
        public required string EmbedTitleText { get; set; }

        [InputLabel("Embed Description Text")]
        [ModalTextInput("embed_description_text", TextInputStyle.Paragraph, maxLength: BirthdayConfig.MaxStringLength)]
        public required string EmbedDescriptionText { get; set; }

        [InputLabel("Embed Footer Text")]
        [ModalTextInput("embed_footer_text", TextInputStyle.Paragraph, maxLength: BirthdayConfig.MaxStringLength)]
        public required string EmbedFooterText { get; set; }

        [InputLabel("Denied for Reason Edit Window Text")]
        [ModalTextInput("denied_edit_window_text", TextInputStyle.Paragraph, maxLength: BirthdayConfig.MaxStringLength)]
        public required string DeniedForReasonEditWindowText { get; set; }

        [InputLabel("Denied for Reason Permissions Text")]
        [ModalTextInput("denied_permissions_text", TextInputStyle.Paragraph, maxLength: BirthdayConfig.MaxStringLength)]
        public required string DeniedForReasonPermissionsText { get; set; }
    }

    [SlashCommand("change-text", "Change the text used in the /birthday command.")]
    public async Task ChangeStringSlash([Summary(description: BirthdayConfigModule.NameDescription), Autocomplete(typeof(BirthdayConfigNameAutocomplete))] string? name = null)
    {
        await using var context = dbService.GetDbContext();

        BirthdayConfig config;
        try
        {
            config = await ResolveConfig(context, name, Context.Guild.Id);
        }
        catch (ConfigException ex)
        {
            await FollowupAsync(embeds: ConfigUtilities.CreateEmbeds(await Context.Guild.GetCurrentUserAsync(), new EmbedBuilder(),
                new ConfigChangeResult(false, ex.Message)));
            return;
        }

        var modal = new BirthdayTextModal
        {
            //DisplayName = config.DisplayName,
            EmbedTitleText = config.EmbedTitleText,
            EmbedDescriptionText = config.EmbedDescriptionText,
            EmbedFooterText = config.EmbedFooterText,
            DeniedForReasonEditWindowText = config.DeniedForReasonEditWindowText,
            DeniedForReasonPermissionsText = config.DeniedForReasonPermissionsText
        };

        await RespondWithModalAsync($"{ModulePrefixes.BIRTHDAY_TEXT_MODAL}{config.Name}", modal);
    }

    [ModalInteraction($"{ModulePrefixes.BIRTHDAY_TEXT_MODAL}*", true)]
    public async Task TextModal(string name, BirthdayTextModal modal)
    {
        await CommonBirthdayConfig(name, Context.Guild.Id, context =>
        {
            context.Config.EmbedTitleText = modal.EmbedTitleText;
            context.Config.EmbedDescriptionText = modal.EmbedDescriptionText;
            context.Config.EmbedFooterText = modal.EmbedFooterText;
            context.Config.DeniedForReasonEditWindowText = modal.DeniedForReasonEditWindowText;
            context.Config.DeniedForReasonPermissionsText = modal.DeniedForReasonPermissionsText;

            return Task.FromResult(new ConfigChangeResult(true, "Set text successfully."));
        });
    }

//#if DEBUG
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
//#endif

    private Task<bool> CommonConfig(Func<BotDbContext, EmbedBuilder, Task<ConfigChangeResult>> updateAction)
    {
        return ConfigUtilities.CommonConfig(Context, dbService, updateAction);
    }

    public record ConfigContext(BotDbContext Context, BirthdayConfig Config, EmbedBuilder EmbedBuilder);

    private async Task<BirthdayConfig> ResolveConfig(BotDbContext context, string? name, ulong guildId, Func<IQueryable<BirthdayConfig>, IQueryable<BirthdayConfig>>? modifyQuery = null)
    {
        BirthdayConfig? config = null;

        if (name == null)
        {
            config = (await context.GetGuildConfig(guildId, x => 
                x.Include(y => y.DefaultBirthdayConfig))).DefaultBirthdayConfig;

            if (config == null)
            {
                throw new ConfigException("No default config set. Please manually specify the config name.");
            }

            name = config.Name;
        }

        name = name.ToLowerInvariant();

        if (!ConfigUtilities.IsValidId().IsMatch(name))
        {
            throw new ConfigException($"`{name}` is not valid.");
        }

        modifyQuery ??= x => x;

        var query = modifyQuery(context.BirthdayConfigs);

        config ??= await query.FirstOrDefaultAsync(x => x.GuildId == guildId && x.Name == name);

        if (config == null)
        {
            throw new ConfigException("Could not find config.");
        }

        return config;
    }

    private Task<bool> CommonBirthdayConfig(string? name, ulong guildId, Func<ConfigContext, Task<ConfigChangeResult>> updateAction,
        Func<IQueryable<BirthdayConfig>, IQueryable<BirthdayConfig>>? modifyQuery = null)
    {
        return CommonConfig(async (context, eb) =>
        {
            try
            {
                var config = await ResolveConfig(context, name, guildId, modifyQuery);

                eb.WithAuthor(config.Name);

                return await updateAction(new ConfigContext(context, config, eb));
            }
            catch (ConfigException ex)
            {
                return new ConfigChangeResult(false, ex.Message);
            }
        });
    }

    // a little dumb
    public class ConfigException(string message) : Exception(message);
}
