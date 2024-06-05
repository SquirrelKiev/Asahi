using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Asahi.Database;

public class PostgresContext : BotDbContext
{
    public PostgresContext(string connStr = "Host=127.0.0.1;Username=postgres;Password=;Database=botdb", ILoggerFactory? loggerFactory = null) : base(connStr, loggerFactory)
    {
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);

        optionsBuilder
            .UseNpgsql(connectionString, x => x.UseNodaTime().UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery));
    }
}