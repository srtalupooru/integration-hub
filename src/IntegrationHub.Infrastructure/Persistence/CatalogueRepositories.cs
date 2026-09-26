using IntegrationHub.Application;
using IntegrationHub.Contracts;
using IntegrationHub.Domain;
using Microsoft.EntityFrameworkCore;
namespace IntegrationHub.Infrastructure.Persistence;

public sealed class SqlIntegrationSearchService(HubDbContext db) : IIntegrationSearchService
{
    public async Task<PagedResult<IntegrationSummary>> SearchAsync(SearchRequest query, CancellationToken ct)
    {
        var page = Math.Max(1, query.Page); var size = Math.Clamp(query.PageSize, 1, 100);
        var entries = Entries(query);
        var total = await entries.CountAsync(ct);
        var result = await entries.Include(i => i.Tags).OrderByDescending(i => i.UpdatedAt).ThenBy(i => i.Id).Skip((page - 1) * size).Take(size).ToListAsync(ct);
        return new(result.Select(IntegrationRepository.Summary).ToArray(), total, page, size);
    }
    internal IQueryable<IntegrationEntity> Entries(SearchRequest query)
    {
        var entries = db.Integrations.AsNoTracking().Where(i => !i.IsDeleted);
        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var term = query.Q.Trim();
            entries = entries.Where(i => i.Name.Contains(term) || i.Description.Contains(term) || i.Owner.Contains(term) || i.BusinessDomain.Contains(term) || i.Tags.Any(t => t.Value.Contains(term)) || i.Nodes.Any(n => n.Name.Contains(term) || n.Type.Contains(term) || n.Technology.Contains(term) || n.Owner.Contains(term) || (n.SystemId != null && n.SystemId.Contains(term)) || (n.SharedResourceId != null && n.SharedResourceId.Contains(term))) || i.Messages.Any(m => m.MessageName.Contains(term) || m.TopicOrQueue.Contains(term)) || i.Repositories.Any(r => r.Name.Contains(term) || r.Url.Contains(term)));
        }
        if (!string.IsNullOrEmpty(query.Status)) entries = entries.Where(i => i.Status == query.Status);
        if (!string.IsNullOrEmpty(query.Domain)) entries = entries.Where(i => i.BusinessDomain == query.Domain);
        if (!string.IsNullOrEmpty(query.Owner)) entries = entries.Where(i => i.Owner == query.Owner);
        if (!string.IsNullOrEmpty(query.Criticality)) entries = entries.Where(i => i.Criticality == query.Criticality);
        if (!string.IsNullOrEmpty(query.Tag)) entries = entries.Where(i => i.Tags.Any(t => t.Value == query.Tag));
        if (!string.IsNullOrEmpty(query.System)) entries = entries.Where(i => i.Nodes.Any(n => n.SystemId == query.System || n.Name.Contains(query.System)));
        if (!string.IsNullOrEmpty(query.Technology)) entries = entries.Where(i => i.Nodes.Any(n => n.Technology.Contains(query.Technology)));
        if (!string.IsNullOrEmpty(query.Source)) entries = entries.Where(i => i.Nodes.Any(n => n.IsSource && (n.Name.Contains(query.Source) || n.SystemId == query.Source)));
        if (!string.IsNullOrEmpty(query.Destination)) entries = entries.Where(i => i.Nodes.Any(n => n.IsDestination && (n.Name.Contains(query.Destination) || n.SystemId == query.Destination)));
        return entries;
    }
}
public sealed class SystemRepository(HubDbContext db) : ISystemRepository
{
    public async Task<IReadOnlyList<SystemDefinition>> ListAsync(CancellationToken ct) => (await db.Systems.AsNoTracking().OrderBy(s => s.Name).Select(s => s.DefinitionJson).ToListAsync(ct)).Select(IntegrationRepository.Read<SystemDefinition>).ToArray();
    public async Task<SystemDefinition?> GetAsync(string id, CancellationToken ct)
    {
        var entity = await db.Systems.AsNoTracking().SingleOrDefaultAsync(s => s.Id == id, ct);
        return entity is null ? null : IntegrationRepository.Read<SystemDefinition>(entity.DefinitionJson);
    }
    public async Task SaveAsync(SystemDefinition system, CancellationToken ct)
    {
        _ = new IntegrationId(system.Id);
        if (string.IsNullOrWhiteSpace(system.Name) || system.Name.Length > 256) throw new ArgumentException("System name must contain 1–256 characters.");
        if (system.DocumentationUrl is not null && (!Uri.TryCreate(system.DocumentationUrl, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))) throw new ArgumentException("Documentation URL must use HTTP or HTTPS.");
        var entity = await db.Systems.SingleOrDefaultAsync(s => s.Id == system.Id, ct);
        if (entity is null) { entity = new() { Id = system.Id }; db.Systems.Add(entity); }
        entity.Name = system.Name; entity.Owner = system.Owner; entity.BusinessDomain = system.BusinessDomain; entity.DefinitionJson = IntegrationRepository.Json(system);
        await db.SaveChangesAsync(ct);
    }
    public async Task DeleteAsync(string id, CancellationToken ct)
    {
        var entity = await db.Systems.SingleOrDefaultAsync(s => s.Id == id, ct) ?? throw new NotFoundException("System was not found.");
        if (await db.Nodes.AnyAsync(n => n.SystemId == id, ct)) throw new ConflictException("System is referenced by current or archived integrations.");
        db.Systems.Remove(entity); await db.SaveChangesAsync(ct);
    }
}
public sealed class MessageRepository(HubDbContext db) : IMessageRepository
{
    public async Task<IReadOnlyList<MessageCatalogueEntry>> ListAsync(CancellationToken ct)
    {
        var rows = await (from message in db.IntegrationMessages.AsNoTracking() join integration in db.Integrations.AsNoTracking() on message.IntegrationId equals integration.Id where !integration.IsDeleted select new { message.MessageName, message.IntegrationId, IntegrationName = integration.Name, message.DefinitionJson }).ToListAsync(ct);
        return rows.GroupBy(r => r.MessageName).OrderBy(g => g.Key).Select(g => new MessageCatalogueEntry(g.Key, g.Select(r => new MessageOccurrence(r.IntegrationId, r.IntegrationName, IntegrationRepository.Read<IntegrationMessage>(r.DefinitionJson))).ToArray())).ToArray();
    }
}
