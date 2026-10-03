# Independent components and message-driven integration discovery

## What you can do

Use **Components → Add component → Form** to enter definitions without writing
YAML or JSON. The **Definition** editor remains available alongside it. Both use
the same validation, connection preview, and save workflow. See the
[component authoring guide](component-form.md) for instructions and conversion behavior.

Define each API, function, service or calling system independently. HTTP and messaging have separate declarations: `endpoints` exposes HTTP operations, `calls` references API endpoints, `messages` declares broker interactions using `action: sends` for commands, `action: publishes` for events, and `action: consumes` for subscriptions. An API can expose endpoints and send commands or publish events without consuming any broker messages. Integration Hub resolves both interaction types into read-only networks, diagrams, documentation and dependency impact.

For example:

```text
Procurement system -- POST /vendors --> Vendor API
Vendor API -- VendorCreated v1.0 / topic --> Vendor sync function
Vendor sync function -- PUT /vendors --> Elite API
```

You do not write a combined integration definition or manually list other components as consumers of every publication. Existing authored integration definitions continue to work. The **Integrations** catalogue shows authored definitions and discovered networks together, labelled by kind. Its kind filter defaults to **All integrations**; search, filters, totals and pagination cover both kinds. Discovered entries open their read-only generated detail pages. **Components** holds independent source definitions, while **Discovered integrations** provides a focused view with matching findings and unlinked components. The dashboard reports these separately. Component messages are available from the Messages page's **Component messages** link, including declarations that do not yet match anything.

A discovered network is a weakly connected set of components joined by resolved HTTP calls and message routes. It is a potential communication/dependency network, not an execution trace or proof that every input causes every output. A multi-purpose API can connect multiple business processes into one network. NServiceBus handlers and sagas can be documented inside their logical endpoint component using optional `processing` metadata. Individual handler classes are not separate broker consumers. See [NServiceBus processing](nservicebus-processing.md) for local message references, correlation, timeouts, validation, and a complete three-component example. The application does not guess payload transformations or workflow causality.

## Where systems appear

The **Systems** page includes saved components with type `InternalSystem`,
`ExternalSystem`, or `SaaS`, even before they have any matching connections. These
entries open the original component details, where calls, messages and discovered
integrations are maintained. APIs, functions and other technical component types
remain in **Components**. Archived components are hidden; retired systems remain
visible with their status, although they do not participate in discovery.

The same page also shows the administrator-managed **Registered systems** used by
`systemId` references in authored integration definitions. Registry entries and
component definitions remain separate, including when their IDs or names match.
Viewing Systems creates no records and does not change either definition.

To add a calling system, open **Components → Add component → Start from a sample
definition → Caller example**, adapt its identity and API endpoint reference, then
validate and save. It will appear in Systems automatically.

## Try the complete example

1. Stop the application with Ctrl+C and run `dotnet run --project src/IntegrationHub.Api` again. In Development, the additive SQLite migration is applied automatically. Existing integrations remain intact.
2. Refresh your browser. Open **Components → Add component**.
3. Click **API example**, then **Validate & preview links**, then **Save component**. The API exposes `POST /vendors` and publishes `VendorCreated`; it has no consumed messages.
4. Add another component using **Function example**, validate and save it.
5. Open **Integrations** with the kind filter set to **All integrations** or **Discovered**. The API and function appear as a discovered integration connected through `VendorCreated` on the same topic. You can also open **Discovered integrations** for matching findings.
6. Add the **Receiver example** and **Caller example**. Refresh Discovered integrations to see the four-component network: system → API → function → receiver API. The two HTTP edges show their methods and paths.
7. Open its Overview, Connections, Documentation and Review tabs. Each edge names both source binding IDs. The source revision table records the definitions used.
8. Edit a component's message version or channel. Preview shows findings and networks whose membership would be added or removed. Save and refresh to see the recomputed result.
9. Use **Dependencies** to analyse a `component:<id>` and see downstream components and affected discovered networks.

All files are available in [examples/components](examples/components) and are never automatically inserted into your database. **Command API example** and **Command worker example** demonstrate an API sending a command to a queue, a function consuming it, and that function publishing a completion event. An exposed endpoint does not require a documented caller and does not create an unresolved message finding. The samples omit broker payload schema fingerprints, so message links display `SCHEMA_UNVERIFIED` until actual schema hashes are supplied; HTTP calls do not make that payload-compatibility claim.

Definitions can be added in any order. Adding the consumer before its publisher produces a visible unresolved finding, then becomes connected when the publisher is saved.

