# ADR-003: Canonical Integration Graph

Status: Accepted

## Context

Real integration flows branch, converge, cycle and share resources across definitions. Renderer-specific graphs would constrain analysis.

## Decision

Use domain nodes and explicit directed edges, entry/exit boundaries, connected components and dependency relationships. Traverse iteratively with visited sets. Merge global components only through explicit system or shared-resource IDs.

## Consequences

Graphs are not assumed to be linear or acyclic. Cycles are warnings where intentional; broken references are errors. Membership is not a causal edge. Integration dependency analysis and component flow analysis preserve separate meanings.
