using System.Diagnostics.CodeAnalysis;
using Asahi.BotEmoteManagement;
using Serilog;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Asahi;

public static class BotConfigFactory
{
    public static readonly string DefaultDataDirectory = Path.Combine(Path.Combine(AppContext.BaseDirectory, "data"));
    public static readonly string BotInternalEmotesDirectory = Environment.GetEnvironmentVariable("INTERNAL_EMOTES_LOCATION") ?? 
                                                               Path.Combine(Path.Combine(DefaultDataDirectory, "InternalEmotes"));
    private static readonly string ConfigPath = Environment.GetEnvironmentVariable("BOT_CONFIG_LOCATION") ??
                                                Path.Combine(DefaultDataDirectory, "bot_config.yaml");

    public static bool GetConfig([NotNullWhen(true)] out BotConfig? botConfig)
    {
        var serializer = new SerializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .WithDefaultScalarStyle(ScalarStyle.DoubleQuoted)
            .WithTagMapping("!unicode", typeof(UnicodeEmoteSpecification))
            .WithTagMapping("!external", typeof(ExternalCustomEmoteSpecification))
            .WithTagMapping("!internal", typeof(InternalCustomEmoteSpecification))
            .EnsureRoundtrip()
            .Build();

        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .WithTagMapping("!unicode", typeof(UnicodeEmoteSpecification))
            .WithTagMapping("!external", typeof(ExternalCustomEmoteSpecification))
            .WithTagMapping("!internal", typeof(InternalCustomEmoteSpecification))
            .IgnoreUnmatchedProperties()
            .Build();

        if (!File.Exists(ConfigPath))
        {
            botConfig = new BotConfig();

            Log.Fatal("Config not found. Creating new config at {ConfigPath}. Please edit this file and restart the bot.", ConfigPath);

            var dirName = Path.GetDirectoryName(ConfigPath);
            if (dirName != null)
                Directory.CreateDirectory(dirName);

            File.WriteAllText(ConfigPath, serializer.Serialize(botConfig));
            return false;
        }

        try
        {
            botConfig = deserializer.Deserialize<BotConfig>(File.ReadAllText(ConfigPath));
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Failed to read config.");
            botConfig = new BotConfig();
            return false;
        }

        File.WriteAllText(ConfigPath, serializer.Serialize(botConfig));

        return true;

    }
}
