using System.Diagnostics;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Seigen.Database;
using Seigen.Database.Models;
using Serilog.Context;

namespace Seigen.Modules.RoleManagement;

[Inject(ServiceLifetime.Singleton)]
public class RoleManagementService(DbService dbService, IDiscordClient client)
{
    public async Task OnUserRolesUpdated(Cacheable<SocketGuildUser, ulong> cacheable, SocketGuildUser user)
    {
        var rolesAdded = user.Roles.Where(x =>
        {
            var val = cacheable.Value.Roles.Contains(x);

            return !val;
        });

        var rolesRemoved = cacheable.Value.Roles.Where(x =>
        {
            var val = user.Roles.Contains(x);

            return !val;
        });

        foreach (var role in rolesAdded)
        {
            Log.Debug("Role {role} added to {user}", role.Name, user.Username);
            //await OnRoleAdded(role, user);

            await using var context = dbService.GetDbContext();

            if (!await IsRoleTracked(context, role))
            {
                Log.Debug("No trackables for {roleName} ({roleId}).", role.Name, role.Id);
                continue;
            }

            await ResolveConflicts(role.Id, [], [user]);
        }

        foreach (var role in rolesRemoved)
        {
            Log.Debug("Role {role} removed from {user}", role.Name, user.Username);
            //await OnRoleRemoved(role, user);

            await using var context = dbService.GetDbContext();

            if (!await IsRoleTracked(context, role))
            {
                Log.Debug("No trackables for {roleName} ({roleId}).", role.Name, role.Id);
                continue;
            }

            await ResolveConflicts(role.Id, [user], []);
        }
    }

    private async Task<bool> IsRoleTracked(BotDbContext context, IRole role)
    {
        var trackablesQ = context.Trackables.Where(trackable => trackable.MonitoredRole == role.Id);
        var trackables = await trackablesQ.ToArrayAsync();

        return trackables.Length != 0;
    }

    public static IEnumerable<IGuildUser> GetUsersWithRole(ulong roleId, IReadOnlyCollection<IGuildUser> users)
    {
        return users.Where(x => x.RoleIds.Contains(roleId));
    }

    public async Task CacheUsers()
    {
        var sw = new Stopwatch();

        sw.Start();

        await using var context = dbService.GetDbContext();

        var trackedRoles = (await context.Trackables.ToArrayAsync())
                .SelectMany(t => new[]
                {
                    new { GuildId = t.MonitoredGuild, RoleId = t.MonitoredRole },
                    new { GuildId = t.AssignableGuild, RoleId = t.AssignableRole }
                })
                .GroupBy(x => x.GuildId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(x => x.RoleId).Distinct().ToList());

        await context.CachedUsersRoles.ExecuteDeleteAsync();

        foreach (var trackedRole in trackedRoles)
        {
            var guild = await client.GetGuildAsync(trackedRole.Key);
            var users = await guild.GetUsersAsync();

            foreach (var roleId in trackedRole.Value)
            {
                var usersWithRole = GetUsersWithRole(roleId, users);

                foreach (var user in usersWithRole)
                {
                    context.CachedUsersRoles.Add(new CachedUserRole()
                    {
                        RoleId = roleId,
                        UserId = user.Id,
                        GuildId = guild.Id
                    });
                }
            }
        }

        await context.SaveChangesAsync();

        sw.Stop();
        Log.Debug("Cached users in {ms}ms", sw.ElapsedMilliseconds);
    }

