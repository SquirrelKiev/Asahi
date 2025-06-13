using System.Reflection;
using Asahi.BotEmoteManagement;
using Asahi.Database;
using Asahi.Modules;
using Asahi.Modules.BirthdayRoles;
using Asahi.Modules.BotManagement;
using Asahi.Modules.FeedsV2;
using Asahi.Modules.Highlights;
using Asahi.Modules.ModSpoilers;
using Asahi.Modules.Seigen;
using Asahi.Modules.Welcome;
using Discord.Commands;
using Discord.Interactions;
using Discord.WebSocket;
using Fergun.Interactive;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Asahi;

public class BotService(
    DiscordSocketClient client,
    BotConfig config,
    IDbContextFactory<BotDbContext> dbService,
    ILogger<BotService> logger,
    CommandHandler commandHandler,
    HighlightsTrackingService highlightsTrackingService,
    CustomStatusService customStatusService,
    ModSpoilerService modSpoilerService,
    WelcomeService welcomeService,
    FeedsTimerService feedsTimerService,
    InteractionService interactionService,
    InteractiveService interactiveService,
    CommandService commandService,
    // RoleManagementService roleManagementService,
    BirthdayTimerService birthdayTimerService,
    BotEmoteService botEmoteService,
    IHostApplicationLifetime appLifetime
) : BackgroundService
{
    public const string WebhookDefaultName =
#if DEBUG
        "[DEBUG] " +
#endif
        "Asahi Webhook";

    // ReSharper disable once InconsistentNaming
    private CancellationToken CancellationToken;

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        CancellationToken = cancellationToken;

        MessageContents.AddRedButtonDefault = false;

        var args = Environment.GetCommandLineArgs();

#if !DEBUG
        if (args.Contains("nomigrate"))
        {
            throw new InvalidOperationException("Disabling migration logic is disabled on release builds.");
        }

        if (args.Contains("nukedb"))
        {
            throw new InvalidOperationException("Nuking the DB is not allowed on release builds.");
        }
#endif

        // ReSharper disable once RedundantAssignment
        bool migrationEnabled = true;
#if DEBUG
        migrationEnabled = !(args.Contains("nomigrate") || args.Contains("nukedb"));
#endif
        await using var context = await dbService.CreateDbContextAsync(cancellationToken);
        await context.InitializeAsync(migrationEnabled);

#if DEBUG
        if (args.Contains("nukedb"))
        {
            logger.LogDebug("Nuking the DB...");

            await context.ResetDatabaseAsync();

            logger.LogDebug("Nuked!");
        }
#endif

        client.Log += Client_Log;

        client.Ready += Client_Ready;

        // could make these dynamic (reflection or smth) but the need hasn't appeared yet
        // client.GuildMemberUpdated += Client_GuildMemberUpdated;
        // client.UserLeft += Client_UserLeft;
        client.UserJoined += Client_UserJoined;
        client.ReactionAdded += Client_ReactionAdded;
        client.ReactionRemoved += Client_ReactionRemoved;
        client.MessageReceived += Client_MessageReceived;

        interactionService.Log += Client_Log;
        commandService.Log += Client_Log;
        interactiveService.Log += Client_Log;

        await client.LoginAsync(TokenType.Bot, config.BotToken);
        await client.StartAsync();

        await Task.Delay(-1, cancellationToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (ExecuteTask == null)
            return;

        await base.StopAsync(cancellationToken);

        // if (client.LoginState is LoginState.LoggedIn or LoginState.LoggingIn)
        await client.LogoutAsync();
        // if (client.ConnectionState is ConnectionState.Connected or ConnectionState.Connecting)
        await client.StopAsync();
    }

    // private Task Client_UserLeft(SocketGuild guild, SocketUser user) =>
    //     roleManagementService.OnUserLeft(guild, user);

    private async Task Client_UserJoined(SocketGuildUser user)
    {
        await welcomeService.OnUserJoined(user);

        // await roleManagementService.OnUserJoined(user);
    }

    // private async Task Client_GuildMemberUpdated(
    //     Cacheable<SocketGuildUser, ulong> cacheable,
    //     SocketGuildUser user
    // )
    // {
    //     if (!cacheable.HasValue)
    //         return;
    //
    //     if (!user.Roles.SequenceEqual(cacheable.Value.Roles))
    //     {
    //         await roleManagementService.OnUserRolesUpdated(cacheable, user);
    //     }
    // }

    private async Task Client_ReactionAdded(
        Cacheable<IUserMessage, ulong> cachedMessage,
        Cacheable<IMessageChannel, ulong> originChannel,
        SocketReaction reaction
    )
    {
        if (reaction.User.IsSpecified && reaction.User.Value.IsBot)
            return;

        if (reaction.Channel is not SocketTextChannel channel)
            return;

        await using var context = await dbService.CreateDbContextAsync(CancellationToken);
        var guildConfig = await context.GetGuildConfig(channel.Guild.Id);

        if (
            !QuotingHelpers.TryParseEmote(guildConfig.SpoilerReactionEmote, out var spoilerEmote)
            || !spoilerEmote.Equals(reaction.Emote)
        )
        {
            highlightsTrackingService.QueueMessage(
                new HighlightsTrackingService.QueuedMessage(
                    channel.Guild.Id,
                    channel.Id,
                    cachedMessage.Id
                ),
                true
            );
        }

        _ = Task.Run(() => modSpoilerService.ReactionCheck(reaction), CancellationToken);
    }

    private async Task Client_ReactionRemoved(
        Cacheable<IUserMessage, ulong> cachedMessage,
        Cacheable<IMessageChannel, ulong> originChannel,
        SocketReaction reaction
    )
    {
        if (reaction.User.IsSpecified && reaction.User.Value.IsBot)
            return;

        if (reaction.Channel is not SocketTextChannel channel)
            return;

        await using var context = await dbService.CreateDbContextAsync(CancellationToken);
        var guildConfig = await context.GetGuildConfig(channel.Guild.Id);

        if (
            !QuotingHelpers.TryParseEmote(guildConfig.SpoilerReactionEmote, out var spoilerEmote)
            || !spoilerEmote.Equals(reaction.Emote)
        )
        {
            highlightsTrackingService.QueueMessage(
                new HighlightsTrackingService.QueuedMessage(
                    channel.Guild.Id,
                    channel.Id,
                    cachedMessage.Id
                ),
                false
            );
        }
    }

    private Task Client_MessageReceived(SocketMessage msg)
    {
        if (msg.Channel is not SocketGuildChannel)
            return Task.CompletedTask;

        highlightsTrackingService.AddMessageToCache(msg);

        return Task.CompletedTask;
    }

    private Task Client_Log(LogMessage message)
    {
        return Client_Log(logger, message);
    }

    public static Task Client_Log(ILogger logger, LogMessage message)
    {
        var level = message.Severity switch
        {
            LogSeverity.Critical => LogLevel.Critical,
            LogSeverity.Error => LogLevel.Error,
            LogSeverity.Warning => LogLevel.Warning,
            LogSeverity.Info => LogLevel.Information,
            LogSeverity.Verbose => LogLevel.Trace,
            LogSeverity.Debug => LogLevel.Debug,
            _ => LogLevel.Information,
        };

        if (message.Exception is not null)
        {
            if (message.Exception.GetType() == typeof(GatewayReconnectException))
            {
                logger.Log(
                    LogLevel.Trace,
                    message.Exception,
                    "{Source} | {Message}",
                    message.Source,
                    message.Exception.Message
                );
            }
            else
            {
                logger.Log(
                    level,
                    message.Exception,
                    "{Source} | {Message}",
                    message.Source,
                    message.Message
                );
            }
        }
        else
        {
            logger.Log(level, "{Source} | {Message}", message.Source, message.Message);
        }

        return Task.CompletedTask;
    }

    private async Task Client_Ready()
    {
        logger.LogInformation(
            "Logged in as {user}#{discriminator} ({id})",
            client.CurrentUser?.Username,
            client.CurrentUser?.Discriminator,
            client.CurrentUser?.Id
        );

        await SyncEmotesAsync(CancellationToken);

        await commandHandler.OnReady(Assembly.GetExecutingAssembly());

        highlightsTrackingService.StartBackgroundTask(CancellationToken);

        customStatusService.StartBackgroundTask(CancellationToken);

        birthdayTimerService.StartBackgroundTask(CancellationToken);

        feedsTimerService.StartBackgroundTask(CancellationToken);

        // await roleManagementService.CacheAndResolve();
    }

    private async Task SyncEmotesAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(BotConfigFactory.BotInternalEmotesDirectory);

        await using var context = await dbService.CreateDbContextAsync(cancellationToken);
        var emoteTracking = await context.InternalCustomEmoteTracking.ToListAsync(cancellationToken: cancellationToken);
        var originalEmoteTracking = new List<InternalCustomEmoteTracking>(emoteTracking);

        try
        {
            await botEmoteService.InitializeAsync(config.Emotes, emoteTracking);
            
            var removed = originalEmoteTracking.Except(emoteTracking);
            var added = emoteTracking.Except(originalEmoteTracking);
        
            foreach (var e in removed)
            {
                context.InternalCustomEmoteTracking.Remove(e);
            }
            
            foreach (var e in added)
            {
                context.InternalCustomEmoteTracking.Add(e);
            }
        }
        catch (Exception e)
        {
            logger.LogCritical(e, "Failed to initialize bot emotes!");
        
            appLifetime.StopApplication();
            throw;
        }

        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Synced emotes.");
    }
}
