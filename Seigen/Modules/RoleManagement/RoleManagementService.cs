using System.Diagnostics;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Seigen.Database;
using Seigen.Database.Models;

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
            Log.Debug("role {role} added to {user}", role.Name, user.Username);
            await OnRoleAdded(role, user);
        }

        foreach (var role in rolesRemoved)
        {
            Log.Debug("role {role} removed", role.Name);
            await OnRoleRemoved(role, user);
        }
    }

    public async Task OnRoleAdded(SocketRole role, SocketGuildUser user)
    {
        await using var context = dbService.GetDbContext();

        var trackablesQ = context.Trackables.Where(trackable => trackable.MonitoredRole == role.Id);
        var trackables = await trackablesQ.ToArrayAsync();

        if (trackables.Length == 0)
        {
            Log.Debug("No trackables for {roleName} ({roleId}).", role.Name, role.Id);
            return;
        }

        foreach (var trackable in trackables)
        {
            var totalTrackedUsers = await context.TrackedUsers.CountAsync(trackedUser => trackedUser.Trackable.Id == trackable.Id);
            var remainingSlots = Math.Max(0, trackable.Limit - totalTrackedUsers);

            if (trackable.Limit != 0 && remainingSlots <= 0)
            {
                Log.Information("Slots for trackable {trackableId} full. Aborting. " +
                                "({remainingSlots}/{limit}, {trackedUserCount} tracked users.)",
                    trackable.Id, remainingSlots, trackable.Limit, totalTrackedUsers);
                continue;
            }

            Log.Information("Slot open for trackable {trackableId}! " +
                            "({remainingSlots}/{limit}, {trackedUserCount} tracked users.)",
                trackable.Id, remainingSlots, trackable.Limit, totalTrackedUsers);

            var trackedUser = new TrackedUser()
            {
                Trackable = trackable,
                UserId = user.Id
            };

            var assignableGuild = await client.GetGuildAsync(trackable.AssignableGuild);

            if (assignableGuild == null)
            {
                Log.Information("Guild {guildId} not found. Skipping.", trackable.AssignableGuild);
                continue;
            }

            var assignableRole = assignableGuild.GetRole(trackable.AssignableRole);

            if (!user.Roles.Contains(assignableRole))
                await user.AddRoleAsync(assignableRole);

            context.TrackedUsers.Add(trackedUser);
        }

        await context.SaveChangesAsync();

        await CacheUsers();
    }

    public async Task OnRoleRemoved(SocketRole role, SocketGuildUser user)
    {
        await using var context = dbService.GetDbContext();

        var trackablesQ = context.Trackables.Where(trackable => trackable.MonitoredRole == role.Id);
        var trackables = await trackablesQ.ToArrayAsync();

        if (trackables.Length == 0)
        {
            Log.Debug("No trackables for {roleName} ({roleId}).", role.Name, role.Id);
            return;
        }

        foreach (var trackable in trackables)
        {
            // TODO: make sure we dont remove the role if another trackable thinks we should still keep it (loop through tracked users and recheck roles?)
            var trackedUser =
                await context.TrackedUsers.FirstOrDefaultAsync(x => x.Trackable.Id == trackable.Id && x.UserId == user.Id);

            if (trackedUser == null)
            {
                Log.Information("User {user} was not tracked, yet received a role removal request. (trackable {trackableId})", user.Id, trackable.Id);
                return;
            }

            var assignableGuild = await client.GetGuildAsync(trackable.AssignableGuild);

            if (assignableGuild == null)
            {
                Log.Information("Guild {guildId} not found. Skipping.", trackable.AssignableGuild);
                continue;
            }

            var assignableRole = assignableGuild.GetRole(trackable.AssignableRole);

            if (user.Roles.Contains(assignableRole))
                await user.RemoveRoleAsync(assignableRole);

            context.TrackedUsers.Remove(trackedUser);
        }

        await context.SaveChangesAsync();

        await CacheUsers();
    }

    public async Task<IEnumerable<IGuildUser>> GetUsersWithRole(IGuild guild, ulong roleId)
    {
        var users = await guild.GetUsersAsync();

        return GetUsersWithRole(roleId, users);
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
                oldUserIds = Enumerable.Empty<ulong>();
            }

            if (!newCache.TryGetValue(roleAndGuild, out var newUserIds))
            {
                newUserIds = Enumerable.Empty<ulong>();
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
            var guild = await client.GetGuildAsync(trackable.AssignableGuild);

            foreach (var trackedUser in scopedTrackedUsers)
            {
                if (usersRemoved.All(x => x.Id != trackedUser.UserId)) continue;

                context.TrackedUsers.Remove(trackedUser);
                await (await guild.GetUserAsync(trackedUser.UserId)).RemoveRoleAsync(trackedUser.Trackable.AssignableRole);

                Log.Debug("{userId} removed from {trackableId} (2)", trackedUser.Id, trackable.Id);
            }

            var currentSlotUsage = scopedTrackedUsers.Count(users => usersRemoved.All(removed => removed.Id != users.UserId));
            var availableSlots = trackable.Limit - currentSlotUsage;

            if (trackable.Limit == 0 || availableSlots > 0)
            {
                var usersToAdd = usersAdded
                    .OrderBy(user => user.PremiumSince ?? DateTimeOffset.MaxValue).AsEnumerable();

                if (trackable.Limit != 0)
                    usersToAdd = usersToAdd.Take((int)availableSlots);

                foreach (var user in usersToAdd)
                {
                    var newTrackedUser = new TrackedUser
                    {
                        Trackable = trackable,
                        UserId = user.Id
                    };

                    context.TrackedUsers.Add(newTrackedUser);
                    await (await guild.GetUserAsync(newTrackedUser.UserId)).AddRoleAsync(newTrackedUser.Trackable.AssignableRole);

                    Log.Debug("{userId} added to {trackableId} (2)", user.Id, trackable.Id);
                }
            }
        }

        await context.SaveChangesAsync();
    }
}