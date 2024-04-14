using Zatsuyou.Database.Models;

namespace Zatsuyou.Database;

public static class TrackablesRelatedExtensions
{
    public static IQueryable<Trackable> GetScopedTrackables(this BotDbContext context, ulong guildId)
    {
        return context.Trackables.Where(x => x.MonitoredGuild == guildId || x.AssignableGuild == guildId);
    }
}