## Definition format

Both JSON and YAML use a `component` wrapper. The authoritative schema is available at `GET /api/component-schema` and in [component-definition.schema.json](../schemas/component-definition.schema.json). Unknown properties are rejected, including misspelled `messages` or `action` fields.

```yaml
schemaVersion: "1.1"
component:
  id: vendor-api-dev
  name: Vendor API
  type: Api
  environment: development
  version: "1.0"
  status: Development
  domain: Procurement
  owner: Vendor platform team
  technology:
    - ASP.NET Core
    - FastEndpoints
  endpoints:
    - id: create-vendor
      method: POST
      path: /vendors
      version: "1.0"
  messages:
    - id: vendor-created
      action: publishes
      contract: VendorCreated
      version: "1.0"
      messageType: Event
      channel:
        kind: Topic
        namespace: procurement-dev.servicebus.windows.net
        name: vendor-events
      contentType: application/json
```

The consuming function repeats the same message route under `messages` with `action: consumes` and supplies its subscription:

```yaml
schemaVersion: "1.1"
component:
  id: vendor-sync-function-dev
  name: Vendor sync function
  type: AzureFunction
  environment: development
  messages:
    - id: on-vendor-created
      action: consumes
      contract: VendorCreated
      version: "1.0"
      messageType: Event
      channel:
        kind: Topic
        namespace: procurement-dev.servicebus.windows.net
        name: vendor-events
      subscription: elite-vendor-sync
```

Component IDs identify one documented deployment or handler. Use different IDs for different environments. Component/binding IDs and environments are lowercase slugs; IDs are at most 96 characters. Message contracts use their actual names, such as `InvoiceCreated` or `ProcessInvoice`, with exact casing (up to 128 characters; letters, digits, dots, underscores, and hyphens, starting with a letter or digit). Dots are optional. Existing dotted contract names remain supported and are not automatically renamed. To correct a previously saved sample, update the contract on both producer and consumer; changing only one side leaves the route unresolved. Display names may be human-readable. IDs must be unique across endpoints, calls and message declarations within a component. Empty sections can be omitted. A self-consuming component uses separate input/output binding IDs.

`version` on the component is its document/application version. `version` on a binding is the message contract version used for matching; these are independent. Contract versions must be explicit numeric versions such as `1.0`, `1.0.0` or `2.0.0-beta.1`. `latest`, `*`, ranges and implicit compatibility are not accepted.

Optional component metadata includes description, domain, owner, technology, tags, status and criticality. These appear in component details and derived documentation. A retired component remains in the component catalogue but does not participate in discovery. Deprecated components still participate and receive a finding.

`technology` is an optional list, defaulting to `[]`. Use a YAML block list or an
inline list such as `technology: [ASP.NET Core, FastEndpoints]`. Up to 50 unique
entries are accepted; each must contain 1–256 characters with no leading or trailing
whitespace. Nulls, blank entries and non-string items are rejected. Search matches
each technology independently, and component details display separate tags.

Existing single-string definitions and stored component records remain readable.
A nonblank legacy string becomes one entry (outer whitespace is trimmed); commas
are not split. Blank legacy strings become an empty list. Original source and
revision history are preserved, while formatted component JSON/YAML exports use
lists. The component detail API also returns `technology` as an array, so external
clients expecting a string must update. Generated integration snapshots retain the
authored integration node format, joining technology names into its string field.
No database migration is required.

## HTTP calls and exposed endpoints

A calling system or function declares a target component and endpoint ID:

```yaml
schemaVersion: "1.1"
component:
  id: procurement-system-dev
  name: Procurement system
  type: InternalSystem
  environment: development
  calls:
    - id: create-vendor-request
      targetComponent: vendor-api-dev
      endpoint: create-vendor
      method: POST
      version: "1.0"
```

The target exposes the endpoint using `endpoints` as in the API example above. Matching uses the exact target component ID, endpoint ID, HTTP method, endpoint version and environment. The path is taken from the target endpoint definition; URLs and similarly named systems are never guessed. Versions default to `1.0` on both endpoints and calls; `1.0` and `1.0.0` remain distinct. Methods must be uppercase. Paths start with `/` and cannot contain whitespace, query strings or fragments. Route templates such as `/vendors/{id}` are allowed; matching is by endpoint ID, not URL template execution.

A missing, archived or retired target, unknown endpoint, or mismatched method/version/environment produces a specific `HTTP_*` finding and no edge. Multiple callers may reference the same endpoint. Duplicate endpoint routes (method/path/version), duplicate calls and IDs repeated across sections are rejected. Exposed endpoints without callers are valid and are not treated as unconsumed or consumed broker messages.

