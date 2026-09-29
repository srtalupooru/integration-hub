using IntegrationHub.Domain;
using IntegrationHub.Web;
using IntegrationHub.Web.Components.Layout;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Moq;
using MudBlazor;
using MudBlazor.Services;
namespace IntegrationHub.Api.Tests;

[TestFixture]
public sealed class IconRenderingTests
{
    [Test]
    public async Task Every_component_type_has_a_distinct_icon_that_MudBlazor_renders_as_SVG()
    {
        var types = Enum.GetNames<NodeType>();
        types.Select(ComponentVisuals.Icon).Should().OnlyHaveUniqueItems();
        var services = new ServiceCollection().AddLogging();
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        foreach (var type in types)
        {
            var html = await renderer.Dispatcher.InvokeAsync(async () =>
                (await renderer.RenderComponentAsync<MudIcon>(ParameterView.FromDictionary(new Dictionary<string, object?>
                { [nameof(MudIcon.Icon)] = ComponentVisuals.Icon(type) }))).ToHtmlString());
            html.Should().Contain("<svg").And.Contain("<path").And.Contain("stroke=\"currentColor\"")
                .And.Contain("fill=\"none\"").And.NotContain("&lt;path");
        }
    }

    [Test]
    public async Task Navigation_keeps_discovered_details_under_integrations_and_exposes_mobile_menu_controls()
    {
        var services = new ServiceCollection().AddLogging();
        services.AddMudServices();
        services.AddSingleton(Mock.Of<IJSRuntime>());
        services.AddSingleton(Mock.Of<IErrorBoundaryLogger>());
        services.AddSingleton<NavigationManager, TestNavigationManager>();
        services.AddScoped<HubApiClient>();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        await using var renderer = new HtmlRenderer(scope.ServiceProvider, provider.GetRequiredService<ILoggerFactory>());
        var html = await renderer.Dispatcher.InvokeAsync(async () =>
            (await renderer.RenderComponentAsync<MainLayout>(ParameterView.FromDictionary(new Dictionary<string, object?>
            { [nameof(MainLayout.Body)] = (RenderFragment)(builder => builder.AddContent(0, "Integration details")) }))).ToHtmlString());
        html.Should().Contain("aria-label=\"Workspace\"").And.Contain("aria-label=\"Explore\"")
            .And.Contain("href=\"/integrations\" class=\"active\" aria-current=\"location\"")
            .And.Contain("aria-controls=\"workspace-navigation\"").And.Contain("aria-expanded=\"false\"")
            .And.NotContain("href=\"/discovered\"").And.Contain("Integration details")
            .And.Contain("Connections &amp; impact").And.Contain("href=\"/component-messages\"");
        var sidebar = html.Split("<aside class=\"sidebar\">")[1].Split("</aside>")[0];
        sidebar.Should().NotContain("href=\"/search\"").And.NotContain("href=\"/import\"")
            .And.NotContain("href=\"/swagger\"");
    }
    private sealed class TestNavigationManager : NavigationManager
    {
        public TestNavigationManager() => Initialize("http://localhost/", "http://localhost/discovered/auto-flow?view=architecture");
        protected override void NavigateToCore(string uri, bool forceLoad) { }
    }
}
