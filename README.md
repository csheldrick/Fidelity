# Fidelity

> Replay real HTTP responses through your real typed API client pipeline without calling the real API.

Fidelity is a lightweight API contract/replay validation utility for finding a dangerous class of integration failure: the HTTP call succeeds, deserialization succeeds, a typed object is returned, but important response semantics disappear somewhere inside the real client pipeline.

The core question is:

> **Does the response sent by the API survive the client pipeline with the meaning the application depends on intact?**

## The problem

Traditional integration checks often stop at conditions such as:

- the request completed;
- the HTTP status was successful;
- the response deserialized;
- the returned object was non-null.

Those checks can all pass while the application still loses meaningful data.

For example, an API may return a valid response containing an application-level error:

```json
{
  "result": {
    "status": "error",
    "error": {
      "message": "operation_not_allowed",
      "code": -180
    }
  }
}
```

If a generated or hand-written response model does not represent `status` or `error`, a permissive serializer may still deserialize successfully while silently discarding them. The application receives a valid-looking object that no longer represents what the API actually said.

Fidelity exists to test that boundary directly.

## Core idea

Fidelity substitutes the network boundary while keeping as much of the real application client path as practical:

```text
fixture.json
    -> replay HTTP transport
    -> real client configuration
    -> real API interface/client method
    -> real serializer, models, and converters
    -> typed application result
    -> semantic assertions
```

The response fixture can be captured from a condition that is difficult, unsafe, expensive, or impossible to reproduce against a live API. Replaying that exact body locally makes the client boundary deterministic and repeatable without replacing the serializer or typed client with a simplified test double.

## File-based app direction

Fidelity should stay deliberately lightweight. The initial direction is .NET file-based apps that can compose a reusable replay harness with `#:include` and reference the consumer's actual packages or projects as needed.

A replay should be able to live close to the client it validates without requiring a dedicated test project, fake API server, application host, or changes to production code.

Conceptually, a user should only need to provide:

- the real API client or interface;
- the real serializer/client configuration;
- the method and request to invoke;
- a raw HTTP response fixture;
- semantic expectations for the typed result.

The harness should own the reusable mechanics such as replay transport, fixture loading, diagnostics, assertion helpers, and common output.

## What Fidelity validates

Fidelity is **not** a byte-for-byte JSON round-trip validator and should not require every vendor field to survive deserialization.

External APIs often return metadata the application intentionally ignores. A replay case should instead describe the semantics that matter for that operation.

For the example above, a useful expectation might be:

```text
status == "error"
error.message == "operation_not_allowed"
error.code == -180
```

If the typed client returns an object while those required semantics disappear, the replay should fail loudly and explain what was expected versus what survived the boundary.

## Initial demos

The repository includes three self-contained, vendor-agnostic file-based apps using the public Refit typed-client library:

1. **Pass: correctly modeled response** — [`examples/HealthyReplay.cs`](examples/HealthyReplay.cs) replays [`fixtures/healthy.json`](fixtures/healthy.json), invokes a real Refit interface, and verifies meaningful fields.
2. **Expected Fidelity failure: incomplete model** — [`examples/LossyModelReplay.cs`](examples/LossyModelReplay.cs) replays the valid application-error response, receives HTTP 200 and a typed object, then fails because the model cannot observe the required error semantics.
3. **Pass: corrected model** — [`examples/CorrectedModelReplay.cs`](examples/CorrectedModelReplay.cs) replays the exact same fixture with a model that represents `status` and `error`.

The expected-failure example exits with code 1 by itself. The repository check treats that exit as success only when the output proves that transport and typed-result production succeeded before the required semantic expectations failed.

## Running the slice

Run these commands from the repository root with the .NET 10 SDK:

```powershell
dotnet run --file examples/HealthyReplay.cs
dotnet run --file examples/LossyModelReplay.cs       # expected exit code: 1
dotnet run --file examples/CorrectedModelReplay.cs
pwsh -NoProfile -File scripts/verify.ps1
noet verify run
```

The file-based apps use `#:include` for the shared replay mechanics and `#:package Refit@8.0.0` for the typed-client pipeline. No project file, fake server, host, or production-code change is required.

The smallest reusable mechanics are in [`src/Fidelity/Replay.cs`](src/Fidelity/Replay.cs): `ReplayHttpMessageHandler` replaces only transport, `ReplayFixture` reads a captured body, and `Fidelity.RunAsync` reports transport/client invocation, typed-result production, and semantic expectation status. Inline JSON remains possible by passing a string directly to the handler.

## Design principles

- **Replay the real client path.** Do not reduce the test to `Deserialize<T>(json)` when the actual application uses a richer client pipeline.
- **Replace only what is necessary.** Prefer replacing the network transport while keeping the real interface, settings, serializer, converters, and models.
- **Assert meaning, not structural completeness.** Required semantics matter; irrelevant vendor metadata does not.
- **Keep cases easy to create.** A useful replay should feel closer to a script than a test framework ceremony.
- **Use real captured responses when valuable.** The hardest integration failures are often conditions that cannot be reproduced on demand.
- **Stay vendor-agnostic.** Fidelity is infrastructure for bringing your own API client, not a catalog of vendor-specific integrations.
- **Earn abstractions from examples.** The first demos should pressure the smallest reusable harness before a generalized framework is designed.

## Non-goals

Fidelity is not intended to:

- validate every operation in a generated SDK;
- prove an external API conforms to its published schema;
- replace ordinary unit, integration, or end-to-end tests;
- require live access to the external API;
- couple the replay engine to a telemetry provider or fixture-harvesting system.

Telemetry-assisted fixture acquisition may be useful later, but it should remain an optional input layer: capture a response, sanitize it, save it as a fixture, and replay it through Fidelity.

## Current status

Fidelity is at project initialization. The motivating proof of concept demonstrated that a raw response captured from a difficult-to-reproduce condition can be returned by a replay `HttpMessageHandler` and passed through the actual typed client stack, exposing silent semantic loss that was otherwise difficult to recreate locally.

The immediate goal is to build the smallest reusable file-app harness and the first generic pass/fail demos. Architecture beyond that is intentionally not fixed yet.
