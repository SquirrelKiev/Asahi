using System.Diagnostics.CodeAnalysis;
using BotBase.Database;

namespace Seigen.Database.Models;

public class Trackable : DbModel
{
    public required ulong GuildToMonitor { get; set; }

    public required ulong GuildToAssignIn { get; set; }

    public required ulong RoleToMonitor { get; set; }

    public required ulong RoleToAssign { get; set; }

    /// <remarks>0 == No limit</remarks>
    public required uint Limit { get; set; }

    // :tomfoolery: (the required is there to make DbModel happy)
    [SetsRequiredMembers]
    public Trackable()
    { }
}