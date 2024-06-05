using BotBase.Database;
using Microsoft.Extensions.Logging;

namespace Asahi.Database;

public class DbService(BotConfigBase botConfig, ILoggerFactory loggerFactory) : DbServiceBase<BotDbContext>(botConfig)
{
    public override BotDbContext GetDbContext()
    {
        BotDbContext context = botConfig.Database switch
        {
            BotConfig.DatabaseType.Postgresql => new PostgresContext(botConfig.DatabaseConnectionString, loggerFactory),
            BotConfig.DatabaseType.Sqlite => new SqliteContext(botConfig.DatabaseConnectionString, loggerFactory),
            _ => throw new NotSupportedException(botConfig.Database.ToString())
        };

        return context;
    }
}