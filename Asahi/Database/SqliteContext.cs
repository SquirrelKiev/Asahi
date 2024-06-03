using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Asahi.Database;

public class SqliteContext : BotDbContext
{
    public SqliteContext(string connStr = "Data Source=data/botDb.db", ILoggerFactory? loggerFactory = null) : base(connStr, loggerFactory)
    {
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);

        var builder = new SqliteConnectionStringBuilder(connectionString);
        builder.DataSource = Path.Combine(AppContext.BaseDirectory, builder.DataSource);
        optionsBuilder.UseSqlite(builder.ToString(), x => x.UseNodaTime());
    }
}