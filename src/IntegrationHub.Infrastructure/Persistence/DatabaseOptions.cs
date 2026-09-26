using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace IntegrationHub.Infrastructure.Persistence;

public enum DatabaseProvider { SqlServer, Sqlite }

public sealed class DatabaseOptions
{
    public DatabaseProvider Provider { get; set; } = DatabaseProvider.SqlServer;
    public bool InitializeSqliteOnStartup { get; set; }

    public void Validate(bool isLocalEnvironment)
    {
        if (!Enum.IsDefined(Provider)) throw new InvalidOperationException("Database:Provider must be SqlServer or Sqlite.");
        if (Provider == DatabaseProvider.Sqlite && !isLocalEnvironment)
            throw new InvalidOperationException("SQLite storage is only supported in Development or Testing. Configure SQL Server for production.");
        if (InitializeSqliteOnStartup && Provider != DatabaseProvider.Sqlite)
            throw new InvalidOperationException("Automatic local database initialization requires SQLite. Set Database:InitializeSqliteOnStartup to false for SQL Server.");
    }

    public string ResolveConnectionString(string connectionString, string contentRoot)
    {
        if (Provider != DatabaseProvider.Sqlite) return connectionString;
        var connection = new SqliteConnectionStringBuilder(connectionString);
        if (string.IsNullOrWhiteSpace(connection.DataSource) || connection.DataSource == ":memory:" || connection.Mode == SqliteOpenMode.Memory || connection.DataSource.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("SQLite development storage requires a persistent database file path in ConnectionStrings:IntegrationHub.");
        connection.DataSource = Path.GetFullPath(connection.DataSource, contentRoot);
        connection.ForeignKeys = true;
        return connection.ToString();
    }
}

public static class DatabaseInitialization
{
    public static async Task ApplyMigrationsAsync(HubDbContext db, CancellationToken ct = default)
    {
        if (db.Database.IsSqlite())
        {
            var connection = new SqliteConnectionStringBuilder(db.Database.GetConnectionString());
            var directory = Path.GetDirectoryName(connection.DataSource);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        }
        await db.Database.MigrateAsync(ct);
    }
}