HTTP links appear as synchronous `POST /vendors` or `PUT /vendors` edges. Message links remain asynchronous and identify Command or Event. HTTP requests are excluded from the component message catalogue and generated Messages/Events documentation; they appear under HTTP calls and Interfaces. The model does not infer request payload compatibility, authentication, or automatic response-message contracts from an endpoint reference.

## Commands sent and events published

An API can send a command after receiving an HTTP request:

```yaml
  messages:
    - id: send-create-vendor
      action: sends
      contract: CreateVendor
      version: "1.0"
      messageType: Command
      channel:
        kind: Queue
        namespace: procurement-dev.servicebus.windows.net
        name: vendor-commands
```

The function repeats this contract and channel in `messages` with `action: consumes`
and its own binding ID. `action: sends` requires `messageType: Command`;
`action: publishes` requires `messageType: Event`. Consumers declare the exact
incoming type (Event, Command, Document, Request or Response). All unified messages
use Queue, Topic or Stream channels; use `calls` for outbound HTTP. Subscriptions
are required only for Topic/Stream consumers and prohibited on outgoing messages
and Queue consumers. The component type does not invent or prohibit capabilities.

Keep all broker declarations in one `messages` list. Do not mix it with top-level
`publishes`, `sends` or `consumes`, even if those legacy sections are empty. An empty
or omitted `messages` list is valid. Separate local IDs are needed when a component
both consumes and emits the same contract. The new list has no `sourceComponent`
or `targetComponent` selectors: message connections are derived from shared routes.
Original source is retained exactly; formatted exports of unified definitions keep
`messages` and each action, without adding empty legacy sections. Formatted exports
of definitions without messages can omit the empty list. The API's component detail
includes `messages`; existing summary counts and message occurrence routes also
include unified declarations.


## Exact broker matching rules

A publication and consumption must agree on all of these fields:

| Field | Rule |
| --- | --- |
| Component environment | Exact, lowercase identity; Development and Production never implicitly connect |
| Contract | Exact, case-sensitive message name, such as `VendorCreated` |
| Contract version | Exact string; `1.0` and `1.0.0` are different contracts |
| Message type | Exact Event, Command, Document, Request or Response |
| Channel kind | Exact Topic, Queue or Stream |
| Channel namespace | Exact broker/endpoint identity; no normalization or DNS inference |
| Channel name | Exact topic, queue or stream; case-sensitive |
| Content type | Exact lowercase media type; defaults to `application/json` |
| Schema fingerprint | If both sides provide one, they must be equal |

Channel names, namespaces and subscriptions cannot contain whitespace. A namespace should distinguish the actual resource or endpoint, not just a generic technology name. HTTP endpoints use the separate `endpoints` / `calls` model described above.

The optional `schemaFingerprint` is a lowercase 64-character SHA-256 digest of an agreed schema artifact. Generate it from the same bytes on both sides (for example `shasum -a 256 vendor-created.schema.json`). Different serialization or whitespace produces a different digest. The application neither downloads schemas nor attempts JSON Schema, Avro or XML compatibility analysis. If either side omits the fingerprint, the route can match but receives `SCHEMA_UNVERIFIED`. Conflicting supplied fingerprints or content types prevent the link.

In legacy definitions, the optional `sourceComponent` on a consumption narrows it to one exact publisher ID. For legacy HTTP message declarations only, `targetComponent` on a publication narrows it to one exact receiver ID. New HTTP calls always name a target component and endpoint. A missing selector target never falls back to another component. These are documentation selectors, not deployed broker filters or access controls.

## Delivery behaviour and ambiguous definitions

