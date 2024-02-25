using System.Diagnostics.CodeAnalysis;
using BotBase.Database;

namespace Seigen.Database.Models;

public class Trackable : DbModel
{
    public required ulong MonitoredGuild { get; set; }

    public required ulong MonitoredRole { get; set; }

    public required ulong AssignableGuild { get; set; }

    public required ulong AssignableRole { get; set; }

    /// <remarks>0 == No limit</remarks>
    public required uint Limit { get; set; }

    [SetsRequiredMembers]
    public Trackable()
    { }
}