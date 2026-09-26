using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace IntegrationHub.Infrastructure.Persistence;

// Inherits the canonical mappings, with an independent provider-specific migration history.
public sealed class SqliteHubDbContext(DbContextOptions<SqliteHubDbContext> options) : HubDbContext(options);

public sealed class SqliteHubDbContextFactory : IDesignTimeDbContextFactory<SqliteHubDbContext>
{
    public SqliteHubDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__IntegrationHub") ?? "";
        return new(new DbContextOptionsBuilder<SqliteHubDbContext>().UseSqlite(connection).Options);
    }
}
