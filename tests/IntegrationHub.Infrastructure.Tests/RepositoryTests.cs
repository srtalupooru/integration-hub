using IntegrationHub.Application;
using IntegrationHub.Contracts;
using IntegrationHub.Domain;
using IntegrationHub.Infrastructure.Persistence;
using IntegrationHub.Tests;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
namespace IntegrationHub.Infrastructure.Tests;

[TestFixture]
public sealed class RepositoryTests
{
    private SqliteConnection _connection = default!;
    private HubDbContext _db = default!;
    private IntegrationRepository _repository = default!;
    private static readonly CancellationToken Ct = CancellationToken.None;
    [SetUp]
    public async Task SetUp()
    {
        _connection = new("Data Source=:memory:"); await _connection.OpenAsync();
        _db = new(new DbContextOptionsBuilder<HubDbContext>().UseSqlite(_connection).Options);
        await _db.Database.EnsureCreatedAsync(); _repository = new(_db, new IntegrationGraphBuilder());
    }
    [TearDown] public async Task TearDown() { await _db.DisposeAsync(); await _connection.DisposeAsync(); }
    private Task<IntegrationDetail> Save(IntegrationDefinition d, int? revision = null) => _repository.SaveAsync(d, TestDefinitions.Json, "json", ValidationResult.Success, "tester", "Test revision", revision, revision is null, Ct);
    [Test]
    public async Task Empty_database_has_real_zero_counts()
    {
        var dashboard = await _repository.GetDashboardAsync(Ct);
        dashboard.TotalIntegrations.Should().Be(0); dashboard.Systems.Should().Be(0); dashboard.Messages.Should().Be(0); dashboard.RecentlyUpdated.Should().BeEmpty();
    }
    [Test]
    public async Task Save_populates_relational_graph_and_preserves_canonical_definition()
    {
        var d = TestDefinitions.Flow(); var detail = await Save(d);
        detail.Revision.Should().Be(1); detail.DefinitionHash.Should().HaveLength(64);
        (await _repository.GetAsync(d.Id, Ct))!.Definition.Should().BeEquivalentTo(d);
        (await _db.Nodes.CountAsync()).Should().Be(2); (await _db.Set<IntegrationEdgeEntity>().CountAsync()).Should().Be(1);
        (await _db.Nodes.SingleAsync(n => n.Id == "a")).IsSource.Should().BeTrue();
    }
    [Test]
    public async Task Update_creates_new_history_without_overwriting_previous_source()
    {
        var d = TestDefinitions.Flow(); await Save(d);
        var updated = await _repository.SaveAsync(d with { Name = "Changed", Tags = ["tag"] }, "new source", "yaml", ValidationResult.Success, "another-author", "Rename", 1, false, Ct);
        updated.Revision.Should().Be(2);
        var history = await _repository.GetVersionsAsync(d.Id, Ct);
        history.Should().HaveCount(2); history[1].OriginalDefinition.Should().Be(TestDefinitions.Json); history[0].OriginalDefinition.Should().Be("new source");
        (await _db.Nodes.CountAsync()).Should().Be(2);
    }
    [Test]
    public async Task Stale_revision_is_rejected_and_history_is_unchanged()
    {
        var d = TestDefinitions.Flow(); await Save(d); await Save(d, 1);
        await FluentActions.Awaiting(() => Save(d, 1)).Should().ThrowAsync<ConflictException>();
        (await _repository.GetVersionsAsync(d.Id, Ct)).Should().HaveCount(2);
    }
    [Test]
    public async Task Duplicate_create_is_conflict()
    {
        var d = TestDefinitions.Flow(); await Save(d);
        await FluentActions.Awaiting(() => Save(d)).Should().ThrowAsync<ConflictException>();
    }
    [Test]
    public async Task Version_history_cannot_be_modified()
    {
        await Save(TestDefinitions.Flow()); var version = await _db.Versions.SingleAsync(); version.OriginalDefinition = "tampered";
        await FluentActions.Awaiting(() => _db.SaveChangesAsync()).Should().ThrowAsync<InvalidOperationException>();
    }
    [Test]
    public async Task Archive_hides_current_definition_and_retains_history()
    {
        var d = TestDefinitions.Flow(); await Save(d); await _repository.DeleteAsync(d.Id, 1, Ct);
        (await _repository.GetAsync(d.Id, Ct)).Should().BeNull(); (await _db.Versions.CountAsync()).Should().Be(1);
        (await _repository.GetDashboardAsync(Ct)).TotalIntegrations.Should().Be(0);
    }
    [Test]
    public async Task Referenced_integration_cannot_be_archived()
    {
        await Save(TestDefinitions.Flow("first")); await Save(TestDefinitions.Flow("second") with { Dependencies = [new("first")] });
        await FluentActions.Awaiting(() => _repository.DeleteAsync(new("first"), 1, Ct)).Should().ThrowAsync<ConflictException>();
    }
    [Test]
    public async Task Search_uses_relational_messages_tags_technology_and_repositories()
    {
        var d = TestDefinitions.Flow(); d = d with { Tags = ["finance"], Messages = [new() { Name = "AccountCreated", Producer = "a", Consumers = ["b"], TopicOrQueue = "accounts" }], Repositories = [new("Source", "https://example.org/repository")], Nodes = [d.Nodes[0] with { Technology = "NServiceBus" }, d.Nodes[1]] };
        await Save(d); var search = new SqlIntegrationSearchService(_db);
        foreach (var term in new[] { "finance", "AccountCreated", "NServiceBus", "repository", "accounts" }) (await search.SearchAsync(new(Q: term), Ct)).Total.Should().Be(1, term);
        (await search.SearchAsync(new(Status: "Production"), Ct)).Total.Should().Be(0);
        (await search.SearchAsync(new(Source: "A"), Ct)).Total.Should().Be(1);
    }
    [Test]
    public async Task Message_catalogue_has_real_occurrences_and_hides_archived_definitions()
    {
        var d = TestDefinitions.Flow() with { Messages = [new() { Name = "Created", Producer = "a", Consumers = ["b"] }] };
        await Save(d); var messages = new MessageRepository(_db);
        (await messages.ListAsync(Ct)).Single().Occurrences.Should().ContainSingle();
        await _repository.DeleteAsync(d.Id, 1, Ct); (await messages.ListAsync(Ct)).Should().BeEmpty();
    }
    [Test]
    public async Task Failed_foreign_key_save_rolls_back_definition_and_version_atomically()
    {
        var definition = TestDefinitions.Flow();
        definition = definition with { Nodes = [definition.Nodes[0] with { SystemId = "missing" }, definition.Nodes[1]] };
        await FluentActions.Awaiting(() => Save(definition)).Should().ThrowAsync<DbUpdateException>();
        await using var verify = new HubDbContext(new DbContextOptionsBuilder<HubDbContext>().UseSqlite(_connection).Options);
        (await verify.Integrations.CountAsync()).Should().Be(0);
        (await verify.Versions.CountAsync()).Should().Be(0);
    }
    [Test]
    public async Task Registered_system_is_queryable_and_referenced_system_cannot_be_deleted()
    {
        var systems = new SystemRepository(_db); await systems.SaveAsync(new() { Id = "erp", Name = "ERP" }, Ct);
        var d = TestDefinitions.Flow(); await Save(d with { Nodes = [d.Nodes[0] with { SystemId = "erp" }, d.Nodes[1]] });
        (await systems.GetAsync("erp", Ct))!.Name.Should().Be("ERP");
        await FluentActions.Awaiting(() => systems.DeleteAsync("erp", Ct)).Should().ThrowAsync<ConflictException>();
    }
}
