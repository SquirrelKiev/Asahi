using BotBase.Database;
using Microsoft.EntityFrameworkCore;
using Zatsuyou.Database.Models;

namespace Zatsuyou.Database;

public abstract class BotDbContext(string connectionString) : BotDbContextBase(connectionString)
{
    public DbSet<GuildConfig> GuildConfigs { get; set; }

    public DbSet<Trackable> Trackables { get; set; }
    public DbSet<TrackedUser> TrackedUsers { get; set; }
    public DbSet<CachedUserRole> CachedUsersRoles { get; set; }

    public DbSet<CustomCommand> CustomCommands { get; set; }

    public DbSet<HighlightBoard> HighlightBoards { get; set; }
    public DbSet<CachedHighlightedMessage> CachedHighlightedMessages { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TrackedUser>()
            .HasIndex(x => new { x.TrackableId, x.UserId })
            .IsUnique();

        modelBuilder.Entity<CachedUserRole>()
            .HasKey(x => new { x.RoleId, x.UserId });

        modelBuilder.Entity<CachedUserRole>()
            .HasIndex(x => x.RoleId);

        modelBuilder.Entity<Trackable>()
            .HasIndex(x => new { x.AssignableRole, x.MonitoredRole })
            .IsUnique();

        modelBuilder.Entity<HighlightBoard>()
            .HasKey(x => new { x.GuildId, x.Name });

        var hmc = modelBuilder.Entity<CachedHighlightedMessage>();

        //hmc.HasIndex(x => new { x.HighlightBoard, x.OriginalMessageId })
        //    .IsUnique();

        hmc.HasIndex(x => x.HighlightMessageId)
            .IsUnique();
    }
}
