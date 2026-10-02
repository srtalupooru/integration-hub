using System.Text.Json;
using IntegrationHub.Contracts;
using IntegrationHub.Domain;
using IntegrationHub.Infrastructure.Parsing;
using IntegrationHub.Tests;
using IntegrationHub.Web;
using IntegrationHub.Web.Components.Pages;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Moq;
using MudBlazor.Services;
namespace IntegrationHub.Api.Tests;

[TestFixture]
public sealed class ContentRenderingTests
{
    [TestCase(false)]
    [TestCase(true)]
    public async Task Component_editor_loads_the_form_for_new_and_existing_definitions(bool existing)
    {
        var responses = new Dictionary<string, object>
        {
            ["/api/session"] = new SessionInfo("editor", ["Editor"], "Development", "csrf"),
            ["/api/component-schema"] = new ComponentDefinitionSchema().Text
        };
        var parameters = ParameterView.Empty;
        if (existing)
        {
            var parser = new ComponentDefinitionParser(new());
            var source = ComponentExamples.Read("saga-worker");
            var definition = parser.Parse(new(source)).Definition!;
            responses[$"/api/components/{definition.Id}"] = new ComponentDetail(definition, source, "yaml", 1, "hash", DateTimeOffset.UnixEpoch, "editor", false);
            responses[$"/api/components/{definition.Id}/definition?format=json"] = parser.Serialize(definition, "json");
            parameters = ParameterView.FromDictionary(new Dictionary<string, object?> { [nameof(ComponentEditor.Id)] = definition.Id });
        }
        var html = await Render<LoadedComponentEditor>(responses, parameters);
        html.Should().Contain("field-component-name").And.Contain("Generated definition").And.Contain("Save component")
            .And.Contain("aria-pressed=\"true\"").And.NotContain("Loading the component form");
        if (existing) html.Should().Contain("InvoiceId").And.Contain("Editing revision 1");
        else html.Should().Contain("Nothing is stored until you save.");
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Messages_show_component_roles_and_routes_or_a_useful_empty_state(bool empty)
    {
        ComponentMessageOccurrence[] items = empty ? [] : [new("invoices", "Invoice <script>", "development", "Consumes", new()
        {
            Id = "on-created", Contract = "invoice.created", Version = "1.0", MessageType = MessageType.Event,
            Channel = new() { Kind = ChannelKind.Topic, Namespace = "finance-dev", Name = "invoice-events" }, Subscription = "workers"
        })];
        var html = await Render<LoadedMessages>(new() { ["/api/component-messages"] = items });
        if (empty) html.Should().Contain("No messages yet").And.Contain("Browse components").And.NotContain("<table>");
        else html.Should().Contain("invoice.created").And.Contain("Invoice &lt;script&gt;").And.NotContain("<script>")
            .And.Contain("Consumes").And.Contain("invoice-events").And.Contain("finance-dev").And.Contain("workers")
            .And.Contain("Routing details").And.Contain("href=\"/components/invoices\"").And.NotContain("No messages yet");
    }

    [Test]
    public async Task Failed_message_loading_does_not_claim_the_catalogue_is_empty()
    {
        var html = await Render<LoadedMessages>(new(), fail: true);
        html.Should().Contain("Messages unavailable").And.NotContain("No messages yet").And.NotContain("<table>");
    }

    [Test]
    public async Task Integration_opens_with_its_map_and_preserves_documentation_in_a_disclosure()
    {
        var flow = TestDefinitions.Flow();
        var detail = new IntegrationDetail(flow, TestDefinitions.Json, "json", 1, DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch, "test", "hash", ValidationResult.Success);
        var html = await Render<LoadedIntegration>(new()
        {
            ["/api/integrations/test-flow"] = detail,
            ["/api/integrations/test-flow/diagram"] = "flowchart LR\n a --> b",
            ["/api/integrations/test-flow/documentation"] = new IntegrationDocumentation("test-flow", "Test flow", "Description", "", [new("Security", ["Private connections <script>"])]),
            ["/api/integrations/test-flow/versions"] = Array.Empty<DefinitionVersion>()
        }, ParameterView.FromDictionary(new Dictionary<string, object?> { [nameof(IntegrationDetails.Id)] = "test-flow" }));
        html.Should().Contain("How this integration connects").And.Contain("aria-label=\"Integration graph\"")
            .And.Contain("Read the full documentation").And.Contain("Private connections &lt;script&gt;").And.NotContain("<script>")
            .And.Contain("Connections").And.Contain("Components").And.Contain("Source &amp; history");
    }

    private static async Task<string> Render<T>(Dictionary<string, object> responses, ParameterView? parameters = null, bool fail = false) where T : IComponent
    {
        var js = new Mock<IJSRuntime>();
        js.Setup(j => j.InvokeAsync<HubApiClient.ApiResponse>("hub.request", It.IsAny<object?[]?>()))
            .ReturnsAsync((string _, object?[]? args) =>
            {
                if (fail) return new(503, "{\"detail\":\"Messages unavailable\"}");
                var value = responses[(string)args![1]!];
                return new(200, value is string text ? text : JsonSerializer.Serialize(value, HubJson.Options));
            });
        var services = new ServiceCollection().AddLogging();
        services.AddMudServices(); services.AddSingleton(js.Object);
        services.AddSingleton<NavigationManager, TestNavigationManager>(); services.AddScoped<HubApiClient>();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        await using var renderer = new HtmlRenderer(scope.ServiceProvider, provider.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            await renderer.RenderComponentAsync<MudBlazor.MudPopoverProvider>();
            return (await renderer.RenderComponentAsync<T>(parameters ?? ParameterView.Empty)).ToHtmlString();
        });
    }
    public sealed class LoadedMessages : ComponentMessages { protected override Task OnInitializedAsync() => ExecuteAsync(LoadAsync); }
    public sealed class LoadedComponentEditor : ComponentEditor
    {
        protected override Task OnInitializedAsync() => ExecuteAsync(async () => { await Api.InitialiseAsync(); await LoadAsync(); });
    }
    public sealed class LoadedIntegration : IntegrationDetails { protected override Task OnInitializedAsync() => ExecuteAsync(LoadAsync); }
    private sealed class TestNavigationManager : NavigationManager
    {
        public TestNavigationManager() => Initialize("http://localhost/", "http://localhost/integrations/test-flow");
        protected override void NavigateToCore(string uri, bool forceLoad) { }
    }
}
