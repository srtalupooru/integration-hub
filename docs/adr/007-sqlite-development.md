# ADR-007: SQLite for Local Development

Status: Accepted (user-authorized addition to ADR-005)

## Context

The developer has an Apple Silicon Mac, no database server and no Docker installation. Local validation needs to include saved definitions, search and version history without introducing a cloud subscription or a SQL Server emulator.

## Decision

Use the EF Core SQLite provider for Development and Testing, with a persistent file under the API's App_Data directory by default. The default Development configuration applies provider-specific migrations at startup. Production and the existing Docker Compose SQL Server environment retain SQL Server and explicit migration application.

Share the canonical model and repository implementations. Use a derived SqliteHubDbContext to isolate SQLite migrations from SQL Server migrations, following [EF Core's multiple-provider migration guidance](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/providers). Validate provider selection at startup. Do not seed records, use a transient in-memory store for the running app, or destroy existing data.

## Consequences

A normal dotnet run starts a usable empty local catalogue and retains data across restarts. SQLite requires no database credentials. Both provider migration sets must be maintained when the shared model changes. SQLite locking, text matching and supported SQL differ from SQL Server, so local success is not SQL Server deployment certification. Moving between providers requires definition export/import; changing configuration does not transfer data.