    public async Task CacheAndResolve()
    {
        var sw = new Stopwatch();

        sw.Start();

        await using var context = dbService.GetDbContext();

        var oldCache = (await context.CachedUsersRoles.ToArrayAsync())
            .GroupBy(x => new { x.GuildId, x.RoleId })
            .ToDictionary(
            x => x.Key,
            x => x.Select(y => y.UserId));

        await CacheUsers();

        var newCache = (await context.CachedUsersRoles.ToArrayAsync())
            .GroupBy(x => new { x.GuildId, x.RoleId })
            .ToDictionary(
                x => x.Key,
                x => x.Select(y => y.UserId));

        var roles = oldCache.Keys.Concat(newCache.Keys).Distinct();

        foreach (var roleAndGuild in roles)
        {
            var roleId = roleAndGuild.RoleId;
            var guild = await client.GetGuildAsync(roleAndGuild.GuildId);

            if (!oldCache.TryGetValue(roleAndGuild, out var oldUserIds))
            {
                oldUserIds = [];
            }

            if (!newCache.TryGetValue(roleAndGuild, out var newUserIds))
            {
                newUserIds = [];
            }

            var oldUsers = new List<IGuildUser>();

            foreach (var oldUserId in oldUserIds)
            {
                oldUsers.Add(await guild.GetUserAsync(oldUserId));
            }

            var newUsers = new List<IGuildUser>();

            foreach (var newUserId in newUserIds)
            {
                newUsers.Add(await guild.GetUserAsync(newUserId));
            }

            await ResolveConflicts(roleId, oldUsers, newUsers);
        }

        sw.Stop();

        Log.Debug("Took {ms}ms to cache and resolve role users.", sw.ElapsedMilliseconds);
    }

    public async Task ResolveConflicts(ulong monitoredRoleId, List<IGuildUser> cachedUserIds, List<IGuildUser> currentUserIds)
    {
        await using var context = dbService.GetDbContext();

        var trackables = await context.Trackables.Where(x => x.MonitoredRole == monitoredRoleId).ToArrayAsync();

        var usersAdded = currentUserIds.Where(x =>
        {
            var val = cachedUserIds.Contains(x);

            return !val;
        }).ToArray();

        var usersRemoved = cachedUserIds.Where(x =>
        {
            var val = currentUserIds.Contains(x);

            return !val;
        }).ToArray();

        foreach (var trackable in trackables)
        {
            var scopedTrackedUsers = await context.TrackedUsers.Where(x => x.Trackable.Id == trackable.Id).ToArrayAsync();

            foreach (var trackedUser in scopedTrackedUsers)
            {
                if (usersRemoved.All(x => x.Id != trackedUser.UserId)) continue;

                await UntrackUser(context, trackedUser, trackable);
            }

            var currentSlotUsage = await GetTakenSlots(context, trackable);
            var availableSlots = trackable.Limit - currentSlotUsage;

            var usersToAdd = usersAdded
                .OrderBy(user => user.PremiumSince ?? DateTimeOffset.MaxValue).AsEnumerable();

            if (trackable.Limit != 0 && availableSlots > 0)
                usersToAdd = usersToAdd.Take((int)availableSlots).ToArray();

            if (trackable.Limit == 0 || availableSlots > 0)
            {
                foreach (var user in usersToAdd)
                {
                    var newTrackedUser = new TrackedUser
                    {
                        Trackable = trackable,
                        UserId = user.Id
                    };

                    await TrackUser(context, newTrackedUser, trackable);
                }
            }

            if (trackable.Limit != 0)
            {
                var skipped = usersAdded.Length - availableSlots;
                if (skipped > 0)
                {
                    Log.Debug("Skipped {skippedCount} user.", skipped);
                    await TryLogToChannel(trackable.LoggingChannel, async () =>
                    {
                        var takenSlots = await GetTakenSlots(context, trackable);

                        var embed = new EmbedBuilder().WithDescription(
                            $"Skipped adding {skipped} users to trackable {trackable.Id} due to slots being full. ({takenSlots}/{trackable.Limit})")
                            .WithColor(0xFF8C00); // DarkOrange

                        return new MessageContents(embed);
                    });
                }
            }
        }

        await context.SaveChangesAsync();
    }

    public async Task<long> GetTakenSlots(BotDbContext context, Trackable trackable)
    {
        var scopedTrackedUsers = await context.TrackedUsers.Where(x => x.Trackable.Id == trackable.Id).ToArrayAsync();

        var currentSlotUsage = scopedTrackedUsers.Length;

        return currentSlotUsage;
    }

