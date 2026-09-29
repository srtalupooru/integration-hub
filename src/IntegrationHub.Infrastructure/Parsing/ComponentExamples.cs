using IntegrationHub.Application;
namespace IntegrationHub.Infrastructure.Parsing;
public static class ComponentExamples
{
    public static string Read(string name)
    {
        if (name is not ("vendor-api" or "vendor-function" or "elite-api" or "vendor-system" or "vendor-command-api" or "vendor-command-function" or "saga-api" or "saga-worker" or "saga-payment-worker")) throw new NotFoundException("Example was not found.");
        var assembly = typeof(ComponentExamples).Assembly;
        using var stream = assembly.GetManifestResourceStream(assembly.GetManifestResourceNames().Single(n => n.EndsWith("." + name + ".yaml", StringComparison.Ordinal)))!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
