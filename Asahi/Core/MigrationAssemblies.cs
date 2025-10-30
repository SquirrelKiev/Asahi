namespace Asahi;

public static class MigrationAssemblies
{
    public static readonly string Sqlite = typeof(Asahi.Migrations.Sqlite.Marker).Assembly.GetName().Name!;
    public static readonly string Postgres = typeof(Asahi.Migrations.Postgres.Marker).Assembly.GetName().Name!;
}
