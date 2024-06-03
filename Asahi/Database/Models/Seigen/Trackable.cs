using System.Diagnostics.CodeAnalysis;
using BotBase.Database;

namespace Asahi.Database.Models;

public class Trackable : DbModel
{
    public required ulong MonitoredGuild { get; set; }

    public required ulong MonitoredRole { get; set; }

    public required ulong AssignableGuild { get; set; }

    public required ulong AssignableRole { get; set; }

    /// <remarks>0 = no channel</remarks>
    public ulong LoggingChannel { get; set; }

    /// <remarks>0 == No limit</remarks>
    public required uint Limit { get; set; }

    [SetsRequiredMembers]
    public Trackable()
    { }

    public string ToDisplayableString(IGuild? monitoredGuild, IGuild? assignableGuild)
    {
        return
            $"**Monitored Guild:** {monitoredGuild?.Name} ({MonitoredGuild})\n" +
            $"**Monitored Role:** {monitoredGuild?.GetRole(MonitoredRole)?.Name} ({MonitoredRole})\n" +
            $"**Assignable Guild:** {assignableGuild?.Name} ({AssignableGuild})\n" +
            $"**Assignable Role:** {assignableGuild?.GetRole(AssignableRole)?.Name} ({AssignableRole})\n" +
            $"**Limit**: {Limit}";
    }
}