# Sample component definitions

These YAML files document independent components using schema version `1.1`.
Import each file separately through **Components → Add component**. Paste the
file contents, choose **Validate & preview links**, then **Save component**.
Use the component editor, rather than the authored integration import screen.
If a sample component ID already exists, open that component and **Edit** it.

## Event flow

Save these four definitions (the order below keeps missing HTTP targets to a minimum):

| Order | File | Editor example button | Purpose |
| --- | --- | --- | --- |
| 1 | [elite-api.yaml](elite-api.yaml) | Receiver example | Exposes `PUT /vendors` |
| 2 | [vendor-api.yaml](vendor-api.yaml) | API example | Exposes `POST /vendors` and publishes `vendors.created` |
| 3 | [vendor-function.yaml](vendor-function.yaml) | Function example | Consumes `vendors.created` and calls the Elite API |
| 4 | [vendor-system.yaml](vendor-system.yaml) | Caller example | Calls the Vendor API |

Open **Integrations** after saving all four and select the entry marked **Discovered**. With only these samples
in the catalogue, expect one network containing four components and three links:

```text
Procurement system
  -- POST /vendors --> Vendor API
  -- vendors.created (Event, topic) --> Vendor sync function
  -- PUT /vendors --> Elite vendor API
```

The APIs expose HTTP endpoints. The function consumes a broker event. The
catalogue links their declarations; it does not execute calls or deploy Azure
resources. You can keep the example namespace as written without an Azure account.
Your application's existing SQLite database stores the definitions.

## Optional command flow

Import [vendor-command-api.yaml](vendor-command-api.yaml) and
[vendor-command-function.yaml](vendor-command-function.yaml) separately, or use
the **Command API example** and **Command worker example** buttons.

```text
Vendor command API
  -- vendors.create (Command, queue) --> Vendor command worker
```

The worker also publishes `vendors.command-created` on `vendor-command-results`.
This pair deliberately has no completion-event consumer or documented HTTP caller.
It forms a separate network from the four-component event example.

## Findings and matching

- Temporary unresolved findings are expected while only some files are saved.
- `SCHEMA_UNVERIFIED` is expected because these examples omit payload schema
  fingerprints. The links still resolve; schema compatibility has not been verified.
- The optional command worker produces `UNCONSUMED_PUBLICATION` for its completion
  event until you add a matching consumer.
- Keep environment, contract, contract version, message type, content type and
  channel kind/namespace/name identical on both sides of a message link.
- HTTP calls must reference the target component ID and endpoint ID, with the same
  method, endpoint version and environment. If you rename IDs, update the calls too.
- Existing matching components in your catalogue can add links to these networks.

See [the component discovery guide](../../component-discovery.md) for the full
format and matching rules. If using the editor's built-in example buttons after
updating these files, restart `dotnet run --project src/IntegrationHub.Api` so the
embedded examples are rebuilt.
