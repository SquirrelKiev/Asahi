﻿using Asahi.BotEmoteManagement;
using Asahi.Database.Models;
using Asahi.Database.Models.Rss;
using Microsoft.EntityFrameworkCore;

namespace Asahi.Database;

public class BotDbContext(DbContextOptions<BotDbContext> contextOptions) : DbContext(contextOptions)
{
    public DbSet<GuildConfig> GuildConfigs { get; set; }

    public DbSet<Trackable> Trackables { get; set; }
    public DbSet<TrackedUser> TrackedUsers { get; set; }
    public DbSet<CachedUserRole> CachedUsersRoles { get; set; }

    public DbSet<CustomCommand> CustomCommands { get; set; }

    public DbSet<HighlightBoard> HighlightBoards { get; set; }
    public DbSet<CachedHighlightedMessage> CachedHighlightedMessages { get; set; }

    public IQueryable<CachedHighlightedMessage> CachedHighlightedMessagesWithStats =>
        CachedHighlightedMessages.Where(x => x.Version == CachedHighlightedMessage.LatestVersion);
    
    public DbSet<CachedMessageReaction> CachedMessageReactions { get; set; }
    public DbSet<EmoteAlias> EmoteAliases { get; set; }

    public DbSet<BotWideConfig> BotWideConfig { get; set; }
    // This property is unused but kept around so the name is kept.
    public DbSet<TrustedId> TrustedIds { get; set; }

    public DbSet<BirthdayConfig> BirthdayConfigs { get; set; }
    public DbSet<BirthdayEntry> Birthdays { get; set; }

    public DbSet<FeedListener> RssFeedListeners { get; set; }
    
    public DbSet<InternalCustomEmoteTracking> InternalCustomEmoteTracking { get; set; }

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

        modelBuilder.Entity<CachedHighlightedMessage>()
            .HasIndex(x => x.HighlightMessageIds)
            .IsUnique();

        modelBuilder.Entity<CachedMessageReaction>()
            .HasKey(x => new { x.EmoteName, x.EmoteId, x.HighlightedMessageId });

        modelBuilder.Entity<HighlightThreshold>()
            .HasKey(
                nameof(HighlightThreshold.OverrideId),
                $"{nameof(HighlightThreshold.HighlightBoard)}{nameof(HighlightBoard.GuildId)}",
                $"{nameof(HighlightThreshold.HighlightBoard)}{nameof(HighlightBoard.Name)}");

        modelBuilder.Entity<SpoilerChannel>()
            .HasKey(nameof(SpoilerChannel.ChannelId),
                $"{nameof(SpoilerChannel.HighlightBoard)}{nameof(HighlightBoard.GuildId)}",
                $"{nameof(SpoilerChannel.HighlightBoard)}{nameof(HighlightBoard.Name)}");

        modelBuilder.Entity<LoggingChannelOverride>()
            .HasKey(nameof(LoggingChannelOverride.OverriddenChannelId),
                $"{nameof(LoggingChannelOverride.HighlightBoard)}{nameof(HighlightBoard.GuildId)}",
                $"{nameof(LoggingChannelOverride.HighlightBoard)}{nameof(HighlightBoard.Name)}");

        modelBuilder.Entity<BirthdayConfig>()
            .HasKey(x => new { x.Name, x.GuildId });

        modelBuilder.Entity<BirthdayEntry>()
            .HasKey(nameof(BirthdayEntry.UserId),
                $"{nameof(BirthdayEntry.BirthdayConfig)}{nameof(BirthdayConfig.GuildId)}",
                $"{nameof(BirthdayEntry.BirthdayConfig)}{nameof(BirthdayConfig.Name)}");
    }
}
