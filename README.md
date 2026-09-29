# Integration Hub

**Define an integration once. Derive architecture, documentation, catalogues and impact analysis.**

Integration Hub is a .NET 8 modular monolith for an enterprise integration landscape. Engineers author JSON or YAML. The runtime validates it, creates a canonical graph, persists revisioned definitions, and generates searchable catalogue entries, diagrams, documentation and dependency analysis.

You can also author **independent component definitions**: systems `call` exposed API `endpoints`; APIs `send` commands or `publish` events; functions `consume` broker messages. Definitions use `endpoints`, `calls`, and `messages` with `action: sends`, `publishes`, or `consumes`; older separate message sections remain supported. HTTP references resolve by target component, endpoint, method, version and environment; broker messages match exact contracts and channels. Open **Components → Add component** for caller, API, function, receiver and command examples, then **Integrations** to browse the generated networks alongside authored definitions. Use **Discovered integrations** for matching findings and unlinked components. See the [complete component discovery guide](docs/component-discovery.md) for matching rules, edge cases and compatibility with older definitions. No sample is stored until you explicitly save it.

NServiceBus components can optionally document their endpoint, handlers, sagas, correlation rules, timeouts, and possible per-message outputs. Open **Handlers & sagas** on a component or generated integration to inspect these declarations. Start with the [NServiceBus guide and three-component sample](docs/nservicebus-processing.md), or use the [annotated component reference](docs/component-definition-reference.yaml). This records design behavior; live saga-instance telemetry is not included.

There is no runtime mock data or automatic seeding. An empty database displays **“No integrations have been documented yet.”** Example definitions in this README are documentation only; tests use isolated, disposable fixture databases.

## What is implemented

- Strict JSON Schema validation, JSON/YAML parsing, syntax/schema/semantic issue levels and safe bounded input handling.
- Nonlinear graphs: fan-out, fan-in, multiple roots/destinations, cycles, nested groups, optional nodes and bidirectional edges.
- Replaceable Mermaid rendering, structured documentation, HTML and Markdown exports.
- Blazor Web App with MudBlazor: live dashboard, catalogue cards/table, filters, editor/upload/preview, detailed tabs, system/message catalogues, search, dependency analysis and version history.
- Workflow-style graph cards with service icons, type labels, connection ports, wrapped names and inline SVG styling; shared across authored, discovered and dependency graphs, with pan/zoom, horizontal/vertical layout and SVG download.
- SQL Server / EF Core persistence with relational search projections, immutable revisions, source hashes, optimistic concurrency and archival deletion.
- Global identity matching through registered systems and explicit shared resources, recursive impact traversal and message producer/consumer analysis.
- REST API, Swagger, ProblemDetails, health checks, structured logging, Entra-ready OIDC/JWT authentication, Viewer/Editor/Admin policies and cookie CSRF protection.
- Automated NUnit/Moq/FluentAssertions tests; Docker image and local SQL Server Compose setup; initial EF migration.

This is a working product foundation. It does not claim deployment certification or runtime telemetry: target-environment SQL Server, Entra registration, TLS, backups, performance and operational acceptance remain deployment checks.

## Architecture and layout

```text
Definition → Parser → Schema → Canonical model → Graph
                                    ↓             ↓
                               Persistence   Diagram / Documentation / Impact
                                    ↓             ↓
                                 SQL Server ← API ← Blazor UI
```

```text
IntegrationHub.sln
src/
  IntegrationHub.Domain/          Domain records, graph, validation, traversal
  IntegrationHub.Contracts/       HTTP request/response contracts
  IntegrationHub.Application/     Workflows, commands/queries, generation, analysis
  IntegrationHub.Infrastructure/  Parsing, EF mappings/migrations, SQL repositories
  IntegrationHub.Api/             Executable host, endpoints, security, health
  IntegrationHub.Web/             Blazor Web App components and local assets
schemas/                         Authoritative JSON Schema
tests/                          Domain, Application, Infrastructure, API tests
docs/                           Architecture and six decision records
tools/                          Mermaid vendoring utility
```

