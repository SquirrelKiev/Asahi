using Asahi.Database;
using Asahi.Database.Models;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NodaTime;

namespace Asahi.Modules.BirthdayRoles;

[Inject(ServiceLifetime.Singleton)]
public class BirthdayTimerService(DiscordSocketClient client, DbService dbService, IClock clock, ILogger<BirthdayTimerService> logger)
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
            await CheckForBirthdays();
            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(30));
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

        logger.LogTrace("checking birthdays for time {date}.", now);

        var birthdays = await GetCurrentBirthdays(context, now.InUtc().Date);
        var groupedBirthdays = birthdays.GroupBy(x => x.BirthdayConfig);

        logger.LogTrace("got {count} birthdays", birthdays.Length);

        List<BirthdayAndy> birthdayAndys = [];

        foreach (var group in groupedBirthdays)
        {
            var birthdayConfig = group.Key;

            var guild = client.GetGuild(birthdayConfig.GuildId);
            var role = guild.GetRole(birthdayConfig.BirthdayRole);

            foreach (var entry in group)
            {
                birthdayAndys.Add(new BirthdayAndy(role, guild.GetUser(entry.UserId)));
            }

            //var usersWithRole = role.Members.ToArray();
            //var birthdayUsers = group.Select(birthday => guild.GetUser(birthday.UserId)).ToArray();

            //var membersToRemove = usersWithRole.Where(x => birthdayUsers.All(y => y.Id != x.Id)).ToArray();
            //var membersToAdd = birthdayUsers.Where(x => usersWithRole.All(y => y.Id != x.Id)).ToArray();

            //logger.LogTrace("removing {count} members from birthday role", membersToRemove.Length);

            //foreach (var user in membersToRemove)
            //{
            //    await user.RemoveRoleAsync(role, new RequestOptions() {AuditLogReason = "It's no longer their birthday :("});
            //}

            //logger.LogTrace("adding {count} members to birthday role", membersToAdd.Length);

            //foreach (var user in membersToAdd)
            //{
            //    await user.AddRoleAsync(role, new RequestOptions {AuditLogReason = "It's their birthday! :D"});
            //}
        }

        var birthdaysInGuild = birthdayAndys
            .GroupBy(x => x.User.Guild).ToDictionary(x => x.Key, x => x.ToArray());

        var configs = await context.BirthdayConfigs.ToArrayAsync();

        foreach (var guild in client.Guilds)
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
                foreach (var birthdayAndy in (andysRoleGroup.Key.Members.Where(x => andysRoleGroup.Any(y => y.User.Id == x.Id))
                             .Select(x => new BirthdayAndy(andysRoleGroup.Key, x))))
                {
                    //var memberToRemove = membersToRemove.FirstOrDefault(x =>
                    //    x.user.Id == birthdayAndy.user.Id && x.role.Id == birthdayAndy.role.Id);

                    //if(!memberToRemove.Equals(default))
                    //    membersToRemove.Remove(memberToRemove);

                    andysToRemove.Remove(birthdayAndy);
                }
                
                andysToAdd.AddRange(andysRoleGroup.Where(x => andysRoleGroup.Key.Members.All(y => y.Id != x.User.Id)));
            }

            logger.LogTrace("removing {count} members from birthday role (guild is {guild})", andysToRemove.Count, guild.Name);

            foreach (var andy in andysToRemove)
            {
                await andy.User.RemoveRoleAsync(andy.Role, new RequestOptions { AuditLogReason = "It's no longer their birthday :(" });
            }

            logger.LogTrace("adding {count} members to birthday role (guild is {guild})", andysToAdd.Count, guild.Name);

            foreach (var andy in andysToAdd)
            {
                await andy.User.AddRoleAsync(andy.Role, new RequestOptions { AuditLogReason = "It's their birthday! :D" });
            }
        }
    }

    private async Task<BirthdayEntry[]> GetCurrentBirthdays(BotDbContext context, LocalDate now)
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

            var birthdayInCurrentYear = entry.BirthDayDate.InYear(now.Year);

            var birthdayStart = birthdayInCurrentYear.AtStartOfDayInZone(timezone);

            var localNow = now.AtStartOfDayInZone(timezone);

            return localNow.Date == birthdayStart.Date;
        }).ToArray();

        return filteredDates;
    }
}