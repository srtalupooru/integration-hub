using System.Security.Claims;
using IntegrationHub.Application;
using IntegrationHub.Contracts;
using IntegrationHub.Domain;
using IntegrationHub.Infrastructure.Parsing;
using IntegrationHub.Infrastructure.Persistence;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
namespace IntegrationHub.Api;

public static class IntegrationEndpoints
{
    public static void MapIntegrationEndpoints(this WebApplication app)
    {
        var api = app.MapGroup("/api").RequireAuthorization("Viewer");
        api.MapGet("/session", (HttpContext context, IAntiforgery antiforgery, IHostEnvironment environment) => new SessionInfo(context.User.Identity?.Name ?? "User", context.User.Claims.Where(c => c.Type is ClaimTypes.Role or "roles").Select(c => c.Value).ToArray(), environment.EnvironmentName, antiforgery.GetAndStoreTokens(context).RequestToken!)).WithSummary("Current authenticated user and CSRF token for browser mutations.");
        api.MapGet("/schema", (DefinitionSchema schema) => Results.Text(schema.Text, "application/schema+json")).WithSummary("Download the authoritative JSON Schema, also applied to YAML imports.");
        api.MapGet("/dashboard", (IIntegrationRepository repository, CancellationToken ct) => repository.GetDashboardAsync(ct)).WithSummary("Live catalogue counts and recent updates; no synthetic metrics.");
        api.MapGet("/integrations", ([AsParameters] SearchRequest query, IIntegrationSearchService search, CancellationToken ct) => search.SearchAsync(query, ct)).WithSummary("Search and filter paged integration catalogue records.");
        api.MapGet("/catalogue", ([AsParameters] SearchRequest query, string? kind, CatalogueSearchService search, CancellationToken ct) => search.SearchAsync(query, kind, ct)).WithSummary("Search authored and discovered integrations together, with shared filtering and pagination.");
        api.MapGet("/search", ([AsParameters] SearchRequest query, IIntegrationSearchService search, CancellationToken ct) => search.SearchAsync(query, ct)).WithSummary("SQL search across integrations, systems, messages, technologies and repositories.");
        api.MapGet("/integrations/{id}", (string id, IntegrationQueries queries, CancellationToken ct) => queries.GetAsync(id, ct)).WithSummary("Get a canonical integration and its original definition.").ProducesProblem(404);
        api.MapPost("/integrations/validate", async (DefinitionRequest request, DefinitionWorkflow workflow, CancellationToken ct) => (await workflow.PreviewAsync(request, ct)).Validation).RequireAuthorization("Editor").WithSummary("Validate syntax, schema and semantics; warnings do not block saving.");
        api.MapPost("/integrations/preview", (DefinitionRequest request, DefinitionWorkflow workflow, CancellationToken ct) => workflow.PreviewAsync(request, ct)).RequireAuthorization("Editor").WithSummary("Validate and generate an architecture and documentation preview without saving.");
        api.MapPost("/integrations", async (DefinitionRequest request, IntegrationCommands commands, HttpContext context, CancellationToken ct) =>
        {
            var result = await commands.Handle(new CreateIntegrationCommand(request, Actor(context)), ct);
            return Results.Created($"/api/integrations/{result.Definition.Id.Value}", result);
        }).RequireAuthorization("Editor").WithSummary("Import a validated definition and create its first immutable revision.").Produces<IntegrationDetail>(201).ProducesProblem(409).ProducesProblem(422);
        api.MapPut("/integrations/{id}", (string id, DefinitionRequest request, IntegrationCommands commands, HttpContext context, CancellationToken ct) => commands.Handle(new UpdateIntegrationCommand(id, request, Actor(context)), ct)).RequireAuthorization("Editor").WithSummary("Replace a definition using expectedRevision for optimistic concurrency.").ProducesProblem(409).ProducesProblem(422);
        api.MapDelete("/integrations/{id}", async (string id, int expectedRevision, IntegrationCommands commands, CancellationToken ct) => { await commands.Handle(new DeleteIntegrationCommand(id, expectedRevision), ct); return Results.NoContent(); }).RequireAuthorization("Admin").WithSummary("Archive an unreferenced integration while preserving historical definitions.");
        api.MapGet("/integrations/{id}/diagram", async (string id, IntegrationQueries queries, CancellationToken ct) => Results.Text(await queries.GetDiagramAsync(id, ct), "text/plain")).WithSummary("Generate Mermaid from the canonical graph.");
        api.MapGet("/integrations/{id}/documentation", async (string id, string? format, IntegrationQueries queries, IEnumerable<IDocumentationRenderer> renderers, CancellationToken ct) =>
        {
            var doc = await queries.GetDocumentationAsync(id, ct);
            if (format is null or "json") return Results.Ok(doc);
            var renderer = renderers.SingleOrDefault(r => r.Format == format) ?? throw new ArgumentException("Format must be json, markdown or html.");
            return Results.Text(renderer.Render(doc), format == "html" ? "text/html" : "text/markdown");
        }).WithSummary("Generate structured documentation or HTML/Markdown exports, including change history.");
        api.MapGet("/integrations/{id}/definition", async (string id, string? format, IntegrationQueries queries, IDefinitionSerializer serializer, CancellationToken ct) =>
        {
            var detail = await queries.GetAsync(id, ct);
            return Results.Text(format is null ? detail.OriginalDefinition : serializer.Serialize(detail.Definition, format), format == "json" ? "application/json" : "text/plain");
        }).WithSummary("Read the original definition or export equivalent canonical JSON/YAML.");
        api.MapGet("/integrations/{id}/versions", (string id, IIntegrationRepository repository, CancellationToken ct) => repository.GetVersionsAsync(new(id), ct)).WithSummary("List immutable revision history with source text and hashes.");
        api.MapGet("/integrations/{id}/dependencies", (string id, IImpactAnalysisService impact, CancellationToken ct) => impact.GetDependencies(id, ct)).WithSummary("Traverse declared upstream integration dependencies.");
        api.MapGet("/integrations/{id}/impact", (string id, IImpactAnalysisService impact, CancellationToken ct) => impact.AnalyzeAsync($"integration:{id}", ct)).WithSummary("Find integrations transitively dependent on this integration.");
        api.MapGet("/dependencies/diagram", async (GlobalGraphQueries queries, CancellationToken ct) => Results.Text(await queries.GetDiagramAsync(ct), "text/plain")).WithSummary("Render the global relationship graph through the diagram adapter.");
        api.MapGet("/dependencies", (IGlobalIntegrationGraphService graphs, CancellationToken ct) => graphs.BuildAsync(ct)).WithSummary("Global graph with stable system/resource identities and explicit dependency relationships.");
        api.MapGet("/impact", (string componentId, IImpactAnalysisService impact, CancellationToken ct) => impact.AnalyzeAsync(componentId, ct)).WithSummary("Cycle-safe upstream/downstream and affected integration analysis for any global component ID.");
        api.MapGet("/systems", (ISystemRepository repository, CancellationToken ct) => repository.ListAsync(ct)).WithSummary("List registered enterprise systems.");
        api.MapGet("/systems/{id}", async (string id, ISystemRepository repository, CancellationToken ct) => await repository.GetAsync(id, ct) ?? throw new NotFoundException("System was not found.")).WithSummary("Get a registered system.");
        api.MapPut("/systems/{id}", async (string id, SystemDefinition system, ISystemRepository repository, CancellationToken ct) => { if (id != system.Id) throw new ArgumentException("Route ID and system ID must match."); await repository.SaveAsync(system, ct); return Results.Ok(system); }).RequireAuthorization("Admin").WithSummary("Register or update an enterprise system.");
        api.MapDelete("/systems/{id}", async (string id, ISystemRepository repository, CancellationToken ct) => { await repository.DeleteAsync(id, ct); return Results.NoContent(); }).RequireAuthorization("Admin").WithSummary("Delete an unreferenced system.");
        api.MapGet("/messages", (IMessageRepository repository, CancellationToken ct) => repository.ListAsync(ct)).WithSummary("Message and event catalogue with producer and consumer occurrences.");
        api.MapGet("/messages/{name}", async (string name, IMessageRepository repository, CancellationToken ct) => (await repository.ListAsync(ct)).SingleOrDefault(m => m.Name == name) ?? throw new NotFoundException("Message was not found.")).WithSummary("Find all producers and consumers of a message across integrations.");
    }
    private static string Actor(HttpContext context) => context.User.FindFirstValue("oid") ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? context.User.Identity?.Name ?? "unknown";
}
