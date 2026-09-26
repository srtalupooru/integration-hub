using System.Text.Json;
using IntegrationHub.Contracts;
using IntegrationHub.Web;
using IntegrationHub.Web.Components.Pages;
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
public sealed class CatalogueRenderingTests
{
    [TestCase(false)]
    [TestCase(true)]
    public async Task Catalogue_loads_both_kinds_and_links_to_the_correct_details(bool table)
    {
        var authored = new IntegrationSummary("same-id", "Authored flow", "", "1.0", "Draft", "", "", "Medium", "development", [], 1, DateTimeOffset.UtcNow, 0);
        var response = new PagedResult<CatalogueEntry>([
            new(authored, "Authored"), new(authored with { Name = "Discovered flow" }, "Discovered")], 2, 1, 24);
        var js = new Mock<IJSRuntime>();
        js.Setup(j => j.InvokeAsync<HubApiClient.ApiResponse>("hub.request", It.IsAny<object?[]?>()))
            .ReturnsAsync(new HubApiClient.ApiResponse(200, JsonSerializer.Serialize(response, HubJson.Options)));
        var services = new ServiceCollection().AddLogging();
        services.AddMudServices();
        services.AddSingleton(js.Object);
        services.AddSingleton<NavigationManager, TestNavigationManager>();
        services.AddScoped<HubApiClient>();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        await using var renderer = new HtmlRenderer(scope.ServiceProvider, provider.GetRequiredService<ILoggerFactory>());
        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            await renderer.RenderComponentAsync<MudPopoverProvider>();
            return (await renderer.RenderComponentAsync<LoadedCatalogue>(ParameterView.FromDictionary(
                new Dictionary<string, object?> { [nameof(LoadedCatalogue.Table)] = table }))).ToHtmlString();
        });
        html.Should().Contain("Authored flow").And.Contain("Discovered flow")
            .And.Contain("href=\"/integrations/same-id\"").And.Contain("href=\"/discovered/same-id\"")
            .And.NotContain("No integrations found");
        js.Verify(j => j.InvokeAsync<HubApiClient.ApiResponse>("hub.request",
            It.Is<object?[]?>(args => args != null && (string)args[1]! == "/api/catalogue?kind=All&page=1")), Times.Once);
    }

    public sealed class LoadedCatalogue : Catalogue
    {
        [Parameter] public bool Table { get; set; }
        protected override async Task OnInitializedAsync()
        {
            _table = Table;
            await LoadAsync();
            Loading = false;
        }
    }
    private sealed class TestNavigationManager : NavigationManager
    {
        public TestNavigationManager() => Initialize("http://localhost/", "http://localhost/integrations");
        protected override void NavigateToCore(string uri, bool forceLoad) { }
    }
}
