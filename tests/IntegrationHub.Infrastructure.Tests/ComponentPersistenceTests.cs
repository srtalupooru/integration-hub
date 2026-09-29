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
    public async Task Existing_scalar_technology_is_read_without_rewriting_source_hash_or_history()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new SqliteHubDbContext(new DbContextOptionsBuilder<SqliteHubDbContext>().UseSqlite(connection).Options);
        await DatabaseInitialization.ApplyMigrationsAsync(db);
        var parser = new ComponentDefinitionParser(new());
        var source = ComponentExamples.Read("vendor-api").Replace("technology:\n    - ASP.NET Core", "technology: ASP.NET Core, FastEndpoints")
            .Replace("  messages:", "  publishes:").Replace("      action: publishes\n", "");
        var root = System.Text.Json.Nodes.JsonNode.Parse(parser.Serialize(parser.Parse(new(source)).Definition!, "json"))!;
        root["component"]!["technology"] = "ASP.NET Core, FastEndpoints";
        var canonical = root["component"]!.ToJsonString();
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
        db.Components.Add(new() { Id = "vendor-api-dev", CanonicalJson = canonical, OriginalDefinition = source, Format = "yaml", DefinitionHash = hash, Revision = 1, ChangedBy = "author", UpdatedAt = DateTimeOffset.UtcNow });
        db.ComponentVersions.Add(new() { ComponentId = "vendor-api-dev", CanonicalJson = canonical, OriginalDefinition = source, Format = "yaml", DefinitionHash = hash, Revision = 1, Version = "1.0", ChangedBy = "author", Timestamp = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var repository = new ComponentRepository(db);
        var loaded = (await repository.GetAsync("vendor-api-dev", default))!;
        loaded.Definition.Technology.Should().Equal("ASP.NET Core, FastEndpoints");
        loaded.Definition.Messages.Should().BeEmpty();
        loaded.Definition.PublishedMessages.Should().ContainSingle().Which.Contract.Should().Be("vendors.created");
        loaded.OriginalDefinition.Should().Be(source);
        loaded.DefinitionHash.Should().Be(hash);
        (await repository.ListAsync(false, default)).Single().Definition.Technology.Should().Equal(loaded.Definition.Technology);
        await repository.SetArchivedAsync("vendor-api-dev", 1, true, "admin", default);
        await repository.SetArchivedAsync("vendor-api-dev", 2, false, "admin", default);
        (await db.ComponentVersions.AsNoTracking().SingleAsync(v => v.Revision == 1)).CanonicalJson.Should().Be(canonical);
        (await db.Components.AsNoTracking().SingleAsync()).CanonicalJson.Should().Be(canonical);
        (await repository.GetAsync("vendor-api-dev", default))!.Definition.Technology.Should().Equal(loaded.Definition.Technology);
    }
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
                await workflow.SaveAsync(null, new(ComponentExamples.Read("saga-worker")), "author", default);
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
                var saga = (await components.GetAsync("invoice-process-dev", default))!;
                saga.OriginalDefinition.Should().Be(ComponentExamples.Read("saga-worker"));
                saga.Definition.Processing!.Sagas.Single().Timeouts.Single().Delay.Should().Be("PT30M");
                saga.Definition.Processing.Sagas.Single().Handles[0].Outputs.Should().Equal("request-payment");
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
