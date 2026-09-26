# ADR-006: JSON and YAML Support

Status: Accepted

## Context

Engineers need readable hand-authored definitions while automation prefers standard JSON. Both formats must share semantics.

## Decision

Parse JSON with System.Text.Json and YAML with YamlDotNet into an internal JSON tree. Evaluate the same embedded draft 2020-12 JSON Schema using JsonSchema.Net, then create the canonical domain model. Return separate syntax, schema and semantic findings.

## Consequences

No JSON/YAML types escape the parsing layer. YAML aliases, custom tags, complex keys and multiple documents are rejected; depth and size are bounded. Plain YAML version values become strings. Unknown properties fail validation so typos cannot silently disappear.
