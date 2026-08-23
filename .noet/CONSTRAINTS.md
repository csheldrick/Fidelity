# CONSTRAINTS

## Musts
- Exercise the real typed HTTP client and serializer pipeline while replacing only the network transport.
- Keep semantic expectations focused on the fields the replay case requires, not complete response equality.
- Keep the expected lossy-model failure testable without making repository verification red.
- Keep the Application Insights/Log Analytics harvester isolated from replay semantics: harvested output must be an ordinary fixture the existing `ReplayHttpMessageHandler`/`ReplayFixture` consume unmodified, with no Azure dependency anywhere in the replay path.
- Have the harvester fail loudly (no fixture written) on zero-row, ambiguous multi-row, invalid explicit row index, missing response field, and empty response field conditions instead of guessing.
- Keep repository verification (`scripts/verify.ps1`) provable with no live Azure access.

## Must-nots
- No vendor-specific, private, work-specific, live-API, telemetry, generated-SDK, or generalized schema-comparison dependency in the first replay slice.
- No Azure SDK dependency, Azure credential storage/management, or generalized telemetry source/plugin framework in the harvester slice.
