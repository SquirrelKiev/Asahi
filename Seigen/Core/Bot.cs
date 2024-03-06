using System.Reflection;
using BotBase.Database;
using BotBase.Modules.About;
using BotBase.Modules.Help;
using Discord.Commands;
using Discord.WebSocket;
using Seigen.Database;
using Seigen.Modules.RoleManagement;
using Serilog.Events;

namespace Seigen;

public class Bot
{
    public DiscordSocketClient Client { get; }
    public InteractionService InteractionService { get; }
    public CommandService CommandService { get; }
    public BotConfig Config { get; }

    private readonly IServiceProvider services;

    public Bot(BotConfig config)
    {
        Config = config;

        Client = new DiscordSocketClient(new DiscordSocketConfig
        {
            GatewayIntents = GatewayIntents.Guilds |
                             GatewayIntents.MessageContent |
                             GatewayIntents.GuildMessages |
                             GatewayIntents.DirectMessages |
                             GatewayIntents.GuildMembers,
            LogLevel = LogSeverity.Verbose,
            AlwaysDownloadUsers = true
        });
        InteractionService = new InteractionService(Client, new InteractionServiceConfig()
        {
            LogLevel = LogSeverity.Verbose,
            DefaultRunMode = Discord.Interactions.RunMode.Async
        });
        CommandService = new CommandService(new CommandServiceConfig
        {
            LogLevel = LogSeverity.Verbose,
            DefaultRunMode = Discord.Commands.RunMode.Async
        });

        services = CreateServices();
        Log.Information("Services created.");
    }

    private ServiceProvider CreateServices()
    {
        var collection = new ServiceCollection()
                .AddCache(Config)
                .AddSingleton<BotConfigBase>(Config)
                .AddSingleton(Config)
                .AddSingleton(Client)
                .AddSingleton<IDiscordClient>(Client)
                .AddSingleton(InteractionService)
                .AddSingleton(CommandService)
                .AddSingleton<DbService>()
                .AddSingleton(x => (DbServiceBase<BotDbContext>)x.GetService<DbService>()!)
                .AddSingleton<CommandHandler, DbCommandHandler<BotDbContext>>()
                // for help command
                .AddSingleton<OverrideTrackerService>()
                .AddSingleton<HelpService>()
                // about command
                .AddSingleton<AboutService>()
        // config command
        //.AddSingleton<ConfigCommandService>()
        //.AddSingleton(x => (ConfigCommandServiceBase<ConfigPage.Page>)x.GetService<ConfigCommandService>()!);
        ;

        collection.Scan(scan => scan.FromAssemblyOf<Bot>()
            .AddClasses(classes => classes.WithAttribute<InjectAttribute>(x =>
                x.ServiceLifetime == ServiceLifetime.Singleton)
            )
            .AsSelf()
            .WithSingletonLifetime()
        );

        collection.Scan(scan => scan.FromAssemblyOf<Bot>()
            .AddClasses(classes => classes.WithAttribute<InjectAttribute>(x =>
                x.ServiceLifetime == ServiceLifetime.Transient)
            )
            .AsSelf()
            .WithTransientLifetime()
        );

        //collection.Scan(scan => scan.FromAssemblyOf<Bot>()
        //    .AddClasses(classes => classes.AssignableTo<ConfigPage>())
        //    .As<ConfigPage>()
        //    .As<ConfigPageBase<ConfigPage.Page>>()
        //    .WithTransientLifetime());

        return collection.BuildServiceProvider();
    }

    public async Task RunAndBlockAsync()
    {
        Log.Information("Starting bot...");
        await RunAsync();
        await Task.Delay(Timeout.Infinite);
    }

    private async Task RunAsync()
    {
        var args = Environment.GetCommandLineArgs();
        var migrationEnabled = !(args.Contains("nomigrate") || args.Contains("nukedb"));
        await services.GetRequiredService<DbService>().Initialize(migrationEnabled);

#if DEBUG
        if (Environment.GetCommandLineArgs().Contains("nukedb"))
        {
            Log.Debug("Nuking the DB...");

            await services.GetRequiredService<DbService>().ResetDatabase();

            Log.Debug("Nuked!");
        }
#endif

        Client.Log += Client_Log;

        Client.Ready += Client_Ready;

        Client.GuildMemberUpdated += Client_GuildMemberUpdated;
        Client.UserLeft += Client_UserLeft;
        Client.UserJoined += Client_UserJoined;

        await Client.LoginAsync(TokenType.Bot, Config.BotToken);
        await Client.StartAsync();
    }

    private Task Client_UserLeft(SocketGuild guild, SocketUser user) => services.GetRequiredService<RoleManagementService>().OnUserLeft(guild, user);

    private Task Client_UserJoined(SocketGuildUser user) => services.GetRequiredService<RoleManagementService>().OnUserJoined(user);

    private async Task Client_GuildMemberUpdated(Cacheable<SocketGuildUser, ulong> cacheable, SocketGuildUser user)
    {
        if (!cacheable.HasValue)
            return;

        if (!user.Roles.SequenceEqual(cacheable.Value.Roles))
        {
            await services.GetRequiredService<RoleManagementService>().OnUserRolesUpdated(cacheable, user);
        }
    }

    private Task Client_Log(LogMessage message)
    {
        var level = message.Severity switch
        {
            LogSeverity.Critical => LogEventLevel.Fatal,
            LogSeverity.Error => LogEventLevel.Error,
            LogSeverity.Warning => LogEventLevel.Warning,
            LogSeverity.Info => LogEventLevel.Information,
            LogSeverity.Verbose => LogEventLevel.Verbose,
            LogSeverity.Debug => LogEventLevel.Debug,
            _ => LogEventLevel.Information,
        };

        if (message.Exception is not null)
        {
            Log.Write(level, message.Exception, "{Source} | {Message}", message.Source, message.Message);
        }
        else
        {
            Log.Write(level, "{Source} | {Message}", message.Source, message.Message);
        }
        return Task.CompletedTask;
    }

    private async Task Client_Ready()
    {
        Log.Information("Logged in as {user}#{discriminator} ({id})", Client.CurrentUser?.Username, Client.CurrentUser?.Discriminator, Client.CurrentUser?.Id);

        await services.GetRequiredService<CommandHandler>().OnReady(Assembly.GetExecutingAssembly());

        var roleManagement = services.GetRequiredService<RoleManagementService>();

        await roleManagement.CacheAndResolve();
    }
}