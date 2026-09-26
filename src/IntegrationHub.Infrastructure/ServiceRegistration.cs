using IntegrationHub.Application;
using IntegrationHub.Domain;
using IntegrationHub.Infrastructure.Parsing;
using IntegrationHub.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace IntegrationHub.Infrastructure;

public static class ServiceRegistration
{
    public static IServiceCollection AddIntegrationHub(this IServiceCollection services, string connectionString, DatabaseProvider provider = DatabaseProvider.SqlServer)
    {
        if (provider == DatabaseProvider.Sqlite)
        {
            services.AddDbContext<SqliteHubDbContext>(options => options.UseSqlite(connectionString));
            services.AddScoped<HubDbContext>(sp => sp.GetRequiredService<SqliteHubDbContext>());
        }
        else if (provider == DatabaseProvider.SqlServer)
            services.AddDbContext<HubDbContext>(options => options.UseSqlServer(connectionString));
        else throw new ArgumentOutOfRangeException(nameof(provider));
        services.AddSingleton<DefinitionSchema>();
        services.AddSingleton<ComponentDefinitionSchema>();
        services.AddSingleton<ComponentDefinitionParser>();
        services.AddSingleton<IComponentDefinitionParser>(sp => sp.GetRequiredService<ComponentDefinitionParser>());
        services.AddSingleton<IComponentDefinitionSerializer>(sp => sp.GetRequiredService<ComponentDefinitionParser>());
        services.AddSingleton<ComponentDiscovery>();
        services.AddScoped<IComponentRepository, ComponentRepository>();
        services.AddScoped<ComponentWorkflow>();
        services.AddSingleton<IIntegrationDefinitionParser, JsonIntegrationDefinitionParser>();
        services.AddSingleton<IIntegrationDefinitionParser, YamlIntegrationDefinitionParser>();
        services.AddSingleton<IDefinitionSerializer, DefinitionSerializer>();
        services.AddSingleton<IIntegrationGraphBuilder, IntegrationGraphBuilder>();
        services.AddSingleton<IntegrationGraphValidator>();
        services.AddSingleton<IDiagramGenerator, MermaidDiagramGenerator>();
        services.AddSingleton<IIntegrationDocumentationGenerator, IntegrationDocumentationGenerator>();
        services.AddSingleton<IDocumentationRenderer, MarkdownDocumentationRenderer>();
        services.AddSingleton<IDocumentationRenderer, HtmlDocumentationRenderer>();
        services.AddScoped<IntegrationRepository>();
        services.AddScoped<IIntegrationRepository>(sp => sp.GetRequiredService<IntegrationRepository>());
        services.AddScoped<IIntegrationDefinitionSource>(sp => sp.GetRequiredService<IntegrationRepository>());
        services.AddScoped<IIntegrationSearchService, SqlIntegrationSearchService>();
        services.AddScoped<CatalogueSearchService>();
        services.AddScoped<ISystemRepository, SystemRepository>();
        services.AddScoped<IMessageRepository, MessageRepository>();
        services.AddScoped<IGlobalIntegrationGraphService, GlobalIntegrationGraphService>();
        services.AddScoped<IImpactAnalysisService, ImpactAnalysisService>();
        services.AddScoped<DefinitionWorkflow>();
        services.AddScoped<IntegrationCommands>();
        services.AddScoped<IntegrationQueries>();
        services.AddScoped<GlobalGraphQueries>();
        return services;
    }
}
