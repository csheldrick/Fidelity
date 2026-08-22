# PROJECT

## Mission
- Replay real HTTP responses through real typed API client pipelines and detect when required semantics are lost.

## Boundaries
- Fidelity remains generic and vendor-agnostic.
- The first slice replaces only HTTP transport and asserts application-required semantics after the real typed client pipeline.
- No live API, work-specific integration, telemetry, or generalized schema comparison is part of this slice.
