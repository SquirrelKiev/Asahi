﻿// <auto-generated />
using Asahi.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NodaTime;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Asahi.Migrations.Postgres.Migrations
{
    [DbContext(typeof(BotDbContext))]
    [Migration("20240617051424_RssFeeds")]
    partial class RssFeeds
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "8.0.6")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("Asahi.Database.Models.BirthdayConfig", b =>
                {
                    b.Property<string>("Name")
                        .HasMaxLength(32)
                        .HasColumnType("character varying(32)");

                    b.Property<decimal>("GuildId")
                        .HasColumnType("numeric(20,0)");

                    b.Property<decimal[]>("AllowedRoles")
                        .IsRequired()
                        .HasColumnType("numeric(20,0)[]");

                    b.Property<decimal>("BirthdayRole")
                        .HasColumnType("numeric(20,0)");

                    b.Property<string>("DeniedForReasonEditWindowText")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)");

                    b.Property<string>("DeniedForReasonPermissionsText")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)");

                    b.Property<decimal[]>("DeniedRoles")
                        .IsRequired()
                        .HasColumnType("numeric(20,0)[]");

                    b.Property<string>("DisplayName")
                        .IsRequired()
                        .HasMaxLength(32)
                        .HasColumnType("character varying(32)");

                    b.Property<int>("EditWindowSeconds")
                        .HasColumnType("integer");

                    b.Property<int>("EmbedColorSource")
                        .HasColumnType("integer");

                    b.Property<string>("EmbedDescriptionText")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)");

                    b.Property<string>("EmbedFooterText")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)");

                    b.Property<string>("EmbedTitleText")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)");

                    b.Property<long>("FallbackEmbedColor")
                        .HasColumnType("bigint");

                    b.HasKey("Name", "GuildId");

                    b.ToTable("BirthdayConfigs");
                });

            modelBuilder.Entity("Asahi.Database.Models.BirthdayEntry", b =>
                {
                    b.Property<decimal>("UserId")
                        .HasColumnType("numeric(20,0)");

                    b.Property<decimal>("BirthdayConfigGuildId")
                        .HasColumnType("numeric(20,0)");

                    b.Property<string>("BirthdayConfigName")
                        .HasColumnType("character varying(32)");

                    b.Property<int>("Day")
                        .HasColumnType("integer");

                    b.Property<int>("Month")
                        .HasColumnType("integer");

                    b.Property<LocalDateTime>("TimeCreatedUtc")
                        .HasColumnType("timestamp without time zone");

                    b.Property<string>("TimeZone")
                        .IsRequired()
                        .HasMaxLength(32)
                        .HasColumnType("character varying(32)");

                    b.HasKey("UserId", "BirthdayConfigGuildId", "BirthdayConfigName");

                    b.HasIndex("BirthdayConfigName", "BirthdayConfigGuildId");

                    b.ToTable("Birthdays");
                });

            modelBuilder.Entity("Asahi.Database.Models.BotWideConfig", b =>
                {
                    b.Property<decimal>("BotId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("numeric(20,0)");

                    b.Property<string>("ActivityStreamingUrl")
                        .IsRequired()
                        .HasMaxLength(128)
                        .HasColumnType("character varying(128)");

                    b.Property<int>("ActivityType")
                        .HasColumnType("integer");

                    b.Property<string>("BotActivity")
                        .IsRequired()
                        .HasMaxLength(128)
                        .HasColumnType("character varying(128)");

                    b.Property<bool>("ShouldHaveActivity")
                        .HasColumnType("boolean");

                    b.Property<int>("UserStatus")
                        .HasColumnType("integer");

                    b.HasKey("BotId");

                    b.ToTable("BotWideConfig");
                });

            modelBuilder.Entity("Asahi.Database.Models.CachedHighlightedMessage", b =>
                {
                    b.Property<long>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bigint");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<long>("Id"));

                    b.Property<decimal>("HighlightBoardGuildId")
                        .HasColumnType("numeric(20,0)");

                    b.Property<string>("HighlightBoardName")
                        .IsRequired()
                        .HasColumnType("character varying(32)");

                    b.Property<decimal[]>("HighlightMessageIds")
                        .IsRequired()
                        .HasColumnType("numeric(20,0)[]");

                    b.Property<decimal>("OriginalMessageChannelId")
                        .HasColumnType("numeric(20,0)");

                    b.Property<decimal>("OriginalMessageId")
                        .HasColumnType("numeric(20,0)");

                    b.HasKey("Id");

                    b.HasIndex("HighlightMessageIds")
                        .IsUnique();

                    b.HasIndex("HighlightBoardGuildId", "HighlightBoardName");

                    b.ToTable("CachedHighlightedMessages");
                });

            modelBuilder.Entity("Asahi.Database.Models.CachedUserRole", b =>
                {
                    b.Property<decimal>("RoleId")
                        .HasColumnType("numeric(20,0)");

                    b.Property<decimal>("UserId")
                        .HasColumnType("numeric(20,0)");

                    b.Property<decimal>("GuildId")
                        .HasColumnType("numeric(20,0)");

                    b.HasKey("RoleId", "UserId");

                    b.HasIndex("RoleId");

                    b.ToTable("CachedUsersRoles");
                });

            modelBuilder.Entity("Asahi.Database.Models.CustomCommand", b =>
                {
                    b.Property<long>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bigint");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<long>("Id"));

                    b.Property<string>("Contents")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<decimal>("GuildId")
                        .HasColumnType("numeric(20,0)");

                    b.Property<bool>("IsRaw")
                        .HasColumnType("boolean");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<decimal>("OwnerId")
                        .HasColumnType("numeric(20,0)");

                    b.HasKey("Id");

                    b.ToTable("CustomCommands");
                });

            modelBuilder.Entity("Asahi.Database.Models.EmoteAlias", b =>
                {
                    b.Property<decimal>("GuildId")
                        .HasColumnType("numeric(20,0)");

                    b.Property<string>("EmoteName")
                        .HasMaxLength(32)
                        .HasColumnType("character varying(32)");

                    b.Property<string>("EmoteReplacement")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)");

                    b.HasKey("GuildId", "EmoteName");

                    b.ToTable("EmoteAliases");
                });

            modelBuilder.Entity("Asahi.Database.Models.GuildConfig", b =>
                {
                    b.Property<decimal>("GuildId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("numeric(20,0)");

                    b.Property<decimal?>("DefaultBirthdayConfigGuildId")
                        .HasColumnType("numeric(20,0)");

                    b.Property<string>("DefaultBirthdayConfigName")
                        .HasColumnType("character varying(32)");

                    b.Property<string>("Prefix")
                        .IsRequired()
                        .HasMaxLength(8)
                        .HasColumnType("character varying(8)");

                    b.Property<bool>("SpoilerBotAutoDeleteContextSetting")
                        .HasColumnType("boolean");

                    b.Property<bool>("SpoilerBotAutoDeleteOriginal")
                        .HasColumnType("boolean");

                    b.Property<string>("SpoilerReactionEmote")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)");

                    b.HasKey("GuildId");

                    b.HasIndex("DefaultBirthdayConfigName", "DefaultBirthdayConfigGuildId");

                    b.ToTable("GuildConfigs");
                });

            modelBuilder.Entity("Asahi.Database.Models.HighlightBoard", b =>
                {
                    b.Property<decimal>("GuildId")
                        .HasColumnType("numeric(20,0)");

                    b.Property<string>("Name")
                        .HasMaxLength(32)
                        .HasColumnType("character varying(32)");

                    b.Property<int>("AutoReactEmoteChoicePreference")
                        .HasColumnType("integer");

                    b.Property<string>("AutoReactFallbackEmoji")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)");

                    b.Property<int>("AutoReactMaxAttempts")
                        .HasColumnType("integer");

                    b.Property<int>("AutoReactMaxReactions")
                        .HasColumnType("integer");

                    b.Property<int>("EmbedColorSource")
                        .HasColumnType("integer");

                    b.Property<long>("FallbackEmbedColor")
                        .HasColumnType("bigint");

                    b.Property<bool>("FilterSelfReactions")
                        .HasColumnType("boolean");

                    b.Property<decimal[]>("FilteredChannels")
                        .IsRequired()
                        .HasColumnType("numeric(20,0)[]");

                    b.Property<bool>("FilteredChannelsIsBlockList")
                        .HasColumnType("boolean");

                    b.Property<decimal>("HighlightsMuteRole")
                        .HasColumnType("numeric(20,0)");

                    b.Property<decimal>("LoggingChannelId")
                        .HasColumnType("numeric(20,0)");

                    b.Property<int>("MaxMessageAgeSeconds")
                        .HasColumnType("integer");

                    b.HasKey("GuildId", "Name");

                    b.ToTable("HighlightBoards");
                });

            modelBuilder.Entity("Asahi.Database.Models.HighlightThreshold", b =>
                {
                    b.Property<decimal>("OverrideId")
                        .HasColumnType("numeric(20,0)");

                    b.Property<decimal>("HighlightBoardGuildId")
                        .HasColumnType("numeric(20,0)");

                    b.Property<string>("HighlightBoardName")
                        .HasColumnType("character varying(32)");

                    b.Property<int>("BaseThreshold")
                        .HasColumnType("integer");

                    b.Property<int>("HighActivityMessageLookBack")
                        .HasColumnType("integer");

                    b.Property<int>("HighActivityMessageMaxAgeSeconds")
                        .HasColumnType("integer");

                    b.Property<float>("HighActivityMultiplier")
                        .HasColumnType("real");

                    b.Property<int>("MaxThreshold")
                        .HasColumnType("integer");

                    b.Property<float>("RoundingThreshold")
                        .HasColumnType("real");

                    b.Property<int>("UniqueUserDecayDelaySeconds")
                        .HasColumnType("integer");

                    b.Property<int>("UniqueUserMessageMaxAgeSeconds")
                        .HasColumnType("integer");

                    b.Property<float>("UniqueUserMultiplier")
                        .HasColumnType("real");

                    b.HasKey("OverrideId", "HighlightBoardGuildId", "HighlightBoardName");

                    b.HasIndex("HighlightBoardGuildId", "HighlightBoardName");

                    b.ToTable("HighlightThreshold");
                });

            modelBuilder.Entity("Asahi.Database.Models.LoggingChannelOverride", b =>
                {
                    b.Property<decimal>("OverriddenChannelId")
                        .HasColumnType("numeric(20,0)");

                    b.Property<decimal>("HighlightBoardGuildId")
                        .HasColumnType("numeric(20,0)");

                    b.Property<string>("HighlightBoardName")
                        .HasColumnType("character varying(32)");

                    b.Property<decimal>("LoggingChannelId")
                        .HasColumnType("numeric(20,0)");

                    b.HasKey("OverriddenChannelId", "HighlightBoardGuildId", "HighlightBoardName");

                    b.HasIndex("HighlightBoardGuildId", "HighlightBoardName");

                    b.ToTable("LoggingChannelOverride");
                });

            modelBuilder.Entity("Asahi.Database.Models.Rss.RssFeedListener", b =>
                {
                    b.Property<long>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bigint");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<long>("Id"));

                    b.Property<decimal>("ChannelId")
                        .HasColumnType("numeric(20,0)");

                    b.Property<string>("FeedUrl")
                        .IsRequired()
                        .HasMaxLength(512)
                        .HasColumnType("character varying(512)");

                    b.Property<decimal>("GuildId")
                        .HasColumnType("numeric(20,0)");

                    b.HasKey("Id");

                    b.ToTable("RssFeedListeners");
                });

            modelBuilder.Entity("Asahi.Database.Models.SpoilerChannel", b =>
                {
                    b.Property<decimal>("ChannelId")
                        .HasColumnType("numeric(20,0)");

                    b.Property<decimal>("HighlightBoardGuildId")
                        .HasColumnType("numeric(20,0)");

                    b.Property<string>("HighlightBoardName")
                        .HasColumnType("character varying(32)");

                    b.Property<string>("SpoilerContext")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)");

                    b.HasKey("ChannelId", "HighlightBoardGuildId", "HighlightBoardName");

                    b.HasIndex("HighlightBoardGuildId", "HighlightBoardName");

                    b.ToTable("SpoilerChannel");
                });

            modelBuilder.Entity("Asahi.Database.Models.Trackable", b =>
                {
                    b.Property<long>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bigint");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<long>("Id"));

                    b.Property<decimal>("AssignableGuild")
                        .HasColumnType("numeric(20,0)");

                    b.Property<decimal>("AssignableRole")
                        .HasColumnType("numeric(20,0)");

                    b.Property<long>("Limit")
                        .HasColumnType("bigint");

                    b.Property<decimal>("LoggingChannel")
                        .HasColumnType("numeric(20,0)");

                    b.Property<decimal>("MonitoredGuild")
                        .HasColumnType("numeric(20,0)");

                    b.Property<decimal>("MonitoredRole")
                        .HasColumnType("numeric(20,0)");

                    b.HasKey("Id");

                    b.HasIndex("AssignableRole", "MonitoredRole")
                        .IsUnique();

                    b.ToTable("Trackables");
                });

            modelBuilder.Entity("Asahi.Database.Models.TrackedUser", b =>
                {
                    b.Property<long>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bigint");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<long>("Id"));

                    b.Property<long>("TrackableId")
                        .HasColumnType("bigint");

                    b.Property<decimal>("UserId")
                        .HasColumnType("numeric(20,0)");

                    b.HasKey("Id");

                    b.HasIndex("TrackableId", "UserId")
                        .IsUnique();

                    b.ToTable("TrackedUsers");
                });

            modelBuilder.Entity("Asahi.Database.Models.TrustedId", b =>
                {
                    b.Property<decimal>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("numeric(20,0)");

                    b.Property<decimal>("BotWideConfigBotId")
                        .HasColumnType("numeric(20,0)");

                    b.Property<string>("Comment")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)");

                    b.Property<int>("PermissionFlags")
                        .HasColumnType("integer");

                    b.HasKey("Id");

                    b.HasIndex("BotWideConfigBotId");

                    b.ToTable("TrustedIds");
                });

            modelBuilder.Entity("Asahi.Database.Models.BirthdayEntry", b =>
                {
                    b.HasOne("Asahi.Database.Models.BirthdayConfig", "BirthdayConfig")
                        .WithMany()
                        .HasForeignKey("BirthdayConfigName", "BirthdayConfigGuildId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("BirthdayConfig");
                });

            modelBuilder.Entity("Asahi.Database.Models.CachedHighlightedMessage", b =>
                {
                    b.HasOne("Asahi.Database.Models.HighlightBoard", "HighlightBoard")
                        .WithMany("HighlightedMessages")
                        .HasForeignKey("HighlightBoardGuildId", "HighlightBoardName")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("HighlightBoard");
                });

            modelBuilder.Entity("Asahi.Database.Models.GuildConfig", b =>
                {
                    b.HasOne("Asahi.Database.Models.BirthdayConfig", "DefaultBirthdayConfig")
                        .WithMany()
                        .HasForeignKey("DefaultBirthdayConfigName", "DefaultBirthdayConfigGuildId");

                    b.Navigation("DefaultBirthdayConfig");
                });

            modelBuilder.Entity("Asahi.Database.Models.HighlightThreshold", b =>
                {
                    b.HasOne("Asahi.Database.Models.HighlightBoard", "HighlightBoard")
                        .WithMany("Thresholds")
                        .HasForeignKey("HighlightBoardGuildId", "HighlightBoardName")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("HighlightBoard");
                });

            modelBuilder.Entity("Asahi.Database.Models.LoggingChannelOverride", b =>
                {
                    b.HasOne("Asahi.Database.Models.HighlightBoard", "HighlightBoard")
                        .WithMany("LoggingChannelOverrides")
                        .HasForeignKey("HighlightBoardGuildId", "HighlightBoardName")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("HighlightBoard");
                });

            modelBuilder.Entity("Asahi.Database.Models.SpoilerChannel", b =>
                {
                    b.HasOne("Asahi.Database.Models.HighlightBoard", "HighlightBoard")
                        .WithMany("SpoilerChannels")
                        .HasForeignKey("HighlightBoardGuildId", "HighlightBoardName")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("HighlightBoard");
                });

            modelBuilder.Entity("Asahi.Database.Models.TrackedUser", b =>
                {
                    b.HasOne("Asahi.Database.Models.Trackable", "Trackable")
                        .WithMany()
                        .HasForeignKey("TrackableId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Trackable");
                });

            modelBuilder.Entity("Asahi.Database.Models.TrustedId", b =>
                {
                    b.HasOne("Asahi.Database.Models.BotWideConfig", "BotWideConfig")
                        .WithMany("TrustedIds")
                        .HasForeignKey("BotWideConfigBotId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("BotWideConfig");
                });

            modelBuilder.Entity("Asahi.Database.Models.BotWideConfig", b =>
                {
                    b.Navigation("TrustedIds");
                });

            modelBuilder.Entity("Asahi.Database.Models.HighlightBoard", b =>
                {
                    b.Navigation("HighlightedMessages");

                    b.Navigation("LoggingChannelOverrides");

                    b.Navigation("SpoilerChannels");

                    b.Navigation("Thresholds");
                });
#pragma warning restore 612, 618
        }
    }
}
