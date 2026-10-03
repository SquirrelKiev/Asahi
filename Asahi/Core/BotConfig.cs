using System.Text;
using Asahi.Modules;
using Serilog;
using Serilog.Events;
using YamlDotNet.Serialization;

namespace Asahi;

public record BotConfig
{
    [YamlMember(Description = @"Your bot token from https://discord.com/developers/applications. Don't share!")]
    public string BotToken { get; set; } = DefaultBotToken;

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
    public string TestingBotToken { get; set; } = DefaultBotToken;
    
    [YamlMember(Description = "The credentials to use for Danbooru API requests.")]
    public DanbooruApiCredentialsModel DanbooruApiCredentials { get; set; } = default;
    [YamlMember(Description = "Your Danbooru user ID. Used for identifying the user in the User-Agent header. Highly recommend specifying this, see https://danbooru.donmai.us/forum_topics/37341 for more information.")]
    public ulong DanbooruUserId { get; set; } = 0;
    
    [YamlMember(Description = "The credentials to use for Reddit API requests. see https://www.reddit.com/prefs/apps")]
    public RedditApiCredentialsModel RedditApiCredentials { get; set; } = default;

    [YamlMember(Description = "Any users in this list are banned from ever making it to highlights.")]
    public HashSet<ulong> BannedHighlightsUsers { get; set; } = [];

    [YamlMember(Description = "The URL pattern to use for proxying images (if necessary).\n{{URL}} will be replaced with the URL, encoded in base64.")]
    public string ProxyUrl { get; set; } = "https://services.f-ck.me/v1/image/{{URL}}?source=asahi_bot";
    
    [YamlMember(Description = "The URL pattern to use for proxying videos (if necessary).\n{{URL}} will be replaced with the URL, encoded in base64." +
                              "Leave blank to use the configured Asahi.WebServices instead.")]
    public string VideoProxyUrl { get; set; } = "";

    [YamlMember(Description = "The Asahi web services url to use. Expects an instance of Asahi.WebServices.")]
    public string AsahiWebServicesBaseUrl { get; set; } = "";

    [YamlMember(Description = "The signing key ID to use for signing web services urls. Leave blank to not sign URLs.")]
    public string AsahiWebServicesSigningKeyId { get; set; } = "";

    [YamlMember(Description = "The signing key to use for signing web services urls. Leave blank to not sign URLs.")]
    public string AsahiWebServicesSigningKey { get; set; } = "";

    public const string DefaultBotToken = "BOT_TOKEN_HERE";
    
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
            if (TestingBotToken != DefaultBotToken)
                TokenUtils.ValidateToken(TokenType.Bot, TestingBotToken);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Supplied testing bot token is invalid. Set as {DefaultBotToken} if unwanted.", DefaultBotToken);
            return false;
        }

        if (string.IsNullOrWhiteSpace(AsahiWebServicesBaseUrl))
        {
            Log.Fatal("An instance of Asahi.WebServices is required.");
            return false;
        }

        bool hasSigningKeyId = !string.IsNullOrWhiteSpace(AsahiWebServicesSigningKeyId);
        bool hasSigningKey = !string.IsNullOrWhiteSpace(AsahiWebServicesSigningKey);
        if (hasSigningKeyId != hasSigningKey)
        {
            Log.Fatal("The web services signing key ID and signing key must either both be set or both be blank.");
            return false;
        }

        if (hasSigningKeyId)
        {
            if (!UrlSignature.IsValidKeyId(AsahiWebServicesSigningKeyId))
            {
                Log.Fatal("Web services signing key ID {KeyId} is invalid. Key IDs must be URL safe.", AsahiWebServicesSigningKeyId);
                return false;
            }

            try
            {
                _ = Convert.FromBase64String(AsahiWebServicesSigningKey);
            }
            catch (FormatException ex)
            {
                Log.Fatal(ex, "Web services signing key is not a valid base64 string.");
                return false;
            }
        }

        return true;
    }

    public enum DatabaseType
    {
        Sqlite,
        Postgresql
    }

    // given these are deserialized to, the constructor is bypassed. aka, can't get away with a simply = on BasicAuthSecret here
    public readonly record struct DanbooruApiCredentialsModel(string Username, string ApiKey)
    {
        public string BasicAuthenticationSecret => Convert.ToBase64String(Encoding.UTF8.GetBytes($"{Username}:{ApiKey}"));
    }
    
    public readonly record struct RedditApiCredentialsModel(string ClientId, string ClientSecret)
    {
        public string BasicAuthenticationSecret => Convert.ToBase64String(Encoding.UTF8.GetBytes($"{ClientId}:{ClientSecret}"));
    }
}