The API hosts the Web Razor class library in one process. The UI calls same-origin API endpoints with the current user's identity. Domain references no infrastructure or rendering package. See [architecture](docs/architecture.md) and [decisions](docs/adr/README.md) for the boundaries and tradeoffs.

Stack: .NET 8, ASP.NET Core, Blazor, MudBlazor, EF Core SQL Server, FluentValidation, YamlDotNet, System.Text.Json, JsonSchema.Net, Mermaid, Microsoft.Extensions.Logging, Swagger, NUnit, Moq and FluentAssertions. SQLite is the local development provider; SQL Server remains the production provider.

## Quick start on your Mac — no database installation

Only the .NET 8 SDK is required. From the repository root:

```bash
dotnet restore IntegrationHub.sln
dotnet run --project src/IntegrationHub.Api
```

Open **http://localhost:5080**. The Development launch profile uses SQLite and creates the database and schema on first launch. You do not need Docker, a SQL Server account, a password or an Azure subscription. The local identity is Admin.

- Database file: `src/IntegrationHub.Api/App_Data/integrationhub.db`.
- Your definitions and version history persist when the application stops or restarts.
- The initial database is empty; nothing is seeded.
- **Import Definition** accepts your JSON/YAML and supports validation, architecture/documentation preview and save.
- **http://localhost:5080/health/ready** reports `Healthy` when the schema is ready.
- **http://localhost:5080/swagger** exposes the API documentation.
- Stop with `Ctrl+C`; start again with the same `dotnet run` command.

If you previously exported a SQL Server connection string, remove that shell override before starting the SQLite profile:

```bash
unset ConnectionStrings__IntegrationHub Database__Provider Database__InitializeSqliteOnStartup
```

Development settings select `Database:Provider=Sqlite`, `Database:InitializeSqliteOnStartup=true` and `Data Source=App_Data/integrationhub.db;Foreign Keys=True`. Relative file paths are resolved against the API content root, not the shell's current directory. You can override `ConnectionStrings__IntegrationHub` with another SQLite file path. Keep the database outside `wwwroot`; it is not served as a static asset. Database files are excluded from Git and Docker build contexts.

Only the SQLite development setting applies migrations automatically. It never resets or recreates an existing database. SQL Server deployments continue to use explicit migrations. SQLite is rejected outside Development/Testing, and enabling automatic SQLite initialization for SQL Server is rejected.

## SQL Server with Docker

Requirements: Docker Engine with Compose and a host capable of running the SQL Server 2022 Linux image. SQL Server's Linux container requires a supported x86-64 environment; use a remote SQL Server for Apple Silicon if that container cannot run on your host.

1. Copy `.env.example` to `.env`.
2. Set your own strong `MSSQL_SA_PASSWORD` and `SQL_CONNECTION_STRING`. Inside Compose, the SQL host is `sql,1433`. Example connection string shape:

   ```text
   Server=sql,1433;Database=IntegrationHub;User Id=sa;Password=<your-password>;Encrypt=True;TrustServerCertificate=True
   ```

3. Start:

   ```bash
   docker compose up --build
   ```

The SQL health check runs first, the one-shot `migrate` service applies migrations, then the Hub starts. Open **http://localhost:5080** or **http://localhost:5080/swagger**. SQL and HTTP ports bind only to loopback. Compose uses Development authentication and is intended for local development.

Database and Data Protection keys live in named volumes. `docker compose down` preserves them. Do not delete volumes containing definitions you need to keep.

## Run natively with SQL Server

Requirements: .NET 8 SDK, SQL Server (local or remote), and the packages restored from NuGet. No Node.js build step is required: the Mermaid browser module is vendored.

```bash
dotnet restore IntegrationHub.sln
dotnet tool restore
```

Set the database connection string through your shell, IDE launch environment or secret manager. The host does not read `.env` automatically; Compose does.

```bash
export Database__Provider=SqlServer
export Database__InitializeSqliteOnStartup=false
export ConnectionStrings__IntegrationHub='<your SQL Server connection string>'
```

Apply the committed migration and start:

```bash
dotnet ef database update --project src/IntegrationHub.Infrastructure --context HubDbContext
dotnet run --project src/IntegrationHub.Api
```