    public async Task<bool> TrackUser(BotDbContext context, TrackedUser newTrackedUser, Trackable trackable)
    {
        using var disposable = LogContext.PushProperty("Trackable ID", trackable.Id);

        var assignableGuild = await client.GetGuildAsync(trackable.AssignableGuild);

        var loggingChannelId = trackable.LoggingChannel;

        if (assignableGuild == null)
        {
            Log.Information("Attempted to track user {UserId} but Assignable Guild {guildId} was not found.", newTrackedUser.UserId, trackable.AssignableGuild);
            await TryLogToChannel(loggingChannelId,
                () => Task.FromResult(new MessageContents($"Attempted to track user <@{newTrackedUser.UserId}> but could not find Guild {trackable.AssignableGuild}.")));

            return false;
        }

        var user = await assignableGuild.GetUserAsync(newTrackedUser.UserId);

        if (user == null)
        {
            Log.Information("Attempted to track user {userId} but they could not found.", newTrackedUser.UserId);
            await TryLogToChannel(loggingChannelId,
                () => Task.FromResult(new MessageContents($"Attempted to track user <@{newTrackedUser.UserId}>, but could not be found within Guild {trackable.AssignableGuild}.")));

            return false;
        }

        context.TrackedUsers.Add(newTrackedUser);
        await user.AddRoleAsync(newTrackedUser.Trackable.AssignableRole);

        // not happy about running this here but im not sure how to get it to include the local cache in queries
        await context.SaveChangesAsync();

        var currentSlotUsage = await GetTakenSlots(context, trackable);

        Log.Debug("{userId} added to trackable {trackableId}. ({currentSlotUsage}/{slotLimit})",
            newTrackedUser.UserId, trackable.Id, currentSlotUsage, trackable.Limit);

        await TryLogToChannel(loggingChannelId, async () =>
        {
            var embed = new EmbedBuilder()
                .WithDescription($"Added <@{newTrackedUser.UserId}> to trackable {trackable.Id}. ({currentSlotUsage}/{trackable.Limit})")
                .WithColor(0x2E8B57) // SeaGreen
                .WithAuthor(user);

            return new MessageContents(embed);
        });

        return true;
    }

    public async Task UntrackUser(BotDbContext context, TrackedUser trackedUser, Trackable trackable)
    {
        using var disposable = LogContext.PushProperty("Trackable ID", trackable.Id);

        var assignableGuild = await client.GetGuildAsync(trackable.AssignableGuild);

        var loggingChannelId = trackable.LoggingChannel;

        context.TrackedUsers.Remove(trackedUser);
        var user = await assignableGuild.GetUserAsync(trackedUser.UserId);
        if (user != null)
            await user.RemoveRoleAsync(trackedUser.Trackable.AssignableRole);

        // see comment in TrackUser
        await context.SaveChangesAsync();

        var currentSlotUsage = await GetTakenSlots(context, trackable);

        Log.Debug("{userId} removed from trackable {trackableId}. ({currentSlotUsage}/{slotLimit})",
            trackedUser.UserId, trackable.Id, currentSlotUsage, trackable.Limit);

        await TryLogToChannel(loggingChannelId, () =>
        {
            var embed = new EmbedBuilder()
                .WithDescription($"Removed <@{trackedUser.UserId}> from trackable {trackable.Id}. ({currentSlotUsage}/{trackable.Limit})")
                .WithColor(0xDC143C) // Crimson
                .WithAuthor(user);

            return Task.FromResult(new MessageContents(embed));
        });
    }

    private async Task TryLogToChannel(ulong loggingChannelId, Func<Task<MessageContents>> contents)
    {
        if (loggingChannelId != 0)
        {
            if (await client.GetChannelAsync(loggingChannelId) is ITextChannel loggingChannel)
            {
                var actualContents = await contents();

                // TODO: should be an extension method on ITextChannel really
                await loggingChannel.SendMessageAsync(actualContents.body, embeds: actualContents.embeds);
            }
        }
    }
}