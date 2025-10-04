using Asahi.Modules;
using Serilog;
using Serilog.Events;
using YamlDotNet.Serialization;

namespace Asahi;

public class BotConfig
{
    [YamlMember(Description = @"Your bot token from https://discord.com/developers/applications. Don't share!")]
    public string BotToken { get; set; } = "BOT_TOKEN_HERE";

    [YamlMember(Description = "The type of database to use.\n" +
                              "Options are \"Sqlite\" and \"Postgresql\".")]
    public DatabaseType Database { get; set; } = DatabaseType.Sqlite;

    [YamlMember(Description = "The connection string for the database specified above.\n" +
                              "Example Postgres string: Host=127.0.0.1;Username=postgres;Password=;Database=botdb\n" +
                              "Example Sqlite string: Data Source=data/BotDb.db")]
    public string DatabaseConnectionString { get; set; } = "Data Source=data/BotDb.db";

    [YamlMember(Description = "The folders to search for emote images. Descending priority order.")]
    public string[] InternalEmoteImagesDirectories { get; set; } = [Path.Combine("%DataDir%", "InternalEmotes")];
    
    public BotEmotesSpecification Emotes { get; set; } = new();

    [YamlMember(Description = "A set of UserIDs. Users in this set will be granted permission to use commands to manage the instance itself.\n" +
                              "This is a dangerous permission to grant.")]
    public HashSet<ulong> ManagerUserIds { get; set; } = [0ul];

    [YamlMember(Description = "An optional URL to an instance of Seq. Empty string is interpreted as not wanting Seq.")]
    public string SeqUrl { get; set; } = "";

    [YamlMember(Description = "An optional API key for Seq. Empty string is interpreted as no API key.")]
    public string SeqApiKey { get; set; } = "";

    public LogEventLevel LogEventLevel { get; set; } = LogEventLevel.Verbose;

    [YamlMember(Description = "The default prefix for the bot.")]
    public string DefaultPrefix { get; set; } = "]";
    
    [YamlMember(Description = "The App ID to use for the Wolfram command. Can get one from https://developer.wolframalpha.com/.")]
    public string WolframAppId { get; set; } = "";

    [YamlMember(Description = "The token of the test bot. This is only used for /bot nuke-test-commands at present. Optional.")]
    public string TestingBotToken { get; set; } = "BOT_TOKEN_HERE";

    [YamlMember(Description = "Any users in this list are banned from ever making it to highlights.")]
    public HashSet<ulong> BannedHighlightsUsers { get; set; } = [];

    // [YamlMember(Description = "The base url to use for magnet redirector links. Used for the Nyaa RSS feed.")]
    // public string MagnetRedirectorBaseUrl { get; set; } = "https://redirector.onk.moe";

    [YamlMember(Description = "The URL pattern to use for proxying images (if necessary).\n{{URL}} will be replaced with the URL, encoded in base64.")]
    public string ProxyUrl { get; set; } = "https://services.f-ck.me/v1/image/{{URL}}?source=asahi_bot";
    
    [YamlMember(Description = "The URL pattern to use for proxying videos (if necessary).\n{{URL}} will be replaced with the URL, encoded in base64.")]
    public string VideoProxyUrl { get; set; } = "https://asahi-services.onk.moe/api/proxy/{{URL}}?source=asahi_bot";

    [YamlMember(Description = "The fxtwitter API url to use as a base.")]
    public string FxTwitterApiUrl { get; set; } = "https://api.fxtwitter.com";

    [YamlMember(Description = "The Asahi web services url to use as a base. Expects an instance of Asahi.WebServices.")]
    public string AsahiWebServicesBaseUrl { get; set; } = "https://asahi-services.onk.moe";

    public bool IsValid()
    {
        try
        {
            TokenUtils.ValidateToken(TokenType.Bot, BotToken);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Supplied bot token is invalid.");
            return false;
        }
        
        try
        {
            if (TestingBotToken != "BOT_TOKEN_HERE")
                TokenUtils.ValidateToken(TokenType.Bot, TestingBotToken);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Supplied testing bot token is invalid. Set as BOT_TOKEN_HERE if unwanted.");
            return false;
        }

        return true;
    }

    public enum DatabaseType
    {
        Sqlite,
        Postgresql
    }
    
    public enum ProxyUrlEncodingFormat
    {
        UrlEscaped,
        Base64
    }

    public struct AboutField
    {
        /// <summary>
        /// For any string here, the following will be replaced:
        /// - {{guilds}} will be substituted with how many guilds (servers) the bot is in.
        /// - {{botUsername}} will be substituted with the bot's username.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// For any string here, the following will be replaced:
        /// - {{guilds}} will be substituted with how many guilds (servers) the bot is in.
        /// - {{botUsername}} will be substituted with the bot's username.
        /// </summary>
        public string Value { get; set; }

        /// <summary>
        /// For any string here, the following will be replaced:
        /// - {{guilds}} will be substituted with how many guilds (servers) the bot is in.
        /// - {{botUsername}} will be substituted with the bot's username.
        /// </summary>
        public bool Inline { get; set; }
    }
}
