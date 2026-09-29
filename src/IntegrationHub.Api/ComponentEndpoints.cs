using System.Security.Claims;
using IntegrationHub.Application;
using IntegrationHub.Contracts;
using IntegrationHub.Domain;
using IntegrationHub.Infrastructure.Parsing;
namespace IntegrationHub.Api;

public static class ComponentEndpoints
{
    public static void MapComponentEndpoints(this WebApplication app)
    {
        var api = app.MapGroup("/api").RequireAuthorization("Viewer");
        api.MapGet("/component-schema", (ComponentDefinitionSchema schema) => Results.Text(schema.Text, "application/schema+json"));
        api.MapGet("/component-examples/{name}", (string name) => Results.Text(ComponentExamples.Read(name), "text/plain"));
        api.MapGet("/component-dashboard", async (ComponentWorkflow workflow, CancellationToken ct) =>
        {
            var result = await workflow.DiscoverAsync(ct);
            return new ComponentDashboard(result.Integrations.SelectMany(i => i.Sources).Select(s => s.Id).Distinct().Count() + result.UnlinkedComponentIds.Count + result.RetiredComponentIds.Count,
                result.Integrations.Count, result.UnlinkedComponentIds.Count, result.Issues.Count);
        });
        api.MapGet("/components", async (bool? includeArchived, IComponentRepository repository, CancellationToken ct) =>
            (await repository.ListAsync(includeArchived == true, ct)).Select(c => new ComponentSummary(c.Definition.Id, c.Definition.Name, c.Definition.Type.ToString(),
                c.Definition.Environment, c.Definition.Owner, c.Definition.Status.ToString(), c.Definition.ConsumedMessages.Count(b => b.Channel.Kind != ChannelKind.Http),
                c.Definition.PublishedMessages.Count(b => b.Channel.Kind != ChannelKind.Http && b.MessageType != MessageType.Command), c.Revision, c.UpdatedAt, c.IsArchived,
                c.Definition.Endpoints.Count + c.Definition.ConsumedMessages.Count(b => b.Channel.Kind == ChannelKind.Http),
                c.Definition.Calls.Count + c.Definition.PublishedMessages.Count(b => b.Channel.Kind == ChannelKind.Http),
                c.Definition.SentMessages.Count + c.Definition.PublishedMessages.Count(b => b.Channel.Kind != ChannelKind.Http && b.MessageType == MessageType.Command))).ToArray());
        api.MapGet("/components/{id}", async (string id, IComponentRepository repository, CancellationToken ct) => await repository.GetAsync(id, ct) ?? throw new NotFoundException("Component was not found."));
        api.MapGet("/components/{id}/versions", (string id, IComponentRepository repository, CancellationToken ct) => repository.VersionsAsync(id, ct));
        api.MapGet("/components/{id}/definition", async (string id, string? format, IComponentRepository repository, IComponentDefinitionSerializer serializer, CancellationToken ct) =>
        {
            var component = await repository.GetAsync(id, ct) ?? throw new NotFoundException("Component was not found.");
            return Results.Text(format is null ? component.OriginalDefinition : serializer.Serialize(component.Definition, format), format == "json" ? "application/json" : "text/plain");
        });
        api.MapPost("/components/validate", (DefinitionRequest request, IComponentDefinitionParser parser) => parser.Parse(request).Validation).RequireAuthorization("Editor");
        api.MapPost("/components/preview", (DefinitionRequest request, ComponentWorkflow workflow, CancellationToken ct) => workflow.PreviewAsync(request, ct)).RequireAuthorization("Editor")
            .WithSummary("Validate a component and preview the recomputed catalogue, including network merges/splits, without saving.");
        api.MapPost("/components", async (DefinitionRequest request, ComponentWorkflow workflow, HttpContext context, CancellationToken ct) =>
        {
            var saved = await workflow.SaveAsync(null, request, Actor(context), ct);
            return Results.Created($"/api/components/{saved.Definition.Id}", saved);
        }).RequireAuthorization("Editor");
        api.MapPut("/components/{id}", (string id, DefinitionRequest request, ComponentWorkflow workflow, HttpContext context, CancellationToken ct) =>
            workflow.SaveAsync(id, request, Actor(context), ct)).RequireAuthorization("Editor");
        api.MapDelete("/components/{id}", async (string id, int expectedRevision, IComponentRepository repository, HttpContext context, CancellationToken ct) =>
        {
            await repository.SetArchivedAsync(id, expectedRevision, true, Actor(context), ct); return Results.NoContent();
        }).RequireAuthorization("Admin").WithSummary("Archive a component. Discovery is recomputed from active definitions; history is retained.");
        api.MapPost("/components/{id}/restore", async (string id, int expectedRevision, IComponentRepository repository, HttpContext context, CancellationToken ct) =>
        {
            await repository.SetArchivedAsync(id, expectedRevision, false, Actor(context), ct); return Results.NoContent();
        }).RequireAuthorization("Admin");
        api.MapGet("/component-messages", async (IComponentRepository repository, CancellationToken ct) =>
            (await repository.ListAsync(false, ct)).SelectMany(c => c.Definition.ConsumedMessages.Where(b => b.Channel.Kind != ChannelKind.Http).Select(b => new ComponentMessageOccurrence(c.Definition.Id, c.Definition.Name, c.Definition.Environment, "Consumes", b))
                .Concat(c.Definition.PublishedMessages.Concat(c.Definition.SentMessages).Where(b => b.Channel.Kind != ChannelKind.Http).Select(b => new ComponentMessageOccurrence(c.Definition.Id, c.Definition.Name, c.Definition.Environment,
                    b.MessageType == MessageType.Command ? "Sends command" : b.MessageType == MessageType.Event ? "Publishes event" : "Publishes message", b)))).ToArray());
        api.MapGet("/discovery", (ComponentWorkflow workflow, CancellationToken ct) => workflow.DiscoverAsync(ct)).WithSummary("Derive connected integration networks from HTTP endpoint references and exact command/event routes.");
        api.MapGet("/discovery/{id}", (string id, ComponentWorkflow workflow, CancellationToken ct) => workflow.GetDiscoveredAsync(id, ct));
        api.MapGet("/discovery/{id}/diagram", async (string id, ComponentWorkflow workflow, CancellationToken ct) => Results.Text((await workflow.GetDiscoveredAsync(id, ct)).Diagram, "text/plain"));
        api.MapGet("/discovery/{id}/definition", async (string id, string? format, ComponentWorkflow workflow, IDefinitionSerializer serializer, CancellationToken ct) =>
            Results.Text(serializer.Serialize((await workflow.GetDiscoveredAsync(id, ct)).Integration.Definition, format ?? "yaml"), format == "json" ? "application/json" : "text/plain"));
        api.MapGet("/discovery/{id}/documentation", async (string id, string? format, ComponentWorkflow workflow, IEnumerable<IDocumentationRenderer> renderers, CancellationToken ct) =>
        {
            var doc = (await workflow.GetDiscoveredAsync(id, ct)).Documentation;
            if (format is null or "json") return Results.Ok(doc);
            var renderer = renderers.SingleOrDefault(r => r.Format == format) ?? throw new ArgumentException("Format must be json, markdown or html.");
            return Results.Text(renderer.Render(doc), format == "html" ? "text/html" : "text/markdown");
        });
    }
    private static string Actor(HttpContext context) => context.User.FindFirstValue("oid") ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? context.User.Identity?.Name ?? "unknown";
}
