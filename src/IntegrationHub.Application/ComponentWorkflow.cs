using IntegrationHub.Contracts;
using IntegrationHub.Domain;
namespace IntegrationHub.Application;

public sealed class ComponentWorkflow(IComponentDefinitionParser parser, IComponentRepository repository, ComponentDiscovery discovery,
    IIntegrationGraphBuilder graphs, IDiagramGenerator diagrams, IIntegrationDocumentationGenerator documentation)
{
    public async Task<ComponentPreview> PreviewAsync(DefinitionRequest request, CancellationToken ct)
    {
        var parsed = parser.Parse(request);
        if (!parsed.Validation.IsValid || parsed.Definition is null) return new(parsed.Definition, parsed.Format, parsed.Validation, null, [], []);
        var current = await repository.ListAsync(false, ct);
        var before = discovery.Build(current, ct);
        var old = current.SingleOrDefault(c => c.Definition.Id == parsed.Definition.Id);
        var proposed = new ComponentDetail(parsed.Definition, request.Definition, parsed.Format, (old?.Revision ?? 0) + 1, "preview", DateTimeOffset.UnixEpoch, "preview", false);
        var after = discovery.Build(current.Where(c => c.Definition.Id != parsed.Definition.Id).Append(proposed).ToArray(), ct);
        return new(parsed.Definition, parsed.Format, parsed.Validation, after,
            before.Integrations.Select(i => i.Id).Except(after.Integrations.Select(i => i.Id)).ToArray(),
            after.Integrations.Select(i => i.Id).Except(before.Integrations.Select(i => i.Id)).ToArray());
    }
    public async Task<ComponentDetail> SaveAsync(string? id, DefinitionRequest request, string actor, CancellationToken ct)
    {
        var preview = await PreviewAsync(request, ct);
        if (!preview.Validation.IsValid || preview.Definition is null) throw new DefinitionValidationException(preview.Validation);
        if (id is not null && id != preview.Definition.Id) throw new ConflictException("Route ID and component ID must match.");
        if (id is not null && request.ExpectedRevision is null) throw new ConflictException("expectedRevision is required to prevent lost updates.");
        if (id is null && request.ExpectedRevision is not null) throw new ConflictException("Use PUT with the component ID to update an existing revision.");
        return await repository.SaveAsync(preview.Definition, request, preview.Format, actor, id is null, ct);
    }
    public async Task<DiscoveryResult> DiscoverAsync(CancellationToken ct) => discovery.Build(await repository.ListAsync(false, ct), ct);
    public async Task<DiscoveredIntegrationView> GetDiscoveredAsync(string id, CancellationToken ct)
    {
        var result = await DiscoverAsync(ct);
        var integration = result.Integrations.SingleOrDefault(i => i.Id == id) ?? throw new NotFoundException("Discovered integration no longer exists. Its components may have been archived or its membership may have changed. Refresh discovered integrations.");
        var graph = graphs.Build(integration.Definition);
        var doc = documentation.Generate(integration.Definition, graph);
        doc = doc with { Sections = doc.Sections.Concat(new[] {
            new DocumentationSection("Source component revisions", integration.Sources.Select(s => $"{s.Id} · revision {s.Revision} · SHA-256 {s.DefinitionHash}").ToArray()),
            new DocumentationSection("Discovery findings", integration.Issues.Select(i => $"{i.Code} · {i.ComponentId}/{i.BindingId}: {i.Message} {i.Resolution}").DefaultIfEmpty("No findings.").ToArray()),
            new DocumentationSection("HTTP calls", integration.Connections.Where(c => c.Kind == ComponentInteractionKind.HttpCall).Select(c => $"{c.ProducerId}/{c.PublishBindingId} calls {c.ConsumerId}/{c.ConsumeBindingId}: {c.HttpMethod ?? "HTTP (legacy)"} {c.HttpPath}; endpoint version {c.Version}").DefaultIfEmpty("No resolved HTTP calls.").ToArray()),
            new DocumentationSection("Matching evidence", integration.Connections.Select(c => c.Kind == ComponentInteractionKind.HttpCall
                ? $"HTTP call: {c.ProducerId}/{c.PublishBindingId} → {c.ConsumerId}/{c.ConsumeBindingId}: {c.HttpMethod ?? "HTTP (legacy)"} {c.HttpPath}; v{c.Version}"
                : $"{c.ProducerId}/{c.PublishBindingId} → {c.ConsumerId}/{c.ConsumeBindingId}: {c.Contract} v{c.Version}; {c.MessageType}; {c.Channel.Kind} {c.Channel.Namespace}/{c.Channel.Name}; {c.Delivery}; {c.Subscription}").ToArray()) }).ToArray() };
        return new(result.Fingerprint, integration, diagrams.Generate(graph), doc);
    }
}
