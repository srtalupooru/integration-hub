# ADR 008: Independent component definitions and deterministic message discovery

Status: accepted.

## Context

Teams know the inputs and outputs of their own API/function before they know the whole end-to-end integration. Requiring a combined definition duplicates ownership and couples updates. Matching only message display names would introduce false cross-environment, cross-channel and cross-version connections.

## Decision

Add an independent component definition type with explicitly identified consumed and published message bindings. Persist components and immutable revisions separately from authored integrations. Derive networks on demand from one snapshot of active component records; never persist generated integration copies or overwrite authored definitions.

The resolver uses exact environment, qualified contract, explicit version, message type, channel kind/namespace/name and content type. Two supplied schema fingerprints must agree; missing fingerprints generate a compatibility finding. HTTP publications must have an unambiguous target. Broker routes preserve all possible publishers/consumers and distinguish competing queue/subscription/group delivery. Unresolved messages are allowed so components can arrive in any order. Unknown fields and local duplicate/invalid binding declarations are rejected.

Connected networks receive deterministic IDs based on membership. Their provenance includes component revisions and canonical hashes. Splits/merges change network IDs; refresh and preview expose these changes. Network traversal is cycle-safe. Archive, restore and retirement change discovery eligibility without destroying history.

Global dependency graphs include discovered components in an explicit namespace. Authored and discovered catalogue navigation, exports and metrics remain distinguishable. Matching never merges unrelated names or invents the internal transformation from consumed inputs to published outputs.

## Consequences

There is no synchronization job or generated-data drift. Schema and transport compatibility remain authored assertions with transparent limitations. Current reads load bounded component snapshots; large-catalogue indexing, incremental calculation and push updates can be added after measurement. A connected network may contain several business workflows; authors needing handler-level separation should model those handlers independently. Live SQL Server deployment and real browser interactions still require their respective smoke tests in an available environment.

Implementation and operational rules: [component discovery guide](../component-discovery.md).
