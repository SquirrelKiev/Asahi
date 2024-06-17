using System.Text;
using Asahi.Database;
using Asahi.Modules.AnimeThemes;
using Asahi.Modules.Tatsu;
using BotBase.Database;
using BotBase.Modules.About;
using Discord.Commands;
using Discord.Interactions;
using Discord.WebSocket;
using Fergun.Interactive;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NodaTime;
using Refit;
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

        var builder = new HostBuilder();

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

        var logger = logConfig.CreateLogger();
        builder.ConfigureLogging(logging =>
            logging
                .AddSerilog(logger)
                .AddFilter<SerilogLoggerProvider>("Microsoft.Extensions.Http.DefaultHttpClientFactory", LogLevel.Warning)
                .AddFilter<SerilogLoggerProvider>("Microsoft.EntityFrameworkCore.*", LogLevel.Warning))
            ;

        builder.ConfigureServices(x => x.AddBotServices(botConfig));
        builder.ConfigureHostConfiguration(configBuilder => configBuilder.AddEnvironmentVariables(prefix: "DOTNET_"));

        await builder.RunConsoleAsync();
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
            .AddSingleton(new InteractiveConfig(){ReturnAfterSendingPaginator = true, ProcessSinglePagePaginators = true})
            .AddSingleton<InteractiveService>()
            .AddSingleton<OverrideTrackerService>()
            .AddSingleton<IClock>(SystemClock.Instance)
            // about command
            .AddSingleton<AboutService>()
            .AddHttpClient(Microsoft.Extensions.Options.Options.DefaultName)
                .ConfigureHttpClient(x => AddDefaultProperties(x));

        serviceCollection.ConfigureHttpClientDefaults(x => x.RemoveAllLoggers());

        // Anime themes says its implementing the JSON:API spec but it's so different lol
        // new JsonApiSerializerSettings()
        var settings = new RefitSettings(new NewtonsoftJsonContentSerializer());

        serviceCollection.AddRefitClient<IAnimeThemesClient>(settings)
            .ConfigureHttpClient(x => AddDefaultProperties(x).BaseAddress = new Uri("https://api.animethemes.moe/"));

        serviceCollection.AddRefitClient<ITatsuClient>(settings)
            .ConfigureHttpClient(x => AddDefaultProperties(x).BaseAddress = new Uri("https://api.tatsu.gg/v1"));

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

        serviceCollection.AddHostedService<BotService>();

        return serviceCollection;

        HttpClient AddDefaultProperties(HttpClient client)
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd(config.UserAgent);
            return client;
        }
    }
}