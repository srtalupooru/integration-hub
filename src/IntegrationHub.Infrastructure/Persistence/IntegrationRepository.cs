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

public sealed class IntegrationRepository(HubDbContext db, IIntegrationGraphBuilder graphs) : IIntegrationRepository, IIntegrationDefinitionSource
{
    internal static string Json<T>(T value) => JsonSerializer.Serialize(value, CanonicalJson.Options);
    internal static T Read<T>(string json) => JsonSerializer.Deserialize<T>(json, CanonicalJson.Options)!;
    internal static IntegrationDetail Detail(IntegrationEntity e) => new(Read<IntegrationDefinition>(e.CanonicalJson), e.OriginalDefinition, e.Format, e.Revision, e.CreatedAt, e.UpdatedAt, e.CreatedBy, e.DefinitionHash, Read<ValidationResult>(e.ValidationJson));
    internal static IntegrationSummary Summary(IntegrationEntity e) => new(e.Id, e.Name, e.Description, e.Version, e.Status, e.BusinessDomain, e.Owner, e.Criticality, e.Environment, e.Tags.Select(t => t.Value).ToArray(), e.Revision, e.UpdatedAt, e.WarningCount);
    public async Task<IntegrationDetail?> GetAsync(IntegrationId id, CancellationToken ct)
    {
        var entity = await db.Integrations.AsNoTracking().SingleOrDefaultAsync(e => e.Id == id.Value && !e.IsDeleted, ct);
        return entity is null ? null : Detail(entity);
    }
    public async Task<string?> ReadAsync(IntegrationId id, CancellationToken ct) => (await GetAsync(id, ct))?.OriginalDefinition;
    public async Task<IReadOnlyList<IntegrationDefinition>> GetDefinitionsAsync(CancellationToken ct) => (await db.Integrations.AsNoTracking().Where(e => !e.IsDeleted).Select(e => e.CanonicalJson).ToListAsync(ct)).Select(Read<IntegrationDefinition>).ToArray();
    public async Task<IntegrationDetail> SaveAsync(IntegrationDefinition definition, string raw, string format, ValidationResult validation, string actor, string changeSummary, int? expectedRevision, bool create, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var id = definition.Id.Value;
            var entity = await db.Integrations.SingleOrDefaultAsync(e => e.Id == id, ct);
            if (create && entity is not null) throw new ConflictException("This integration ID already exists or has been archived.");
            if (!create && (entity is null || entity.IsDeleted)) throw new NotFoundException($"Integration '{id}' was not found.");
            if (!create && entity!.Revision != expectedRevision) throw new ConflictException("The definition has changed. Reload it before saving.");
            if (entity is null)
            {
                entity = new IntegrationEntity { Id = id, CreatedAt = DateTimeOffset.UtcNow, CreatedBy = actor };
                db.Integrations.Add(entity);
            }
            else
            {
                // Explicit deletes avoid duplicate composite keys when replacing the read model.
                await db.Nodes.Where(n => n.IntegrationId == id).ExecuteDeleteAsync(ct);
                await db.Set<IntegrationEdgeEntity>().Where(e => e.IntegrationId == id).ExecuteDeleteAsync(ct);
                await db.IntegrationMessages.Where(m => m.IntegrationId == id).ExecuteDeleteAsync(ct);
                await db.Set<TagEntity>().Where(t => t.IntegrationId == id).ExecuteDeleteAsync(ct);
                await db.Set<RepositoryEntity>().Where(r => r.IntegrationId == id).ExecuteDeleteAsync(ct);
                await db.Set<RunbookEntity>().Where(r => r.IntegrationId == id).ExecuteDeleteAsync(ct);
                await db.Set<DependencyEntity>().Where(d => d.IntegrationId == id).ExecuteDeleteAsync(ct);
                // A context may be reused by an importer; detach stale children after the bulk deletes.
                foreach (var entry in db.ChangeTracker.Entries().Where(e => e.Entity is IntegrationNodeEntity or IntegrationEdgeEntity or IntegrationMessageEntity or TagEntity or RepositoryEntity or RunbookEntity or DependencyEntity).ToArray()) entry.State = EntityState.Detached;
            }
            foreach (var dependency in definition.Dependencies)
                if (!await db.Integrations.AnyAsync(e => e.Id == dependency.IntegrationId && !e.IsDeleted, ct)) throw new ConflictException("A referenced integration no longer exists.");
            entity.Name = definition.Name; entity.Description = definition.Description; entity.Version = definition.Version;
            entity.Status = definition.Status.ToString(); entity.BusinessDomain = definition.BusinessDomain; entity.Owner = definition.Ownership.Team;
            entity.Criticality = definition.Criticality.ToString(); entity.Environment = definition.Environment;
            entity.CanonicalJson = Json(definition); entity.OriginalDefinition = raw; entity.Format = format;
            entity.DefinitionHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();
            entity.ValidationJson = Json(validation); entity.WarningCount = validation.Issues.Count(i => i.Severity == ValidationSeverity.Warning);
            entity.Revision++; entity.UpdatedAt = DateTimeOffset.UtcNow;
            var graph = graphs.Build(definition);
            entity.Nodes = definition.Nodes.Select(n => new IntegrationNodeEntity { IntegrationId = id, Id = n.Id, Name = n.Name, Type = n.Type.ToString(), Technology = n.Technology, Owner = n.Owner, SystemId = n.SystemId, SharedResourceId = n.SharedResourceId, IsSource = graph.EntryNodes.Contains(n.Id), IsDestination = graph.ExitNodes.Contains(n.Id), DefinitionJson = Json(n) }).ToList();
            entity.Edges = definition.Edges.Select(e => new IntegrationEdgeEntity { IntegrationId = id, Id = e.Id, FromNodeId = e.FromNodeId, ToNodeId = e.ToNodeId, Label = e.Label, Protocol = e.Protocol, MessageName = e.MessageName, DefinitionJson = Json(e) }).ToList();
            var names = definition.AllMessages.Select(m => m.Name).ToArray();
            var existingNames = await db.Set<MessageEntity>().Where(m => names.Contains(m.Name)).Select(m => m.Name).ToListAsync(ct);
            foreach (var name in names.Except(existingNames)) db.Set<MessageEntity>().Add(new() { Name = name });
            entity.Messages = definition.AllMessages.Select(m => new IntegrationMessageEntity { IntegrationId = id, MessageName = m.Name, Producer = m.Producer, Type = m.Type.ToString(), TopicOrQueue = m.TopicOrQueue, Version = m.Version, DefinitionJson = Json(m) }).ToList();
            entity.Tags = definition.Tags.Distinct().Select(t => new TagEntity { IntegrationId = id, Value = t }).ToList();
            entity.Repositories = definition.Repositories.Concat(definition.Nodes.Where(n => n.RepositoryUrl is not null).Select(n => new NamedLink(n.Name, n.RepositoryUrl!))).Select(r => new RepositoryEntity { IntegrationId = id, Name = r.Name, Url = r.Url }).ToList();
            entity.Runbooks = definition.Runbooks.Select(r => new RunbookEntity { IntegrationId = id, Name = r.Name, Url = r.Url }).ToList();
            entity.Dependencies = definition.Dependencies.Select(d => new DependencyEntity { IntegrationId = id, TargetIntegrationId = d.IntegrationId, Description = d.Description }).ToList();
            db.Versions.Add(new() { IntegrationId = id, Revision = entity.Revision, Version = definition.Version, Timestamp = entity.UpdatedAt, ChangedBy = actor, ChangeSummary = changeSummary, DefinitionHash = entity.DefinitionHash, Format = format, OriginalDefinition = raw, CanonicalJson = entity.CanonicalJson });
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return Detail(entity);
        }
        catch (DbUpdateConcurrencyException) { throw new ConflictException("Another author changed this integration. Reload before saving."); }
        catch (DbUpdateException ex) when (ex.InnerException is Microsoft.Data.SqlClient.SqlException { Number: 2601 or 2627 }) { throw new ConflictException("A definition or shared message was changed concurrently. Reload and retry."); }
    }
    public async Task DeleteAsync(IntegrationId id, int expectedRevision, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var entity = await db.Integrations.SingleOrDefaultAsync(e => e.Id == id.Value && !e.IsDeleted, ct) ?? throw new NotFoundException("Integration was not found.");
        if (entity.Revision != expectedRevision) throw new ConflictException("Revision has changed. Reload before deleting.");
        if (await db.Set<DependencyEntity>().AnyAsync(d => d.TargetIntegrationId == id.Value && db.Integrations.Any(i => i.Id == d.IntegrationId && !i.IsDeleted), ct)) throw new ConflictException("Other integrations depend on this definition. Remove those dependencies first.");
        entity.IsDeleted = true;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
    }
    public async Task<IReadOnlyList<DefinitionVersion>> GetVersionsAsync(IntegrationId id, CancellationToken ct)
    {
        if (!await db.Integrations.AnyAsync(i => i.Id == id.Value && !i.IsDeleted, ct)) throw new NotFoundException("Integration was not found.");
        return await db.Versions.AsNoTracking().Where(v => v.IntegrationId == id.Value).OrderByDescending(v => v.Revision).Select(v => new DefinitionVersion(v.Revision, v.Version, v.Timestamp, v.ChangedBy, v.ChangeSummary, v.DefinitionHash, v.Format, v.OriginalDefinition)).ToListAsync(ct);
    }
    public async Task<DashboardSummary> GetDashboardAsync(CancellationToken ct)
    {
        var active = db.Integrations.AsNoTracking().Where(i => !i.IsDeleted);
        var recent = await active.Include(i => i.Tags).OrderByDescending(i => i.UpdatedAt).Take(6).ToListAsync(ct);
        var domains = await active.GroupBy(i => i.BusinessDomain).Select(g => new CountBy(g.Key, g.Count())).ToListAsync(ct);
        var statuses = await active.GroupBy(i => i.Status).Select(g => new CountBy(g.Key, g.Count())).ToListAsync(ct);
        var referenced = await db.Nodes.Where(n => n.SystemId != null && active.Any(i => i.Id == n.IntegrationId)).GroupBy(n => n.SystemId!).Select(g => new { Name = g.Key, Count = g.Select(n => n.IntegrationId).Distinct().Count() }).OrderByDescending(c => c.Count).Take(6).ToListAsync(ct);
        return new(await active.CountAsync(ct), await active.CountAsync(i => i.Status == "Production", ct), await db.Systems.CountAsync(ct), await db.IntegrationMessages.Where(m => active.Any(i => i.Id == m.IntegrationId)).Select(m => m.MessageName).Distinct().CountAsync(ct), await active.CountAsync(i => i.Criticality == "High" || i.Criticality == "Critical", ct), await active.SumAsync(i => i.WarningCount, ct), recent.Select(Summary).ToArray(), domains, statuses, referenced.Select(r => new CountBy(r.Name, r.Count)).ToArray());
    }
}
