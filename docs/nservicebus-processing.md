# NServiceBus handlers and sagas

A component can optionally describe the handlers and sagas running inside its logical NServiceBus endpoint. Connections between components still come from their message contracts and routes. Internal processing refers only to message IDs, HTTP call IDs, and timeout IDs declared in the same component; it never names another handler or component.

For example, an Azure Function App is the component (`type: AzureFunction`), and its sagas live under `component.processing.sagas`. The same component can contain several sagas and regular handlers. Its `messages` describe the Function App's external messaging; each saga references those local message IDs. The graph node remains the Function App, with its internal processors shown in **Handlers & sagas** and connection evidence.

```text
Invoice API → InvoiceCreated → Invoice processing Function App
                                ├─ Audit invoice handler
                                └─ Invoice lifecycle saga
                                   ├─ consumes: on-invoice-created, on-payment-confirmed
                                   ├─ may send: request-payment
                                   └─ timeout: payment-deadline
```

This is design-time documentation. Importing a definition does not run NServiceBus, provision a broker or persistence, schedule a timeout, or query live saga instances. Components remain independently authored; a saga itself is stateful.

## Try the complete example

Import these three files separately through **Components → Add component**. The editor also offers matching sample buttons.

1. [Invoice API](examples/components/saga-api.yaml): exposes HTTP and publishes `InvoiceCreated` from a send-only messaging endpoint.
2. [Invoice processing Function App](examples/components/saga-worker.yaml): hosts the saga and audit handler, and consumes that event in both an audit handler and an invoice saga. The saga may send `RequestPayment` and request an internal deadline.
3. [Payment worker](examples/components/saga-payment-worker.yaml): handles the command and may publish `PaymentConfirmed`, which continues the invoice saga.

Open the component's **Handlers & sagas** tab to inspect its rules. Open the resulting network under **Integrations** to see processing evidence on its connections and its **Handlers & sagas** tab. Validation previews show the same declarations before saving. Component exports, revision history, discovery responses, and generated Markdown documentation preserve the processing details. Catalogue text search includes handler implementations, saga types, and endpoint metadata.

The example creates three component nodes and three message connections. Two processors share the invoice-created delivery; they do not create two broker subscriptions. The graph contains an intentional payment request/confirmation cycle. Completion and expiry events have no external consumers in these three files, so those declarations remain visible on the component without generating extra connections. Missing schema fingerprints and unconsumed events can produce expected discovery findings.

## Authoring rules

See the [annotated reference](component-definition-reference.yaml) for the complete field shape.

| Element | Required when | Meaning |
| --- | --- | --- |
| `processing` | Never; opt in for a messaging endpoint | Optional on any component type that actually hosts NServiceBus. Existing components need no changes. |
| `framework`, `endpoint` | `processing` exists | Framework must be `NServiceBus`; endpoint is its logical endpoint name. |
| `transport`, `persistence`, `outbox`, `recoverability` | Optional | Declared configuration, not a verified deployment. Missing saga persistence produces a warning. An omitted Outbox value means unknown. |
| `sendOnly` | Optional, defaults to false | When true, incoming broker messages, handlers, and sagas are forbidden. Exposed HTTP endpoints are allowed. |
| `handlers[].id`, `handles` | A handler is declared | A unique local processor ID and at least one incoming broker message reference. |
| `sagas[].id` | A saga is declared | Unique across both handler and saga IDs. At least one message rule or timeout is needed. |
| `name`, `implementation`, `description`, `dataType` | Optional metadata | Display name, CLR implementation, explanation, and persisted saga state type. |
| `handles[].message` | A handling rule is declared | ID of a local message with `action: consumes`. HTTP endpoints cannot be handled this way. |
| `outputs` | Optional on each rule | IDs of local messages with `action: publishes` or `sends`. Only explicitly declared possibilities are shown. |
| `calls` | Optional on each rule | IDs of existing outbound HTTP calls. These effects are not protected by the messaging Outbox; validation flags retry/idempotency considerations. |
| `condition` | Optional on each rule | Plain-language preconditions or idempotency behavior; never evaluated. |
| `startsSaga` | Optional, defaults to false | May create a new instance, or handle an existing one. Several message types may be starters. |
| `completesSaga` | Optional, defaults to false | This rule may complete the instance, subject to its condition. |
| `correlation` | Every saga message rule | One of the modes below. |
| `whenNotFound` | Optional, defaults to `Discard` | `Discard`, `Throw`, or `Custom`. `Custom` requires `notFoundDescription`. This describes application behavior; it does not configure it. |
| `timeouts[].id`, `stateType` | A timeout is declared | Saga-local callback ID and CLR timeout state type. |
| `delay` / `schedule` | Optional; missing timing warns | A positive ISO 8601 duration such as `PT30M`, or a description of dynamic scheduling. Cannot specify both. |
| `requestsTimeouts` | Optional on saga message/timeout rules | IDs of timeouts declared by this same saga. Timeout callbacks can request further timeouts. |

