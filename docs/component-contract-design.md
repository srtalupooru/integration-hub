# Self-contained component definitions

Status: unified `messages` with `action` is implemented. The independent HTTP
endpoint-contract fields in the examples below remain proposed and are not yet
accepted by the application. The current importable format is documented in
[component-definition-reference.yaml](component-definition-reference.yaml).

## Independence and identity

Each definition describes one component's own capabilities and the contracts it
uses. It never names another component's ID. Definitions may be saved in any order,
including when no provider or consumer is documented. Discovery derives connections
without modifying the source definitions.

This is independence between definitions, not runtime statelessness. A database,
queue, or stateful service can still have a self-contained definition. Components
necessarily agree on shared interface contracts and routing identities.

Keep `id` as the catalogue identity of a component. IDs remain necessary for editing,
history, graph nodes and generated results; they are not used as selectors in source
definitions. Renaming a provider's component ID must not require editing its callers.

## Connection rules

### HTTP endpoints

Replace `calls.targetComponent` and the foreign `calls.endpoint` reference with a
contract declaration shared by the caller and provider. Both sides declare:

| Field | Purpose |
| --- | --- |
| `namespace` | Stable logical service boundary, such as `finance.invoicing`; not a component ID or secret URL. |
| `contract` | Operation identity, such as `invoice.create`. |
| `version` | Exact endpoint contract version, independently of the component version. |
| `method` | HTTP verb. |
| `path` | Exact documented route template. |

Discovery matches these fields plus the component environment. Matching is exact:
no fuzzy name matching, path parameter substitution, version-range inference or
automatic cross-environment connections. In the first implementation, paths remain
case-sensitive and trailing slashes and route parameter names must agree.

Namespace and contract identifiers should be lowercase slugs with dot separators
allowed. Paths begin with `/` and contain no query string, fragment or whitespace.
Endpoint IDs are local to their component and do not need to agree across components.

An endpoint namespace identifies an exposed service boundary; it must not secretly
be treated as a component lookup. If a gateway and backend expose different routes,
give them distinct service boundaries and describe the gateway's own outbound call.

With no matching provider, save the definition and report an unresolved call. With
one matching provider, create a connection. With multiple providers for the exact
same identity, report ambiguity and create no resolved HTTP connection. Do not
invent fan-out or pick a provider by name. Document a load-balanced service as one
logical provider; instance-level deployment topology is outside this schema.

These are declared connections, not runtime availability checks or proof of request
and response payload compatibility. HTTP payload contracts would need their own
explicit extension before such compatibility can be asserted.

### Broker messages

Use one `messages` list with required `action: publishes | sends | consumes`.
`publishes` requires Event, `sends` requires Command, and `consumes` declares the
incoming message type. Match environment, contract, version,
message type, channel kind, namespace, name and content type. Remove component-ID
selectors from the new format. Do not infer connections from a contract name alone.

Topic consumers must declare subscriptions; stream consumers must declare consumer
groups through `subscription`. Queue consumers omit it. Distinct subscriptions/groups
represent independent delivery; shared subscriptions/groups and queues represent
competing consumers. Multiple publishers may legitimately match the same consumer.
Show all possible links rather than assuming every producer reaches every consumer
instance on every delivery.

Different declared schema fingerprints prevent matching. Missing fingerprints allow
a route connection with an unverified-schema finding. Equal fingerprints assert
the same declared fingerprint; the catalogue does not independently fetch or validate
the referenced payload schema.

Local duplicate bindings are validation errors. Cycles and self-delivery must remain
visible, with a finding for self-delivery, rather than breaking graph traversal.
Retired and archived components are excluded from discovery. Unmatched inputs and
outputs remain visible findings, not errors that prevent importing a valid component.

## Required and optional fields

| Element | Requirement | Applicable to / purpose |
| --- | --- | --- |
| `schemaVersion` | Required in the proposed new format | Selects the new contract-based matching semantics; proposed value `2.0`. |
| `component` | Required | Root component object. |
| `id`, `name`, `type`, `environment` | Required for every type | Catalogue identity, display name, classification and matching boundary. |
| `version` | Optional; default `1.0` | Version of the component description; does not replace interface versions. |
| `description`, `owner`, `domain` | Optional; recommended | Explain purpose, accountability and business context. |
| `technology` | Optional; default `[]` | Implementation technologies; descriptive, never a connection key. |
| `status` | Optional; default `Draft` | Lifecycle; retired definitions do not participate in discovery. |
| `criticality` | Optional; default `Medium` | Impact prioritisation. |
| `tags` | Optional; default `[]` | Search and classification. |
| `endpoints` | Optional; default `[]` | Declare when the component exposes HTTP operations. |
| `calls` | Optional; default `[]` | Declare when it calls HTTP operations. |
| `messages` | Optional; default `[]` | Broker declarations. Each item has action `publishes`, `sends`, or `consumes`; HTTP requests remain separate. |