The launch profile selects Development and http://localhost:5080. With SQL Server selected, it does **not** create a database automatically. Alternatively, run the executable host with `--migrate` as an explicit migration job; configure the environment/identity settings for that host.

If SQL Server is selected but unavailable, catalogue operations and readiness fail. For a full local workflow without a server, use the default SQLite Development profile above. Switching providers does not copy definitions between databases; export and import your source definitions when moving to SQL Server.

### Migrations

```bash
# Add a migration after changing mappings
dotnet ef migrations add DescribeTheChange --project src/IntegrationHub.Infrastructure --context HubDbContext --output-dir Persistence/Migrations

# Generate a SQL script for review/deployment
dotnet ef migrations script --idempotent --project src/IntegrationHub.Infrastructure --context HubDbContext --output artifacts/migrations.sql

# Apply committed migrations explicitly
dotnet ef database update --project src/IntegrationHub.Infrastructure --context HubDbContext
```

SQLite uses a separate migration set on `SqliteHubDbContext`, with the same inherited relational mappings. When changing the shared model, generate and review a migration for **each** provider:

```bash
dotnet ef migrations add DescribeTheChange --project src/IntegrationHub.Infrastructure --context SqliteHubDbContext --output-dir Persistence/SqliteMigrations
```

The app's `--migrate` mode uses the configured provider and connection string. To initialize the default local SQLite database without starting the web server:

```bash
dotnet run --project src/IntegrationHub.Api -- --migrate
```

The design-time factory reads `ConnectionStrings__IntegrationHub`. Generating a migration or script does not require a live SQL connection. Application startup never calls EnsureCreated, drops databases or seeds definitions. SQLite Development startup applies pending provider-specific migrations. Use a migration principal with schema permissions and a separate restricted runtime principal in production.

## Author and import a definition

1. Register any enterprise systems in **Systems** as an Admin, if you intend to use `systemId`.
2. Open **Import Definition** (or **Create Integration**, which opens the authoring editor).
3. Paste or upload JSON/YAML, up to 1 MB.
4. Choose **Validate & preview**. Review the three validation levels, the architecture and generated documentation.
5. Resolve errors; review warnings for intentional cycles, disconnected components or multiple roots.
6. Add a change summary and **Save definition**.

Saving revalidates the definition and commits its relational read model and first version together. Edit an integration from its details page; every update creates another immutable revision. The editor sends its loaded revision to prevent silently overwriting another author's changes.

### YAML example

The identifiers below illustrate syntax; nothing imports them automatically. Message producers/consumers and edge endpoints refer to node IDs, not names.

```yaml
integration:
  id: document-transfer
  name: Document transfer
  description: Transfers a document between two systems
  version: '1.0'
  status: Draft
  domain: Operations
  criticality: Medium
  ownership:
    team: Integration Engineering
    supportTeam: Integration Support
  nodes:
    - id: source
      name: Source system
      type: ExternalSystem
    - id: destination
      name: Destination API
      type: Api
      technology: ASP.NET Core
  edges:
    - id: transfer
      from: source
      to: destination
      label: Submit document
      protocol: HTTPS
      mode: Synchronous
      messageName: DocumentSubmitted
  messages:
    - name: DocumentSubmitted
      type: Command
      producer: source
      consumers: [destination]
      contentType: application/json
  reliability:
    retries: Exponential backoff
    idempotency: true
    deadLetterQueue: false
```

### Equivalent minimal JSON

```json
{
  "integration": {
    "id": "document-transfer",
    "name": "Document transfer",
    "version": "1.0",
    "status": "Draft",
    "nodes": [
      { "id": "source", "name": "Source system", "type": "ExternalSystem" },
      { "id": "destination", "name": "Destination API", "type": "Api" }
    ],
    "edges": [
      { "from": "source", "to": "destination", "label": "Submit document" }
    ]
  }
}
```

The JSON example shows the minimum required shape. JSON and YAML accept the same optional properties. See [the complete schema](schemas/integration-definition.schema.json) or authenticated `GET /api/schema` for all fields.

### Identity and graph conventions

