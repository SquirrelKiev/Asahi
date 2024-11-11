using System.Diagnostics;
using System.Linq.Expressions;
using Asahi.Database;
using Asahi.Database.Models;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Asahi.Modules.Seigen;

[Inject(ServiceLifetime.Singleton)]
public class RoleManagementService(IDbService dbService, DiscordSocketClient client, ILogger<BotService> logger)
{
    public async Task OnUserLeft(SocketGuild guild, SocketUser user)
    {
        await using var context = dbService.GetDbContext();

        if (await context.TrackedUsers.Where(x => x.UserId == user.Id && x.Trackable.MonitoredGuild == guild.Id ||
                                                 x.Trackable.AssignableGuild == guild.Id).AnyAsync())
            await CacheAndResolve();
    }

    public async Task OnUserJoined(SocketGuildUser user)
    {
        await using var context = dbService.GetDbContext();

        await DoubleCheckUserHasAllValidAssignableRoles(context, x => x.UserId == user.Id && x.Trackable.AssignableGuild == user.Guild.Id);
    }

    public async Task DoubleCheckUserHasAllValidAssignableRoles(BotDbContext context, Expression<Func<TrackedUser, bool>> userFilter)
    {
        var trackedUsersQ = context.TrackedUsers.Where(userFilter);

        var trackedUsers = await trackedUsersQ
            .Include(trackedUser => trackedUser.Trackable).ToArrayAsync();

        if (trackedUsers.Length == 0) return;

        foreach (var trackedUser in trackedUsers)
        {
            // so many null checks :harold:
            var guild = client.GetGuild(trackedUser.Trackable.AssignableGuild);

            if (guild == null) continue;

            var user = guild.GetUser(trackedUser.UserId);

            if (user == null) continue;

            var assignableRole = guild.GetRole(trackedUser.Trackable.AssignableRole);

            if (assignableRole == null) continue;

            if (user.Roles.FirstOrDefault(x => x.Id == assignableRole.Id) != null)
                continue;

            await user.AddRoleAsync(assignableRole);

            logger.LogInformation("Tracked user {userId} seems to have lost their reward role for whatever reason. Re-assigned. " +
                            "(Trackable {trackableId})", user.Id, trackedUser.Trackable.Id);
            await TryLogToChannel(trackedUser.Trackable.LoggingChannel, () =>
            {
                var embed = new EmbedBuilder()
                    .WithDescription(
                        $"Tracked user <@{user.Id}> seems to have lost their reward role for whatever reason. Re-assigned. " +
                        $"(Trackable {trackedUser.Trackable.Id})")
                    .WithColor(0xFF8C00)
                    .WithAuthor(user);

                return Task.FromResult(new MessageContents(embed));
            });
        }
    }

    public async Task OnUserRolesUpdated(Cacheable<SocketGuildUser, ulong> cacheable, SocketGuildUser user)
    {
        await using var context = dbService.GetDbContext();

        // so we don't cache unnecessarily, especially don't want that when we're running CacheAndResolve etc, sounds painful
        bool userHasMonitoredRoles = false;
        foreach (var role in cacheable.Value.Roles.Concat(user.Roles))
        {
            if (await IsRoleTracked(context, role))
            {
                userHasMonitoredRoles = true;
                break;
            }
        }

        // the bool exists in case I want to do the "100% guarantee that the user has the role" thing again
        if (userHasMonitoredRoles)
            await CacheAndResolve();
        // doesnt seem to be worth doing this
        //else
        //{
        //    var rolesRemoved = cacheable.Value.Roles.Any(x =>
        //    {
        //        var val = user.Roles.Contains(x);

        //        return !val;
        //    });

        //    if (rolesRemoved)
        //        await DoubleCheckUserHasAllValidAssignableRoles(context, x => x.UserId == user.Id && x.Trackable.AssignableGuild == user.Guild.Id);
        //}
    }

    private async Task<bool> IsRoleTracked(BotDbContext context, IRole role)
    {
        var trackablesQ = context.Trackables.Where(trackable => trackable.MonitoredRole == role.Id);
        return await trackablesQ.AnyAsync();
    }

