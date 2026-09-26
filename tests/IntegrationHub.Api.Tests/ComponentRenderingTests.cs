using IntegrationHub.Contracts;
using IntegrationHub.Domain;
using IntegrationHub.Web.Components.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using IntegrationHub.Web;
using Moq;
using MudBlazor.Services;
namespace IntegrationHub.Api.Tests;

[TestFixture]
public sealed class ComponentRenderingTests
{
    [TestCase(false)]
    [TestCase(true)]
    public async Task Api_http_endpoints_are_not_labelled_as_consumed_messages(bool legacy)
    {
        var services = new ServiceCollection().AddLogging();
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        var definition = new ComponentDefinition { Id = "api", Type = NodeType.Api,
            Endpoints = legacy ? [] : [new() { Id = "create", Method = HttpVerb.POST, Path = "/vendors" }],
            Consumes = legacy ? [new() { Id = "old", Contract = "vendors.create", Version = "1.0", MessageType = MessageType.Request, Channel = new() { Kind = ChannelKind.Http, Name = "/vendors" } }] : [],
            Sends = [new() { Id = "send", Contract = "vendors.create", MessageType = MessageType.Command, Channel = new() { Kind = ChannelKind.Queue, Name = "commands" } }],
            Publishes = [new() { Id = "event", Contract = "vendors.created", MessageType = MessageType.Event, Channel = new() { Kind = ChannelKind.Topic, Name = "events" } }] };
        var html = await renderer.Dispatcher.InvokeAsync(async () => (await renderer.RenderComponentAsync<ComponentInteractions>(ParameterView.FromDictionary(
            new Dictionary<string, object?> { [nameof(ComponentInteractions.Definition)] = definition }))).ToHtmlString());
        html.Should().Contain(legacy ? "Legacy HTTP declarations" : "Exposed HTTP endpoints")
            .And.Contain("Commands sent").And.Contain("Events / messages published").And.NotContain("Commands / events consumed");
    }
    [TestCase(typeof(IntegrationHub.Web.Components.Pages.Components), "Components")]
    [TestCase(typeof(IntegrationHub.Web.Components.Pages.ComponentEditor), "Validate &amp; preview links")]
    [TestCase(typeof(IntegrationHub.Web.Components.Pages.Discovered), "Discovered integrations")]
    [TestCase(typeof(IntegrationHub.Web.Components.Pages.ComponentMessages), "Component messages")]
    public async Task Authoring_and_discovery_pages_render_their_controls(Type componentType, string expected)
    {
        var services = new ServiceCollection().AddLogging();
        services.AddMudServices();
        services.AddSingleton(Mock.Of<IJSRuntime>());
        services.AddSingleton<NavigationManager, TestNavigationManager>();
        services.AddScoped<HubApiClient>();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        await using var renderer = new HtmlRenderer(scope.ServiceProvider, provider.GetRequiredService<ILoggerFactory>());
        var html = await renderer.Dispatcher.InvokeAsync(async () => (await renderer.RenderComponentAsync(componentType, ParameterView.Empty)).ToHtmlString());
        html.Should().Contain(expected).And.NotContain("mud-alert");
    }
    private sealed class TestNavigationManager : NavigationManager
    {
        public TestNavigationManager() => Initialize("http://localhost/", "http://localhost/");
        protected override void NavigateToCore(string uri, bool forceLoad) { }
    }
    [Test]
    public async Task Binding_and_finding_tables_display_real_values_and_encode_authored_text()
    {
        var services = new ServiceCollection().AddLogging();
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        var binding = new MessageBinding { Id = "input", Contract = "vendors.created", Version = "1.0", Description = "<script>alert('x')</script>",
            Channel = new() { Kind = ChannelKind.Topic, Namespace = "broker-dev", Name = "vendors" }, Subscription = "sync" };
        var bindingHtml = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var component = await renderer.RenderComponentAsync<BindingTable>(ParameterView.FromDictionary(new Dictionary<string, object?> { [nameof(BindingTable.Bindings)] = new[] { binding } }));
            return component.ToHtmlString();
        });
        bindingHtml.Should().Contain("vendors.created").And.Contain("broker-dev").And.Contain("sync").And.Contain("&lt;script&gt;").And.NotContain("<script>");
        var findingsHtml = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var component = await renderer.RenderComponentAsync<DiscoveryFindings>(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(DiscoveryFindings.Issues)] = new[] { new DiscoveryIssue("VERSION_MISMATCH", "function", "input", "Exact version differs", "Align the contract version") }
            }));
            return component.ToHtmlString();
        });
        findingsHtml.Should().Contain("VERSION_MISMATCH").And.Contain("/components/function").And.Contain("Align the contract version");
    }
}
