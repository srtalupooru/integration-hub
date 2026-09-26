using IntegrationHub.Web;
using IntegrationHub.Contracts;
using System.Text.Json;
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
public sealed class DashboardRenderingTests
{
    [Test]
    public async Task Dashboard_includes_discovered_integrations_in_totals_and_recent_links()
    {
        var authored = new IntegrationSummary("authored", "Authored flow", "", "1.0", "Draft", "Finance", "Team", "Medium", "development", [], 1, DateTimeOffset.UtcNow, 0);
        var responses = new Dictionary<string, object>
        {
            ["/api/dashboard"] = new DashboardSummary(1, 0, 0, 0, 0, 0, [authored], [], [], []),
            ["/api/component-dashboard"] = new ComponentDashboard(4, 2, 0, 0),
            ["/api/catalogue?pageSize=5"] = new PagedResult<CatalogueEntry>([
                new(authored, "Authored"), new(authored with { Id = "auto-flow", Name = "Discovered flow" }, "Discovered")], 3, 1, 5)
        };
        var js = new Mock<IJSRuntime>();
        js.Setup(j => j.InvokeAsync<HubApiClient.ApiResponse>("hub.request", It.IsAny<object?[]?>()))
            .ReturnsAsync((string _, object?[]? args) => new HubApiClient.ApiResponse(200,
                JsonSerializer.Serialize(responses[(string)args![1]!], HubJson.Options)));
        var services = new ServiceCollection().AddLogging();
        services.AddMudServices();
        services.AddSingleton(js.Object);
        services.AddSingleton<NavigationManager, TestNavigationManager>();
        services.AddScoped<HubApiClient>();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        await using var renderer = new HtmlRenderer(scope.ServiceProvider, provider.GetRequiredService<ILoggerFactory>());
        var html = await renderer.Dispatcher.InvokeAsync(async () => (await renderer.RenderComponentAsync<LoadedDashboard>()).ToHtmlString());
        html.Should().Contain("All integrations").And.Contain("<strong>3</strong>")
            .And.Contain("Discovered flow").And.Contain("href=\"/discovered/auto-flow\"")
            .And.Contain("Authored flow").And.Contain("href=\"/integrations/authored\"");
    }

    public sealed class LoadedDashboard : Dashboard
    {
        protected override async Task OnInitializedAsync() { await LoadAsync(); Loading = false; }
    }

    [TestCase(null)]
    [TestCase("The catalogue database is unavailable.")]
    public async Task Dashboard_only_renders_an_alert_for_an_actual_failure(string? failure)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMudServices();
        services.AddSingleton(Mock.Of<IJSRuntime>());
        services.AddSingleton<NavigationManager, TestNavigationManager>();
        services.AddScoped<HubApiClient>();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        await using var renderer = new HtmlRenderer(scope.ServiceProvider, provider.GetRequiredService<ILoggerFactory>());
        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var component = await renderer.RenderComponentAsync<DashboardWithState>(ParameterView.FromDictionary(new Dictionary<string, object?> { [nameof(DashboardWithState.Failure)] = failure }));
            return component.ToHtmlString();
        });
        html.Should().Contain("Workspace overview");
        if (failure is null) html.Should().NotContain("mud-alert");
        else html.Should().Contain("mud-alert").And.Contain(failure);
    }

    // Render the real Dashboard markup with a controlled loading/error state.
    public sealed class DashboardWithState : Dashboard
    {
        [Parameter] public string? Failure { get; set; }
        protected override void OnInitialized() { Error = Failure; Loading = false; }
    }
    private sealed class TestNavigationManager : NavigationManager
    {
        public TestNavigationManager() => Initialize("http://localhost/", "http://localhost/");
        protected override void NavigateToCore(string uri, bool forceLoad) { }
    }
}
