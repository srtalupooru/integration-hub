using IntegrationHub.Application;
using IntegrationHub.Contracts;
using IntegrationHub.Infrastructure.Parsing;
using IntegrationHub.Infrastructure.Persistence;
using IntegrationHub.Tests;
using IntegrationHub.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
namespace IntegrationHub.Infrastructure.Tests;

[TestFixture]
public sealed class ComponentPersistenceTests
{
    [Test]
    public async Task Upgrade_preserves_authored_data_and_component_history_survives_restart()
    {
        var directory = Path.Combine(Path.GetTempPath(), "component-upgrade", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var connection = new SqliteConnectionStringBuilder { DataSource = Path.Combine(directory, "hub.db"), Pooling = false }.ToString();
        ServiceProvider Services() => new ServiceCollection().AddLogging().AddIntegrationHub(connection, DatabaseProvider.Sqlite).BuildServiceProvider();
        try
        {
            await using (var provider = Services())
            {
                await using var scope = provider.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<HubDbContext>();
                await db.GetService<IMigrator>().MigrateAsync(db.Database.GetMigrations().First());
                var repo = scope.ServiceProvider.GetRequiredService<IIntegrationRepository>();
                await repo.SaveAsync(TestDefinitions.Flow(), TestDefinitions.Json, "json", ValidationResult.Success, "author", "Before upgrade", null, true, default);
                await DatabaseInitialization.ApplyMigrationsAsync(db);
                (await repo.GetAsync(new("test-flow"), default))!.OriginalDefinition.Should().Be(TestDefinitions.Json);
                var workflow = scope.ServiceProvider.GetRequiredService<ComponentWorkflow>();
                await workflow.SaveAsync(null, new(ComponentExamples.Read("vendor-api")), "author", default);
                await workflow.SaveAsync(null, new(ComponentExamples.Read("vendor-function")), "author", default);
                var components = scope.ServiceProvider.GetRequiredService<IComponentRepository>();
                await components.SetArchivedAsync("vendor-api-dev", 1, true, "admin", default);
                await components.SetArchivedAsync("vendor-api-dev", 2, false, "admin", default);
                db.Database.HasPendingModelChanges().Should().BeFalse();
            }
            await using (var provider = Services())
            {
                await using var scope = provider.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<HubDbContext>();
                await DatabaseInitialization.ApplyMigrationsAsync(db);
                var components = scope.ServiceProvider.GetRequiredService<IComponentRepository>();
                (await components.GetAsync("vendor-api-dev", default))!.Revision.Should().Be(3);
                (await components.VersionsAsync("vendor-api-dev", default)).Select(v => v.IsArchived).Should().Equal(false, true, false);
                (await scope.ServiceProvider.GetRequiredService<ComponentWorkflow>().DiscoverAsync(default)).Integrations.Should().ContainSingle();
                (await scope.ServiceProvider.GetRequiredService<IIntegrationRepository>().GetVersionsAsync(new("test-flow"), default)).Should().ContainSingle();
                var history = await db.ComponentVersions.FirstAsync(); history.OriginalDefinition = "tamper";
                var tamper = async () => await db.SaveChangesAsync();
                await tamper.Should().ThrowAsync<InvalidOperationException>().WithMessage("*immutable*");
                var syncTamper = () => db.SaveChanges();
                syncTamper.Should().Throw<InvalidOperationException>();
            }
        }
        finally { Directory.Delete(directory, true); }
    }
    [Test]
    public void Both_provider_snapshots_match_current_model()
    {
        using var sqlite = new SqliteHubDbContext(new DbContextOptionsBuilder<SqliteHubDbContext>().UseSqlite("Data Source=:memory:").Options);
        using var sql = new HubDbContext(new DbContextOptionsBuilder<HubDbContext>().UseSqlServer().Options);
        sqlite.Database.HasPendingModelChanges().Should().BeFalse();
        sql.Database.HasPendingModelChanges().Should().BeFalse();
    }
}