## Correlation

| Mode | Required fields | Use |
| --- | --- | --- |
| `Property` | Saga `correlationProperty`, rule `messageProperty` | Business key mapped from a message property. |
| `Header` | Saga `correlationProperty`, rule `header` | Business key obtained from a message header. |
| `Custom` | Rule `description` | Document a custom finder or correlation expression, including tenant boundaries and uniqueness requirements. |
| `SagaId` | No property/header | Existing-instance automatic correlation via framework metadata; cannot be a starter. |

The catalogue validates structure and references, not CLR properties, header values, uniqueness, or a custom finder's code. Do not use the internal saga data `Id` as a business correlation property. Automatic reply correlation also has NServiceBus-specific restrictions, including saga-to-saga interactions; a `SagaId` declaration does not prove runtime headers exist. [Particular correlation documentation](https://docs.particular.net/nservicebus/sagas/message-correlation).

## Delivery, lifecycle, and failure cases

- Multiple handlers and sagas can reference one consumed binding. They remain inside the endpoint's delivery and retry boundary; their order is not inferred. Partial side effects must be considered when retries rerun handlers. [Handler documentation](https://docs.particular.net/nservicebus/handlers/).
- The output list belongs to the individual rule. An invoice starter sending a payment command does not imply it also publishes the completion event. Empty outputs are valid. Several rules can produce the same output.
- Starters can also handle existing instances; a later starter can create a new instance after completion. Continuations arriving before creation or after completion need an explicit not-found strategy. A saga with no starter is accepted as partial documentation, with a warning. [Saga lifecycle](https://docs.particular.net/nservicebus/sagas/), [not-found handling](https://docs.particular.net/nservicebus/sagas/saga-not-found).
- Timeouts are callbacks for their owning saga, not external message subscriptions. Completed instances ignore outstanding timeouts. Requesting another timeout does not cancel an earlier request, and a duration does not guarantee exact execution time. Cycles, unreachable callbacks, and requesting a timeout from a potentially completing rule produce review warnings. [Timeout documentation](https://docs.particular.net/nservicebus/sagas/timeouts).
- Completion with outgoing messages and no declared Outbox produces a consistency warning. Declaring Outbox enabled still does not prove the actual transport and persistence transaction guarantees. Check the deployed configuration.
- Duplicate local processor/rule/timeout IDs, wrong-direction message references, missing correlation details, unknown timeout/call/output references, invalid schedules, and send-only endpoints with incoming work are rejected before saving.
- Archive, restore, revisions, and environment boundaries continue to apply. Processing annotations cannot create a connection when contract, version, channel, or environment matching fails.

## Boundaries and scale

Use one component per logical endpoint. Multiple processes running replicas of the same endpoint are not extra component definitions. If one host runs several endpoints, give each endpoint its own definition. Names of handler classes and saga classes are implementation metadata, not routing identities.

Message matching uses the catalogue's exact logical contract/version/channel rules. When a native NServiceBus transport uses a different physical publisher/subscriber topology, document a consistent logical route on both sides. CLR inheritance, message conventions, polymorphic subscriptions, and assembly scanning are not inferred. Declare the relevant contracts explicitly. The current unified message schema supports outgoing events and commands; automatic `Reply` routing and outbound response messages are not introduced by this feature. Existing HTTP calls retain their current target/endpoint reference model.

The component graph shows possible communication. It does not simulate state transitions, retries, ordering, concurrency, message loss, or business outcomes. Conditions describe alternatives; they are not executable guards. Instance-level tracking (active/completed sagas, received message IDs, retries, timestamps) would require an additional telemetry integration, such as ServiceControl data.

Processing is stored in existing versioned component JSON, so no database migration or NServiceBus package is needed. Component definition exports preserve the complete declarations. Integration definition snapshots retain the component-level graph; use component exports or discovery/Markdown exports for full processing detail.

Limits: 100 handlers and 100 sagas per component; 100 handling rules or timeouts per respective array; 500 total processing rules per component; 50 references per effect list; 20,000 processing rules and 100,000 attached connection references per discovery run. These limits complement existing component and connection limits.