Each declared HTTP endpoint/call requires a local `id`, `namespace`, `contract`,
`method` and `path`; `version` defaults to `1.0`; `description` is optional. Each
declared broker binding requires `id`, `action`, `contract`, `version`, `messageType` and
`channel.kind/namespace/name`. `action: sends` requires `Command`; `action: publishes` requires `Event`. `contentType`
defaults to `application/json`; `description` and `schemaFingerprint` are optional.
`subscription` is required only on Topic/Stream consumers and prohibited elsewhere.

No interaction list is universally mandatory based solely on `type`. A component
with no interactions is a valid inventory entry, with a finding that it remains
unlinked. Once an interaction is declared, all its required fields must be present.
Do not use blank/null optional values: omit the field or use an allowed empty list.

## Typical capabilities by component type

This table is authoring guidance, not a rule that fabricates or forbids capabilities.

| Type | Typical sections | Guidance |
| --- | --- | --- |
| `Api`, `ApiManagement` | `endpoints`; optionally `calls` and outgoing `messages` | Add `action: consumes` only if this actual component includes broker consumption. |
| `AzureFunction`, `LogicApp` | `endpoints` for HTTP-triggered work or `messages` with `action: consumes` for broker-triggered work; optional outbound sections | Timer-triggered work can have only outputs. Timer scheduling is not represented in this schema. |
| `InternalSystem`, `ExternalSystem`, `SaaS` | Any interaction section actually supported | A caller-only system needs `calls`; it does not need exposed endpoints. |
| `Transformation` | `messages` with incoming and outgoing actions, or HTTP sections | Describe its real exposed boundary. Do not infer which input causes which output. |
| `ServiceBusTopic`, `ServiceBusQueue`, `MessageBroker`, `EventGrid` | Usually inventory metadata | Channel declarations identify delivery routes. A broker must not be labelled a business message consumer merely to draw it as a node. |
| `Database`, `Storage`, `Sftp`, `FileShare` | Usually inventory metadata; HTTP/message sections only if actually supported | SQL, filesystem and SFTP access need dedicated protocol extensions; do not disguise them as HTTP/message interactions. |
| `Library`, `NuGetPackage` | Usually inventory metadata | Package/code dependencies are not HTTP or broker connections. Model a deployed wrapper separately if appropriate. |
| `ManualProcess`, `Custom` | Only interactions actually supported | No fabricated HTTP or messaging capability is required. |

## Proposed independent HTTP example

These are two separate documents. `2.0`, `namespace` and `contract` on HTTP
declarations are proposed fields and cannot be imported until implementation.

Provider:

```yaml
schemaVersion: "2.0"
component:
  id: invoice-api-dev
  name: Invoice API
  type: Api
  environment: development
  technology: [ASP.NET Core, FastEndpoints]
  endpoints:
    - id: accept-invoice
      namespace: finance.invoicing
      contract: invoice.create
      version: "1.0"
      method: POST
      path: /invoices
```

Caller:

```yaml
schemaVersion: "2.0"
component:
  id: finance-system-dev
  name: Finance system
  type: InternalSystem
  environment: development
  calls:
    - id: submit-invoice
      namespace: finance.invoicing
      contract: invoice.create
      version: "1.0"
      method: POST
      path: /invoices
```

Neither document names the other component. Discovery derives the connection from
the shared endpoint declaration. The provider can additionally publish a message,
and a function can independently consume the same message route using the existing
`messages` declarations without `sourceComponent`.

## Compatibility and implementation boundary

The running application currently requires `calls.targetComponent` and
`calls.endpoint`. Its message consumers may optionally use `sourceComponent`.
Legacy HTTP-message bindings also support `targetComponent`. These fields remain
in the current 1.1 reference so that it continues to describe importable documents.

Implement the new contract model behind an explicit schema version: retain legacy
matching for existing definitions, reject component selectors in new definitions,
and never silently convert old calls by guessing endpoint namespaces or contracts.
Do not mix legacy and new HTTP selector styles within one definition. Cross-format
HTTP matches should remain unresolved until providers and callers are explicitly
migrated to the new contract declarations. Message matching can continue across
versions when it meets the same routing rules and any legacy selectors are honoured.

Implementation must retain the source schema version through persistence, API
responses, JSON/YAML exports and revision history. The current serializer always
exports 1.1, so it must change alongside the parser, model and discovery engine.
Update editor guidance, generated documentation and findings with the new semantics.
Regression checks should cover import order, unchanged callers after provider-ID
changes, environment isolation, ambiguity, route/version mismatches, legacy reading,
export round trips and immutable history. This design does not add operation-level
causality: a component exposing endpoints and publishing events does not prove which
endpoint produces each event.
