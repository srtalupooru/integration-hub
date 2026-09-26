# ADR-005: SQL Server Persistence

Status: Accepted

## Context

Enterprise adoption needs transactional updates, relational queries and immutable definition history on a familiar platform.

## Decision

Use EF Core with SQL Server. Persist normalized graph/search entities, canonical snapshots, raw source and immutable versions. Save under serializable transactions with optimistic revision checks. Archive integrations rather than destroying history. Apply explicit migrations.

## Consequences

No database is seeded or recreated at startup. Search initially uses parameterized SQL substring predicates. SQL Server-specific deployment checks complement SQLite test contracts. Large graph loads and search performance should be measured before adding a cache or search service.
