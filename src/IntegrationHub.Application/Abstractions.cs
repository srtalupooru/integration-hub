using IntegrationHub.Contracts;
using IntegrationHub.Domain;
namespace IntegrationHub.Application;

public interface IIntegrationDefinitionParser
{
    string Format { get; }
    ParseResult Parse(string source);
}
public interface IDefinitionSerializer { string Serialize(IntegrationDefinition definition, string format); }
public interface IDiagramGenerator { string Generate(IntegrationGraph graph); }
public interface IIntegrationDocumentationGenerator { IntegrationDocumentation Generate(IntegrationDefinition definition, IntegrationGraph graph); }
public interface IDocumentationRenderer { string Format { get; } string Render(IntegrationDocumentation documentation); }
public interface IIntegrationRepository
{
    Task<IntegrationDetail?> GetAsync(IntegrationId id, CancellationToken ct);
    Task<IReadOnlyList<IntegrationDefinition>> GetDefinitionsAsync(CancellationToken ct);
    Task<IntegrationDetail> SaveAsync(IntegrationDefinition definition, string raw, string format, ValidationResult validation, string actor, string changeSummary, int? expectedRevision, bool create, CancellationToken ct);
    Task DeleteAsync(IntegrationId id, int expectedRevision, CancellationToken ct);
    Task<IReadOnlyList<DefinitionVersion>> GetVersionsAsync(IntegrationId id, CancellationToken ct);
    Task<DashboardSummary> GetDashboardAsync(CancellationToken ct);
}
public interface IIntegrationSearchService { Task<PagedResult<IntegrationSummary>> SearchAsync(SearchRequest query, CancellationToken ct); }
public interface ISystemRepository
{
    Task<IReadOnlyList<SystemDefinition>> ListAsync(CancellationToken ct);
    Task<SystemDefinition?> GetAsync(string id, CancellationToken ct);
    Task SaveAsync(SystemDefinition system, CancellationToken ct);
    Task DeleteAsync(string id, CancellationToken ct);
}
public interface IMessageRepository { Task<IReadOnlyList<MessageCatalogueEntry>> ListAsync(CancellationToken ct); }
public interface IIntegrationDefinitionSource { Task<string?> ReadAsync(IntegrationId id, CancellationToken ct); }
public interface IGlobalIntegrationGraphService { Task<GlobalGraph> BuildAsync(CancellationToken ct); }
public interface IImpactAnalysisService
{
    Task<ImpactResult> AnalyzeAsync(string componentId, CancellationToken ct);
    Task<IReadOnlyList<GlobalNode>> GetUpstreamDependencies(string componentId, CancellationToken ct);
    Task<IReadOnlyList<GlobalNode>> GetDownstreamDependencies(string componentId, CancellationToken ct);
    Task<IReadOnlyList<string>> GetAffectedIntegrations(string componentId, CancellationToken ct);
    Task<IReadOnlyList<GlobalNode>> GetDependentSystems(string componentId, CancellationToken ct);
    Task<IReadOnlyList<string>> GetDependencies(string integrationId, CancellationToken ct);
}
public sealed class NotFoundException(string message) : Exception(message);
public sealed class ConflictException(string message) : Exception(message);
public sealed class DefinitionValidationException(ValidationResult validation) : Exception("The integration definition is invalid.") { public ValidationResult Validation { get; } = validation; }
