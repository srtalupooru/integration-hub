using FluentValidation;
using IntegrationHub.Contracts;
using IntegrationHub.Domain;
using Microsoft.Extensions.Logging;
namespace IntegrationHub.Application;

public sealed class DefinitionRequestValidator : AbstractValidator<DefinitionRequest>
{
    public DefinitionRequestValidator()
    {
        RuleFor(x => x.Definition).NotEmpty().MaximumLength(1_000_000)
            .Must(source => source is null || System.Text.Encoding.UTF8.GetByteCount(source) <= 1_000_000).WithMessage("Definition must not exceed 1 MB in UTF-8.");
        RuleFor(x => x.Format).Must(f => f is null or "json" or "yaml" or "yml").WithMessage("Format must be json or yaml.");
        RuleFor(x => x.ChangeSummary).MaximumLength(2000);
        RuleFor(x => x.ExpectedRevision).GreaterThan(0).When(x => x.ExpectedRevision.HasValue);
    }
}
public sealed class DefinitionWorkflow(IEnumerable<IIntegrationDefinitionParser> parsers, IIntegrationGraphBuilder graphBuilder,
    IntegrationGraphValidator graphValidator, IDiagramGenerator diagrams, IIntegrationDocumentationGenerator documentation,
    IIntegrationRepository repository, ISystemRepository systems, ILogger<DefinitionWorkflow> logger)
{
    public async Task<PreviewResult> PreviewAsync(DefinitionRequest request, CancellationToken ct)
    {
        var requestValidation = await new DefinitionRequestValidator().ValidateAsync(request, ct);
        if (!requestValidation.IsValid)
            return new(null, new(requestValidation.Errors.Select(e => new ValidationIssue("REQUEST", ValidationSeverity.Error, e.ErrorMessage, ValidationLevel.Schema, Path: e.PropertyName)).ToArray()), null, null, request.Format ?? "unknown");
        var format = request.Format is null ? (request.Definition.TrimStart().StartsWith('{') || request.Definition.TrimStart().StartsWith('[') ? "json" : "yaml") : request.Format == "yml" ? "yaml" : request.Format;
        var parsed = parsers.Single(p => p.Format == format).Parse(request.Definition);
        if (parsed.Definition is null) return new(null, parsed.Validation, null, null, format);
        var definition = parsed.Definition;
        var issues = parsed.Validation.Issues.Concat(graphValidator.Validate(definition).Issues).ToList();
        // Only load the catalogue when cross-definition references require it; offline previews still work.
        if (definition.Dependencies.Count > 0)
        {
            var definitions = (await repository.GetDefinitionsAsync(ct)).Where(d => d.Id != definition.Id).Append(definition).ToArray();
            var ids = definitions.Select(d => d.Id.Value).ToHashSet();
            foreach (var dependency in definition.Dependencies.Where(d => !ids.Contains(d.IntegrationId)))
                issues.Add(new("UNKNOWN_DEPENDENCY", ValidationSeverity.Error, $"Integration '{dependency.IntegrationId}' is not registered.", SuggestedResolution: "Import the dependency first."));
            var arcs = definitions.SelectMany(d => d.Dependencies.Select(dep => (d.Id.Value, dep.IntegrationId)));
            if (GraphTraversal.HasCycle(ids, arcs)) issues.Add(new("DEPENDENCY_CYCLE", ValidationSeverity.Warning, "The integration dependency graph contains a cycle."));
        }
        if (definition.Nodes.Any(n => n.SystemId is not null))
        {
            var registered = (await systems.ListAsync(ct)).Select(s => s.Id).ToHashSet();
            foreach (var node in definition.Nodes.Where(n => n.SystemId is not null && !registered.Contains(n.SystemId)))
                issues.Add(new("UNKNOWN_SYSTEM", ValidationSeverity.Error, $"System '{node.SystemId}' is not registered.", NodeId: node.Id, SuggestedResolution: "Register the system before saving this definition."));
        }
        var result = new Domain.ValidationResult(issues);
        if (!result.IsValid)
        {
            logger.LogInformation("Definition validation failed for {IntegrationId}; {IssueCount} issues", definition.Id.Value, issues.Count);
            return new(definition, result, null, null, format);
        }
        var graph = graphBuilder.Build(definition);
        logger.LogDebug("Generated graph for {IntegrationId} with {NodeCount} nodes", definition.Id.Value, graph.Nodes.Count);
        return new(definition, result, diagrams.Generate(graph), documentation.Generate(definition, graph), format);
    }
}
public sealed record CreateIntegrationCommand(DefinitionRequest Request, string Actor);
public sealed record UpdateIntegrationCommand(string Id, DefinitionRequest Request, string Actor);
public sealed record DeleteIntegrationCommand(string Id, int ExpectedRevision);
public sealed class IntegrationCommands(DefinitionWorkflow workflow, IIntegrationRepository repository, ILogger<IntegrationCommands> logger)
{
    public Task<IntegrationDetail> Handle(CreateIntegrationCommand command, CancellationToken ct) => SaveAsync(command.Request, command.Actor, null, ct);
    public Task<IntegrationDetail> Handle(UpdateIntegrationCommand command, CancellationToken ct) => SaveAsync(command.Request, command.Actor, command.Id, ct);
    public Task Handle(DeleteIntegrationCommand command, CancellationToken ct) => repository.DeleteAsync(new(command.Id), command.ExpectedRevision, ct);
    private async Task<IntegrationDetail> SaveAsync(DefinitionRequest request, string actor, string? id, CancellationToken ct)
    {
        var preview = await workflow.PreviewAsync(request, ct);
        if (!preview.Validation.IsValid || preview.Definition is null) throw new DefinitionValidationException(preview.Validation);
        if (id is not null && id != preview.Definition.Id.Value) throw new ConflictException("Route ID and definition ID must match.");
        if (id is not null && request.ExpectedRevision is null) throw new ConflictException("An expectedRevision is required to prevent lost updates.");
        var saved = await repository.SaveAsync(preview.Definition, request.Definition, preview.Format, preview.Validation, actor, request.ChangeSummary, request.ExpectedRevision, id is null, ct);
        logger.LogInformation("Imported integration {IntegrationId}, revision {Revision}, actor {Actor}", saved.Definition.Id.Value, saved.Revision, actor);
        return saved;
    }
}
public sealed class IntegrationQueries(IIntegrationRepository repository, IIntegrationGraphBuilder graphs, IDiagramGenerator diagrams, IIntegrationDocumentationGenerator documentation)
{
    public async Task<IntegrationDetail> GetAsync(string id, CancellationToken ct) => await repository.GetAsync(new(id), ct) ?? throw new NotFoundException($"Integration '{id}' was not found.");
    public async Task<string> GetDiagramAsync(string id, CancellationToken ct) => diagrams.Generate(graphs.Build((await GetAsync(id, ct)).Definition));
    public async Task<IntegrationDocumentation> GetDocumentationAsync(string id, CancellationToken ct)
    {
        var definition = (await GetAsync(id, ct)).Definition;
        var generated = documentation.Generate(definition, graphs.Build(definition));
        var versions = await repository.GetVersionsAsync(new(id), ct);
        return generated with { Sections = generated.Sections.Append(new DocumentationSection("Change history", versions.Select(v => $"Revision {v.Revision} · {v.Version} · {v.Timestamp:O} · {v.ChangedBy} · {v.ChangeSummary}").ToArray())).ToArray() };
    }
}
