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
- `--offset <value>` — **required for `--mode application-insights`**, passed
  through unchanged as `az monitor app-insights query --offset <value>` (see
  "Two separate time boundaries" below). Harvesting fails before invoking
  `az` at all if this is omitted in Application Insights mode.

Optional:

- `--row-index <n>` — required when the query returns more than one row.
- `--no-provenance` / `--provenance <path>` — control the sidecar (see below).
- `--include-query-text` — store the literal query text in the provenance
  sidecar instead of a SHA-256 fingerprint.

## Two separate time boundaries (Application Insights)

Application Insights harvesting has two independent, non-overlapping
concepts of "time range," and Fidelity leaves both entirely in your control:

- **Outer Azure CLI query window (`--offset`)** — `az monitor app-insights
  query` limits which telemetry it asks the service for. If `--offset` is
  not given, Azure CLI silently defaults to a 1-hour window. Fidelity's
  `--offset` maps directly to the CLI's own `--offset` and is required in
  `--mode application-insights` specifically so this window is never a
  silent default.
- **Inner KQL `timestamp` predicate (inside your `--query`)** — a clause
  such as `| where timestamp > ago(4d)` only *filters within* whatever
  window the outer CLI request already retrieved. It cannot widen that
  window.

These two must agree, or the query can return an empty result even though
the Application Insights UI (which does not share the CLI's default) shows
the row you expect: a KQL predicate like `ago(4d)` inside a query issued
with the CLI's default 1-hour `--offset` will filter against data that was
never fetched in the first place, yielding a well-formed `rows: []` result
rather than an error. Fidelity does not parse or infer a time range from
your KQL and does not validate that `--offset` and any inner `timestamp`
predicate agree — that judgment call is yours; picking `--offset` at least
as wide as any inner predicate is the caller's responsibility.

`--offset` is Application Insights–specific and is not sent for
`--mode log-analytics`, which uses the CLI's own `--timespan` option
(not currently exposed by Fidelity) and defaults to querying all available
data when no timespan is given.

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
  --query "requests | where timestamp > ago(4d) | where name == 'GET /operation' | where success == false | take 1 | project responseBody = customDimensions.responseBody" `
  --response-field responseBody `
  --offset 4d `
  --output fixtures/harvested-example.json
```

Note `--offset 4d` (the outer Azure CLI request window) matches the inner
`timestamp > ago(4d)` KQL predicate above — if `--offset` had been narrower
than the KQL predicate, e.g. left at Azure CLI's 1-hour default, this query
would return the normal `tables`/`columns` schema with `rows: []` instead of
the expected row, because the CLI never fetched data outside its own
window for the inner predicate to filter.

Then replay it exactly like any other fixture:

```csharp
var replay = new ReplayHttpMessageHandler(ReplayFixture.Read("fixtures/harvested-example.json"));
```

## Ambiguity fails closed

The harvest fails loudly — with no fixture written — when:

- `--mode application-insights` is used without `--offset` (fails before
  `az` is invoked at all);
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
`queryText` (with `--include-query-text`). For `--mode application-insights`
it also records `offset`, so a fixture's acquisition boundary can be
reconstructed later; this is metadata only and is not present for
`--mode log-analytics`. This file is a separate artifact: it is never read
by `ReplayFixture` and never participates in replay or semantic assertions.

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
