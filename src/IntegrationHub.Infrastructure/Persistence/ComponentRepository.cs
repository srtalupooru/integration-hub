using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using IntegrationHub.Application;
using IntegrationHub.Contracts;
using IntegrationHub.Domain;
using IntegrationHub.Infrastructure.Parsing;
using Microsoft.EntityFrameworkCore;
namespace IntegrationHub.Infrastructure.Persistence;

public sealed class ComponentRepository(HubDbContext db) : IComponentRepository
{
    private static ComponentDetail Detail(ComponentEntity entity) => new(JsonSerializer.Deserialize<ComponentDefinition>(entity.CanonicalJson, CanonicalJson.Options)!,
        entity.OriginalDefinition, entity.Format, entity.Revision, entity.DefinitionHash, entity.UpdatedAt, entity.ChangedBy, entity.IsArchived);
    public async Task<IReadOnlyList<ComponentDetail>> ListAsync(bool includeArchived, CancellationToken ct) =>
        (await db.Components.AsNoTracking().Where(c => includeArchived || !c.IsArchived).OrderBy(c => c.Id).ToListAsync(ct)).Select(Detail).ToArray();
    public async Task<ComponentDetail?> GetAsync(string id, CancellationToken ct)
    {
        _ = new IntegrationId(id);
        var entity = await db.Components.AsNoTracking().SingleOrDefaultAsync(c => c.Id == id, ct);
        return entity is null ? null : Detail(entity);
    }
    public async Task<ComponentDetail> SaveAsync(ComponentDefinition definition, DefinitionRequest request, string format, string actor, bool create, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var entity = await db.Components.SingleOrDefaultAsync(c => c.Id == definition.Id, ct);
        if (create && entity is not null) throw new ConflictException("This component ID already exists, including archived definitions. Edit or restore the existing component.");
        if (!create && entity is null) throw new NotFoundException("Component was not found.");
        if (!create && (entity!.IsArchived || request.ExpectedRevision != entity.Revision)) throw new ConflictException("Component is archived or its revision has changed. Restore or reload it before editing.");
        if (entity is null) { entity = new() { Id = definition.Id }; db.Components.Add(entity); }
        entity.CanonicalJson = JsonSerializer.Serialize(definition, CanonicalJson.Options);
        entity.OriginalDefinition = request.Definition;
        entity.Format = format;
        entity.DefinitionHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(entity.CanonicalJson))).ToLowerInvariant();
        entity.Revision++;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        entity.ChangedBy = actor;
        AddVersion(entity, definition.Version, request.ChangeSummary);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Detail(entity);
    }
    public async Task SetArchivedAsync(string id, int expectedRevision, bool archived, string actor, CancellationToken ct)
    {
        _ = new IntegrationId(id);
        if (expectedRevision <= 0) throw new ArgumentException("A positive expectedRevision is required.");
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var entity = await db.Components.SingleOrDefaultAsync(c => c.Id == id, ct) ?? throw new NotFoundException("Component was not found.");
        if (entity.Revision != expectedRevision || entity.IsArchived == archived) throw new ConflictException("Component revision or archive state has changed. Reload before continuing.");
        entity.IsArchived = archived;
        entity.Revision++;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        entity.ChangedBy = actor;
        AddVersion(entity, Detail(entity).Definition.Version, archived ? "Archived component" : "Restored component");
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }
    private void AddVersion(ComponentEntity e, string version, string summary) => db.ComponentVersions.Add(new()
    {
        ComponentId = e.Id, Revision = e.Revision, Version = version, Timestamp = e.UpdatedAt, ChangedBy = e.ChangedBy,
        ChangeSummary = summary, DefinitionHash = e.DefinitionHash, Format = e.Format, OriginalDefinition = e.OriginalDefinition,
        CanonicalJson = e.CanonicalJson, IsArchived = e.IsArchived
    });
    public async Task<IReadOnlyList<ComponentVersion>> VersionsAsync(string id, CancellationToken ct)
    {
        _ = new IntegrationId(id);
        if (!await db.Components.AnyAsync(c => c.Id == id, ct)) throw new NotFoundException("Component was not found.");
        return await db.ComponentVersions.AsNoTracking().Where(v => v.ComponentId == id).OrderByDescending(v => v.Revision)
            .Select(v => new ComponentVersion(v.Revision, v.Version, v.Timestamp, v.ChangedBy, v.ChangeSummary, v.DefinitionHash, v.Format, v.OriginalDefinition, v.IsArchived)).ToArrayAsync(ct);
    }
}
