using Asahi.Database;
using Asahi.Database.Models;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NodaTime;

namespace Asahi.Modules.BirthdayRoles;

[Inject(ServiceLifetime.Singleton)]
public class BirthdayTimerService(DiscordSocketClient client, IDbService dbService, IClock clock, ILogger<BirthdayTimerService> logger)
{
    public Task? timerTask;

    public void StartBackgroundTask(CancellationToken token)
    {
        timerTask ??= Task.Run(() => TimerTask(token), token);
    }

    /// <remarks>Should only be one of these running!</remarks>
    private async Task TimerTask(CancellationToken cancellationToken)
    {
        try
        {
            logger.LogTrace("Birthday timer task started");
            try
            {
                await CheckForBirthdays();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unhandled exception in TimerTask! {message}", ex.Message);
            }
            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                try
                {
                    await CheckForBirthdays();
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Unhandled exception in TimerTask! {message}", ex.Message);
                }
            }
        }
        catch (TaskCanceledException) {}
        catch (OperationCanceledException) {}
        catch (Exception e)
        {
            logger.LogCritical(e, "Unhandled exception in TimerTask! Except much worse because this was outside of the loop!!");
        }
    }

    private record BirthdayAndy(SocketRole Role, SocketGuildUser User);
    //{
    //    public SocketRole role = role;
    //    public SocketGuildUser user = user;
    //}

    public async Task CheckForBirthdays(Instant? currentInstant = null)
    {
        await using var context = dbService.GetDbContext();

        currentInstant ??= clock.GetCurrentInstant();

        var now = currentInstant.Value;

        //logger.LogTrace("checking birthdays for time {date}.", now);

        var birthdays = await GetCurrentBirthdays(context, now);
        var groupedBirthdays = birthdays.GroupBy(x => x.BirthdayConfig);

        if(birthdays.Length != 0)
            logger.LogTrace("got {count} birthdays", birthdays.Length);

        List<BirthdayAndy> birthdayAndys = [];

        foreach (var group in groupedBirthdays)
        {
            var birthdayConfig = group.Key;

            var guild = client.GetGuild(birthdayConfig.GuildId);
            await guild.DownloadUsersAsync();
            var role = guild.GetRole(birthdayConfig.BirthdayRole);

            foreach (var entry in group)
            {
                birthdayAndys.Add(new BirthdayAndy(role, guild.GetUser(entry.UserId)));
            }
        }

        
        var birthdaysInGuild = birthdayAndys
            .GroupBy(x => x.User.Guild).ToDictionary(x => x.Key, x => x.ToArray());

        var configs = await context.BirthdayConfigs.ToArrayAsync();

        foreach (var guild in client.Guilds)
        {
            try
            {
                if (!birthdaysInGuild.TryGetValue(guild, out var guildBirthdayAndys))
                {
                    guildBirthdayAndys = [];
                }

                var configsForGuild = configs.Where(x => x.GuildId == guild.Id);

                List<BirthdayAndy> andysToRemove = [];
                List<BirthdayAndy> andysToAdd = [];

                foreach (var config in configsForGuild)
                {
                    var role = guild.GetRole(config.BirthdayRole);

                    andysToRemove.AddRange(role.Members.Select(user => new BirthdayAndy(role, user)));
                }

                var birthdayAndysGroupedRole = guildBirthdayAndys.GroupBy(x => x.Role);
                foreach (var andysRoleGroup in birthdayAndysGroupedRole)
                {
                    foreach (var birthdayAndy in (andysRoleGroup.Key.Members
                                 .Where(x => andysRoleGroup.Any(y => y.User.Id == x.Id))
                                 .Select(x => new BirthdayAndy(andysRoleGroup.Key, x))))
                    {
                        //var memberToRemove = membersToRemove.FirstOrDefault(x =>
                        //    x.user.Id == birthdayAndy.user.Id && x.role.Id == birthdayAndy.role.Id);

                        //if(!memberToRemove.Equals(default))
                        //    membersToRemove.Remove(memberToRemove);

                        andysToRemove.Remove(birthdayAndy);
                    }

                    andysToAdd.AddRange(
                        andysRoleGroup.Where(x => andysRoleGroup.Key.Members.All(y => y.Id != x.User.Id)));
                }

                logger.LogTrace("removing {count} members from birthday role (guild is {guild})", andysToRemove.Count, guild.Name);

                foreach (var andy in andysToRemove)
                {
                    await andy.User.RemoveRoleAsync(andy.Role,
                        new RequestOptions { AuditLogReason = "It's no longer their birthday :(" });
                }

                logger.LogTrace("adding {count} members to birthday role (guild is {guild})", andysToAdd.Count, guild.Name);

                foreach (var andy in andysToAdd)
                {
                    await andy.User.AddRoleAsync(andy.Role,
                        new RequestOptions { AuditLogReason = "It's their birthday! :D" });
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to do birthday roles for guild {guild}.", guild.Id);
            }
        }
    }

    private async Task<BirthdayEntry[]> GetCurrentBirthdays(BotDbContext context, Instant now)
    {
        //var yesterday = now.PlusDays(-1);
        //var tomorrow = now.PlusDays(1);

        //var unfilteredDates = await context.Birthdays
        //    .Where(entry => entry.BirthDayDateBacking >= yesterday && entry.BirthDayDateBacking <= tomorrow).ToArrayAsync();

        // ok just gonna be lazy for now until this causes issues. figuring out annual date queries with respect to timezones and leap years is pain
        // maybe I could just pull all the birthdays within the next 14 days and that would do
        // something for the next guy (me) to worry about
        var unfilteredDates = await context.Birthdays.Include(x => x.BirthdayConfig).ToArrayAsync();

        var filteredDates = unfilteredDates.Where(entry =>
        {
            var timezone = DateTimeZoneProviders.Tzdb.GetZoneOrNull(entry.TimeZone);
            if (timezone == null)
                return false;

            var localNow = now.InZone(timezone);

            var birthdayInCurrentYear = entry.BirthDayDate.InYear(localNow.Year);

            //logger.LogTrace("(u:{user}, g:{guild}, c:{config}) now is {now}, birthday is {birthday}.", 
            //    entry.UserId, entry.BirthdayConfig.GuildId, entry.BirthdayConfig.Name, localNow, birthdayInCurrentYear);

            return localNow.Date == birthdayInCurrentYear;
        }).ToArray();

        return filteredDates;
    }
}
