using BotBase.Database;
using Microsoft.EntityFrameworkCore;
using Seigen.Database.Models;

namespace Seigen.Database;

public abstract class BotDbContext : BotDbContextBase
{
    public DbSet<Trackable> Trackables { get; set; }
    public DbSet<TrackedUser> TrackedUsers { get; set; }
    public DbSet<CachedUserRole> CachedUsersRoles { get; set; }

    protected BotDbContext(string connectionString) : base(connectionString)
    {
    }
}