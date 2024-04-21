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
    public override string DefaultPrefix { get; set; } = "]";
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