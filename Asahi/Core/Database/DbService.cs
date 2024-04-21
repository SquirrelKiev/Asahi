using BotBase.Database;

namespace Asahi.Database;

public class DbService(BotConfigBase botConfig) : DbServiceBase<BotDbContext>(botConfig)
{
    public override BotDbContext GetDbContext()
    {
        BotDbContext context = botConfig.Database switch
        {
            BotConfig.DatabaseType.Postgresql => new PostgresqlContext(botConfig.DatabaseConnectionString),
            BotConfig.DatabaseType.Sqlite => new SqliteContext(botConfig.DatabaseConnectionString),
            _ => throw new NotSupportedException(botConfig.Database.ToString())
        };

        return context;
    }
}