namespace Asahi;

public static class MigrationAssemblies
{
    public static string Sqlite = typeof(Asahi.Migrations.Sqlite.Marker).Assembly.GetName().Name!;
    public static string Postgres = typeof(Asahi.Migrations.Postgres.Marker).Assembly.GetName().Name!;
}
