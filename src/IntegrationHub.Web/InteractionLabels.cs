using IntegrationHub.Contracts;
using IntegrationHub.Domain;
namespace IntegrationHub.Web;
public static class InteractionLabels
{
    public static string Describe(DiscoveredConnection c) => c.Kind == ComponentInteractionKind.HttpCall
        ? $"HTTP call: {c.HttpMethod ?? "method unspecified (legacy)"} {c.HttpPath} · v{c.Version}"
        : $"{c.MessageType}: {c.Contract} v{c.Version} ({c.Delivery})";
}
