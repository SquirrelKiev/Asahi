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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TrackedUser>()
            .HasIndex(x => new { x.TrackableId, x.UserId })
            .IsUnique();

        modelBuilder.Entity<CachedUserRole>().HasNoKey();

        modelBuilder.Entity<CachedUserRole>()
            .HasIndex(x => x.RoleId);

        modelBuilder.Entity<CachedUserRole>()
            .HasIndex(x => new { x.RoleId, x.UserId })
            .IsUnique();

        modelBuilder.Entity<Trackable>()
            .HasIndex(x => new { x.AssignableRole, x.MonitoredRole })
            .IsUnique();
    }
}