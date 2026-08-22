# CONSTRAINTS

## Musts
- Exercise the real typed HTTP client and serializer pipeline while replacing only the network transport.
- Keep semantic expectations focused on the fields the replay case requires, not complete response equality.
- Keep the expected lossy-model failure testable without making repository verification red.

## Must-nots
- No vendor-specific, private, work-specific, live-API, telemetry, generated-SDK, or generalized schema-comparison dependency in the first slice.