- Integration/node/group IDs are lowercase slugs, up to 128 characters.
- `version` is a string; plain YAML numeric version scalars are accepted as strings.
- Optional edge IDs are generated by position (`edge-1`, etc.); explicit IDs are preferable for stable review references.
- Custom node types use `type: Custom` with `customType: YourType`.
- `sources` and `destinations` are node-ID arrays; omit them to infer boundaries.
- `groups` have `id`, `name`, optional `parentId`; nodes use `groupId`.
- Use `systemId` only for an existing system. Use `sharedResourceId` for shared infrastructure; include environment in that identity where necessary.
- `dependencies` is an array of `{ integrationId, description }`. Import dependencies first. Self/unknown dependencies fail; circular dependency warnings are permitted.
- `events` uses the same message shape as `messages`. Each message name is unique within a definition.
- Aliases, custom YAML tags, multiple documents and duplicate object keys are rejected. Metadata values are strings. Unknown properties are errors, which helps detect misspellings.
- Do not place secrets in definitions: raw source and historical revisions are visible to authorised Viewers.

## Diagrams, documentation and impact

Diagrams are generated from `IntegrationGraph` through `IDiagramGenerator`. Mermaid gets safe generated IDs and escaped labels and renders in strict mode. It can be replaced without changing definitions. Source text is never interpreted as arbitrary Mermaid code.

The graph viewer supports drag-to-pan, zoom buttons, **Fit**, **100%**, horizontal/vertical layouts, fullscreen (where supported), and **Download SVG**. Component colours match the legend; optional nodes retain dashed borders. Hold Ctrl/⌘ while scrolling to zoom around the pointer. Focus the canvas to use arrow keys to pan, +/− to zoom, and 0/Home to fit. Double-click also fits the graph. Layout and zoom are local viewing preferences and do not modify the saved definition. SVG download includes the entire graph, even when the canvas is zoomed in.

Documentation is first a structured model, then rendered by the UI or exported as Markdown/HTML. Markdown contains a Mermaid block; HTML contains encoded Mermaid source. PDF is an extension point.

The global graph combines explicit system/resource identities, per-integration flow edges and message producer/consumer relationships. Impact traverses with a visited set, so cycles terminate. It returns upstream/downstream components, direct and indirect affected integrations, and dependent systems. Integration-level impact uses declared integration dependencies; component-level impact follows flow/message relationships and expands affected integrations through declared dependencies. Names alone do not merge systems. This is documented architectural reachability; it does not claim to simulate outages or runtime retries.

## API

Swagger: `/swagger`. Definition bodies are `{ definition, format?, changeSummary?, expectedRevision? }`. Omit `format` for auto-detection. Invalid validation/preview requests return structured findings; attempts to save an invalid definition return 422 ProblemDetails.

| Endpoint | Purpose | Role |
| --- | --- | --- |
| GET `/api/integrations`, `/api/search` | Paged SQL search and filters | Viewer |
| GET `/api/integrations/{id}` | Canonical definition and source | Viewer |
| POST `/api/integrations/validate`, `/preview` | Validate / preview authoring text | Editor |
| POST `/api/integrations` | Create with first revision | Editor |
| PUT `/api/integrations/{id}` | Update with `expectedRevision` | Editor |
| DELETE `/api/integrations/{id}?expectedRevision=N` | Archive, preserving history | Admin |
| GET `/api/integrations/{id}/diagram` | Mermaid source | Viewer |
| GET `/api/integrations/{id}/documentation?format=markdown` | JSON / markdown / html documentation | Viewer |
| GET `/api/integrations/{id}/definition?format=yaml` | Original or equivalent JSON/YAML | Viewer |
| GET `/api/integrations/{id}/versions` | Source history and hashes | Viewer |
| GET `/api/integrations/{id}/dependencies`, `/impact` | Integration dependency analysis | Viewer |
| GET `/api/dependencies`, `/api/dependencies/diagram` | Global graph / diagram | Viewer |
| GET `/api/impact?componentId=system:ID` | Component-level impact | Viewer |
| GET `/api/systems`, `/api/systems/{id}` | System registry | Viewer |
| PUT / DELETE `/api/systems/{id}` | Manage registry | Admin |
| GET `/api/messages`, `/api/messages/{name}` | Messages and consumers | Viewer |
| GET `/api/dashboard`, `/api/schema`, `/api/session` | Counts, schema, authenticated session | Viewer |