| Situation | Behaviour |
| --- | --- |
| One publisher, several topic subscriptions | Every matching subscription receives a possible connection |
| Multiple matching publishers | All are linked; `MULTIPLE_PUBLISHERS` asks authors to review the route (source selectors remain legacy-only) |
| Several consumers on one queue | All are possible recipients; `COMPETING_CONSUMERS` explains that delivery is not guaranteed to each |
| Shared topic subscription / stream consumer group | Marked as competing; different subscriptions/groups represent independent fan-out |
| New HTTP call cannot resolve its explicit endpoint | No edge is generated; a specific `HTTP_*` finding explains the problem |
| Legacy HTTP publication matches multiple receivers | No edge is generated; `AMBIGUOUS_HTTP_TARGET` requests an explicit target or migration to endpoints/calls |
| Unmatched consumption | Saved with an unresolved or mismatch finding; becomes connected when a compatible publication appears |
| Unconsumed publication | Saved with `UNCONSUMED_PUBLICATION`; can be a legitimate external boundary |
| Different environment/version/channel/type/payload | No connection; a diagnostic describes the mismatch |
| Duplicate binding IDs or duplicate declarations | Local validation error; cannot save |
| Bidirectional exchange | Declare each direction as its own publication/consumption pair |
| HTTP request/response | An explicit call references an exposed endpoint; no broker messages or response events are invented |
| Broker request/reply | Legacy outgoing Request/Response bindings remain supported; unified outgoing actions currently cover Events and Commands only |
| Self-delivery or cycles | Preserved and flagged; traversal is cycle-safe |
| No bindings | Valid with a warning; appears under unlinked components |
| Retired/archived component | Excluded; remaining components report any newly unmatched messages |

No fuzzy name matching, wildcard subscription rules, implicit schema conversion, channel aliases, cross-environment bridges or runtime telemetry are inferred. To document a bridge or transformation, add a component that explicitly consumes the first contract/channel and publishes the second. Discovery evaluates the declared bindings; it does not provision or verify broker infrastructure.

## Compatibility with existing definitions

Schema versions `1.0` and `1.1` are accepted. The unified `messages` field is an additive schema capability; use `1.1` for new definitions. Existing `publishes`, `sends` and `consumes` definitions remain supported, including connections between legacy and unified components. Older `channel.kind: Http` entries in `consumes` or `publishes` remain readable and keep their existing exact contract/channel matching behaviour. They are displayed as **Legacy HTTP declarations**, excluded from broker message catalogues, and flagged with `LEGACY_HTTP_BINDING`. An HTTP method is never inferred from an old Request contract. A new `calls` reference does not silently bind to a legacy declaration with an unknown method.

To migrate, replace an API's old HTTP `consumes` entry with an `endpoints` entry and replace the caller's HTTP `publishes` entry with a `calls` reference using the known method and endpoint ID. For broker-only definitions, combine publications, sent commands and consumptions into `messages`, add the corresponding action to each item, and remove the legacy lists. Old command publications still match and receive a migration warning. Do not automatically migrate legacy selectors or outgoing Document/Request/Response declarations: the new format does not represent those outgoing semantics. Definitions with legacy HTTP-message declarations must migrate those to endpoints/calls before using messages. Preview the changes and save the components explicitly. Source text, archived definitions, historical revisions and stored hashes are not rewritten. These additions use the existing canonical JSON storage and require no additional database migration.

## Identity, refresh and provenance

Every query derives the current networks from one read of the current component rows. There is no persisted generated integration copy to become stale and no background job to wait for. Refresh an open page after saving another component; there is no push notification to other browser sessions.

A generated ID is `auto-` followed by SHA-256 over a deterministic, ordered list of member component IDs. Editing a description, changing a source revision or changing bindings without altering network membership keeps that ID. Merging or splitting networks changes membership and therefore IDs. A stale network URL returns 404 with instructions to refresh. Preview lists added and removed network IDs; links and findings within an unchanged network are shown in its preview as well.

The catalogue snapshot fingerprint covers active component IDs, revisions and canonical definition hashes. Source revisions and hashes are included in generated details and documentation. The same saved records produce the same links and IDs regardless of read/import order. Archival and restoration increment revisions. Generated status is Production only when all members are Production; otherwise it is conservatively Draft. Per-component lifecycle status remains visible in component details.

The combined catalogue is a read-only aggregation: `/api/integrations` and its mutation routes still manage authored definitions only. Discovered entries are recomputed from current components, sorted by their latest source update, and are never inserted as authored records. Domain, owner and tag filters also match individual source components; source/destination filters match senders/callers and receivers in resolved connections. Status and criticality use the generated network values described above.

Exports are snapshots of a derived network, not a second editable source of truth. If you choose to import an exported snapshot through the authored integration workflow, it becomes an independent authored copy. It will not automatically track later component edits.

The global dependency graph includes components as `component:<id>`. Affected integration IDs for discovered networks use `discovered:auto-...`, disjoint from authored slug IDs; the web UI routes these links to the discovered detail page. Existing authored systems/messages are not implicitly merged with independently authored components by display name.

## Persistence and concurrency

Two additive tables, `Components` and `ComponentVersions`, store canonical definitions, original text, format, SHA-256 hash, actor, UTC timestamp, revision and archive state. Separate SQLite and SQL Server migrations create these tables without altering existing integration data.

