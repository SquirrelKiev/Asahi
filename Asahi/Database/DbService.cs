using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Serilog;

namespace Asahi.Database;

public class DbService(BotConfig botConfig, ILoggerFactory loggerFactory)
{
    public async Task Initialize(bool migrationEnabled)
    {
        Log.Debug("Database migration: {migrationStatus}", migrationEnabled);

        var context = GetDbContext();

        PreMigration(context);

        if (migrationEnabled)
        {
            await context.Database.MigrateAsync();
        }
    }

    public BotDbContext GetDbContext()
    {
        BotDbContext context = botConfig.Database switch
        {
            BotConfig.DatabaseType.Postgresql => new PostgresContext(botConfig.DatabaseConnectionString, loggerFactory),
            BotConfig.DatabaseType.Sqlite => new SqliteContext(botConfig.DatabaseConnectionString, loggerFactory),
            _ => throw new NotSupportedException(botConfig.Database.ToString())
        };

        return context;
    }

    public async Task ResetDatabase()
    {
        await using var dbContext = GetDbContext();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();
    }

    public void PreMigration(BotDbContext context) {}
}