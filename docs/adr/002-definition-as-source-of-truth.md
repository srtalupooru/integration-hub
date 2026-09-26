# ADR-002: Definition as Source of Truth

Status: Accepted

## Context

Manually maintained diagrams and documents drift when each can be independently edited.

## Decision

Author JSON/YAML definitions. Parse to the canonical model, persist both source and read models, and derive all diagrams, catalogues and documentation. Revalidate at save. Every change appends a source revision.

## Consequences

Generated artifacts cannot be edited separately. Restoring old content is a new revision. Source, canonical and read-model concerns remain explicit. Governance can later operate through Git without replacing the runtime.