    public static IAsyncEnumerable<IGuildUser> GetUsersWithRole(ulong roleId, IAsyncEnumerable<IReadOnlyCollection<IGuildUser>> users)
    {
        return users.Flatten().Where(x => x.RoleIds.Contains(roleId));
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
                    //new { GuildId = t.AssignableGuild, RoleId = t.AssignableRole }
                })
                .GroupBy(x => x.GuildId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(x => x.RoleId).Distinct().ToList());

        await context.CachedUsersRoles.ExecuteDeleteAsync();

        foreach (var trackedRole in trackedRoles)
        {
            var guild = client.GetGuild(trackedRole.Key);

            foreach (var roleId in trackedRole.Value)
            {
                var usersWithRole = GetUsersWithRole(roleId, guild.GetUsersAsync());

                await foreach (var user in usersWithRole)
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
        logger.LogDebug("Cached users in {ms}ms", sw.ElapsedMilliseconds);
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
            var guild = client.GetGuild(roleAndGuild.GuildId);

            if (!oldCache.TryGetValue(roleAndGuild, out var oldUserIds))
            {
                oldUserIds = [];
            }

            if (!newCache.TryGetValue(roleAndGuild, out var newUserIds))
            {
                newUserIds = [];
            }

            var oldUsers = oldUserIds.ToList();

            var newUsers = newUserIds.ToList();

            await ResolveConflicts(roleId, oldUsers, newUsers);
        }

        sw.Stop();

        logger.LogDebug("Took {ms}ms to cache and resolve role users.", sw.ElapsedMilliseconds);
    }

    public async Task ResolveConflicts(ulong monitoredRoleId, List<ulong> cachedUserIds, List<ulong> currentUserIds)
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
                if (usersRemoved.All(x => x != trackedUser.UserId)) continue;

                await UntrackUser(context, trackedUser, trackable);
            }

            var currentSlotUsage = await GetTakenSlots(context, trackable);
            var availableSlots = trackable.Limit - currentSlotUsage;

            var monitoredGuild = client.GetGuild(trackable.MonitoredGuild);
            var assignableGuild = client.GetGuild(trackable.AssignableGuild);

            // a list of the users from the monitored guild
            List<IGuildUser> usersToAdd = [];

            foreach (var userAdded in usersAdded)
            {
                var guildUser = monitoredGuild.GetUser(userAdded);
                if (guildUser == null)
                {
                    continue;
                }

                usersToAdd.Add(guildUser);
            }

            usersToAdd = [.. usersToAdd.OrderBy(user => user.PremiumSince ?? DateTimeOffset.MaxValue)];

            //await TryLogToChannel(trackable.LoggingChannel,
            //    () =>
            //    {
            //        var desc = new StringBuilder();
            //        desc.AppendLine("**OK here's all the boosters sorted by when they started, for reference:");

            //        int index = 1;
            //        foreach (var guildUser in usersToAdd)
            //        {
            //            desc.Append(index);
            //            desc.Append(". <@");
            //            desc.Append(guildUser.Id);
            //            desc.Append(">, ");
            //            index++;
            //        }

            //        return Task.FromResult(new MessageContents(new EmbedBuilder().WithDescription(desc.ToString())));
            //    });

            //if (trackable.Limit != 0 && availableSlots > 0)
            //{
            //    List<IGuildUser> usersToAddAssignable = [];

            //    foreach (var user in usersToAdd)
            //    {
            //        Log.Debug("doing user {user}", user.Id);
            //        var userId = user.Id;

            //        var userAssignable = await assignableGuild.GetUserAsync(userId);

            //        if (userAssignable == null)
            //        {
            //            await LogUserNotInAssignableGuild(userId, trackable.AssignableGuild, trackable.LoggingChannel);
            //            continue;
            //        }

            //        usersToAddAssignable.Add(userAssignable);

            //        if (usersToAddAssignable.Count >= trackable.Limit)
            //            break;
            //    }

            //    usersToAdd = usersToAddAssignable;
            //}

            if (trackable.Limit == 0 || availableSlots > 0)
            {
                var totalTrackedUsers = 0;

                foreach (var user in usersToAdd)
                {
                    logger.LogTrace("Processing user {user}", user.Id);
                    var userId = user.Id;

                    var userAssignable = assignableGuild.GetUser(userId);

                    if (userAssignable == null)
                    {
                        await LogUserNotInAssignableGuild(userId, trackable.AssignableGuild, trackable.LoggingChannel, trackable.Id);
                        continue;
                    }

                    var newTrackedUser = new TrackedUser
                    {
                        Trackable = trackable,
                        UserId = user.Id
                    };

                    await TrackUser(context, newTrackedUser, trackable);
                    totalTrackedUsers++;

                    if (totalTrackedUsers >= trackable.Limit)
                        break;
                }
            }

            if (trackable.Limit != 0)
            {
                var skipped = usersAdded.Length - availableSlots;
                if (skipped > 0)
                {
                    logger.LogTrace("Skipped adding {skippedCount} users. potentialUsersToAdd: {potentialUsersToAdd}, availableSlots: {availableSlots}",
                        skipped, usersAdded.Length, availableSlots);
                    await TryLogToChannel(trackable.LoggingChannel, async () =>
                    {
                        var takenSlots = await GetTakenSlots(context, trackable);

                        var embed = new EmbedBuilder().WithDescription(
                            $"Skipped processing {skipped} users for trackable {trackable.Id} due to slots being full. ({takenSlots}/{trackable.Limit})")
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
        using var disposable1 = logger.BeginScope("Tracking User");
        using var disposable2 = logger.BeginScope("Trackable ID: {TrackableId}", trackable.Id);

        var assignableGuild = client.GetGuild(trackable.AssignableGuild);

        var loggingChannelId = trackable.LoggingChannel;

        if (assignableGuild == null)
        {
            logger.LogTrace("Attempted to track user {UserId} but Assignable Guild {guildId} was not found.",
                newTrackedUser.UserId, trackable.AssignableGuild);
            await TryLogToChannel(loggingChannelId,
                () => Task.FromResult(new MessageContents(new EmbedBuilder()
                    .WithDescription($"Attempted to track user <@{newTrackedUser.UserId}> but could not find Guild {trackable.AssignableGuild}."))));

            return false;
        }

        var user = assignableGuild.GetUser(newTrackedUser.UserId);

        if (user == null)
        {
            await LogUserNotInAssignableGuild(newTrackedUser.UserId, trackable.AssignableGuild, loggingChannelId, trackable.Id);

            return false;
        }

        if (assignableGuild.GetRole(newTrackedUser.Trackable.AssignableRole) == null)
        {
            logger.LogTrace("Attempted to track user {userId} but the assignable role {roleId} could not be found.",
                newTrackedUser.UserId, newTrackedUser.Trackable.AssignableRole);
            await TryLogToChannel(loggingChannelId,
                () => Task.FromResult(new MessageContents(new EmbedBuilder()
                    .WithDescription($"Attempted to track user <@{newTrackedUser.UserId}>, but could not find assignable role {trackable.AssignableRole}."))));
            return false;
        }

        context.TrackedUsers.Add(newTrackedUser);
        await user.AddRoleAsync(newTrackedUser.Trackable.AssignableRole);

        // not happy about running this here but im not sure how to get it to include the local cache in queries
        await context.SaveChangesAsync();

        var currentSlotUsage = await GetTakenSlots(context, trackable);

        logger.LogTrace("{userId} added to trackable {trackableId}. ({currentSlotUsage}/{slotLimit})",
            newTrackedUser.UserId, trackable.Id, currentSlotUsage, trackable.Limit);

        await TryLogToChannel(loggingChannelId, () =>
        {
            var embed = new EmbedBuilder()
                .WithDescription($"Added <@{newTrackedUser.UserId}> to trackable {trackable.Id}. ({currentSlotUsage}/{trackable.Limit})")
                .WithColor(0x2E8B57) // SeaGreen
                .WithAuthor(user);

            return Task.FromResult(new MessageContents(embed));
        });

        return true;
    }

    private async Task LogUserNotInAssignableGuild(ulong userId, ulong assignableGuild, ulong loggingChannelId, uint trackableId)
    {
        logger.LogTrace("Attempted to track user {userId} but they could not found within the guild {guildId}.",
            userId, assignableGuild);
        await TryLogToChannel(loggingChannelId,
            async () =>
            {
                var embed = new EmbedBuilder()
                    .WithDescription(
                        $"Attempted to track user <@{userId}>, but could not be found within Assignable Guild {assignableGuild}. (Trackable {trackableId})")
                    .WithColor(0xFF8C00);

                var user = client.GetUserAsync(userId);

                // null annotations trolling
                // ReSharper disable once ConditionIsAlwaysTrueOrFalse
#pragma warning disable CS8073 // The result of the expression is always the same since a value of this type is never equal to 'null'
                if (user != null)
                {
                    embed.WithAuthor(await client.GetUserAsync(userId));
                }
#pragma warning restore CS8073 // The result of the expression is always the same since a value of this type is never equal to 'null'

                return new MessageContents(embed);
            });
    }

    public async Task UntrackUser(BotDbContext context, TrackedUser trackedUser, Trackable trackable)
    {
        using var disposable1 = logger.BeginScope("Un-tracking User");
        using var disposable2 = logger.BeginScope("Trackable ID: {TrackableId}", trackable.Id);

        var assignableGuild = client.GetGuild(trackable.AssignableGuild);

        var loggingChannelId = trackable.LoggingChannel;

        context.TrackedUsers.Remove(trackedUser);
        var user = assignableGuild.GetUser(trackedUser.UserId);
        if (user != null)
            await user.RemoveRoleAsync(trackedUser.Trackable.AssignableRole);

        // see comment in TrackUser
        await context.SaveChangesAsync();

        var currentSlotUsage = await GetTakenSlots(context, trackable);

        logger.LogDebug("{userId} removed from trackable {trackableId}. ({currentSlotUsage}/{slotLimit})",
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