Create rejects an existing ID, including archived IDs. Update requires the matching route ID and a positive `expectedRevision`. Saves and history insertion occur in one transaction. The revision is an optimistic concurrency token. Stale updates return 409; a transient database lock may return 503 and can be retried after reloading. The server validates again when saving, even if a preview was already obtained. A preview is not a reservation of the catalogue: other authors can save between preview and save.

Editors can create and update components. Admins can archive or restore with a current revision. Archival is reversible and keeps all source history. Historical rows are immutable through the EF save paths. Restore creates another audit revision and immediately makes the original bindings eligible again, unless their status is Retired. Browser writes require the existing same-origin CSRF token. Viewer access is read-only.

## API reference

All endpoints require Viewer access unless a stronger role is listed.

| Endpoint | Purpose / role |
| --- | --- |
| GET `/api/catalogue?kind=All` | Combined paged catalogue; `kind` is All, Authored or Discovered; accepts the existing search/filter parameters |
| GET `/api/component-schema` | JSON Schema for JSON and YAML |
| GET `/api/component-examples/{name}` | `vendor-system`, `vendor-api`, `vendor-function`, `elite-api`, `vendor-command-api`, `vendor-command-function` templates |
| GET `/api/components?includeArchived=true` | Component summaries, optionally archived |
| GET `/api/components/{id}` | Current source, canonical definition, revision and archive state |
| GET `/api/components/{id}/versions` | Immutable source history, including archive/restore events |
| GET `/api/components/{id}/definition?format=json` | Canonical JSON/YAML; omit format for original source |
| POST `/api/components/validate` | Local syntax/schema/semantic validation; Editor |
| POST `/api/components/preview` | Validation and hypothetical catalogue discovery; Editor |
| POST `/api/components` | Create; Editor |
| PUT `/api/components/{id}` | Update with expectedRevision; Editor |
| DELETE `/api/components/{id}?expectedRevision=N` | Archive; Admin |
| POST `/api/components/{id}/restore?expectedRevision=N` | Restore; Admin |
| GET `/api/component-messages` | Broker message declarations, including unresolved/retired declarations; HTTP interactions excluded |
| GET `/api/component-dashboard` | Component, network, unlinked and finding counts |
| GET `/api/discovery` | Derived networks, snapshot fingerprint, findings, unlinked and retired IDs |
| GET `/api/discovery/{id}` | One network, diagram, documentation and revision provenance |
| GET `/api/discovery/{id}/diagram` | Mermaid source |
| GET `/api/discovery/{id}/definition?format=yaml` | Snapshot JSON/YAML |
| GET `/api/discovery/{id}/documentation?format=markdown` | JSON, Markdown or HTML |
| GET `/api/impact?componentId=component:vendor-api-dev` | Upstream/downstream component dependency analysis |

Mutation bodies use the same request envelope as integration imports:

```json
{
  "definition": "component:\n  ...",
  "format": "yaml",
  "changeSummary": "Add vendor-created publication",
  "expectedRevision": 2
}
```

Omit `expectedRevision` when creating. A valid but unresolved definition can be saved. Malformed/invalid saves return 422 with structured validation, conflicts return 409, unknown resources return 404, and invalid request parameters return 400.

## Limits and verification

Individual definitions are limited to 1 MB of UTF-8, nesting depth 64, 300 entries in messages, 100 entries per other interaction section (endpoints, calls and legacy sends/consumes/publishes), and 50 tags. YAML aliases, explicit tags and multiple documents are rejected; duplicate JSON/YAML keys are rejected. Discovery fails explicitly rather than truncating if the catalogue exceeds 2,000 active components, 20,000 total interaction declarations or 50,000 resolved connections. These are protective limits, not measured throughput guarantees; the current implementation reads active definitions and derives networks on demand. Very large graphs may require inspecting the connection table or exporting the graph instead of rendering it interactively.

Automated coverage includes explicit HTTP endpoint references, method/version/environment mismatches, command sends, legacy HTTP compatibility, API-specific UI labels, exact message matching and mismatch dimensions, ordering determinism, chains, fan-out, competing groups, multiple publishers, selectors, HTTP ambiguity, missing schema fingerprints, cycles, self-delivery, membership merges/splits, retirement, archive/restore, invalid syntax/schema/semantics, both serialization formats, API access controls, CSRF, optimistic and simultaneous edits, immutable history, migration upgrades and persistence across restarts. See [verification.md](verification.md) for executed results and environment limitations.
