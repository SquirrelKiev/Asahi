using Discord;
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
                // hmm they probably should have been tracked
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
    }
}