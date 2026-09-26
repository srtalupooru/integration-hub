# ADR-004: Mermaid for Initial Rendering

Status: Accepted

## Context

The initial UI needs understandable diagrams with low operational complexity. The model must remain independent of diagram syntax.

## Decision

Place Mermaid generation behind IDiagramGenerator. Generate safe IDs and encode labels. Render with strict security settings using a pinned local ESM bundle. Keep global diagram formatting in the application layer.

## Consequences

Mermaid is a derived visualization and supports no independent editing. Layout interactivity is limited. React Flow, Cytoscape, PlantUML or custom SVG can be future adapters without a definition migration.
