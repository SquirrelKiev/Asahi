using Serilog.Events;
using YamlDotNet.Serialization;

namespace Asahi;

public class BotConfig : BotConfigBase
{
    public override string BotToken { get; set; } = "BOT_TOKEN_HERE";
    public override CacheType Cache { get; set; } = CacheType.Memory;
    public override DatabaseType Database { get; set; } = DatabaseType.Sqlite;
    public override string DatabaseConnectionString { get; set; } = "Data Source=data/BotDb.db";
    public override string ErrorEmote { get; set; } = "\u2753";
    public override HashSet<ulong> ManagerUserIds { get; set; } = [0ul];

    public override string SeqUrl { get; set; } = "";
    public override string SeqApiKey { get; set; } = "";

    public LogEventLevel LogEventLevel { get; set; } = LogEventLevel.Verbose;

    public override string DefaultPrefix { get; set; } = "]";

    [YamlMember(Description = "The default UserAgent to use when making web requests.")]
    public string UserAgent { get; set; } = "Asahi/NoSetVersion (https://github.com/SquirrelKiev/Asahi)";
    [YamlMember(Description = "The App ID to use for the Wolfram command. Can get one from https://developer.wolframalpha.com/.")]
    public string WolframAppId { get; set; } = "";

    [YamlMember(Description = "Any users in this list are banned from ever making it to highlights.")]
    public HashSet<ulong> BannedHighlightsUsers { get; set; } = [];

    public override string AboutPageTitle { get; set; } = "About {{botUsername}}";
    public override string AboutPageDescription { get; set; } = "Various miscellaneous tools. " +
                                                                "Originally called Seigen and just had the trackables stuff, " +
                                                                "but has since expanded to include more.";

    public override AboutField[] AboutPageFields { get; set; } =
    [
        new AboutField
        {
            Name = "Servers",
            Value = "{{guilds}}"
        },
        new AboutField
        {
            Name = "Credits:",
            Value = "Bot by [enonibobble](https://github.com/SquirrelKiev)"
        },
        new AboutField
        {
            Name = "Source Code:",
            Value = "https://github.com/SquirrelKiev/Seigen"
        }
    ];
}