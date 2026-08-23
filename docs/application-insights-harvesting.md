# Application Insights fixture harvesting (optional)

Fidelity core has no Azure dependency. This is a thin, optional adapter that
turns one Application Insights or Log Analytics telemetry row into an
ordinary local Fidelity fixture:

```text
Application Insights / KQL
        ↓
optional fixture harvester (tools/HarvestApplicationInsights.cs)
        ↓
raw-response.json
        ↓
existing Fidelity replay transport (ReplayHttpMessageHandler)
        ↓
real typed client pipeline
        ↓
semantic assertions
```

Nothing about Application Insights participates in replay semantics. The
harvester's only job is to produce a fixture file; from that point on it is
indistinguishable from any other fixture in `fixtures/`.

## Authentication

The harvester relies entirely on the caller's existing `az login` session. It
never stores, reads, refreshes, or otherwise manages Azure credentials — it
shells out to the locally authenticated `az` CLI and nothing else.

## What you provide

- `--mode` — `application-insights` (default) or `log-analytics`, selecting
  `az monitor app-insights query` or `az monitor log-analytics query`.
- `--target` — the Application Insights app id/name, or the Log Analytics
  workspace id, required by the chosen CLI command.
- `--query` or `--query-file` — your own KQL. Fidelity does not know your
  telemetry schema; the query is entirely how the relevant row is located.
- `--response-field` — the result column that holds the raw HTTP response
  body text.
- `--output` — the fixture path to write.

Optional:

- `--row-index <n>` — required when the query returns more than one row.
- `--no-provenance` / `--provenance <path>` — control the sidecar (see below).
- `--include-query-text` — store the literal query text in the provenance
  sidecar instead of a SHA-256 fingerprint.

## Example

`--target` is passed straight through as `az monitor app-insights query --app <target>` /
`az monitor log-analytics query --workspace <target>`. The Azure CLI accepts a plain
Application Insights resource *name* only when it can resolve a default resource group
(e.g. via `az configure --defaults group=...`); otherwise pass the fully-qualified resource
ID (`/subscriptions/<sub>/resourceGroups/<rg>/providers/microsoft.insights/components/<name>`)
or the workspace GUID so the query is unambiguous regardless of shell defaults:

```powershell
dotnet run --file tools/HarvestApplicationInsights.cs -- `
  --target /subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/my-rg/providers/microsoft.insights/components/my-app-insights-resource `
  --query "requests | where name == 'GET /operation' | where success == false | take 1 | project responseBody = customDimensions.responseBody" `
  --response-field responseBody `
  --output fixtures/harvested-example.json
```

Then replay it exactly like any other fixture:

```csharp
var replay = new ReplayHttpMessageHandler(ReplayFixture.Read("fixtures/harvested-example.json"));
```

## Ambiguity fails closed

The harvest fails loudly — with no fixture written — when:

- the query returns zero rows;
- the configured `--response-field` is missing from the result columns;
- the configured field is empty or null in the selected row;
- more than one row is returned and `--row-index` was not given;
- an explicit `--row-index` is out of range;
- the fixture (or provenance sidecar) cannot be written.

The harvester never guesses a "first row" when the result is ambiguous.

## Raw body preservation

The response-body column is expected to already be a JSON string containing
the response text. The harvester unwraps only the CLI result envelope's own
JSON string-escaping and writes that text unchanged — it does not
deserialize and reserialize the body itself.

## Provenance

By default a sidecar `<output>.provenance.json` is written next to the
fixture, containing `harvestedAtUtc`, `sourceKind`, `target`,
`selectedRowIndex`, `totalRows`, and either `queryFingerprint` (default) or
`queryText` (with `--include-query-text`). This file is a separate artifact:
it is never read by `ReplayFixture` and never participates in replay or
semantic assertions.

## Security

Production telemetry can contain sensitive data. The harvester:

- never commits or pushes anything — it only writes local files;
- never collects headers, tokens, or credentials by default;
- writes only the one response-body field you selected, plus the narrow
  provenance fields above.

**You are responsible for reviewing and sanitizing a harvested fixture
before committing or sharing it.** Treat harvested fixtures the same as any
other file that may contain production data.

## Testing without live Azure

`tests/HarvestExtractionTests.cs` exercises the extraction logic against
captured `az` CLI result envelopes under `tests/fixtures/`, and
`tests/HarvestedFixtureReplay.cs` proves a harvested fixture replays through
the real, unmodified Fidelity pipeline. Neither requires network access or
an Azure subscription; both run as part of `scripts/verify.ps1`.

Azure CLI process execution is isolated behind the `AzCliRunner` delegate in
`src/Fidelity/Harvest.cs`, so tests inject captured output instead of
invoking `az`. Only `tools/HarvestApplicationInsights.cs` wires up the real
process runner (`AzCliProcess.RunAsync`).
