using Seigen.Database.Models;

namespace Seigen.Database;

public static class TrackablesRelatedExtensions
{
    public static IQueryable<Trackable> GetScopedTrackables(this BotDbContext context, ulong guildId)
    {
        return context.Trackables.Where(x => x.GuildToMonitor == guildId || x.GuildToAssignIn == guildId);
    }
}