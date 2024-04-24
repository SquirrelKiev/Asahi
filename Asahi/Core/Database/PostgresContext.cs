using Microsoft.EntityFrameworkCore;

namespace Asahi.Database;

public class PostgresContext : BotDbContext
{
    public PostgresContext(string connStr = "Host=127.0.0.1;Username=postgres;Password=;Database=botdb") : base(connStr)
    {
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder
            .UseNpgsql(connectionString);
    }
}