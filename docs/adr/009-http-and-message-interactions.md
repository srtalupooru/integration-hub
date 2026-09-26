# ADR 009: Separate HTTP calls from broker messaging

Status: accepted. Refines ADR 008.

APIs expose endpoints to callers; they may subsequently send commands or publish events. Treating HTTP requests as message consumption obscured the distinction in authoring and generated catalogues.

Component definitions now have `endpoints`, `calls`, `sends`, `publishes` and `consumes`. HTTP callers explicitly reference a target component and endpoint ID with an exact method, version and environment. HTTP edges are synchronous and include the method/path. They are excluded from broker message catalogues and generated Messages/Events sections. Commands sent use the same exact broker resolver as events published, with their message type preserved. A real broker subscriber can still declare consumption regardless of component type.

Legacy HTTP message bindings remain supported without guessing an HTTP method. They receive migration findings and are displayed separately. Legacy commands in `publishes` continue to resolve but are labelled as commands sent. New source sections are persisted in the existing canonical JSON; historical source and hashes are not rewritten and no database schema change is required.

See the [component discovery guide](../component-discovery.md) for examples, matching rules and compatibility.
