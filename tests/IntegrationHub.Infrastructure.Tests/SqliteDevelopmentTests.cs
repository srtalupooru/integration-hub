using IntegrationHub.Application;
using IntegrationHub.Domain;
using IntegrationHub.Infrastructure.Persistence;
using IntegrationHub.Tests;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace IntegrationHub.Infrastructure.Tests;

[TestFixture]
public sealed class SqliteDevelopmentTests
{
    [Test]
    public void Production_defaults_to_sql_server_and_rejects_sqlite()
    {
        new DatabaseOptions().Provider.Should().Be(DatabaseProvider.SqlServer);
        var options = new DatabaseOptions { Provider = DatabaseProvider.Sqlite };
        var action = () => options.Validate(isLocalEnvironment: false);
        action.Should().Throw<InvalidOperationException>().WithMessage("*only supported in Development or Testing*");
    }

    [Test]
    public void Sql_server_cannot_accidentally_auto_migrate()
    {
        var options = new DatabaseOptions { Provider = DatabaseProvider.SqlServer, InitializeSqliteOnStartup = true };
        var action = () => options.Validate(isLocalEnvironment: true);
        action.Should().Throw<InvalidOperationException>().WithMessage("*requires SQLite*");
    }

    [Test]
    public void Relative_database_paths_are_resolved_against_content_root_and_enable_foreign_keys()
    {
        var options = new DatabaseOptions { Provider = DatabaseProvider.Sqlite };
        var contentRoot = Path.Combine(Path.GetTempPath(), "hub-content");
        var resolved = new SqliteConnectionStringBuilder(options.ResolveConnectionString("Data Source=App_Data/hub.db;Foreign Keys=False", contentRoot));
        resolved.DataSource.Should().Be(Path.Combine(contentRoot, "App_Data", "hub.db"));
        resolved.ForeignKeys.Should().BeTrue();
    }

    [TestCase(":memory:")]
    [TestCase("")]
    [TestCase("file:temporary?mode=memory")]
    public void Runtime_development_storage_requires_a_persistent_file(string source)
    {
        var options = new DatabaseOptions { Provider = DatabaseProvider.Sqlite };
        var action = () => options.ResolveConnectionString(new SqliteConnectionStringBuilder { DataSource = source }.ToString(), Path.GetTempPath());
        action.Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void Migration_sets_are_isolated_by_provider()
    {
        using var sqlite = new SqliteHubDbContext(new DbContextOptionsBuilder<SqliteHubDbContext>().UseSqlite("Data Source=:memory:").Options);
        using var sqlServer = new HubDbContext(new DbContextOptionsBuilder<HubDbContext>().UseSqlServer().Options);
        sqlite.Database.GetMigrations().Should().HaveCount(2).And.Contain(m => m.EndsWith("_InitialSqliteCatalogue")).And.Contain(m => m.EndsWith("_ComponentDefinitions"));
        sqlServer.Database.GetMigrations().Should().HaveCount(2).And.Contain(m => m.EndsWith("_InitialCatalogue")).And.Contain(m => m.EndsWith("_ComponentDefinitions"));
    }

    [Test]
    public async Task Startup_migrations_are_repeatable_and_preserve_definitions_and_history_across_restarts()
    {
        var directory = Path.Combine(Path.GetTempPath(), "integrationhub-restart-tests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "nested", "hub.db");
        var connection = new SqliteConnectionStringBuilder { DataSource = path, Pooling = false, ForeignKeys = true }.ToString();
        var definition = TestDefinitions.Flow();
        ServiceProvider BuildServices() => new ServiceCollection().AddLogging().AddIntegrationHub(connection, DatabaseProvider.Sqlite).BuildServiceProvider();
        try
        {
            await using (var provider = BuildServices())
            {
                await using var scope = provider.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<HubDbContext>();
                db.Should().BeOfType<SqliteHubDbContext>();
                await DatabaseInitialization.ApplyMigrationsAsync(db);
                var repository = scope.ServiceProvider.GetRequiredService<IIntegrationRepository>();
                (await repository.GetDashboardAsync(default)).TotalIntegrations.Should().Be(0);
                await repository.SaveAsync(definition, TestDefinitions.Json, "json", ValidationResult.Success, "test-author", "Create", null, true, default);
                await repository.SaveAsync(definition with { Name = "Revised" }, "updated source", "yaml", ValidationResult.Success, "test-author", "Update", 1, false, default);
            }
            await using (var provider = BuildServices())
            {
                await using var scope = provider.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<HubDbContext>();
                await DatabaseInitialization.ApplyMigrationsAsync(db);
                (await db.Database.GetPendingMigrationsAsync()).Should().BeEmpty();
                var repository = scope.ServiceProvider.GetRequiredService<IIntegrationRepository>();
                var persisted = await repository.GetAsync(definition.Id, default);
                persisted!.Revision.Should().Be(2);
                persisted.Definition.Name.Should().Be("Revised");
                var history = await repository.GetVersionsAsync(definition.Id, default);
                history.Should().HaveCount(2);
                history[1].OriginalDefinition.Should().Be(TestDefinitions.Json);
                (await repository.GetDashboardAsync(default)).TotalIntegrations.Should().Be(1);
            }
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true); }
    }
}
