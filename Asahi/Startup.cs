﻿using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using Asahi.Database;
using Asahi.Modules;
using Asahi.Modules.About;
using Asahi.Modules.AnimeThemes;
using Asahi.Modules.FeedsV2;
using Asahi.Modules.Tatsu;
using Discord.Commands;
using Discord.Interactions;
using Discord.Rest;
using Discord.WebSocket;
using Fergun.Interactive;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NodaTime;
using Refit;
using Serilog;
using Serilog.Core;
using Serilog.Extensions.Logging;
using Serilog.Sinks.SystemConsole.Themes;

namespace Asahi;

public static class Startup
{
    public static async Task Main()
    {
        Logger? logger = null;
        try
        {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

            Console.OutputEncoding = Encoding.UTF8;
            Log.Logger = new LoggerConfiguration().WriteTo
                .Console(
                    outputTemplate: "[FALLBACK] [{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            if (!BotConfigFactory.GetConfig(out var botConfig))
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
                    .WriteTo.Console(theme: AnsiConsoleTheme.Sixteen)
                ;

            Directory.CreateDirectory(BotConfigFactory.DefaultDataDirectory);

            if (!string.IsNullOrWhiteSpace(botConfig.SeqUrl))
            {
                logConfig.WriteTo.Seq(botConfig.SeqUrl, apiKey: botConfig.SeqApiKey);
            }

            logger = logConfig.CreateLogger();
            builder.ConfigureLogging(logging =>
                    logging
                        .AddSerilog(logger)
                        .AddFilter<SerilogLoggerProvider>("Microsoft.Extensions.Http.DefaultHttpClientFactory",
                            LogLevel.Warning)
                        .AddFilter<SerilogLoggerProvider>("Microsoft.EntityFrameworkCore.*", LogLevel.Warning))
                ;

            builder.ConfigureServices(x => x.AddBotServices(botConfig));
            builder.ConfigureHostConfiguration(
                configBuilder => configBuilder.AddEnvironmentVariables(prefix: "DOTNET_"));

            await builder.RunConsoleAsync();
        }
        finally
        {
            if(logger != null)
                await logger.DisposeAsync();
        }
    }

    private static IServiceCollection AddBotServices(this IServiceCollection serviceCollection, BotConfig config)
    {
        const LogSeverity logLevel = LogSeverity.Verbose;

        serviceCollection
            .AddSingleton(config)
            .AddSingleton(new DiscordSocketClient(new DiscordSocketConfig
            {
                GatewayIntents = GatewayIntents.Guilds |
                                 GatewayIntents.MessageContent |
                                 GatewayIntents.GuildMessageReactions |
                                 GatewayIntents.GuildMessages |
                                 GatewayIntents.DirectMessages |
                                 GatewayIntents.GuildMembers,
                LogLevel = logLevel,
                AlwaysDownloadUsers = true
            }))
            .AddTransient(_ => new DiscordRestConfig() { LogLevel = logLevel })
            .AddSingleton<IDiscordClient>(x => x.GetRequiredService<DiscordSocketClient>())
            .AddSingleton(x => new InteractionService(x.GetRequiredService<DiscordSocketClient>(),
                new InteractionServiceConfig()
                {
                    LogLevel = logLevel,
                    DefaultRunMode = Discord.Interactions.RunMode.Async
                }))
            .AddSingleton(new CommandService(new CommandServiceConfig
            {
                LogLevel = logLevel,
                DefaultRunMode = Discord.Commands.RunMode.Async
            }))
            .AddSingleton<CommandHandler>()
            .AddSingleton<IDbService, DbService>()
            .AddSingleton<IFeedProviderFactory, DefaultFeedProviderFactory>()
            .AddTransient<IFeedMessageDispatcher, DiscordFeedMessageDispatcher>()
            .AddSingleton(new InteractiveConfig()
                { ReturnAfterSendingPaginator = true, ProcessSinglePagePaginators = true, LogLevel = logLevel })
            .AddSingleton<InteractiveService>()
            .AddSingleton<OverrideTrackerService>()
            .AddSingleton<IClock>(SystemClock.Instance)
            // about command
            .AddSingleton<AboutService>()
            .AddHttpClient(Microsoft.Extensions.Options.Options.DefaultName)
            .ConfigureHttpClient(x => AddDefaultProperties(x));

        serviceCollection.AddHttpClient("rss")
            .ConfigureHttpClient(x =>
            {
                AddDefaultProperties(x);
                x.MaxResponseContentBufferSize = 8000000;

                x.DefaultRequestHeaders.Accept.Clear();
                x.DefaultRequestHeaders.Accept.Add(MediaTypeWithQualityHeaderValue.Parse("application/atom+xml"));
                x.DefaultRequestHeaders.Accept.Add(MediaTypeWithQualityHeaderValue.Parse("application/rss+xml"));
                x.DefaultRequestHeaders.Accept.Add(MediaTypeWithQualityHeaderValue.Parse("text/xml"));
                x.DefaultRequestHeaders.Accept.Add(MediaTypeWithQualityHeaderValue.Parse("application/xml"));
            });

        serviceCollection.ConfigureHttpClientDefaults(x => x.RemoveAllLoggers());

        // Anime themes says its implementing the JSON:API spec, but it's so different lol
        // new JsonApiSerializerSettings()
        var settings = new RefitSettings(new NewtonsoftJsonContentSerializer());

        serviceCollection.AddRefitClient<IAnimeThemesClient>(settings)
            .ConfigureHttpClient(x => AddDefaultProperties(x).BaseAddress = new Uri("https://api.animethemes.moe/"));

        serviceCollection.AddRefitClient<ITatsuClient>(settings)
            .ConfigureHttpClient(x => AddDefaultProperties(x).BaseAddress = new Uri("https://api.tatsu.gg/v1"));

        serviceCollection.AddRefitClient<IRedditApi>(settings)
            .ConfigureHttpClient(x =>
            {
                AddDefaultProperties(x).BaseAddress = new Uri("https://www.reddit.com");
            }
        );
        
        serviceCollection.AddRefitClient<IFxTwitterApi>(settings)
            .ConfigureHttpClient(x =>
                {
                    AddDefaultProperties(x).BaseAddress = new Uri(config.FxTwitterApiUrl);
                }
            );

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
