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
public sealed class DashboardRenderingTests
{
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
