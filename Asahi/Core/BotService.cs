using System.Reflection;
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
using Discord.Rest;
using Discord.WebSocket;
using Fergun.Interactive;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Asahi;

public class BotService(
    DiscordSocketClient client,
    BotConfig config,
    IDbService dbService,
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
    RoleManagementService roleManagementService,
    BirthdayTimerService birthdayTimerService
) : BackgroundService
{
    public const string WebhookDefaultName =
#if DEBUG
        "[DEBUG] " +
#endif
        "Asahi Webhook";

    public CancellationTokenSource cts = new();

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        MessageContents.AddRedButtonDefault = false;

        var args = Environment.GetCommandLineArgs();
        var migrationEnabled = !(args.Contains("nomigrate") || args.Contains("nukedb"));
        await dbService.Initialize(migrationEnabled);

#if DEBUG
        if (Environment.GetCommandLineArgs().Contains("nukedb"))
        {
            logger.LogDebug("Nuking the DB...");

            await dbService.ResetDatabase();

            logger.LogDebug("Nuked!");
        }
#endif

        client.Log += Client_Log;

        client.Ready += Client_Ready;

        // could make these dynamic (reflection or smth) but the need hasn't appeared yet so
        client.GuildMemberUpdated += Client_GuildMemberUpdated;
        client.UserLeft += Client_UserLeft;
        client.UserJoined += Client_UserJoined;
        client.ReactionAdded += Client_ReactionAdded;
        client.ReactionRemoved += Client_ReactionRemoved;
        client.MessageReceived += Client_MessageReceived;

        interactionService.Log += Client_Log;
        commandService.Log += Client_Log;
        interactiveService.Log += Client_Log;

        await client.LoginAsync(TokenType.Bot, config.BotToken);
        await client.StartAsync();
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (ExecuteTask == null)
            return;

        await cts.CancelAsync();

        await client.LogoutAsync();
        await client.StopAsync();

        await base.StopAsync(cancellationToken);
    }

    private Task Client_UserLeft(SocketGuild guild, SocketUser user) =>
        roleManagementService.OnUserLeft(guild, user);

    private async Task Client_UserJoined(SocketGuildUser user)
    {
        await welcomeService.OnUserJoined(user);

        await roleManagementService.OnUserJoined(user);
    }

    private async Task Client_GuildMemberUpdated(
        Cacheable<SocketGuildUser, ulong> cacheable,
        SocketGuildUser user
    )
    {
        if (!cacheable.HasValue)
            return;

        if (!user.Roles.SequenceEqual(cacheable.Value.Roles))
        {
            await roleManagementService.OnUserRolesUpdated(cacheable, user);
        }
    }

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

        await using var context = dbService.GetDbContext();
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

        _ = Task.Run(() => modSpoilerService.ReactionCheck(reaction));
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

        await using var context = dbService.GetDbContext();
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

        await commandHandler.OnReady(Assembly.GetExecutingAssembly());

        highlightsTrackingService.StartBackgroundTask(cts.Token);

        customStatusService.StartBackgroundTask(cts.Token);

        birthdayTimerService.StartBackgroundTask(cts.Token);

        feedsTimerService.StartBackgroundTask(cts.Token);

        await roleManagementService.CacheAndResolve();
    }
}
