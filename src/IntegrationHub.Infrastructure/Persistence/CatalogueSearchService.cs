using IntegrationHub.Application;
using IntegrationHub.Contracts;
using IntegrationHub.Domain;
using Microsoft.EntityFrameworkCore;
namespace IntegrationHub.Infrastructure.Persistence;

// Combines read models only: discovery never inserts authored integration records.
public sealed class CatalogueSearchService(HubDbContext db, IComponentRepository components)
{
    public async Task<PagedResult<CatalogueEntry>> SearchAsync(SearchRequest query, string? kind, CancellationToken ct)
    {
        kind = string.IsNullOrEmpty(kind) ? "All" : kind;
        if (kind is not ("All" or "Authored" or "Discovered"))
            throw new ArgumentException("Kind must be All, Authored or Discovered.");
        var page = Math.Max(1, query.Page);
        var size = Math.Clamp(query.PageSize, 1, 100);
        var offset = (long)(page - 1) * size;
        var entries = new List<CatalogueEntry>();
        if (kind != "Authored")
        {
            var records = await components.ListAsync(false, ct);
            var byId = records.ToDictionary(c => c.Definition.Id, StringComparer.Ordinal);
            foreach (var network in new ComponentDiscovery().Build(records, ct).Integrations)
            {
                var members = network.Sources.Select(s => byId[s.Id]).ToArray();
                if (!Matches(network, members.Select(m => m.Definition).ToArray(), query)) continue;
                var d = network.Definition;
                entries.Add(new(new(network.Id, network.Name, d.Description, d.Version, d.Status.ToString(),
                    d.BusinessDomain, d.Ownership.Team, d.Criticality.ToString(), network.Environment,
                    d.Tags.Concat(members.SelectMany(m => m.Definition.Tags)).Distinct().ToArray(),
                    0, members.Max(m => m.UpdatedAt), network.Issues.Count), "Discovered"));
            }
        }
        var total = entries.Count;
        if (kind != "Discovered")
        {
            var authored = new SqlIntegrationSearchService(db).Entries(query);
            total += await authored.CountAsync(ct);
            if (offset < total)
            {
                // Only the first offset + size authored rows can occur on the merged page.
                var rows = await authored.Include(i => i.Tags).OrderByDescending(i => i.UpdatedAt).ThenBy(i => i.Id)
                    .Take((int)Math.Min(int.MaxValue, offset + size)).ToListAsync(ct);
                entries.AddRange(rows.Select(i => new CatalogueEntry(IntegrationRepository.Summary(i), "Authored")));
            }
        }
        var items = offset >= total ? [] : entries.OrderByDescending(e => e.Summary.UpdatedAt)
            .ThenBy(e => e.Summary.Id, StringComparer.Ordinal).ThenBy(e => e.Origin, StringComparer.Ordinal)
            .Skip((int)offset).Take(size).ToArray();
        return new(items, total, page, size);
    }

    private static bool Matches(DiscoveredIntegration network, ComponentDefinition[] members, SearchRequest q)
    {
        var d = network.Definition;
        bool Contains(string? value, string? term) => value?.Contains(term?.Trim() ?? "", StringComparison.OrdinalIgnoreCase) == true;
        bool Equal(string value, string term) => string.Equals(value, term, StringComparison.OrdinalIgnoreCase);
        bool ComponentMatches(ComponentDefinition c, string term) => Equal(c.Id, term) || Contains(c.Name, term);
        if (!string.IsNullOrWhiteSpace(q.Q))
        {
            var searchable = new[] { network.Id, network.Name, d.Description, network.Environment }
                .Concat(members.SelectMany(c => new[] { c.Id, c.Name, c.Description, c.Domain, c.Owner, c.Technology, c.Type.ToString() }.Concat(c.Tags)))
                .Concat(network.Connections.SelectMany(c => new[] { c.Contract, c.Channel.Namespace, c.Channel.Name, c.HttpMethod, c.HttpPath }));
            if (!searchable.Any(s => Contains(s, q.Q))) return false;
        }
        return (string.IsNullOrEmpty(q.Status) || Equal(d.Status.ToString(), q.Status))
            && (string.IsNullOrEmpty(q.Criticality) || Equal(d.Criticality.ToString(), q.Criticality))
            && (string.IsNullOrEmpty(q.Domain) || Equal(d.BusinessDomain, q.Domain) || members.Any(c => Equal(c.Domain, q.Domain)))
            && (string.IsNullOrEmpty(q.Owner) || Equal(d.Ownership.Team, q.Owner) || members.Any(c => Equal(c.Owner, q.Owner)))
            && (string.IsNullOrEmpty(q.Tag) || d.Tags.Concat(members.SelectMany(c => c.Tags)).Any(t => Equal(t, q.Tag)))
            && (string.IsNullOrEmpty(q.System) || members.Any(c => ComponentMatches(c, q.System)))
            && (string.IsNullOrEmpty(q.Technology) || members.Any(c => Contains(c.Technology, q.Technology)))
            && (string.IsNullOrEmpty(q.Source) || members.Any(c => ComponentMatches(c, q.Source) && network.Connections.Any(l => l.ProducerId == c.Id)))
            && (string.IsNullOrEmpty(q.Destination) || members.Any(c => ComponentMatches(c, q.Destination) && network.Connections.Any(l => l.ConsumerId == c.Id)));
    }
}
