using System.Net;
using System.Net.Http.Json;
using IntegrationHub.Contracts;
using IntegrationHub.Infrastructure.Persistence;
using IntegrationHub.Tests;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
namespace IntegrationHub.Api.Tests;

public sealed class HubFactory(string role = "Admin") : WebApplicationFactory<Program>
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "integrationhub-api-tests", Guid.NewGuid().ToString("N"));
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseStaticWebAssets();
        builder.UseSetting("Authentication:Mode", "Development");
        builder.UseSetting("Authentication:DevelopmentRole", role);
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?> { ["Authentication:Mode"] = "Development", ["Authentication:DevelopmentRole"] = role }));
        builder.UseSetting("Database:Provider", "Sqlite");
        builder.UseSetting("Database:InitializeSqliteOnStartup", "true");
        builder.UseSetting("ConnectionStrings:IntegrationHub", new SqliteConnectionStringBuilder { DataSource = Path.Combine(_directory, "catalogue.db"), Pooling = false }.ToString());
    }
    public async Task<HttpClient> CreateReadyClient()
    {
        var client = CreateClient();
        var session = await client.GetFromJsonAsync<SessionInfo>("/api/session", HubJson.Options);
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", session!.CsrfToken);
        return client;
    }
    protected override void Dispose(bool disposing) { base.Dispose(disposing); if (disposing && Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true); }
}
[TestFixture]
public sealed class ApiTests
{
    private HubFactory _factory = default!;
    private HttpClient _client = default!;
    [SetUp] public async Task SetUp() { _factory = new(); _client = await _factory.CreateReadyClient(); }
    [TearDown] public void TearDown() { _client?.Dispose(); _factory?.Dispose(); }
    [Test]
    public async Task Empty_dashboard_and_catalogue_have_no_seed_data()
    {
        var dashboard = await _client.GetFromJsonAsync<DashboardSummary>("/api/dashboard", HubJson.Options);
        dashboard!.TotalIntegrations.Should().Be(0);
        var page = await _client.GetFromJsonAsync<PagedResult<IntegrationSummary>>("/api/integrations", HubJson.Options);
        page!.Items.Should().BeEmpty();
    }
    [Test]
    public async Task Complete_import_read_preview_export_update_and_archive_workflow()
    {
        var preview = await _client.PostAsJsonAsync("/api/integrations/preview", new DefinitionRequest(TestDefinitions.Yaml));
        preview.StatusCode.Should().Be(HttpStatusCode.OK);
        (await preview.Content.ReadFromJsonAsync<PreviewResult>(HubJson.Options))!.Diagram.Should().StartWith("flowchart LR");
        var create = await _client.PostAsJsonAsync("/api/integrations", new DefinitionRequest(TestDefinitions.Yaml));
        create.StatusCode.Should().Be(HttpStatusCode.Created); create.Headers.Location!.ToString().Should().Be("/api/integrations/test-flow");
        var detail = await _client.GetFromJsonAsync<IntegrationDetail>("/api/integrations/test-flow", HubJson.Options);
        detail!.Definition.Name.Should().Be("Test flow"); detail.Revision.Should().Be(1);
        (await _client.GetStringAsync("/api/integrations/test-flow/documentation?format=markdown")).Should().Contain("## Architecture");
        (await _client.PutAsJsonAsync("/api/integrations/test-flow", new DefinitionRequest(TestDefinitions.Json, ExpectedRevision: 1))).StatusCode.Should().Be(HttpStatusCode.OK);
        (await _client.GetFromJsonAsync<DefinitionVersion[]>("/api/integrations/test-flow/versions", HubJson.Options)).Should().HaveCount(2);
        (await _client.DeleteAsync("/api/integrations/test-flow?expectedRevision=2")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await _client.GetAsync("/api/integrations/test-flow")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
    [Test]
    public async Task Semantic_error_returns_422_problem_details()
    {
        var response = await _client.PostAsJsonAsync("/api/integrations", new DefinitionRequest(TestDefinitions.Json.Replace("\"to\":\"b\"", "\"to\":\"missing\"")));
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        (await response.Content.ReadAsStringAsync()).Should().Contain("UNKNOWN_NODE").And.Contain("Semantic");
    }
    [Test]
    public async Task Syntax_and_schema_failures_are_returned_by_validation_endpoint()
    {
        foreach (var (source, level) in new[] { ("{", "Syntax"), ("{}", "Schema") })
        { var response = await _client.PostAsJsonAsync("/api/integrations/validate", new DefinitionRequest(source)); response.EnsureSuccessStatusCode(); (await response.Content.ReadAsStringAsync()).Should().Contain(level); }
    }
    [Test]
    public async Task Duplicate_create_and_stale_update_return_conflict()
    {
        await _client.PostAsJsonAsync("/api/integrations", new DefinitionRequest(TestDefinitions.Json));
        (await _client.PostAsJsonAsync("/api/integrations", new DefinitionRequest(TestDefinitions.Json))).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await _client.PutAsJsonAsync("/api/integrations/test-flow", new DefinitionRequest(TestDefinitions.Json, ExpectedRevision: 3))).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
    [Test]
    public async Task Browser_writes_require_csrf_token()
    {
        _client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        (await _client.PostAsJsonAsync("/api/integrations", new DefinitionRequest(TestDefinitions.Json))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
    [Test]
    public async Task Viewer_can_read_but_cannot_import_or_manage_systems()
    {
        using var factory = new HubFactory("Viewer"); using var viewer = await factory.CreateReadyClient();
        (await viewer.GetAsync("/api/integrations")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await viewer.PostAsJsonAsync("/api/integrations", new DefinitionRequest(TestDefinitions.Json))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await viewer.DeleteAsync("/api/systems/erp")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
    [TestCase("/_content/IntegrationHub.Web/hub.css", "text/css")]
    [TestCase("/_content/IntegrationHub.Web/workspace.css", "text/css")]
    [TestCase("/_content/IntegrationHub.Web/hub.js", "text/javascript")]
    [TestCase("/_content/IntegrationHub.Web/diagram-viewer.mjs", "text/javascript")]
    [TestCase("/_content/IntegrationHub.Web/diagram-nodes.mjs", "text/javascript")]
    [TestCase("/_content/IntegrationHub.Web/diagram-viewport.mjs", "text/javascript")]
    [TestCase("/_content/IntegrationHub.Web/vendor/mermaid/mermaid.esm.min.mjs", "text/javascript")]
    public async Task Browser_assets_are_served_locally_with_executable_mime_types(string path, string mimeType)
    {
        var response = await _client.GetAsync(path);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be(mimeType);
        (await response.Content.ReadAsStringAsync()).Should().NotBeEmpty();
    }
    [Test]
    public async Task Editor_can_import_but_cannot_archive_or_manage_systems()
    {
        using var factory = new HubFactory("Editor"); using var editor = await factory.CreateReadyClient();
        (await editor.PostAsJsonAsync("/api/integrations", new DefinitionRequest(TestDefinitions.Json))).StatusCode.Should().Be(HttpStatusCode.Created);
        (await editor.DeleteAsync("/api/integrations/test-flow?expectedRevision=1")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await editor.PutAsJsonAsync("/api/systems/erp", new IntegrationHub.Domain.SystemDefinition { Id = "erp", Name = "ERP" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
    [Test]
    public void Development_authentication_is_rejected_in_production()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Authentication:Mode"] = "Development" }).Build();
        var environment = Moq.Mock.Of<Microsoft.Extensions.Hosting.IHostEnvironment>(e => e.EnvironmentName == "Production");
        var action = () => IntegrationHub.Api.Security.HubSecurity.AddHubSecurity(new ServiceCollection(), config, environment);
        action.Should().Throw<InvalidOperationException>().WithMessage("*only allowed*");
    }
    [Test]
    public async Task Readiness_rejects_a_database_without_the_catalogue_schema()
    {
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<HubDbContext>().Database.ExecuteSqlRawAsync("DROP TABLE Integrations");
        (await _client.GetAsync("/health/ready")).StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await _client.GetAsync("/health/live")).StatusCode.Should().Be(HttpStatusCode.OK);
    }
    [Test]
    public async Task Missing_resource_returns_problem_details_and_health_checks_work()
    {
        var response = await _client.GetAsync("/api/integrations/missing");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound); (await response.Content.ReadAsStringAsync()).Should().Contain("traceId");
        (await _client.GetAsync("/health/live")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await _client.GetAsync("/health/ready")).StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
