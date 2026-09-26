# ADR-001: Modular Monolith

Status: Accepted

## Context

An integration catalogue needs cohesive authoring, analysis and documentation workflows without distributed operational overhead.

## Decision

Deploy one ASP.NET Core host with isolated Domain, Application, Infrastructure, Contracts and Web projects. Host Blazor and REST endpoints together. Use explicit service interfaces at storage, parsing, generation and analysis boundaries.

## Consequences

Transactions stay local and identity stays same-origin. Module extraction remains possible behind application contracts, but is not an initial objective. A separate service requires evidence of independent scaling or ownership needs.
