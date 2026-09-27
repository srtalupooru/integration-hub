using System.Text.Json;
using IntegrationHub.Contracts;
using IntegrationHub.Domain;
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
public sealed class SystemsRenderingTests
{
    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(true, true)]
    public async Task Systems_shows_both_sources_with_distinct_routes_and_excludes_technical_and_archived_components(bool registry, bool components)
    {
        SystemDefinition[] registered = registry ? [new() { Id = "internalsystem", Name = "Registry application" }] : [];
        var definitions = components ? Enum.GetValues<NodeType>().Select(type => new ComponentSummary(
            type.ToString().ToLowerInvariant(), $"Definition {type}", type.ToString(), "development", "Team", "Development",
            0, 0, 1, DateTimeOffset.UtcNow, false)).Append(new("archived", "Archived application", "InternalSystem", "test", "", "Development", 0, 0, 2, DateTimeOffset.UtcNow, true))
            .Append(new("retired", "Retired application", "SaaS", "production", "", "Retired", 0, 0, 1, DateTimeOffset.UtcNow, false)).ToArray() : [];
        var html = await Render(new Dictionary<string, object> { ["/api/systems"] = registered, ["/api/components"] = definitions });
        html.Should().Contain("InternalSystem").And.Contain("ExternalSystem").And.Contain("SaaS").And.Contain("href=\"/components\"");
        if (registry) html.Should().Contain("Registry application").And.Contain("href=\"/systems/internalsystem\"");
        else html.Should().NotContain("Registry application");
        if (components)
        {
            foreach (var type in Enum.GetValues<NodeType>())
            {
                if (type is NodeType.InternalSystem or NodeType.ExternalSystem or NodeType.SaaS)
                    html.Should().Contain($"Definition {type}").And.Contain($"href=\"/components/{type.ToString().ToLowerInvariant()}\"");
                else html.Should().NotContain($"Definition {type}");
            }
            html.Should().NotContain("Archived application").And.Contain("Retired application").And.Contain("Retired");
        }
        if (!registry && !components) html.Should().Contain("No systems yet").And.Contain("Caller example");
        else html.Should().NotContain("No systems yet");
    }

    [Test]
    public async Task Failed_component_request_shows_the_error_without_a_misleading_empty_state()
    {
        var html = await Render(new Dictionary<string, object> { ["/api/systems"] = Array.Empty<SystemDefinition>() }, failComponents: true);
        html.Should().Contain("Component catalogue unavailable").And.NotContain("No systems yet");
    }

    [Test]
    public async Task Registry_details_do_not_request_components_and_encode_authored_text()
    {
        var html = await Render(new Dictionary<string, object>
        {
            ["/api/systems/erp"] = new SystemDefinition { Id = "erp", Name = "ERP <script>", Description = "Registered application", Owner = "Finance" }
        }, id: "erp");
        html.Should().Contain("ERP &lt;script&gt;").And.Contain("Registered application").And.Contain("Finance")
            .And.NotContain("<script>").And.NotContain("No systems yet").And.Contain("All systems");
    }

    private static async Task<string> Render(Dictionary<string, object> responses, bool failComponents = false, string? id = null)
    {
        var js = new Mock<IJSRuntime>();
        js.Setup(j => j.InvokeAsync<HubApiClient.ApiResponse>("hub.request", It.IsAny<object?[]?>()))
            .ReturnsAsync((string _, object?[]? args) =>
            {
                var path = (string)args![1]!;
                return failComponents && path == "/api/components"
                    ? new HubApiClient.ApiResponse(503, "{\"detail\":\"Component catalogue unavailable\"}")
                    : new HubApiClient.ApiResponse(200, JsonSerializer.Serialize(responses[path], HubJson.Options));
            });
        var services = new ServiceCollection().AddLogging();
        services.AddMudServices();
        services.AddSingleton(js.Object);
        services.AddSingleton<NavigationManager, TestNavigationManager>();
        services.AddScoped<HubApiClient>();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        await using var renderer = new HtmlRenderer(scope.ServiceProvider, provider.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () => (await renderer.RenderComponentAsync<LoadedSystems>(
            ParameterView.FromDictionary(new Dictionary<string, object?> { [nameof(Systems.Id)] = id }))).ToHtmlString());
    }

    public sealed class LoadedSystems : Systems
    {
        protected override Task OnInitializedAsync() => ExecuteAsync(LoadAsync);
    }
    private sealed class TestNavigationManager : NavigationManager
    {
        public TestNavigationManager() => Initialize("http://localhost/", "http://localhost/systems");
        protected override void NavigateToCore(string uri, bool forceLoad) { }
    }
}
