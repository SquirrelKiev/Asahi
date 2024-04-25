using System.Text;
using Asahi.Database;
using BotBase.Database;
using BotBase.Modules.About;
using BotBase.Modules.Help;
using Discord.Commands;
using Discord.Interactions;
using Discord.WebSocket;
using Fergun.Interactive;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Extensions.Logging;
using Serilog.Sinks.SystemConsole.Themes;

namespace Asahi;

public static class Startup
{
    public static async Task Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        Log.Logger = new LoggerConfiguration().WriteTo.Console(outputTemplate: "[FALLBACK] [{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}").CreateLogger();

        if (!new BotConfigFactory<BotConfig>().GetConfig(out var botConfig))
        {
            Environment.Exit(1);
        }
        if (!botConfig.IsValid())
        {
            Environment.Exit(1);
        }

        var builder = new HostApplicationBuilder();

        var logLevel = botConfig.LogEventLevel;

        var logConfig = new LoggerConfiguration()
                .MinimumLevel.Is(logLevel)
                .Enrich.FromLogContext()
                .Enrich.WithThreadId()
                .Enrich.WithThreadName()
                .WriteTo.Console(theme: AnsiConsoleTheme.Literate)
            ;

        if (!string.IsNullOrWhiteSpace(botConfig.SeqUrl))
        {
            logConfig.WriteTo.Seq(botConfig.SeqUrl, apiKey: botConfig.SeqApiKey);
        }

        builder.Logging.ClearProviders();
        var logger = logConfig.CreateLogger();

        builder.Logging.AddSerilog(logger);

        builder.Services.AddBotServices(botConfig);
        // please let me be free of the log spam
        builder.Logging.AddFilter<SerilogLoggerProvider>("Microsoft.Extensions.Http.DefaultHttpClientFactory", LogLevel.None);

        builder.Services.AddHostedService<BotService>();

        var app = builder.Build();

        await app.RunAsync();
    }

    private static IServiceCollection AddBotServices(this IServiceCollection serviceCollection, BotConfig config)
    {
        serviceCollection
            .AddSingleton<BotConfigBase>(config)
            .AddCache(config)
            .AddSingleton(config)
            .AddSingleton(new DiscordSocketClient(new DiscordSocketConfig
            {
                GatewayIntents = GatewayIntents.Guilds |
                                 GatewayIntents.MessageContent |
                                 GatewayIntents.GuildMessageReactions |
                                 GatewayIntents.GuildMessages |
                                 GatewayIntents.DirectMessages |
                                 GatewayIntents.GuildMembers,
                LogLevel = LogSeverity.Verbose,
                AlwaysDownloadUsers = true
            }))
            .AddSingleton(x => new InteractionService(x.GetRequiredService<DiscordSocketClient>(),
                new InteractionServiceConfig()
                {
                    LogLevel = LogSeverity.Verbose,
                    DefaultRunMode = Discord.Interactions.RunMode.Async
                }))
            .AddSingleton(new CommandService(new CommandServiceConfig
            {
                LogLevel = LogSeverity.Verbose,
                DefaultRunMode = Discord.Commands.RunMode.Async
            }))
            .AddSingleton<CommandHandler>()
            .AddSingleton<DbService>()
            .AddSingleton(x => (DbServiceBase<BotDbContext>)x.GetService<DbService>()!)
            .AddSingleton<InteractiveService>()
            // for help command
            .AddSingleton<OverrideTrackerService>()
            .AddSingleton<HelpService>()
            // about command
            .AddSingleton<AboutService>()
            .AddHttpClient(Microsoft.Extensions.Options.Options.DefaultName)
                .ConfigureHttpClient(x => x.DefaultRequestHeaders.UserAgent.ParseAdd(config.UserAgent));

        serviceCollection.ConfigureHttpClientDefaults(x => x.RemoveAllLoggers());

        serviceCollection.Scan(scan => scan.FromAssemblyOf<BotService>()
            .AddClasses(classes => classes.WithAttribute<InjectAttribute>(x =>
                x.ServiceLifetime == ServiceLifetime.Singleton)
            )
            .AsSelf()
            .WithSingletonLifetime()
        );

        serviceCollection.Scan(scan => scan.FromAssemblyOf<BotService>()
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

        return serviceCollection;
    }
}