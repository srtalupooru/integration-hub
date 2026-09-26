namespace IntegrationHub.Web;
public static class CatalogueLinks
{
    public static string Integration(string id) => id.StartsWith("discovered:", StringComparison.Ordinal)
        ? "/discovered/" + Uri.EscapeDataString(id["discovered:".Length..]) : "/integrations/" + Uri.EscapeDataString(id);
}