Search parameters: `q`, `status`, `domain`, `owner`, `criticality`, `tag`, `system`, `technology`, `source`, `destination`, `page`, `pageSize` (maximum 100). Text search uses SQL substring semantics and the configured database collation. Filters are exact where appropriate.

Local browser/cookie automation must first request `/api/session`, retain its antiforgery cookie, and send `csrfToken` as `X-CSRF-TOKEN` for POST/PUT/DELETE. Entra bearer clients send a valid access token and do not use cookie antiforgery tokens. Known errors map to 400, 404, 409, 422; unavailable SQL maps to 503 and unexpected errors to 500 with a trace ID.

## Authentication and configuration

`appsettings.json` is secure-by-default Entra mode; credentials and connection strings are empty. Production configuration requires:

```text
Authentication__Mode=Entra
Authentication__Authority=https://login.microsoftonline.com/<tenant-id>/v2.0
Authentication__ClientId=<web-app-client-id>
Authentication__ClientSecret=<secret from your secret manager>
Authentication__Audience=<API audience>
ConnectionStrings__IntegrationHub=<SQL Server connection string>
```

Configure the Entra application for the deployed HTTPS redirect URI `/signin-oidc`. Define and assign app roles `Viewer`, `Editor`, `Admin`; they must appear in `roles` claims. OIDC uses authorization code + PKCE. API bearer tokens validate issuer and audience. UI/API policies are enforced server-side. Admin includes Editor/Viewer privileges; Editor includes Viewer.

Development uses a fixed local principal and defaults to Admin. Set `Authentication__DevelopmentRole=Viewer` or `Editor` to exercise permissions. Development mode is rejected outside Development/Testing. Do not expose this mode publicly. No user-editable identity or role headers are trusted.

Use HTTPS, persist Data Protection keys, configure trusted reverse proxies explicitly if terminating TLS upstream, restrict hostnames, and supply least-privilege database credentials for deployment. Definition text and sensitive payloads are not written to application logs. `Microsoft.Extensions.Logging` and normal ASP.NET diagnostics leave a standard path for an Application Insights provider; live telemetry is deliberately not implemented.

Health endpoints:

- `/health/live`: process liveness, independent of SQL.
- `/health/ready`: SQL connectivity/readiness.
- `/health`: aggregate health.

## Build and test

```bash
dotnet build IntegrationHub.sln
dotnet test IntegrationHub.sln --logger trx
node --test tests/diagram-viewport.test.mjs
dotnet publish src/IntegrationHub.Api -c Release -o artifacts/publish
```

The suite covers sequential/parallel/fan-in/long/cyclic graphs, missing/duplicate references, safe rendering, schema equivalence and round trips, persistence mappings, immutable versions, stale writes, relational search, end-to-end API import/update/archive, ProblemDetails, CSRF and role restrictions. Test databases contain only test fixtures and never touch your configured production connection string.

SQL Server migrations can be generated and reviewed offline, but release acceptance should apply them to a disposable SQL Server database and run the import workflow against that provider. The test suite's SQLite contract tests are not a substitute for SQL Server deployment verification.

### Updating the local Mermaid asset

`tools/fetch-mermaid.sh` downloads the pinned package and vendors only the ESM dependency tree plus its license. Run it deliberately when updating the renderer version; there is no runtime CDN. Review and commit the resulting assets with a renderer smoke test.

## Future roadmap

- Git-backed definition sources and CI validation through `IIntegrationDefinitionSource`.
- Alternative diagram adapters, PDF export, graph search and component inspection.
- Revision-aware graph caching and larger-catalogue query optimization after measurement.
- Independent runtime telemetry adapters and explicit health observations.
- Organization-specific schema governance, lifecycle policies, retention and audit integrations.
- Canonical-data-grounded assistant features, if later requested. There is no LLM in this implementation.
