using IntegrationHub.Web.Components.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Moq;
namespace IntegrationHub.Api.Tests;

[TestFixture]
public sealed class DiagramRenderingTests
{
    [Test]
    public async Task Graph_has_accessible_navigation_layout_export_and_legend()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(Mock.Of<IJSRuntime>());
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var component = await renderer.RenderComponentAsync<Diagram>(ParameterView.FromDictionary(
                new Dictionary<string, object?> { [nameof(Diagram.Markup)] = "flowchart LR\n n0[\"Source\"] --> n1[\"Destination\"]" }));
            return component.ToHtmlString();
        });
        html.Should().Contain("aria-label=\"Zoom in\"").And.Contain("aria-label=\"Zoom out\"")
            .And.Contain("data-graph-action=\"fit\"").And.Contain("data-graph-action=\"actual\"")
            .And.Contain("data-graph-action=\"fullscreen\"").And.Contain("Download SVG")
            .And.Contain("Horizontal").And.Contain("Vertical")
            .And.Contain("tabindex=\"0\"").And.Contain("aria-describedby=")
            .And.Contain("Graph legend").And.Contain("Dashed border: optional");
    }
}
