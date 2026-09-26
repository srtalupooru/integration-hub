using System.Net;
using System.Net.Http.Json;
using IntegrationHub.Contracts;
using IntegrationHub.Domain;
using IntegrationHub.Infrastructure.Parsing;
using IntegrationHub.Tests;
namespace IntegrationHub.Api.Tests;

[TestFixture]
public sealed class ComponentApiTests
{
    private HubFactory _factory = default!;
    private HttpClient _client = default!;
    [SetUp] public async Task SetUp() { _factory = new(); _client = await _factory.CreateReadyClient(); }
    [TearDown] public void TearDown() { _client.Dispose(); _factory.Dispose(); }
    private async Task<ComponentDetail> Add(string name)
    {
        var response = await _client.PostAsJsonAsync("/api/components", new DefinitionRequest(ComponentExamples.Read(name)));
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<ComponentDetail>(HubJson.Options))!;
    }
    private async Task<DiscoveryResult> Discover() => (await _client.GetFromJsonAsync<DiscoveryResult>("/api/discovery", HubJson.Options))!;
    private async Task<PagedResult<CatalogueEntry>> Catalogue(string query = "") =>
        (await _client.GetFromJsonAsync<PagedResult<CatalogueEntry>>("/api/catalogue" + query, HubJson.Options))!;

    [Test]
    public async Task Combined_catalogue_includes_discovery_without_creating_authored_records_and_pages_both_kinds()
    {
        (await Catalogue()).Total.Should().Be(0);
        await Add("vendor-api");
        (await Catalogue()).Total.Should().Be(0); // An unlinked component is not an integration.
        await Add("vendor-function");
        var generated = (await Catalogue()).Items.Single();
        generated.Origin.Should().Be("Discovered");
        generated.DetailUrl.Should().Be("/discovered/" + generated.Summary.Id);
        (await _client.GetFromJsonAsync<PagedResult<IntegrationSummary>>("/api/integrations", HubJson.Options))!.Total.Should().Be(0);
        // Even identical IDs in the two catalogues retain separate routes and entries.
        (await _client.PostAsJsonAsync("/api/integrations", new DefinitionRequest(TestDefinitions.Json.Replace("test-flow", generated.Summary.Id)))).StatusCode.Should().Be(HttpStatusCode.Created);
        var combined = await Catalogue();
        combined.Total.Should().Be(2);
        combined.Items.Select(e => e.Origin).Should().Equal("Authored", "Discovered");
        combined.Items[0].DetailUrl.Should().Be("/integrations/" + generated.Summary.Id);
        foreach (var page in new[] { 1, 2 })
        {
            var result = await Catalogue($"?pageSize=1&page={page}");
            result.Total.Should().Be(2);
            result.Items.Single().Should().BeEquivalentTo(combined.Items[page - 1]);
        }
        (await Catalogue("?pageSize=1&page=3")).Items.Should().BeEmpty();
        (await Catalogue("?pageSize=100&page=2147483647")).Items.Should().BeEmpty();
        (await Catalogue("?kind=Authored")).Items.Should().ContainSingle(e => e.Origin == "Authored");
        (await Catalogue("?kind=Discovered")).Items.Should().ContainSingle(e => e.Origin == "Discovered");
        (await _client.GetAsync("/api/catalogue?kind=invalid")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [TestCase("q=VENDORS.CREATED", 1)]
    [TestCase("q=%2Fvendors", 1)]
    [TestCase("q=missing-contract", 0)]
    [TestCase("domain=Finance&owner=Integration%20team&technology=Azure%20Functions&tag=discovered&status=Draft&criticality=Medium", 1)]
    [TestCase("system=procurement-system-dev&source=vendor-api-dev&destination=elite-api-dev", 1)]
    [TestCase("source=elite-api-dev", 0)]
    [TestCase("destination=procurement-system-dev", 0)]
    [TestCase("status=Production", 0)]
    public async Task Combined_catalogue_filters_discovered_networks(string query, int expected)
    {
        await Add("vendor-system"); await Add("vendor-api"); await Add("vendor-function"); await Add("elite-api");
        var result = await Catalogue("?" + query);
        result.Total.Should().Be(expected);
        result.Items.Should().HaveCount(expected);
    }

    [Test]
    public async Task Combined_catalogue_recomputes_after_source_edits_archive_and_restore()
    {
        var api = await Add("vendor-api"); await Add("vendor-function");
        var before = (await Catalogue()).Items.Single();
        var updated = await _client.PutAsJsonAsync("/api/components/vendor-api-dev",
            new DefinitionRequest(api.OriginalDefinition.Replace("name: Vendor API", "name: Updated vendor API"), ExpectedRevision: 1));
        updated.EnsureSuccessStatusCode();
        var after = (await Catalogue()).Items.Single();
        after.Summary.Id.Should().Be(before.Summary.Id);
        after.Summary.Name.Should().Contain("Updated vendor API");
        after.Summary.UpdatedAt.Should().BeAfter(before.Summary.UpdatedAt);
        (await _client.DeleteAsync("/api/components/vendor-api-dev?expectedRevision=2")).EnsureSuccessStatusCode();
        (await Catalogue()).Total.Should().Be(0);
        (await _client.PostAsync("/api/components/vendor-api-dev/restore?expectedRevision=3", null)).EnsureSuccessStatusCode();
        (await Catalogue()).Items.Single().Summary.Id.Should().Be(before.Summary.Id);
    }

    [Test]
    public async Task System_calls_api_which_publishes_event_and_function_calls_receiver()
    {
        await Add("vendor-system"); var api = await Add("vendor-api"); await Add("vendor-function"); await Add("elite-api");
        var discovered = await Discover(); var network = discovered.Integrations.Single();
        network.Connections.Count(c => c.Kind == ComponentInteractionKind.HttpCall).Should().Be(2);
        network.Connections.Count(c => c.Kind == ComponentInteractionKind.Message).Should().Be(1);
        network.Definition.Messages.Should().ContainSingle().Which.Type.Should().Be(MessageType.Event);
        var summaries = (await _client.GetFromJsonAsync<ComponentSummary[]>("/api/components", HubJson.Options))!;
        var apiSummary = summaries.Single(c => c.Id == "vendor-api-dev");
        apiSummary.Endpoints.Should().Be(1); apiSummary.Consumes.Should().Be(0); apiSummary.Publishes.Should().Be(1);
        summaries.Single(c => c.Id == "procurement-system-dev").Calls.Should().Be(1);
        var catalogue = (await _client.GetFromJsonAsync<ComponentMessageOccurrence[]>("/api/component-messages", HubJson.Options))!;
        catalogue.Should().HaveCount(2).And.OnlyContain(m => m.Binding.Channel.Kind != ChannelKind.Http);
        var detail = (await _client.GetFromJsonAsync<DiscoveredIntegrationView>($"/api/discovery/{network.Id}", HubJson.Options))!;
        detail.Documentation.Sections.Should().Contain(s => s.Title == "HTTP calls" && s.Lines.Any(l => l.Contains("POST /vendors")));
        // HTTP method changes invalidate a pinned call without affecting the event connection.
        (await _client.PutAsJsonAsync("/api/components/vendor-api-dev", new DefinitionRequest(api.OriginalDefinition.Replace("method: POST", "method: PUT"), ExpectedRevision: 1))).StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await Discover();
        updated.UnlinkedComponentIds.Should().Contain("procurement-system-dev");
        updated.Issues.Should().Contain(i => i.Code == "HTTP_METHOD_MISMATCH");
        updated.Integrations.Single().Connections.Should().HaveCount(2);
    }
    [Test]
    public async Task Sent_command_matches_a_function_consumer_and_is_catalogued_as_a_command()
    {
        await Add("vendor-command-api"); await Add("vendor-command-function");
        var network = (await Discover()).Integrations.Single();
        network.Connections.Should().ContainSingle().Which.MessageType.Should().Be(MessageType.Command);
        var summary = (await _client.GetFromJsonAsync<ComponentSummary[]>("/api/components", HubJson.Options))!.Single(c => c.Id == "vendor-command-api-dev");
        summary.Sends.Should().Be(1); summary.Consumes.Should().Be(0); summary.Publishes.Should().Be(0);
        var messages = (await _client.GetFromJsonAsync<ComponentMessageOccurrence[]>("/api/component-messages", HubJson.Options))!;
        messages.Should().Contain(m => m.ComponentId == "vendor-command-api-dev" && m.Direction == "Sends command");
    }

    [Test]
    public async Task Separate_definitions_generate_a_flow_in_any_order_and_support_all_read_views()
    {
        (await Discover()).Integrations.Should().BeEmpty();
        await Add("vendor-function");
        var alone = await Discover();
        alone.Integrations.Should().BeEmpty(); alone.UnlinkedComponentIds.Should().Equal("vendor-sync-function-dev");
        await Add("vendor-api"); await Add("elite-api");
        var discovery = await Discover();
        var flow = discovery.Integrations.Should().ContainSingle().Subject;
        flow.Sources.Should().HaveCount(3); flow.Connections.Should().HaveCount(2);
        var view = await _client.GetFromJsonAsync<DiscoveredIntegrationView>($"/api/discovery/{flow.Id}", HubJson.Options);
        view!.Diagram.Should().StartWith("flowchart LR").And.Contain("vendors#46;created");
        view.Documentation.Sections.Should().Contain(s => s.Title == "Matching evidence").And.Contain(s => s.Title == "Source component revisions");
        (await _client.GetStringAsync($"/api/discovery/{flow.Id}/documentation?format=markdown")).Should().Contain("Source component revisions");
        (await _client.GetStringAsync($"/api/discovery/{flow.Id}/diagram")).Should().Be(view.Diagram);
        var exported = await _client.GetStringAsync($"/api/discovery/{flow.Id}/definition?format=json");
        var parsed = new JsonIntegrationDefinitionParser(new()).Parse(exported);
        parsed.Validation.IsValid.Should().BeTrue(string.Join(";", parsed.Validation.Issues.Select(i => i.Message)));
        var global = await _client.GetFromJsonAsync<GlobalGraph>("/api/dependencies", HubJson.Options);
        global!.Nodes.Should().Contain(n => n.Id == "component:vendor-api-dev");
        global.Edges.Should().Contain(e => e.From == "component:vendor-api-dev" && e.Mode == InteractionMode.Asynchronous);
        var impact = await _client.GetFromJsonAsync<ImpactResult>("/api/impact?componentId=component:vendor-api-dev", HubJson.Options);
        impact!.Downstream.Should().Contain(n => n.Id == "component:elite-api-dev");
        impact.AffectedIntegrations.Should().Contain("discovered:" + flow.Id);
        (await _client.GetFromJsonAsync<ComponentMessageOccurrence[]>("/api/component-messages", HubJson.Options)).Should().HaveCount(2);
        var dashboard = await _client.GetFromJsonAsync<ComponentDashboard>("/api/component-dashboard", HubJson.Options);
        dashboard!.Components.Should().Be(3); dashboard.Networks.Should().Be(1);
        // The generated network does not silently overwrite or populate the authored catalogue.
        (await _client.GetFromJsonAsync<DashboardSummary>("/api/dashboard", HubJson.Options))!.TotalIntegrations.Should().Be(0);
    }
    [Test]
    public async Task Preview_is_read_only_and_exposes_network_membership_changes()
    {
        await Add("vendor-api");
        var response = await _client.PostAsJsonAsync("/api/components/preview", new DefinitionRequest(ComponentExamples.Read("vendor-function")));
        var preview = (await response.Content.ReadFromJsonAsync<ComponentPreview>(HubJson.Options))!;
        preview.Validation.IsValid.Should().BeTrue(); preview.AddedIntegrationIds.Should().ContainSingle();
        (await Discover()).Integrations.Should().BeEmpty();
        (await _client.GetFromJsonAsync<ComponentSummary[]>("/api/components", HubJson.Options)).Should().ContainSingle();
    }
    [Test]
    public async Task Edits_invalidate_links_archive_removes_them_restore_rebuilds_and_history_is_preserved()
    {
        var api = await Add("vendor-api"); await Add("vendor-function");
        var initial = await Discover(); var id = initial.Integrations.Single().Id;
        var original = api.OriginalDefinition;
        var changed = original.Replace("vendors.created", "vendors.changed", StringComparison.Ordinal);
        var previewResponse = await _client.PostAsJsonAsync("/api/components/preview", new DefinitionRequest(changed, ExpectedRevision: 1));
        var preview = (await previewResponse.Content.ReadFromJsonAsync<ComponentPreview>(HubJson.Options))!;
        preview.RemovedIntegrationIds.Should().Contain(id);
        (await _client.PutAsJsonAsync($"/api/components/{api.Definition.Id}", new DefinitionRequest(changed, ExpectedRevision: 1))).StatusCode.Should().Be(HttpStatusCode.OK);
        (await Discover()).Integrations.Should().BeEmpty();
        (await _client.GetAsync($"/api/discovery/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await _client.PutAsJsonAsync($"/api/components/{api.Definition.Id}", new DefinitionRequest(original, ExpectedRevision: 2))).StatusCode.Should().Be(HttpStatusCode.OK);
        (await Discover()).Integrations.Single().Id.Should().Be(id);
        (await _client.DeleteAsync($"/api/components/{api.Definition.Id}?expectedRevision=3")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await Discover()).Integrations.Should().BeEmpty();
        (await _client.GetFromJsonAsync<ComponentDetail>($"/api/components/{api.Definition.Id}", HubJson.Options))!.IsArchived.Should().BeTrue();
        (await _client.GetFromJsonAsync<ComponentSummary[]>("/api/components", HubJson.Options)).Should().ContainSingle();
        (await _client.GetFromJsonAsync<ComponentSummary[]>("/api/components?includeArchived=true", HubJson.Options)).Should().HaveCount(2);
        (await _client.PostAsync($"/api/components/{api.Definition.Id}/restore?expectedRevision=4", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var restored = await Discover(); restored.Integrations.Single().Id.Should().Be(id); restored.Fingerprint.Should().NotBe(initial.Fingerprint);
        var versions = (await _client.GetFromJsonAsync<ComponentVersion[]>($"/api/components/{api.Definition.Id}/versions", HubJson.Options))!;
        versions.Select(v => v.Revision).Should().Equal(5, 4, 3, 2, 1);
        versions[1].IsArchived.Should().BeTrue(); versions[^1].OriginalDefinition.Should().Be(original);
    }
    [Test]
    public async Task Duplicate_stale_missing_revision_route_mismatch_and_archived_writes_are_rejected()
    {
        var api = await Add("vendor-api"); var path = "/api/components/" + api.Definition.Id;
        (await _client.PostAsJsonAsync("/api/components", new DefinitionRequest(api.OriginalDefinition))).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await _client.PutAsJsonAsync(path, new DefinitionRequest(api.OriginalDefinition))).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await _client.PutAsJsonAsync(path, new DefinitionRequest(api.OriginalDefinition, ExpectedRevision: 99))).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await _client.PutAsJsonAsync("/api/components/different", new DefinitionRequest(api.OriginalDefinition, ExpectedRevision: 1))).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await _client.DeleteAsync(path + "?expectedRevision=99")).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await _client.DeleteAsync(path + "?expectedRevision=1")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await _client.PutAsJsonAsync(path, new DefinitionRequest(api.OriginalDefinition, ExpectedRevision: 2))).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await _client.PostAsJsonAsync("/api/components", new DefinitionRequest(api.OriginalDefinition))).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await _client.PostAsync(path + "/restore?expectedRevision=1", null)).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
    [Test]
    public async Task Invalid_component_never_persists_and_examples_schema_exports_are_available()
    {
        (await _client.PostAsJsonAsync("/api/components", new DefinitionRequest("{}"))).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var check = await _client.PostAsJsonAsync("/api/components/validate", new DefinitionRequest("{}"));
        check.StatusCode.Should().Be(HttpStatusCode.OK);
        (await check.Content.ReadFromJsonAsync<ValidationResult>(HubJson.Options))!.IsValid.Should().BeFalse();
        (await _client.GetFromJsonAsync<ComponentSummary[]>("/api/components", HubJson.Options)).Should().BeEmpty();
        (await _client.GetStringAsync("/api/component-examples/vendor-api")).Should().Be(ComponentExamples.Read("vendor-api"));
        (await _client.GetAsync("/api/component-examples/unknown")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await _client.GetStringAsync("/api/component-schema")).Should().Contain("sourceComponent");
        var component = await Add("vendor-api");
        foreach (var format in new[] { "json", "yaml" })
        {
            var exported = await _client.GetStringAsync($"/api/components/{component.Definition.Id}/definition?format={format}");
            new ComponentDefinitionParser(new()).Parse(new(exported, format)).Validation.IsValid.Should().BeTrue();
        }
    }
    [Test]
    public async Task Component_mutations_enforce_roles_and_csrf()
    {
        using var viewerFactory = new HubFactory("Viewer"); using var viewer = await viewerFactory.CreateReadyClient();
        (await viewer.GetAsync("/api/discovery")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await viewer.PostAsJsonAsync("/api/components", new DefinitionRequest(ComponentExamples.Read("vendor-api")))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        using var editorFactory = new HubFactory("Editor"); using var editor = await editorFactory.CreateReadyClient();
        (await editor.PostAsJsonAsync("/api/components", new DefinitionRequest(ComponentExamples.Read("vendor-api")))).StatusCode.Should().Be(HttpStatusCode.Created);
        (await editor.DeleteAsync("/api/components/vendor-api-dev?expectedRevision=1")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await editor.PostAsync("/api/components/vendor-api-dev/restore?expectedRevision=1", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        _client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        (await _client.PostAsJsonAsync("/api/components", new DefinitionRequest(ComponentExamples.Read("vendor-api")))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
    [Test]
    public async Task Concurrent_edits_never_overwrite_each_other_or_create_duplicate_history()
    {
        var component = await Add("vendor-api");
        var path = "/api/components/" + component.Definition.Id;
        var requests = new[] { "First author", "Second author" }.Select(name => _client.PutAsJsonAsync(path,
            new DefinitionRequest(component.OriginalDefinition.Replace("name: Vendor API", "name: " + name, StringComparison.Ordinal), ExpectedRevision: 1))).ToArray();
        var responses = await Task.WhenAll(requests);
        responses.Count(r => r.StatusCode == HttpStatusCode.OK).Should().Be(1);
        responses.Where(r => r.StatusCode != HttpStatusCode.OK).Should().OnlyContain(r => r.StatusCode == HttpStatusCode.Conflict || r.StatusCode == HttpStatusCode.ServiceUnavailable);
        var current = (await _client.GetFromJsonAsync<ComponentDetail>(path, HubJson.Options))!;
        current.Revision.Should().Be(2);
        var history = (await _client.GetFromJsonAsync<ComponentVersion[]>(path + "/versions", HubJson.Options))!;
        history.Should().HaveCount(2);
        history[0].OriginalDefinition.Should().Be(current.OriginalDefinition);
        history[1].OriginalDefinition.Should().Be(component.OriginalDefinition);
        foreach (var response in responses) response.Dispose();
    }
    [TestCase("/components")]
    [TestCase("/components/new")]
    [TestCase("/discovered")]
    [TestCase("/component-messages")]
    public async Task New_routes_serve_the_interactive_application_shell(string path)
    {
        var response = await _client.GetAsync(path);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        // Prerendering is deliberately disabled; actual page markup is checked with HtmlRenderer.
        html.Should().Contain("_framework/blazor.web.js").And.Contain("<!--Blazor:").And.NotContain("This view could not load");
    }
}
