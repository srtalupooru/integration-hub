using IntegrationHub.Contracts;
using IntegrationHub.Domain;
namespace IntegrationHub.Application;

public interface IComponentDefinitionParser { ComponentParseResult Parse(DefinitionRequest request); }
public interface IComponentDefinitionSerializer { string Serialize(ComponentDefinition definition, string format); }
public interface IComponentRepository
{
    Task<IReadOnlyList<ComponentDetail>> ListAsync(bool includeArchived, CancellationToken ct);
    Task<ComponentDetail?> GetAsync(string id, CancellationToken ct);
    Task<ComponentDetail> SaveAsync(ComponentDefinition definition, DefinitionRequest request, string format, string actor, bool create, CancellationToken ct);
    Task SetArchivedAsync(string id, int expectedRevision, bool archived, string actor, CancellationToken ct);
    Task<IReadOnlyList<ComponentVersion>> VersionsAsync(string id, CancellationToken ct);
}
