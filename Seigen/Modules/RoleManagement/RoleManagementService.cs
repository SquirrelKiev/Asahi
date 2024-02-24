using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Seigen.Database;
using Seigen.Database.Models;

namespace Seigen.Modules.RoleManagement;

[Inject(ServiceLifetime.Singleton)]
public class RoleManagementService(DbService dbService)
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
        }
    }

    public async Task OnRoleAdded(SocketRole role, SocketGuildUser user)
    {
        await using var context = dbService.GetDbContext();

        var trackablesQ = context.Trackables.Where(trackable => trackable.RoleToMonitor == role.Id);
        var trackables = await trackablesQ.ToArrayAsync();

        if (trackables.Length == 0)
        {
            Log.Debug("No trackables for {roleName} ({roleId}).", role.Name, role.Id);
            return;
        }

        foreach (var trackable in trackables)
        {
            var totalTrackedUsers = await context.TrackedUsers.CountAsync(trackedUser => trackedUser.Trackable.Id == trackable.Id);
            var remainingSlots = trackable.Limit - totalTrackedUsers;

            if (remainingSlots < 0)
            {
                Log.Information("Slots for trackable {trackableId} full. Aborting. " +
                                "({remainingSlots}/{limit}, {trackedUserCount} tracked users.)", 
                    trackable.Id, remainingSlots, trackable.Limit, totalTrackedUsers);
                return;
            }

            Log.Information("Slot opened for trackable {trackableId}! " +
                            "({remainingSlots}/{limit}, {trackedUserCount} tracked users.)", 
                trackable.Id, remainingSlots, trackable.Limit, totalTrackedUsers);
        }
    }
}